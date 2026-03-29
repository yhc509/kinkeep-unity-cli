using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityCli.Protocol;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
{
    [InitializeOnLoad]
    internal static class BridgeBootstrap
    {
        private static readonly BridgeHost _host;

        static BridgeBootstrap()
        {
            _host = new BridgeHost();
            _host.Start();
        }
    }

    internal sealed class BridgeHost : IDisposable
    {
        private readonly ConcurrentQueue<PendingRequest> _pendingRequests = new ConcurrentQueue<PendingRequest>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly string[] _capabilities;
        private readonly string _projectRoot;
        private readonly string _projectName;
        private readonly string _projectHash;
        private readonly string _pipeName;
        private readonly string _registryFilePath;
        private readonly AssetCommandHandler _assetCommandHandler;
        private readonly SceneCommandHandler _sceneCommandHandler;
        private readonly PrefabCommandHandler _prefabCommandHandler;
        private readonly ScreenshotCommandHandler _screenshotCommandHandler;
        private readonly ExecuteCodeHandler _executeCodeHandler;
        private readonly CustomCommandHandler _customCommandHandler;
        private readonly PackageCommandHandler _packageCommandHandler;
        private readonly MaterialCommandHandler _materialCommandHandler;
        private Socket _unixListener;
        private double _lastHeartbeatTime;
        private bool _isStarted;
        private bool _isDisposed;

        public BridgeHost()
        {
            _projectRoot = ProtocolConstants.GetCanonicalPath(Path.Combine(Application.dataPath, ".."));
            _projectName = Path.GetFileName(_projectRoot);
            _projectHash = ProtocolConstants.ComputeProjectHash(_projectRoot);
            _pipeName = ProtocolConstants.BuildPipeName(_projectHash);
            _registryFilePath = RegistryPathUtility.GetRegistryFilePath();
            _capabilities = ProtocolHelpers.GetSupportedCommands();
            _assetCommandHandler = new AssetCommandHandler();
            _sceneCommandHandler = new SceneCommandHandler();
            _prefabCommandHandler = new PrefabCommandHandler();
            _screenshotCommandHandler = new ScreenshotCommandHandler();
            _executeCodeHandler = new ExecuteCodeHandler();
            _customCommandHandler = new CustomCommandHandler();
            _packageCommandHandler = new PackageCommandHandler();
            _materialCommandHandler = new MaterialCommandHandler();
        }

        public void Start()
        {
            if (_isStarted || Application.isBatchMode)
            {
                return;
            }

            _isStarted = true;
            ConsoleLogBuffer.Start();
            RegisterInstance();
            _lastHeartbeatTime = EditorApplication.timeSinceStartup;
            StartListener();

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.quitting += OnEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.quitting -= OnEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch
            {
            }

            DisposeUnixListener();
            ConsoleLogBuffer.Stop();
            RemoveInstance();
            CleanupSocketFile();
            _cancellationTokenSource.Dispose();
        }

        private void StartListener()
        {
            if (Path.DirectorySeparatorChar == '\\')
            {
                Task.Run(() => RunNamedPipeLoopAsync(_cancellationTokenSource.Token));
                return;
            }

            Task.Run(() => RunUnixSocketLoopAsync(_cancellationTokenSource.Token));
        }

        private async Task RunNamedPipeLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                try
                {
                    await server.WaitForConnectionAsync(cancellationToken);
                    _ = Task.Run(() => HandleStreamClientAsync(server, cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    server.Dispose();
                }
                catch (Exception exception)
                {
                    ReportBackgroundException("named pipe accept", exception);
                    server.Dispose();
                }
            }
        }

        private async Task RunUnixSocketLoopAsync(CancellationToken cancellationToken)
        {
            CleanupSocketFile();

            var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _unixListener = listener;

            try
            {
                listener.Bind(new UnixDomainSocketEndPoint(_pipeName));
                listener.Listen(8);

                while (!cancellationToken.IsCancellationRequested)
                {
                    Socket client = await listener.AcceptAsync();
                    _ = Task.Run(() => HandleSocketClientAsync(client, cancellationToken));
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception exception)
            {
                ReportBackgroundException("unix socket accept", exception);
            }
            finally
            {
                if (ReferenceEquals(_unixListener, listener))
                {
                    _unixListener = null;
                }

                listener.Dispose();
                CleanupSocketFile();
            }
        }

        private Task HandleSocketClientAsync(Socket client, CancellationToken cancellationToken)
        {
            return HandleStreamClientAsync(new NetworkStream(client, true), cancellationToken);
        }

        private async Task HandleStreamClientAsync(Stream stream, CancellationToken cancellationToken)
        {
            using (stream)
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true))
            {
                string line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                CommandEnvelope command = ProtocolJson.Deserialize<CommandEnvelope>(line);
                if (command == null || string.IsNullOrWhiteSpace(command.command))
                {
                    string requestId = command != null && !string.IsNullOrWhiteSpace(command.requestId)
                        ? command.requestId
                        : Guid.NewGuid().ToString("N");
                    var error = ResponseEnvelope.Failure(
                        requestId,
                        _projectHash,
                        "INVALID_COMMAND",
                        "command payload를 해석하지 못했습니다.",
                        false,
                        0,
                        ProtocolConstants.TransportLive,
                        line);
                    await WriteResponseAsync(writer, error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(command.requestId))
                {
                    command.requestId = Guid.NewGuid().ToString("N");
                }

                var pending = new PendingRequest(command);
                _pendingRequests.Enqueue(pending);

                using (cancellationToken.Register(() => pending.Completion.TrySetCanceled()))
                {
                    ResponseEnvelope response;
                    try
                    {
                        response = await pending.Completion.Task;
                    }
                    catch (TaskCanceledException)
                    {
                        response = ResponseEnvelope.Failure(
                            command.requestId,
                            _projectHash,
                            "REQUEST_CANCELLED",
                            "요청 처리 중 브리지가 종료되었습니다.",
                            true,
                            0,
                            ProtocolConstants.TransportLive,
                            null);
                    }

                    await WriteResponseAsync(writer, response);
                }
            }
        }

        private static async Task WriteResponseAsync(StreamWriter writer, ResponseEnvelope response)
        {
            await writer.WriteLineAsync(ProtocolJson.Serialize(response));
            await writer.FlushAsync();
        }

        private void OnEditorUpdate()
        {
            if (_isDisposed)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - _lastHeartbeatTime >= ProtocolConstants.RegistryHeartbeatSeconds)
            {
                RegisterInstance();
                _lastHeartbeatTime = EditorApplication.timeSinceStartup;
            }

            while (_pendingRequests.TryDequeue(out PendingRequest pending))
            {
                ResponseEnvelope response = HandleCommand(pending.Command);
                pending.Completion.TrySetResult(response);
            }
        }

        private void OnEditorQuitting()
        {
            Dispose();
        }

        private void OnBeforeAssemblyReload()
        {
            Dispose();
        }

        private ResponseEnvelope HandleCommand(CommandEnvelope command)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (IsBusyEditorCommand(command.command))
                {
                    stopwatch.Stop();
                    return ResponseEnvelope.Failure(
                        command.requestId,
                        _projectHash,
                        ProtocolConstants.BusyErrorCode,
                        "Unity가 compile/update 중이라 지금 명령을 처리할 수 없습니다.",
                        true,
                        stopwatch.ElapsedMilliseconds,
                        ProtocolConstants.TransportLive,
                        null);
                }

                string dataJson;
                if (_assetCommandHandler.CanHandle(command.command))
                {
                    dataJson = _assetCommandHandler.Handle(command.command, command.argumentsJson);
                }
                else if (_sceneCommandHandler.CanHandle(command.command))
                {
                    dataJson = _sceneCommandHandler.Handle(command.command, command.argumentsJson);
                }
                else if (_prefabCommandHandler.CanHandle(command.command))
                {
                    dataJson = _prefabCommandHandler.Handle(command.command, command.argumentsJson);
                }
                else if (_screenshotCommandHandler.CanHandle(command.command))
                {
                    dataJson = _screenshotCommandHandler.Handle(command.command, command.argumentsJson);
                }
                else if (_executeCodeHandler.CanHandle(command.command))
                {
                    dataJson = _executeCodeHandler.Handle(command.command, command.argumentsJson);
                }
                else if (_customCommandHandler.CanHandle(command.command))
                {
                    dataJson = _customCommandHandler.Handle(command.command, command.argumentsJson);
                }
                else if (_materialCommandHandler.CanHandle(command.command))
                {
                    dataJson = _materialCommandHandler.Handle(command.command, command.argumentsJson);
                }
                else if (_packageCommandHandler.CanHandle(command.command))
                {
                    dataJson = _packageCommandHandler.Handle(command.command, command.argumentsJson);
                }
                else
                {
                    switch (command.command)
                    {
                        case ProtocolConstants.CommandPing:
                            dataJson = ProtocolJson.Serialize(new PingPayload
                            {
                                message = "pong",
                                timestampUtc = DateTimeOffset.UtcNow.ToString("O"),
                            });
                            break;
                        case ProtocolConstants.CommandStatus:
                            dataJson = BuildStatusJson();
                            break;
                        case ProtocolConstants.CommandRefresh:
                            AssetDatabase.Refresh();
                            dataJson = ProtocolJson.Serialize(new MessagePayload { message = "AssetDatabase.Refresh 완료" });
                            break;
                        case ProtocolConstants.CommandCompile:
                            CompilationPipeline.RequestScriptCompilation();
                            dataJson = ProtocolJson.Serialize(new MessagePayload { message = "script compilation 요청 완료" });
                            break;
                        case ProtocolConstants.CommandPlay:
                            EditorApplication.isPaused = false;
                            EditorApplication.isPlaying = true;
                            dataJson = ProtocolJson.Serialize(new PlayStatePayload { isPlaying = true });
                            break;
                        case ProtocolConstants.CommandPause:
                            EditorApplication.isPaused = true;
                            dataJson = ProtocolJson.Serialize(new PauseStatePayload { isPaused = true });
                            break;
                        case ProtocolConstants.CommandStop:
                            EditorApplication.isPlaying = false;
                            EditorApplication.isPaused = false;
                            dataJson = ProtocolJson.Serialize(new StopStatePayload
                            {
                                isPlaying = false,
                                isPaused = false,
                            });
                            break;
                        case ProtocolConstants.CommandExecuteMenu:
                            dataJson = HandleExecuteMenu(command.argumentsJson);
                            break;
                        case ProtocolConstants.CommandReadConsole:
                            dataJson = HandleReadConsole(command.argumentsJson);
                            break;
                        default:
                            throw new InvalidOperationException("지원하지 않는 명령입니다: " + command.command);
                    }
                }

                stopwatch.Stop();
                return ResponseEnvelope.Success(
                    command.requestId,
                    _projectHash,
                    dataJson,
                    stopwatch.ElapsedMilliseconds,
                    ProtocolConstants.TransportLive);
            }
            catch (CommandFailureException exception)
            {
                stopwatch.Stop();
                return ResponseEnvelope.Failure(
                    command.requestId,
                    _projectHash,
                    exception.ErrorCode,
                    exception.Message,
                    exception.IsRetryable,
                    stopwatch.ElapsedMilliseconds,
                    ProtocolConstants.TransportLive,
                    exception.Details);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                return ResponseEnvelope.Failure(
                    command.requestId,
                    _projectHash,
                    "COMMAND_FAILED",
                    exception.Message,
                    false,
                    stopwatch.ElapsedMilliseconds,
                    ProtocolConstants.TransportLive,
                    exception.ToString());
            }
        }

        private bool IsBusyEditorCommand(string command)
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                return false;
            }

            return !ProtocolHelpers.IsCommandAllowedWhileBusy(command);
        }

        private string BuildStatusJson()
        {
            return ProtocolJson.Serialize(new StatusPayload
            {
                projectRoot = _projectRoot,
                projectHash = _projectHash,
                projectName = _projectName,
                unityVersion = Application.unityVersion,
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                activeScenePath = EditorSceneManager.GetActiveScene().path,
                pipeName = _pipeName,
            });
        }

        private string HandleExecuteMenu(string argumentsJson)
        {
            ExecuteMenuArgs args = ProtocolJson.Deserialize<ExecuteMenuArgs>(argumentsJson) ?? new ExecuteMenuArgs();
            if (string.IsNullOrWhiteSpace(args.path))
            {
                throw new InvalidOperationException("execute-menu에는 path가 필요합니다.");
            }

            bool isExecuted = EditorApplication.ExecuteMenuItem(args.path);
            return ProtocolJson.Serialize(new ExecuteMenuPayload
            {
                path = args.path,
                executed = isExecuted,
            });
        }

        private string HandleReadConsole(string argumentsJson)
        {
            ReadConsoleArgs args = ProtocolJson.Deserialize<ReadConsoleArgs>(argumentsJson) ?? new ReadConsoleArgs();
            int limit = args.limit <= 0 ? ProtocolConstants.DefaultConsoleLimit : args.limit;
            ConsoleLogEntry[] entries = ConsoleLogBuffer.Read(limit, args.type);
            return ProtocolJson.Serialize(new ReadConsolePayload { entries = entries });
        }

        private void RegisterInstance()
        {
            UpdateRegistrySafely(delegate(InstanceRegistry registry)
            {
                List<InstanceRecord> records = registry.instances
                    .Where(record => !string.Equals(record.projectHash, _projectHash, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                records.Add(BuildInstanceRecord());
                registry.instances = records.OrderBy(record => record.projectName, StringComparer.OrdinalIgnoreCase).ToArray();

                InstanceRecord activeRecord = null;
                if (!string.IsNullOrWhiteSpace(registry.activeProjectHash))
                {
                    activeRecord = registry.instances.FirstOrDefault(
                        record => string.Equals(record.projectHash, registry.activeProjectHash, StringComparison.OrdinalIgnoreCase));
                }

                bool isCurrentProjectPromotionNeeded = activeRecord == null
                    || string.Equals(activeRecord.state, "offline", StringComparison.OrdinalIgnoreCase)
                    || activeRecord.editorProcessId <= 0;

                if (isCurrentProjectPromotionNeeded)
                {
                    registry.activeProjectHash = _projectHash;
                }

                return registry;
            });
        }

        private void RemoveInstance()
        {
            UpdateRegistrySafely(delegate(InstanceRegistry registry)
            {
                registry.instances = registry.instances
                    .Where(record => !string.Equals(record.projectHash, _projectHash, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (string.Equals(registry.activeProjectHash, _projectHash, StringComparison.OrdinalIgnoreCase))
                {
                    registry.activeProjectHash = registry.instances.Length > 0 ? registry.instances[0].projectHash : null;
                }

                return registry;
            });
        }

        private InstanceRecord BuildInstanceRecord()
        {
            return new InstanceRecord
            {
                projectRoot = _projectRoot,
                projectName = _projectName,
                projectHash = _projectHash,
                pipeName = _pipeName,
                editorProcessId = Process.GetCurrentProcess().Id,
                unityVersion = Application.unityVersion,
                state = BuildStateLabel(),
                lastSeenUtc = DateTimeOffset.UtcNow.ToString("O"),
                capabilities = (string[])_capabilities.Clone(),
            };
        }

        private string BuildStateLabel()
        {
            if (EditorApplication.isCompiling)
            {
                return "compiling";
            }

            if (EditorApplication.isUpdating)
            {
                return "updating";
            }

            if (EditorApplication.isPlaying)
            {
                return "playing";
            }

            return "idle";
        }

        private void UpdateRegistrySafely(Func<InstanceRegistry, InstanceRegistry> update)
        {
            try
            {
                InstanceRegistryFile.Update(_registryFilePath, update);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(string.Format("Unity CLI bridge registry 갱신 실패: {0}", exception));
            }
        }

        private void CleanupSocketFile()
        {
            if (Path.DirectorySeparatorChar != '\\' && File.Exists(_pipeName))
            {
                try
                {
                    File.Delete(_pipeName);
                }
                catch
                {
                }
            }
        }

        private void DisposeUnixListener()
        {
            if (_unixListener == null)
            {
                return;
            }

            try
            {
                _unixListener.Dispose();
            }
            catch
            {
            }

            _unixListener = null;
        }

        private void ReportBackgroundException(string operation, Exception exception)
        {
            if (_isDisposed || exception is OperationCanceledException)
            {
                return;
            }

            UnityEngine.Debug.LogWarning(string.Format("Unity CLI bridge {0} 실패: {1}", operation, exception));
        }

        private sealed class PendingRequest
        {
            public PendingRequest(CommandEnvelope command)
            {
                Command = command;
                Completion = new TaskCompletionSource<ResponseEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public CommandEnvelope Command { get; private set; }
            public TaskCompletionSource<ResponseEnvelope> Completion { get; private set; }
        }
    }
}

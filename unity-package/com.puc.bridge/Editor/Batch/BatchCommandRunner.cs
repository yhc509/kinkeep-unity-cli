using System;
using System.IO;
using UnityCli.Protocol;
using UnityEditor;

namespace PUC.Editor.Batch
{
    public static class BatchCommandRunner
    {
        private static readonly AssetCommandHandler AssetCommandHandler = new AssetCommandHandler();
        private static readonly PrefabCommandHandler PrefabCommandHandler = new PrefabCommandHandler();

        public static void Refresh()
        {
            string requestId = Guid.NewGuid().ToString("N");
            string resultFile = TryReadArgument("-unityCliResultFile");

            try
            {
                AssetDatabase.Refresh();
                var response = ResponseEnvelope.Success(
                    requestId,
                    null,
                    ProtocolJson.Serialize(new MessagePayload { message = "AssetDatabase.Refresh 완료" }),
                    0,
                    ProtocolConstants.TransportBatch);
                WriteResultAndExit(resultFile, response, 0);
            }
            catch (Exception exception)
            {
                var response = ResponseEnvelope.Failure(
                    requestId,
                    null,
                    "BATCH_REFRESH_FAILED",
                    exception.Message,
                    false,
                    0,
                    ProtocolConstants.TransportBatch,
                    exception.ToString());
                WriteResultAndExit(resultFile, response, 1);
            }
        }

        public static void RunRequest()
        {
            string requestFile = TryReadArgument("-unityCliRequestFile");
            string resultFile = TryReadArgument("-unityCliResultFile");

            try
            {
                if (string.IsNullOrWhiteSpace(requestFile) || !File.Exists(requestFile))
                {
                    throw new InvalidOperationException("batch request file을 찾지 못했습니다.");
                }

                CommandEnvelope command = ProtocolJson.Deserialize<CommandEnvelope>(File.ReadAllText(requestFile));
                if (command == null || string.IsNullOrWhiteSpace(command.command))
                {
                    throw new InvalidOperationException("batch request를 해석하지 못했습니다.");
                }

                if (string.IsNullOrWhiteSpace(command.requestId))
                {
                    command.requestId = Guid.NewGuid().ToString("N");
                }

                ResponseEnvelope response = Execute(command);
                WriteResultAndExit(resultFile, response, response.status == ProtocolConstants.StatusSuccess ? 0 : 1);
            }
            catch (Exception exception)
            {
                var response = ResponseEnvelope.Failure(
                    Guid.NewGuid().ToString("N"),
                    ProtocolConstants.ComputeProjectHash(Path.Combine(UnityEngine.Application.dataPath, "..")),
                    "BATCH_REQUEST_FAILED",
                    exception.Message,
                    false,
                    0,
                    ProtocolConstants.TransportBatch,
                    exception.ToString());
                WriteResultAndExit(resultFile, response, 1);
            }
        }

        private static string TryReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == name)
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static ResponseEnvelope Execute(CommandEnvelope command)
        {
            string projectRoot = ProtocolConstants.GetCanonicalPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string projectHash = ProtocolConstants.ComputeProjectHash(projectRoot);
            var startedAt = DateTimeOffset.UtcNow;

            try
            {
                string dataJson;
                if (AssetCommandHandler.CanHandle(command.command))
                {
                    dataJson = AssetCommandHandler.Handle(command.command, command.argumentsJson);
                }
                else if (PrefabCommandHandler.CanHandle(command.command))
                {
                    dataJson = PrefabCommandHandler.Handle(command.command, command.argumentsJson);
                }
                else
                {
                    switch (command.command)
                    {
                        case ProtocolConstants.CommandRefresh:
                            AssetDatabase.Refresh();
                            dataJson = ProtocolJson.Serialize(new MessagePayload { message = "AssetDatabase.Refresh 완료" });
                            break;
                        default:
                            throw new InvalidOperationException("batch에서 지원하지 않는 명령입니다: " + command.command);
                    }
                }

                return ResponseEnvelope.Success(
                    command.requestId,
                    projectHash,
                    dataJson,
                    (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
                    ProtocolConstants.TransportBatch);
            }
            catch (CommandFailureException exception)
            {
                return ResponseEnvelope.Failure(
                    command.requestId,
                    projectHash,
                    exception.ErrorCode,
                    exception.Message,
                    exception.Retryable,
                    (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
                    ProtocolConstants.TransportBatch,
                    exception.Details);
            }
            catch (Exception exception)
            {
                return ResponseEnvelope.Failure(
                    command.requestId,
                    projectHash,
                    "COMMAND_FAILED",
                    exception.Message,
                    false,
                    (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
                    ProtocolConstants.TransportBatch,
                    exception.ToString());
            }
        }

        private static void WriteResultAndExit(string resultFile, ResponseEnvelope response, int exitCode)
        {
            if (!string.IsNullOrWhiteSpace(resultFile))
            {
                File.WriteAllText(resultFile, ProtocolJson.Serialize(response));
            }

            EditorApplication.Exit(exitCode);
        }
    }
}

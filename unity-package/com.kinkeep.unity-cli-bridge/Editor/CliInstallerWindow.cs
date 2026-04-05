#nullable enable
using System;
using UnityEditor;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
{
    public sealed class CliInstallerWindow : EditorWindow
    {
        private const string WindowTitle = "CLI Manager";
        private const string OpenWindowMenuItemPath = "KinKeep/CLI Manager";
        private static readonly Vector2 WindowMinSize = new Vector2(420f, 340f);

        private Vector2 _scrollPosition;
        private CliInstallStatus _status;
        private string _packageVersion = string.Empty;
        private string _installedVersion = string.Empty;
        private string _executablePath = string.Empty;
        private string _platformDisplayName = string.Empty;
        private string _downloadUrl = string.Empty;
        private string _releasePageUrl = string.Empty;
        private string _pathCommand = string.Empty;
        private string _errorMessage = string.Empty;
        private float _downloadProgress;
        private bool _hasLoadedState;
        private bool _isDownloading;

        [MenuItem(OpenWindowMenuItemPath)]
        private static void OpenWindow()
        {
            CliInstallerWindow window = GetWindow<CliInstallerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = WindowMinSize;
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = WindowMinSize;
            RefreshState(true);
        }

        private void OnFocus()
        {
            if (!_isDownloading)
            {
                RefreshState(false);
            }
        }

        private void OnGUI()
        {
            if (!_hasLoadedState && !_isDownloading)
            {
                RefreshState(false);
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            GUILayout.Space(8f);
            DrawPackageInfoSection();
            GUILayout.Space(8f);
            DrawCliStatusSection();
            GUILayout.Space(8f);
            DrawActionSection();
            GUILayout.Space(8f);
            DrawPathSetupSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPackageInfoSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Package Info", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Package Version", FormatVersion(_packageVersion));
                DrawLinkButton("Repository", CliInstallerState.GetRepositoryUrl(), "Open Repository");
                DrawLinkButton("Release", _releasePageUrl, "Open Release");
            }
        }

        private void DrawCliStatusSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("CLI Status", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Status", GetStatusLabel());
                EditorGUILayout.LabelField("Platform", _platformDisplayName);
                DrawSelectableValue("Path", _executablePath);
            }
        }

        private void DrawActionSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Actions", EditorStyles.boldLabel);

                if (!string.IsNullOrWhiteSpace(_errorMessage))
                {
                    EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);
                }

                if (_isDownloading)
                {
                    Rect progressRect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(progressRect, _downloadProgress, "Downloading CLI...");
                    GUILayout.Space(4f);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_errorMessage))
                {
                    if (GUILayout.Button("Retry", GUILayout.Height(28f)))
                    {
                        BeginInstall();
                    }

                    return;
                }

                switch (_status)
                {
                    case CliInstallStatus.NotInstalled:
                        if (GUILayout.Button("Install CLI", GUILayout.Height(28f)))
                        {
                            BeginInstall();
                        }

                        return;
                    case CliInstallStatus.UpdateRequired:
                        if (GUILayout.Button("Update CLI", GUILayout.Height(28f)))
                        {
                            BeginInstall();
                        }

                        return;
                    case CliInstallStatus.UpToDate:
                        using (new EditorGUI.DisabledScope(true))
                        {
                            GUILayout.Button("Up to date", GUILayout.Height(28f));
                        }

                        return;
                    default:
                        throw new InvalidOperationException("지원하지 않는 CLI 설치 상태입니다: " + _status);
                }
            }
        }

        private void DrawPathSetupSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("PATH Setup", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Install directory를 PATH에 추가하면 터미널에서 CLI 실행 파일을 바로 찾을 수 있습니다.", MessageType.None);
                DrawSelectableValue("Command", _pathCommand);

                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_pathCommand));
                if (GUILayout.Button("Copy PATH command"))
                {
                    EditorGUIUtility.systemCopyBuffer = _pathCommand;
                }

                EditorGUI.EndDisabledGroup();
            }
        }

        private void DrawLinkButton(string label, string url, string buttonLabel)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);

                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(url));
                if (GUILayout.Button(buttonLabel))
                {
                    Application.OpenURL(url);
                }

                EditorGUI.EndDisabledGroup();
            }
        }

        private void DrawSelectableValue(string label, string value)
        {
            EditorGUILayout.LabelField(label);
            EditorGUILayout.SelectableLabel(
                value,
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private void BeginInstall()
        {
            try
            {
                _errorMessage = string.Empty;
                _downloadProgress = 0f;
                _isDownloading = true;
                Repaint();

                CliDownloader.DownloadAndInstallAsync(
                    _downloadUrl,
                    CliInstallerState.GetInstallDirectory(),
                    HandleDownloadProgress,
                    HandleDownloadError,
                    HandleDownloadComplete);
            }
            catch (Exception exception)
            {
                _isDownloading = false;
                _errorMessage = exception.Message;
                Repaint();
            }
        }

        private void HandleDownloadProgress(float progress)
        {
            _downloadProgress = progress;
            Repaint();
        }

        private void HandleDownloadError(string errorMessage)
        {
            _isDownloading = false;
            _errorMessage = errorMessage;
            RefreshState(false);
            Repaint();
        }

        private void HandleDownloadComplete()
        {
            _isDownloading = false;
            _downloadProgress = 1f;
            RefreshState(true);
            Repaint();
        }

        private void RefreshState(bool shouldClearError)
        {
            if (shouldClearError)
            {
                _errorMessage = string.Empty;
            }

            try
            {
                _packageVersion = CliInstallerState.GetPackageVersion();
                _installedVersion = CliInstallerState.GetInstalledVersion() ?? string.Empty;
                _executablePath = CliInstallerState.GetExecutablePath();
                _platformDisplayName = CliInstallerState.GetPlatformDisplayName();
                _downloadUrl = CliInstallerState.GetDownloadUrl();
                _releasePageUrl = CliInstallerState.GetReleasePageUrl();
                _pathCommand = GetPathCommand();
                _status = CliInstallerState.GetStatus();
                _hasLoadedState = true;
            }
            catch (Exception exception)
            {
                _hasLoadedState = true;
                _packageVersion = string.Empty;
                _installedVersion = string.Empty;
                _executablePath = string.Empty;
                _platformDisplayName = string.Empty;
                _downloadUrl = string.Empty;
                _releasePageUrl = string.Empty;
                _pathCommand = string.Empty;
                _status = CliInstallStatus.NotInstalled;

                if (string.IsNullOrWhiteSpace(_errorMessage))
                {
                    _errorMessage = exception.Message;
                }
            }
        }

        private string GetStatusLabel()
        {
            switch (_status)
            {
                case CliInstallStatus.NotInstalled:
                    return "Not Installed";
                case CliInstallStatus.UpToDate:
                    return string.IsNullOrWhiteSpace(_installedVersion)
                        ? "Installed"
                        : "Installed (" + FormatVersion(_installedVersion) + ")";
                case CliInstallStatus.UpdateRequired:
                    return string.IsNullOrWhiteSpace(_installedVersion)
                        ? "Update Required"
                        : "Update Required (" + FormatVersion(_installedVersion) + " -> " + FormatVersion(_packageVersion) + ")";
                default:
                    throw new InvalidOperationException("지원하지 않는 CLI 설치 상태입니다: " + _status);
            }
        }

        private static string GetPathCommand()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                    return "export PATH=\"$HOME/.kinkeep/unity-cli:$PATH\"";
                case RuntimePlatform.WindowsEditor:
                    return "$env:Path = \"$env:USERPROFILE\\.kinkeep\\unity-cli;$env:Path\"";
                default:
                    throw new PlatformNotSupportedException("CLI Installer는 macOS arm64와 Windows x64 Editor만 지원합니다.");
            }
        }

        private static string FormatVersion(string version)
        {
            return string.IsNullOrWhiteSpace(version)
                ? "-"
                : "v" + version.Trim().TrimStart('v', 'V');
        }
    }
}

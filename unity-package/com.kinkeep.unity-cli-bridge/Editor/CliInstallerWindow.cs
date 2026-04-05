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
        private static GUIStyle? _updateAvailableLabelStyle;

        private Vector2 _scrollPosition;
        private CliInstallStatus _status;
        private string _packageVersion = string.Empty;
        private string _latestReleaseVersion = string.Empty;
        private string _installedVersion = string.Empty;
        private string _executablePath = string.Empty;
        private string _platformDisplayName = string.Empty;
        private string _downloadUrl = string.Empty;
        private string _releasePageUrl = string.Empty;
        private string _pathCommand = string.Empty;
        private string _errorMessage = string.Empty;
        private string _skillFeedbackMessage = string.Empty;
        private MessageType _skillFeedbackType = MessageType.Info;
        private float _downloadProgress;
        private bool _hasLoadedState;
        private bool _isDownloading;
        private bool _isFetchingLatestVersion;
        private SkillTarget _skillTarget;

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
            GUILayout.Space(8f);
            DrawSkillSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPackageInfoSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Package Info", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Package Version", FormatVersion(_packageVersion));
                if (IsUpdateAvailable())
                {
                    EditorGUILayout.LabelField("Latest Release", GetLatestReleaseVersionLabel(), UpdateAvailableLabelStyle);
                }
                else
                {
                    EditorGUILayout.LabelField("Latest Release", GetLatestReleaseVersionLabel());
                }

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
                        throw new InvalidOperationException("Unsupported CLI install status: " + _status);
                }
            }
        }

        private void DrawPathSetupSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("PATH Setup", EditorStyles.boldLabel);

                EditorGUILayout.HelpBox("Add the install directory to your PATH to use the CLI from the terminal. A short, fixed path also saves tokens when AI agents invoke the CLI repeatedly.", MessageType.None);
                DrawSelectableValue("Command", _pathCommand);

                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_pathCommand));
                if (GUILayout.Button("Copy PATH command"))
                {
                    EditorGUIUtility.systemCopyBuffer = _pathCommand;
                }

                EditorGUI.EndDisabledGroup();
            }
        }

        private void DrawSkillSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("AI Agent Skill", EditorStyles.boldLabel);
                _skillTarget = (SkillTarget)EditorGUILayout.EnumPopup("Target", _skillTarget);

                if (GUILayout.Button("Install Skill", GUILayout.Height(24f)))
                {
                    InstallSkill();
                }

                if (!string.IsNullOrWhiteSpace(_skillFeedbackMessage))
                {
                    EditorGUILayout.HelpBox(_skillFeedbackMessage, _skillFeedbackType);
                }
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

        private void InstallSkill()
        {
            try
            {
                SkillInstaller.Install(_skillTarget);
                string destination = SkillInstaller.GetDestination(_skillTarget);
                _skillFeedbackType = MessageType.Info;
                _skillFeedbackMessage = "Installed to: " + destination;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _skillFeedbackType = MessageType.Error;
                _skillFeedbackMessage = exception.Message;
            }
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
                RefreshLatestReleaseVersion();
                _hasLoadedState = true;
            }
            catch (Exception exception)
            {
                _hasLoadedState = true;
                _packageVersion = string.Empty;
                _latestReleaseVersion = string.Empty;
                _installedVersion = string.Empty;
                _executablePath = string.Empty;
                _platformDisplayName = string.Empty;
                _downloadUrl = string.Empty;
                _releasePageUrl = string.Empty;
                _pathCommand = string.Empty;
                _status = CliInstallStatus.NotInstalled;
                _isFetchingLatestVersion = false;

                if (string.IsNullOrWhiteSpace(_errorMessage))
                {
                    _errorMessage = exception.Message;
                }
            }
        }

        private void RefreshLatestReleaseVersion()
        {
            _latestReleaseVersion = CliInstallerState.GetCachedLatestReleaseVersion() ?? string.Empty;
            if (_isFetchingLatestVersion || !CliInstallerState.IsLatestReleaseCacheExpired())
            {
                return;
            }

            _isFetchingLatestVersion = true;
            CliInstallerState.FetchLatestReleaseVersion(HandleLatestReleaseVersionFetched);
        }

        private void HandleLatestReleaseVersionFetched(string? latestReleaseVersion)
        {
            _latestReleaseVersion = latestReleaseVersion ?? string.Empty;
            _isFetchingLatestVersion = false;
            Repaint();
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
                    throw new InvalidOperationException("Unsupported CLI install status: " + _status);
            }
        }

        private string GetLatestReleaseVersionLabel()
        {
            if (_isFetchingLatestVersion && string.IsNullOrWhiteSpace(_latestReleaseVersion))
            {
                return "Checking...";
            }

            return FormatVersion(_latestReleaseVersion);
        }

        private bool IsUpdateAvailable()
        {
            if (string.IsNullOrWhiteSpace(_latestReleaseVersion) || string.IsNullOrWhiteSpace(_installedVersion))
            {
                return false;
            }

            return CliInstallerState.CompareVersions(_installedVersion, _latestReleaseVersion) < 0;
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
                    throw new PlatformNotSupportedException("CLI Installer only supports macOS arm64 and Windows x64 editors.");
            }
        }

        private static string FormatVersion(string version)
        {
            return string.IsNullOrWhiteSpace(version)
                ? "-"
                : "v" + version.Trim().TrimStart('v', 'V');
        }

        private static GUIStyle UpdateAvailableLabelStyle =>
            _updateAvailableLabelStyle ??= CreateUpdateAvailableLabelStyle();

        private static GUIStyle CreateUpdateAvailableLabelStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
            return style;
        }
    }
}

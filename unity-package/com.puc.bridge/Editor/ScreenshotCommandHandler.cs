#nullable enable
using System;
using System.IO;
using UnityCli.Protocol;
using UnityEditor;
using UnityEngine;

namespace PUC.Editor
{
    internal sealed class ScreenshotCommandHandler
    {
        public bool CanHandle(string command)
        {
            return string.Equals(command, ProtocolConstants.CommandScreenshot, StringComparison.Ordinal);
        }

        public string Handle(string command, string argumentsJson)
        {
            ScreenshotArgs args = ProtocolJson.Deserialize<ScreenshotArgs>(argumentsJson) ?? new ScreenshotArgs();

            string outputPath;
            int capturedWidth;
            int capturedHeight;

            if (!string.IsNullOrWhiteSpace(args.camera))
            {
                var result = CaptureFromCamera(args.camera!, args.width, args.height);
                outputPath = result.path;
                capturedWidth = result.width;
                capturedHeight = result.height;
            }
            else
            {
                string view = string.IsNullOrWhiteSpace(args.view) ? "game" : args.view!;
                var result = CaptureView(view, args.width, args.height);
                outputPath = result.path;
                capturedWidth = result.width;
                capturedHeight = result.height;
            }

            string resolvedPath = !string.IsNullOrWhiteSpace(args.outputPath) ? args.outputPath! : outputPath;

            if (!string.Equals(outputPath, resolvedPath, StringComparison.Ordinal))
            {
                string? directory = Path.GetDirectoryName(resolvedPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                if (File.Exists(resolvedPath))
                {
                    File.Delete(resolvedPath);
                }

                File.Move(outputPath, resolvedPath);
            }

            var fileInfo = new FileInfo(resolvedPath);

            return ProtocolJson.Serialize(new ScreenshotPayload
            {
                savedPath = resolvedPath,
                width = capturedWidth,
                height = capturedHeight,
                fileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            });
        }

        private (string path, int width, int height) CaptureView(string view, int requestedWidth, int requestedHeight)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"puc-screenshot-{Guid.NewGuid():N}.png");

            if (string.Equals(view, "game", StringComparison.OrdinalIgnoreCase))
            {
                Camera? camera = Camera.main;
                if (camera == null && Camera.allCameras.Length > 0)
                {
                    camera = Camera.allCameras[0];
                }

                if (camera == null)
                {
                    throw new CommandFailureException("SCREENSHOT_FAILED", "Game View 캡처를 위한 카메라가 없습니다.", false, null);
                }

                var gameView = GetMainGameViewSize();
                int width = requestedWidth > 0 ? requestedWidth : camera.pixelWidth;
                int height = requestedHeight > 0 ? requestedHeight : camera.pixelHeight;
                if (width <= 0)
                {
                    width = gameView.width;
                }

                if (height <= 0)
                {
                    height = gameView.height;
                }

                CaptureCameraToPath(camera, width, height, tempPath);
                return (tempPath, width, height);
            }

            if (string.Equals(view, "scene", StringComparison.OrdinalIgnoreCase))
            {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null)
                {
                    throw new CommandFailureException("SCREENSHOT_FAILED", "Scene View가 열려 있지 않습니다.", false, null);
                }

                int width = requestedWidth > 0 ? requestedWidth : (int)sceneView.position.width;
                int height = requestedHeight > 0 ? requestedHeight : (int)sceneView.position.height;

                Camera? camera = sceneView.camera;
                if (camera == null)
                {
                    throw new CommandFailureException("SCREENSHOT_FAILED", "Scene View 캡처를 위한 카메라가 없습니다.", false, null);
                }

                CaptureCameraToPath(camera, width, height, tempPath);
                return (tempPath, width, height);
            }

            throw new CommandFailureException("INVALID_VIEW", $"지원하지 않는 view입니다: {view}", false, null);
        }

        private (string path, int width, int height) CaptureFromCamera(string cameraName, int requestedWidth, int requestedHeight)
        {
            var camera = FindCamera(cameraName);
            if (camera == null)
            {
                throw new CommandFailureException("CAMERA_NOT_FOUND", $"카메라를 찾지 못했습니다: {cameraName}", false, null);
            }

            int width = requestedWidth > 0 ? requestedWidth : camera.pixelWidth;
            int height = requestedHeight > 0 ? requestedHeight : camera.pixelHeight;
            var gameView = GetMainGameViewSize();
            if (width <= 0)
            {
                width = gameView.width;
            }

            if (height <= 0)
            {
                height = gameView.height;
            }

            string tempPath = Path.Combine(Path.GetTempPath(), $"puc-screenshot-{Guid.NewGuid():N}.png");
            CaptureCameraToPath(camera, width, height, tempPath);
            return (tempPath, width, height);
        }

        private static void CaptureCameraToPath(Camera camera, int width, int height, string path)
        {
            var renderTexture = new RenderTexture(width, height, 24);
            RenderTexture? previousActive = RenderTexture.active;
            RenderTexture? previousTarget = camera.targetTexture;
            Texture2D? texture = null;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                byte[] pngBytes = texture.EncodeToPNG();
                File.WriteAllBytes(path, pngBytes);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;

                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static Camera? FindCamera(string name)
        {
            foreach (var camera in Camera.allCameras)
            {
                if (string.Equals(camera.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return camera;
                }
            }

            return null;
        }

        private static (int width, int height) GetMainGameViewSize()
        {
            var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                return (1920, 1080);
            }

            var window = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (window == null)
            {
                return (1920, 1080);
            }

            return ((int)window.position.width, (int)window.position.height);
        }
    }
}

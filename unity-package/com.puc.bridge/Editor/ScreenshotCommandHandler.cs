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
            int superSize = 1;

            if (string.Equals(view, "game", StringComparison.OrdinalIgnoreCase))
            {
                ScreenCapture.CaptureScreenshot(tempPath, superSize);
                var gameView = GetMainGameViewSize();
                return (tempPath, gameView.width, gameView.height);
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

                var camera = sceneView.camera;
                var renderTexture = new RenderTexture(width, height, 24);
                camera.targetTexture = renderTexture;
                camera.Render();
                camera.targetTexture = null;

                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = null;

                byte[] pngBytes = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(renderTexture);

                File.WriteAllBytes(tempPath, pngBytes);
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

            var renderTexture = new RenderTexture(width, height, 24);
            var previousTarget = camera.targetTexture;
            camera.targetTexture = renderTexture;
            camera.Render();
            camera.targetTexture = previousTarget;

            RenderTexture.active = renderTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            string tempPath = Path.Combine(Path.GetTempPath(), $"puc-screenshot-{Guid.NewGuid():N}.png");
            byte[] pngBytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);

            File.WriteAllBytes(tempPath, pngBytes);
            return (tempPath, width, height);
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

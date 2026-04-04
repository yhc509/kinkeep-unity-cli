#nullable enable
using System;
using System.IO;
using UnityCli.Protocol;
using UnityEditor;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
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
                return CaptureGameView(tempPath, requestedWidth, requestedHeight);
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

        private static (string path, int width, int height) CaptureGameView(string path, int requestedWidth, int requestedHeight)
        {
            if (!EditorApplication.isPlaying)
            {
                return CaptureGameViewFromCamera(path, requestedWidth, requestedHeight);
            }

            Texture2D? capturedTexture = null;
            Texture2D? outputTexture = null;

            try
            {
                capturedTexture = ScreenCapture.CaptureScreenshotAsTexture();
                if (capturedTexture == null)
                {
                    throw new CommandFailureException("SCREENSHOT_FAILED", "Play Mode Game View 캡처에 실패했습니다.", false, null);
                }

                var outputSize = ResolvePlayModeGameViewOutputSize(capturedTexture, requestedWidth, requestedHeight);
                int width = outputSize.width;
                int height = outputSize.height;

                outputTexture = outputSize.shouldResize
                    ? ResizeTexture(capturedTexture, width, height)
                    : capturedTexture;

                WriteTextureToPath(outputTexture, path);
                return (path, width, height);
            }
            finally
            {
                if (outputTexture != null && !ReferenceEquals(outputTexture, capturedTexture))
                {
                    UnityEngine.Object.DestroyImmediate(outputTexture);
                }

                if (capturedTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(capturedTexture);
                }
            }
        }

        private static (int width, int height, bool shouldResize) ResolvePlayModeGameViewOutputSize(
            Texture2D capturedTexture,
            int requestedWidth,
            int requestedHeight)
        {
            int width = requestedWidth > 0 ? requestedWidth : capturedTexture.width;
            int height = requestedHeight > 0 ? requestedHeight : capturedTexture.height;
            if (width <= 0 || height <= 0)
            {
                throw new CommandFailureException("SCREENSHOT_FAILED", "유효한 Game View 캡처 크기를 확인하지 못했습니다.", false, null);
            }

            if (width > capturedTexture.width || height > capturedTexture.height)
            {
                UnityEngine.Debug.LogWarning(
                    $"[KinKeep] Play Mode Game View screenshot requested {width}x{height}, but ScreenCapture.CaptureScreenshotAsTexture() only captures the native Game View size {capturedTexture.width}x{capturedTexture.height}. Saving the native capture without upscaling.");
                return (capturedTexture.width, capturedTexture.height, false);
            }

            return (width, height, width != capturedTexture.width || height != capturedTexture.height);
        }

        private static (string path, int width, int height) CaptureGameViewFromCamera(string path, int requestedWidth, int requestedHeight)
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

            CaptureCameraToPath(camera, width, height, path);
            return (path, width, height);
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
                WriteTextureToPath(texture, path);
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

        private static Texture2D ResizeTexture(Texture2D sourceTexture, int width, int height)
        {
            var renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture? previousActive = RenderTexture.active;

            try
            {
                Graphics.Blit(sourceTexture, renderTexture);
                RenderTexture.active = renderTexture;

                var resizedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                resizedTexture.Apply();
                return resizedTexture;
            }
            finally
            {
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static void WriteTextureToPath(Texture2D texture, string path)
        {
            byte[] pngBytes = texture.EncodeToPNG();
            File.WriteAllBytes(path, pngBytes);
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

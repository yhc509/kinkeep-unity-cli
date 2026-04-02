#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityCli.Protocol;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal sealed class QaCommandHandler
    {
        public QaCommandHandler()
        {
            QaTargetRegistry.EnsureSubscribed();
        }

        public bool CanHandle(string command)
        {
            return string.Equals(command, ProtocolConstants.CommandQaClick, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandQaTap, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandQaSwipe, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandQaKey, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandQaWaitUntil, StringComparison.Ordinal);
        }

        public string Handle(string command, string argumentsJson)
        {
            if (IsDeferred(command))
            {
                throw new InvalidOperationException("Deferred QA command must be started through StartDeferred: " + command);
            }

            RequirePlayMode();

            if (string.Equals(command, ProtocolConstants.CommandQaClick, StringComparison.Ordinal))
            {
                return HandleClick(argumentsJson);
            }

            if (string.Equals(command, ProtocolConstants.CommandQaTap, StringComparison.Ordinal))
            {
                return HandleTap(argumentsJson);
            }

            if (string.Equals(command, ProtocolConstants.CommandQaKey, StringComparison.Ordinal))
            {
                return HandleKey(argumentsJson);
            }

            throw new InvalidOperationException("Unhandled QA command: " + command);
        }

        public bool IsDeferred(string command)
        {
            return string.Equals(command, ProtocolConstants.CommandQaSwipe, StringComparison.Ordinal)
                || string.Equals(command, ProtocolConstants.CommandQaWaitUntil, StringComparison.Ordinal);
        }

        public void StartDeferred(
            string command,
            string argumentsJson,
            TaskCompletionSource<ResponseEnvelope> completion,
            string projectHash)
        {
            if (completion.Task.IsCompleted)
            {
                return;
            }

            RequirePlayMode();
            string requestId = GetRequestId(completion);

            if (string.Equals(command, ProtocolConstants.CommandQaWaitUntil, StringComparison.Ordinal))
            {
                StartWaitUntilDeferred(argumentsJson, completion, projectHash, requestId);
                return;
            }

            if (string.Equals(command, ProtocolConstants.CommandQaSwipe, StringComparison.Ordinal))
            {
#if ENABLE_INPUT_SYSTEM
                StartSwipeDeferred(argumentsJson, completion, projectHash, requestId);
#else
                throw CreateInputSystemRequiredException("qa swipe");
#endif
                return;
            }

            throw new InvalidOperationException("Unhandled deferred QA command: " + command);
        }

        private static void RequirePlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new CommandFailureException("QA_NOT_PLAYING", "QA commands require Play Mode.", false, null);
            }
        }

        private static string HandleClick(string argumentsJson)
        {
            QaClickArgs args = ProtocolJson.Deserialize<QaClickArgs>(argumentsJson) ?? new QaClickArgs();

            GameObject? target = null;
            string? resolvedQaId = null;

            if (!string.IsNullOrWhiteSpace(args.qaId))
            {
                resolvedQaId = args.qaId;
                if (!QaTargetRegistry.TryResolve(args.qaId!, out target) || target == null)
                {
                    throw new CommandFailureException("QA_TARGET_NOT_FOUND", $"No active GameObject found for QA ID '{args.qaId}'.", false, null);
                }
            }
            else if (!string.IsNullOrWhiteSpace(args.target))
            {
                target = GameObject.Find(args.target!);
                if (target == null)
                {
                    throw new CommandFailureException("QA_TARGET_NOT_FOUND", $"No active GameObject found at path '{args.target}'.", false, null);
                }
            }
            else
            {
                throw new CommandFailureException("QA_MISSING_TARGET", "Either --qa-id or --target is required for qa click.", false, null);
            }

            string resolvedPath = GetGameObjectPath(target);
            ClickGameObject(target);

            return ProtocolJson.Serialize(new QaClickPayload
            {
                targetFound = true,
                resolvedPath = resolvedPath,
                qaId = resolvedQaId,
            });
        }

        private static string HandleTap(string argumentsJson)
        {
            QaTapArgs args = ProtocolJson.Deserialize<QaTapArgs>(argumentsJson) ?? new QaTapArgs();

#if ENABLE_INPUT_SYSTEM
            QaInputSimulator.TapAtScreenPosition(args.x, args.y);
#else
            throw CreateInputSystemRequiredException("qa tap");
#endif

            return ProtocolJson.Serialize(new QaTapPayload
            {
                completed = true,
            });
        }

        private static string HandleKey(string argumentsJson)
        {
            QaKeyArgs args = ProtocolJson.Deserialize<QaKeyArgs>(argumentsJson) ?? new QaKeyArgs();
            if (string.IsNullOrWhiteSpace(args.key))
            {
                throw new CommandFailureException("QA_MISSING_KEY", "--key is required for qa key.", false, null);
            }

#if ENABLE_INPUT_SYSTEM
            QaInputSimulator.SimulateKey(args.key);
#else
            throw CreateInputSystemRequiredException("qa key");
#endif
            return ProtocolJson.Serialize(new QaKeyPayload
            {
                completed = true,
            });
        }

        private static void StartWaitUntilDeferred(
            string argumentsJson,
            TaskCompletionSource<ResponseEnvelope> completion,
            string projectHash,
            string requestId)
        {
            QaWaitUntilArgs args = ProtocolJson.Deserialize<QaWaitUntilArgs>(argumentsJson) ?? new QaWaitUntilArgs();
            ValidateWaitUntilArgs(args);
            int timeoutMs = args.timeoutMs > 0 ? args.timeoutMs : ProtocolConstants.DefaultQaWaitUntilTimeoutMs;
            var stopwatch = Stopwatch.StartNew();

            void Poll()
            {
                if (completion.Task.IsCompleted)
                {
                    EditorApplication.update -= Poll;
                    return;
                }

                try
                {
                    EnsureDeferredPlayMode();

                    if (CheckCondition(args, out string? reason))
                    {
                        int elapsedMs = GetElapsedMilliseconds(stopwatch);
                        CompleteSuccess(new QaWaitUntilPayload
                        {
                            conditionMet = true,
                            elapsedMs = elapsedMs,
                            reason = reason,
                        });
                        return;
                    }

                    int elapsedMs = GetElapsedMilliseconds(stopwatch);
                    if (elapsedMs >= timeoutMs)
                    {
                        EditorApplication.update -= Poll;
                        stopwatch.Stop();
                        completion.TrySetResult(ResponseEnvelope.Failure(
                            requestId,
                            projectHash,
                            "QA_WAIT_TIMEOUT",
                            "Timeout reached before condition was met.",
                            false,
                            stopwatch.ElapsedMilliseconds,
                            ProtocolConstants.TransportLive,
                            null));
                        return;
                    }
                }
                catch (Exception exception)
                {
                    CompleteFailure(exception);
                }
            }

            void CompleteSuccess(QaWaitUntilPayload payload)
            {
                EditorApplication.update -= Poll;
                stopwatch.Stop();
                completion.TrySetResult(CreateSuccessResponse(requestId, projectHash, payload, stopwatch.ElapsedMilliseconds));
            }

            void CompleteFailure(Exception exception)
            {
                EditorApplication.update -= Poll;
                stopwatch.Stop();
                completion.TrySetResult(CreateFailureResponse(requestId, projectHash, exception, stopwatch.ElapsedMilliseconds));
            }

            EditorApplication.update += Poll;
            Poll();
        }

#if ENABLE_INPUT_SYSTEM
        private static void StartSwipeDeferred(
            string argumentsJson,
            TaskCompletionSource<ResponseEnvelope> completion,
            string projectHash,
            string requestId)
        {
            QaSwipeArgs args = ProtocolJson.Deserialize<QaSwipeArgs>(argumentsJson) ?? new QaSwipeArgs();
            QaInputSimulator.SwipeOperation swipe = QaInputSimulator.BeginSwipe(
                new Vector2(args.fromX, args.fromY),
                new Vector2(args.toX, args.toY),
                args.durationMs);
            var stopwatch = Stopwatch.StartNew();

            void Poll()
            {
                if (completion.Task.IsCompleted)
                {
                    EditorApplication.update -= Poll;
                    swipe.Abort();
                    return;
                }

                try
                {
                    EnsureDeferredPlayMode();

                    if (swipe.Advance())
                    {
                        EditorApplication.update -= Poll;
                        stopwatch.Stop();
                        completion.TrySetResult(CreateSuccessResponse(
                            requestId,
                            projectHash,
                            new QaSwipePayload
                            {
                                completed = true,
                            },
                            stopwatch.ElapsedMilliseconds));
                    }
                }
                catch (Exception exception)
                {
                    EditorApplication.update -= Poll;
                    swipe.Abort();
                    stopwatch.Stop();
                    completion.TrySetResult(CreateFailureResponse(requestId, projectHash, exception, stopwatch.ElapsedMilliseconds));
                }
            }

            EditorApplication.update += Poll;
            Poll();
        }
#endif

        private static void ValidateWaitUntilArgs(QaWaitUntilArgs args)
        {
            bool hasCondition = !string.IsNullOrWhiteSpace(args.scene)
                || !string.IsNullOrWhiteSpace(args.logContains)
                || !string.IsNullOrWhiteSpace(args.objectExists);

            if (!hasCondition)
            {
                throw new CommandFailureException(
                    "QA_MISSING_CONDITION",
                    "At least one condition (--scene, --log-contains, --object-exists) is required.",
                    false,
                    null);
            }
        }

        private static void EnsureDeferredPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new CommandFailureException("QA_NOT_PLAYING", "QA commands require Play Mode.", false, null);
            }
        }

        private static string GetRequestId(TaskCompletionSource<ResponseEnvelope> completion)
        {
            string? requestId = completion.Task.AsyncState as string;
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new InvalidOperationException("Deferred QA request ID is missing.");
            }

            return requestId;
        }

        private static int GetElapsedMilliseconds(Stopwatch stopwatch)
        {
            return stopwatch.ElapsedMilliseconds >= int.MaxValue
                ? int.MaxValue
                : (int)stopwatch.ElapsedMilliseconds;
        }

        private static ResponseEnvelope CreateSuccessResponse(string requestId, string projectHash, object payload, long durationMs)
        {
            return ResponseEnvelope.Success(
                requestId,
                projectHash,
                ProtocolJson.Serialize(payload),
                durationMs,
                ProtocolConstants.TransportLive);
        }

        private static ResponseEnvelope CreateFailureResponse(
            string requestId,
            string projectHash,
            Exception exception,
            long durationMs)
        {
            if (exception is CommandFailureException failure)
            {
                return ResponseEnvelope.Failure(
                    requestId,
                    projectHash,
                    failure.ErrorCode,
                    failure.Message,
                    failure.IsRetryable,
                    durationMs,
                    ProtocolConstants.TransportLive,
                    failure.Details);
            }

            return ResponseEnvelope.Failure(
                requestId,
                projectHash,
                "COMMAND_FAILED",
                exception.Message,
                false,
                durationMs,
                ProtocolConstants.TransportLive,
                exception.ToString());
        }

        private static CommandFailureException CreateInputSystemRequiredException(string commandName)
        {
            return new CommandFailureException(
                "QA_INPUT_SYSTEM_REQUIRED",
                $"{commandName} requires the Unity Input System package (com.unity.inputsystem).",
                false,
                null);
        }

        private static void ClickGameObject(GameObject target)
        {
            EventSystem eventSystem = RequireEventSystem();
            var pointerData = new PointerEventData(eventSystem)
            {
                position = GetScreenPosition(target),
            };

            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);
        }

        private static EventSystem RequireEventSystem()
        {
            EventSystem? eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                throw new CommandFailureException("QA_NO_EVENT_SYSTEM", "An active EventSystem is required for QA pointer commands.", false, null);
            }

            return eventSystem;
        }

        private static bool CheckCondition(QaWaitUntilArgs args, out string? reason)
        {
            reason = null;
            var reasons = new List<string>();

            if (!string.IsNullOrWhiteSpace(args.scene))
            {
                Scene activeScene = SceneManager.GetActiveScene();
                if (!string.Equals(activeScene.name, args.scene, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                reasons.Add($"Active scene is '{activeScene.name}'.");
            }

            if (!string.IsNullOrWhiteSpace(args.logContains))
            {
                ConsoleLogEntry[] entries = ConsoleLogBuffer.Read(100, string.Empty);
                bool found = false;
                foreach (ConsoleLogEntry entry in entries)
                {
                    if (entry.message.Contains(args.logContains!, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }

                reasons.Add($"Log contains '{args.logContains}'.");
            }

            if (!string.IsNullOrWhiteSpace(args.objectExists))
            {
                GameObject? target;
                if (!QaTargetRegistry.TryResolve(args.objectExists!, out target) || target == null)
                {
                    target = GameObject.Find(args.objectExists!);
                }

                if (target == null)
                {
                    return false;
                }

                reasons.Add($"Object '{args.objectExists}' exists.");
            }

            reason = reasons.Count > 0 ? string.Join(" ", reasons) : null;
            return true;
        }

        private static Vector2 GetScreenPosition(GameObject gameObject)
        {
            RectTransform? rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                var corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                Vector3 center = (corners[0] + corners[2]) * 0.5f;

                Canvas? canvas = gameObject.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    Camera? canvasCamera = canvas.worldCamera ?? Camera.main;
                    if (canvasCamera != null)
                    {
                        return RectTransformUtility.WorldToScreenPoint(canvasCamera, center);
                    }
                }

                return new Vector2(center.x, center.y);
            }

            Camera? mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 screenPoint = mainCamera.WorldToScreenPoint(gameObject.transform.position);
                return new Vector2(screenPoint.x, screenPoint.y);
            }

            return Vector2.zero;
        }

        private static string GetGameObjectPath(GameObject gameObject)
        {
            string path = gameObject.name;
            Transform? parent = gameObject.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return "/" + path;
        }
    }
}

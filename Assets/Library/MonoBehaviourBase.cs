using System.Runtime.CompilerServices;
using System.Text;
using BitBox.Library.Constants;
using BitBox.Library.Eventing;
using UnityEngine;
using BitBox.Library.Constants.Enums;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Possible improvements:
// - Log once
// - Log every X seconds
namespace BitBox.Library
{
    public abstract class MonoBehaviourBase : MonoBehaviour
    {
        [PropertyOrder(-1000)]
        [FoldoutGroup("Base Utilities")]
        [Header("Debug Settings")]
        [Tooltip("Enable to draw gizmos for this object.")]
        public bool ShowGizmos = false;

        [PropertyOrder(-1000)]
        [FoldoutGroup("Base Utilities")]
        [Tooltip("Minimum log level to output to the console.")]
        public LogLevel logLevel = LogLevel.Info;

#if UNITY_EDITOR
        private static bool IsPlayingInEditor => Application.isPlaying;

        [PropertyOrder(-1000)]
        [FoldoutGroup("Runtime Utilities")]
        [Button]
        [ShowIf(nameof(IsPlayingInEditor))]
        private void ApplyRuntimeValues()
        {
            ApplyRuntimeValuesInternal();
        }

        private void ApplyRuntimeValuesInternal()
        {
            EditorUtility.SetDirty(this);
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        protected MessageBus _sceneMessageBus;
        protected MessageBus _globalMessageBus;
        private Logging.Logger _loggerInstance;

        private void EnsureLoggerInitialized()
        {
            if (_loggerInstance != null)
            {
                return;
            }

            string objectName = gameObject != null ? gameObject.name : GetType().Name;
            _loggerInstance = new Logging.Logger(
                objectName,
                GetInstanceID(),
                () => logLevel
            );
        }

        private void Awake()
        {
            _globalMessageBus = GlobalStaticData.GlobalMessageBus;
            _sceneMessageBus = GameObject.FindWithTag(Tags.SceneMessageBus)?.GetComponent<MessageBus>();

            EnsureLoggerInitialized();

            OnAwakened();
        }

        private void OnEnable()
        {
            var requireRuntimeMessageBuses = Application.isPlaying;

            if (!_globalMessageBus)
            {
                _globalMessageBus = GlobalStaticData.GlobalMessageBus;
                if (requireRuntimeMessageBuses)
                {
                    Assert.IsNotNull(_globalMessageBus, "Game Manager Message Bus not found!");
                }
            }

            if (!_sceneMessageBus)
            {
                _sceneMessageBus = GameObject.FindWithTag(Tags.SceneMessageBus)?.GetComponent<MessageBus>();

                if (requireRuntimeMessageBuses && !_sceneMessageBus)
                {
                    LogWarning($"Scene Message Bus reference was not set on {gameObject.name}. Attempting to find in scene. Found: {_sceneMessageBus != null}");
                }
            }

            OnEnabled();
        }

        private void OnDisable()
        {
            OnDisabled();
        }

        private void Start()
        {
            OnStarted();
        }

        private void Update()
        {
            OnUpdated();
        }

        private void FixedUpdate()
        {
            OnFixedUpdated();
        }

        private void LateUpdate()
        {
            OnLateUpdated();
        }

        private void OnTriggerEnter(Collider other)
        {
            OnTriggerEntered(other);
        }

        private void OnTriggerExit(Collider other)
        {
            OnTriggerExited(other);
        }

        private void OnDestroy()
        {
            OnDestroyed();
        }

        private void OnDrawGizmos()
        {
            if (ShowGizmos)
            {
                OnDrawnGizmos();
            }
        }

        protected virtual void OnAwakened() { }
        protected virtual void OnEnabled() { }
        protected virtual void OnStarted() { }
        protected virtual void OnUpdated() { }
        protected virtual void OnFixedUpdated() { }
        protected virtual void OnLateUpdated() { }
        protected virtual void OnDisabled() { }
        protected virtual void OnDestroyed() { }
        protected virtual void OnDrawnGizmos() { }
        protected virtual void OnTriggerEntered(Collider other) { }
        protected virtual void OnTriggerExited(Collider other) { }

        [HideInCallstack]
        protected void LogDebug(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0
        )
        {
            EnsureLoggerInitialized();
            _loggerInstance.Debug(message, filePath, lineNumber);
        }

        [HideInCallstack]
        protected void LogDebug(
            string message,
            (string key, object value) data,
            params (string key, object value)[] additionalData
        ) => LogWithStructuredData(LogLevel.Debug, message, data, additionalData);

        [HideInCallstack]
        protected void LogInfo(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0
        )
        {
            EnsureLoggerInitialized();
            _loggerInstance.Info(message, filePath, lineNumber);
        }

        [HideInCallstack]
        protected void LogInfo(
            string message,
            (string key, object value) data,
            params (string key, object value)[] additionalData
        ) => LogWithStructuredData(LogLevel.Info, message, data, additionalData);

        [HideInCallstack]
        protected void LogWarning(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0
        )
        {
            EnsureLoggerInitialized();
            _loggerInstance.Warning(message, filePath, lineNumber);
        }

        [HideInCallstack]
        protected void LogWarning(
            string message,
            (string key, object value) data,
            params (string key, object value)[] additionalData
        ) => LogWithStructuredData(LogLevel.Warning, message, data, additionalData);

        [HideInCallstack]
        protected void LogError(string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0
        )
        {
            EnsureLoggerInitialized();
            _loggerInstance.Error(message, filePath, lineNumber);
        }

        [HideInCallstack]
        protected void LogError(
            string message,
            (string key, object value) data,
            params (string key, object value)[] additionalData
        ) => LogWithStructuredData(LogLevel.Error, message, data, additionalData);

        [HideInCallstack]
        private void LogWithStructuredData(
            LogLevel level,
            string message,
            (string key, object value) firstData,
            (string key, object value)[] additionalData
        )
        {
            EnsureLoggerInitialized();
            string formattedMessage = BuildStructuredMessage(message, firstData, additionalData);
            ResolveCallerLocation(out string filePath, out int lineNumber);

            switch (level)
            {
                case LogLevel.Debug:
                    _loggerInstance.Debug(formattedMessage, filePath, lineNumber);
                    break;
                case LogLevel.Info:
                    _loggerInstance.Info(formattedMessage, filePath, lineNumber);
                    break;
                case LogLevel.Warning:
                    _loggerInstance.Warning(formattedMessage, filePath, lineNumber);
                    break;
                case LogLevel.Error:
                    _loggerInstance.Error(formattedMessage, filePath, lineNumber);
                    break;
            }
        }

        private static string BuildStructuredMessage(
            string message,
            (string key, object value) firstData,
            (string key, object value)[] additionalData
        )
        {
            StringBuilder builder = new StringBuilder(
                (message?.Length ?? 0) + 48 + (additionalData?.Length ?? 0) * 16
            );
            builder.Append(message);
            builder.Append(" | ");
            AppendData(builder, firstData.key, firstData.value);

            if (additionalData != null)
            {
                for (int i = 0; i < additionalData.Length; i++)
                {
                    builder.Append(", ");
                    AppendData(builder, additionalData[i].key, additionalData[i].value);
                }
            }

            return builder.ToString();
        }

        private static void AppendData(StringBuilder builder, string key, object value)
        {
            builder.Append(string.IsNullOrWhiteSpace(key) ? "<null>" : key);
            builder.Append('=');
            builder.Append(value != null ? value : "<null>");
        }

        private static void ResolveCallerLocation(out string filePath, out int lineNumber)
        {
            var stackTrace = new System.Diagnostics.StackTrace(true);
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                var method = frame?.GetMethod();
                var declaringType = method?.DeclaringType;
                if (declaringType == null)
                {
                    continue;
                }

                if (declaringType == typeof(MonoBehaviourBase) || declaringType == typeof(Logging.Logger))
                {
                    continue;
                }

                string resolvedFilePath = frame.GetFileName();
                int resolvedLineNumber = frame.GetFileLineNumber();
                filePath = string.IsNullOrEmpty(resolvedFilePath) ? "UnknownFile.cs" : resolvedFilePath;
                lineNumber = resolvedLineNumber > 0 ? resolvedLineNumber : 0;
                return;
            }

            filePath = "UnknownFile.cs";
            lineNumber = 0;
        }

        protected T FindComponentInParents<T>(bool includeSelf = true, bool assertIfNotFound = true) where T : Component
        {
            var current = includeSelf ? transform : transform.parent;
            while (current != null)
            {
                T comp = current.GetComponent<T>();
                if (comp != null)
                {
                    return comp;
                }

                current = current.parent;
            }

            if (assertIfNotFound)
            {
                Assert.IsTrue(false, $"Component of type {typeof(T)} not found in parents of {gameObject.name}");
            }

            return null;
        }
    }
}

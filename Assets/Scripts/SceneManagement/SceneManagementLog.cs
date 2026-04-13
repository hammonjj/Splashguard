using System.Runtime.CompilerServices;
using BitBox.Library.Constants.Enums;
using BitBox.Library.Logging;

namespace BitBox.Toymageddon.SceneManagement
{
    public static class SceneManagementLog
    {
        private static readonly Logger Logger = new Logger(
            "SceneManagement",
            0,
            () => CurrentLogLevel
        );

        public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Info;

        [UnityEngine.HideInCallstack]
        public static void Debug(
            string category,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0
        )
        {
            Logger.Debug($"[{category}] {message}", filePath, lineNumber);
        }

        [UnityEngine.HideInCallstack]
        public static void Info(
            string category,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0
        )
        {
            Logger.Info($"[{category}] {message}", filePath, lineNumber);
        }

        [UnityEngine.HideInCallstack]
        public static void Warning(
            string category,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0
        )
        {
            Logger.Warning($"[{category}] {message}", filePath, lineNumber);
        }

        [UnityEngine.HideInCallstack]
        public static void Error(
            string category,
            string message,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0
        )
        {
            Logger.Error($"[{category}] {message}", filePath, lineNumber);
        }
    }
}

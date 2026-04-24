using System;
using System.Collections;
using BitBox.Library.Constants.Enums;
using UnityEngine;

namespace BitBox.Toymageddon.SceneManagement
{
    public readonly struct GameplaySceneStartupContext
    {
        private readonly Action<float, string> _reportProgress;

        public GameplaySceneStartupContext(
            MacroSceneType sceneType,
            Action<float, string> reportProgress)
        {
            SceneType = sceneType;
            _reportProgress = reportProgress;
        }

        public MacroSceneType SceneType { get; }

        public void ReportProgress(float progress, string progressText)
        {
            _reportProgress?.Invoke(Mathf.Clamp01(progress), progressText ?? string.Empty);
        }
    }

    public interface IGameplaySceneStartupTask
    {
        bool ShouldRunForScene(MacroSceneType sceneType);
        IEnumerator ExecuteStartup(GameplaySceneStartupContext context);
    }
}

using System.Collections.Generic;
using BitBox.Library.Constants.Enums;

namespace BitBox.Toymageddon.SceneManagement
{
    public sealed class SceneTransitionPlan
    {
        public SceneTransitionPlan(
            MacroSceneType currentScene,
            MacroSceneType targetScene,
            List<string> scenesToLoad,
            List<string> scenesToUnload,
            List<string> scenesPreserved,
            List<string> dynamicScenesToUnload,
            bool isNoOp,
            string summary
        )
        {
            CurrentScene = currentScene;
            TargetScene = targetScene;
            ScenesToLoad = scenesToLoad ?? new List<string>();
            ScenesToUnload = scenesToUnload ?? new List<string>();
            ScenesPreserved = scenesPreserved ?? new List<string>();
            DynamicScenesToUnload = dynamicScenesToUnload ?? new List<string>();
            IsNoOp = isNoOp;
            Summary = summary ?? string.Empty;
        }

        public MacroSceneType CurrentScene { get; }
        public MacroSceneType TargetScene { get; }
        public List<string> ScenesToLoad { get; }
        public List<string> ScenesToUnload { get; }
        public List<string> ScenesPreserved { get; }
        public List<string> DynamicScenesToUnload { get; }
        public bool IsNoOp { get; }
        public string Summary { get; }

        public int TotalOperationCount => ScenesToLoad.Count + ScenesToUnload.Count + DynamicScenesToUnload.Count;

        public IReadOnlyList<string> GetCombinedUnloadPaths()
        {
            var combined = new List<string>(ScenesToUnload.Count + DynamicScenesToUnload.Count);
            combined.AddRange(ScenesToUnload);
            combined.AddRange(DynamicScenesToUnload);
            return combined;
        }
    }
}

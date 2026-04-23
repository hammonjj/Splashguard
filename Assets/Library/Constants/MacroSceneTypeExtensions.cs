using BitBox.Library.Constants.Enums;

namespace BitBox.Library.Constants
{
    public static class MacroSceneTypeExtensions
    {
        public static bool IsGameplayScene(this MacroSceneType sceneType)
        {
            return sceneType == MacroSceneType.HubWorld
                || sceneType == MacroSceneType.Sandbox
                || sceneType == MacroSceneType.CombatArena;
        }
    }
}

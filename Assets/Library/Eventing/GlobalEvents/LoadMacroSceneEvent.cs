using BitBox.Library.Constants.Enums;

namespace BitBox.Library.Eventing.GlobalEvents
{
    public class LoadMacroSceneEvent
    {
        public MacroSceneType SceneType { get; private set; }

        public LoadMacroSceneEvent(MacroSceneType sceneType)
        {
            SceneType = sceneType;
        }
    }
}

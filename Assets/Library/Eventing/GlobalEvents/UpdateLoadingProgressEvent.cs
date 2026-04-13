namespace BitBox.Library.Eventing.GlobalEvents
{
  public class UpdateLoadingProgressEvent
    {
        public float Progress { get; private set; }
        public string ProgressText { get; private set; }

        public UpdateLoadingProgressEvent(float progress, string progressText = "")
        {
            Progress = progress;
            ProgressText = progressText;
        }
    }
}

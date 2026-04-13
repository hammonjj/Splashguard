using BitBox.Library.CameraUtils;

namespace Bitbox.Toymageddon.Nautical
{
    // Migration shim that preserves existing prefab references while the reusable
    // anchor implementation lives in the shared library assembly.
    public sealed class InteractionCameraAnchors : CameraTargetAnchors
    {
    }
}

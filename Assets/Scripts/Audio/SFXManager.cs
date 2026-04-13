using UnityEngine;
using BitBox.Library;

namespace Bitbox.Toymageddon.Audio
{
  public class SFXManager : MonoBehaviourBase
  {
    [SerializeField] private AudioSource oneShotSource;

    [Header("Volume")]
    [SerializeField][Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField][Range(0f, 1f)] private float sfxVolume = 1f;

    protected override void OnEnabled()
    {
      if (oneShotSource == null)
      {
        oneShotSource = gameObject.AddComponent<AudioSource>();
      }

      LogInfo("SFX Manager ready");
    }

    public void Play(AudioClip clip, float volume = 1f)
    {
      if (clip == null)
      {
        LogWarning("Tried to play null clip");
        return;
      }

      float finalVolume = Mathf.Clamp01(masterVolume * sfxVolume * volume);

      oneShotSource.PlayOneShot(clip, finalVolume);

      LogInfo($"Playing SFX: {clip.name}");
    }
  }
}

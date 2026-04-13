using UnityEngine;

[CreateAssetMenu(fileName = "BackgroundMusicData", menuName = "Audio/Background Music Data")]
public class BackgroundMusicData : ScriptableObject
{
    [Header("Title Menu")]
    public AudioClip[] titleMenuTracks;

    [Header("Loading Screen")]
    public AudioClip[] loadingScreenTracks;

    [Header("Hub")]
    public AudioClip[] hubTracks;
}

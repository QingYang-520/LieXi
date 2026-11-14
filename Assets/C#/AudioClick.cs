using UnityEngine;

public class AudioClick : MonoBehaviour
{
    public AudioClip audioClip;

    public void Play()
    {
        if (audioClip == null) return;

        GameObject go = new GameObject("TempAudio");
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = audioClip;
        source.Play();
        DontDestroyOnLoad(go);
        Destroy(go, audioClip.length);
    }
}
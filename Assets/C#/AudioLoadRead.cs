using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioLoadRead : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneFinishedLoad;
        ReadAndApplyVolume();
    }

    private void OnSceneFinishedLoad(Scene scene, LoadSceneMode mode)
    {
        ReadAndApplyVolume();
    }

    private void ReadAndApplyVolume()
    {
        float vol = PlayerPrefs.GetFloat("MusicVolume", 80f);
        AudioListener.volume = Mathf.Clamp01(vol / 100f);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneFinishedLoad;
    }
}
using UnityEngine;
using UnityEngine.UI;

public class MusicManager : MonoBehaviour
{
    private Slider slider;

    private void Start()
    {
        slider = GetComponent<Slider>();
        float saved = PlayerPrefs.GetFloat("MusicVolume", 80f);
        slider.SetValueWithoutNotify(saved);
        ApplyVolume(saved);
        slider.onValueChanged.AddListener(SetVolume);
    }

    public void SetVolume(float value)
    {
        ApplyVolume(value);
        PlayerPrefs.SetFloat("MusicVolume", value);
    }

    private void ApplyVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value / 100f);
    }
}
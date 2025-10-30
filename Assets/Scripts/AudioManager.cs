using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{
    public AudioMixer audioMixer;
    public Slider musicSlider;
    public Slider sfxSlider;

    private const float minVolume = 0.0001f; 
    private const float muteVolume = -80f;  

    void Start()
    {
        PlayerPrefs.DeleteKey("MusicVolume");
        PlayerPrefs.DeleteKey("SFXVolume");

        float musicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);

        musicSlider.value = musicVol;
        sfxSlider.value = sfxVol;

        ApplyMusicVolume(musicVol);
        ApplySFXVolume(sfxVol);

        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    public void SetMusicVolume(float volume)
    {
        ApplyMusicVolume(volume);
        PlayerPrefs.SetFloat("MusicVolume", volume);
    }

    public void SetSFXVolume(float volume)
    {
        ApplySFXVolume(volume);
        PlayerPrefs.SetFloat("SFXVolume", volume);
    }

    private void ApplyMusicVolume(float volume)
    {
        float dB = (volume <= 0.0001f) ? muteVolume : Mathf.Log10(volume) * 20;
        audioMixer.SetFloat("MusicVolume", dB);
    }

    private void ApplySFXVolume(float volume)
    {
        float dB = (volume <= 0.0001f) ? muteVolume : Mathf.Log10(volume) * 20;
        audioMixer.SetFloat("SFXVolume", dB);
    }
}

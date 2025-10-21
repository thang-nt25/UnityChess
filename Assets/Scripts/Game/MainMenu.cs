using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Menu BGM")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip menuBgm;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.6f;
    [SerializeField, Range(0.05f, 3f)] private float fadeTime = 0.5f;

    [Header("UI Panels")]
    public GameObject mainButtonsContainer;
    public GameObject playModePopup;

    private string gameSceneName = "Board";

    private void Awake()
    {
        EnsureMusicSource();
        PlayMenuBgm();
        if (playModePopup != null) playModePopup.SetActive(false);
        if (mainButtonsContainer != null) mainButtonsContainer.SetActive(true);
    }

    private void EnsureMusicSource()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.GetComponent<AudioSource>();
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        }
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;
    }

    private void PlayMenuBgm()
    {
        if (menuBgm == null) return;
        if (musicSource.clip != menuBgm) musicSource.clip = menuBgm;
        if (!musicSource.isPlaying) musicSource.Play();
    }

    private IEnumerator FadeOutAndStop()
    {
        float start = musicSource.volume;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(start, 0f, t / fadeTime);
            yield return null;
        }
        musicSource.Stop();
        musicSource.volume = musicVolume;
    }

    private IEnumerator FadeThenLoad(string sceneName)
    {
        if (musicSource != null && musicSource.isPlaying)
            yield return StartCoroutine(FadeOutAndStop());
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator FadeThenQuit()
    {
        if (musicSource != null && musicSource.isPlaying)
            yield return StartCoroutine(FadeOutAndStop());

        PlayerPrefs.DeleteKey("GameMode");

        Application.Quit();
    }

    public void ShowPlayModePopup()
    {
        playModePopup.SetActive(true);
        mainButtonsContainer.SetActive(false);
    }

    public void HidePlayModePopup()
    {
        playModePopup.SetActive(false);
        mainButtonsContainer.SetActive(true);
    }

    public void StartPlayVsPlayer()
    {
        PlayerPrefs.SetString("GameMode", "PlayerVsPlayer");
        PlayerPrefs.Save();
        StartCoroutine(FadeThenLoad(gameSceneName));
    }

    public void StartPlayVsAI_White()
    {
        PlayerPrefs.SetString("GameMode", "PlayerVsAI_White");
        PlayerPrefs.Save();
        StartCoroutine(FadeThenLoad(gameSceneName));
    }

    public void StartPlayVsAI_Black()
    {
        PlayerPrefs.SetString("GameMode", "PlayerVsAI_Black");
        PlayerPrefs.Save();
        StartCoroutine(FadeThenLoad(gameSceneName));
    }

    public void OpenSettings()
    {
    }

    public void QuitGame()
    {
        StartCoroutine(FadeThenQuit());
    }
}
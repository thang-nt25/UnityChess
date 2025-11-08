using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static GameManager;

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
    public GameObject settingsPopup;
    public GameObject historyPopup;
    public GameObject whiteAIDifficultyPopup;
    public GameObject blackAIDifficultyPopup;

    private List<GameObject> allPopups;
    private string gameSceneName = "Board";

    private void Awake()
    {
        PlayerPrefs.DeleteKey("PlayerIsWhiteKey");
        PlayerPrefs.DeleteKey("PlayerIsWhite");

        EnsureMusicSource();
        PlayMenuBgm();

        allPopups = new List<GameObject>
        {
            playModePopup,
            settingsPopup,
            historyPopup,
            whiteAIDifficultyPopup,
            blackAIDifficultyPopup
        };

        CloseAllPopups();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.DeleteKey("GameMode");
        PlayerPrefs.DeleteKey("WhiteAIDifficulty");
        PlayerPrefs.DeleteKey("BlackAIDifficulty");
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

        PlayerPrefs.Save();
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator FadeThenQuit()
    {
        if (musicSource != null && musicSource.isPlaying)
            yield return StartCoroutine(FadeOutAndStop());

        PlayerPrefs.DeleteKey("GameMode");
        PlayerPrefs.DeleteKey("WhiteAIDifficulty");
        PlayerPrefs.DeleteKey("BlackAIDifficulty");

        Application.Quit();
    }

    // --- Generic Popup Management ---

    public void OpenPopup(GameObject popupToOpen)
    {
        if (mainButtonsContainer != null)
        {
            mainButtonsContainer.SetActive(false);
        }

        foreach (var p in allPopups)
        {
            if (p != null && p != popupToOpen)
            {
                p.SetActive(false);
            }
        }

        if (popupToOpen != null)
        {
            popupToOpen.SetActive(true);
        }
    }

    public void CloseAllPopups()
    {
        foreach (var p in allPopups)
        {
            if (p != null)
            {
                p.SetActive(false);
            }
        }

        if (mainButtonsContainer != null)
        {
            mainButtonsContainer.SetActive(true);
        }
    }

    // --- UI Button Actions ---

    public void ShowPlayModePopup()
    {
        OpenPopup(playModePopup);
    }

    public void OpenSettings()
    {
        OpenPopup(settingsPopup);
    }

    public void OpenHistory()
    {
        OpenPopup(historyPopup);
    }

    public void ShowWhiteAIDifficultyPopup()
    {
        PlayerPrefs.SetString("GameMode", AIMode.HumanVsAI_White.ToString());
        PlayerPrefs.DeleteKey("BlackAIDifficulty");
        OpenPopup(whiteAIDifficultyPopup);
    }

    public void ShowBlackAIDifficultyPopup()
    {
        PlayerPrefs.SetString("GameMode", AIMode.HumanVsAI_Black.ToString());
        PlayerPrefs.DeleteKey("WhiteAIDifficulty");
        OpenPopup(blackAIDifficultyPopup);
    }

    public void ShowWhiteAIDifficultyPopup_AIVsAI()
    {
        PlayerPrefs.SetString("GameMode", AIMode.AIVsAI.ToString());
        OpenPopup(whiteAIDifficultyPopup);
    }
    
    public void ReturnToPlayModePopup()
    {
        OpenPopup(playModePopup);
    }

    // --- Game Start and Difficulty Settings ---

    public void SetWhiteAIDifficultyAndContinue(int difficulty)
    {
        PlayerPrefs.SetInt("WhiteAIDifficulty", difficulty);
        string mode = PlayerPrefs.GetString("GameMode", AIMode.HumanVsHuman.ToString());

        if (mode == AIMode.AIVsAI.ToString())
        {
            OpenPopup(blackAIDifficultyPopup);
        }
        else
        {
            StartCoroutine(FadeThenLoad(gameSceneName));
        }
    }

    public void SetBlackAIDifficultyAndStart(int difficulty)
    {
        PlayerPrefs.SetInt("BlackAIDifficulty", difficulty);
        StartCoroutine(FadeThenLoad(gameSceneName));
    }

    public void StartPlayVsPlayer()
    {
        PlayerPrefs.SetString("GameMode", AIMode.HumanVsHuman.ToString());
        PlayerPrefs.DeleteKey("WhiteAIDifficulty");
        PlayerPrefs.DeleteKey("BlackAIDifficulty");
        StartCoroutine(FadeThenLoad(gameSceneName));
    }

    public void QuitGame()
    {
        StartCoroutine(FadeThenQuit());
    }
}
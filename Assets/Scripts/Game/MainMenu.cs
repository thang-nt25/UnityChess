using System.Collections;
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

    // Các Popup để chọn cấp độ khó
    public GameObject whiteAIDifficultyPopup;
    public GameObject blackAIDifficultyPopup;

    private string gameSceneName = "Board";

    private void Awake()
    {
        // **QUAN TRỌNG: THÊM DÒNG NÀY ĐỂ XÓA KEY LỖI THỜI CŨ (CHỈ CHẠY MỘT LẦN)**
        PlayerPrefs.DeleteKey("PlayerIsWhiteKey");
        PlayerPrefs.DeleteKey("PlayerIsWhite");
        // ----------------------------------------------------------------------

        EnsureMusicSource();
        PlayMenuBgm();

        // Đảm bảo tất cả các popup độ khó đều ẩn khi Awake
        if (playModePopup != null) playModePopup.SetActive(false);
        if (whiteAIDifficultyPopup != null) whiteAIDifficultyPopup.SetActive(false);
        if (blackAIDifficultyPopup != null) blackAIDifficultyPopup.SetActive(false);
        if (mainButtonsContainer != null) mainButtonsContainer.SetActive(true);
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

    // --- Quản lý UI Popups ---

    public void ShowPlayModePopup()
    {
        playModePopup.SetActive(true);
        mainButtonsContainer.SetActive(false);
    }

    public void HidePlayModePopup()
    {
        playModePopup.SetActive(false);
        mainButtonsContainer.SetActive(true);
        if (whiteAIDifficultyPopup != null) whiteAIDifficultyPopup.SetActive(false);
        if (blackAIDifficultyPopup != null) blackAIDifficultyPopup.SetActive(false);
    }

    // GỌI KHI NHẤN "P vs AI (Trắng)"
    public void ShowWhiteAIDifficultyPopup()
    {
        if (playModePopup != null) playModePopup.SetActive(false);
        if (whiteAIDifficultyPopup != null) whiteAIDifficultyPopup.SetActive(true);
        // Lưu chế độ chơi: Human (Đen) vs AI (Trắng)
        PlayerPrefs.SetString("GameMode", AIMode.HumanVsAI_White.ToString());

        PlayerPrefs.DeleteKey("BlackAIDifficulty");
    }

    // QUAY LẠI TỪ POPUP AI TRẮNG
    public void HideWhiteAIDifficultyPopup()
    {
        if (whiteAIDifficultyPopup != null) whiteAIDifficultyPopup.SetActive(false);
        if (playModePopup != null) playModePopup.SetActive(true);
    }

    // GỌI KHI NHẤN "P vs AI (Đen)"
    public void ShowBlackAIDifficultyPopup()
    {
        if (playModePopup != null) playModePopup.SetActive(false);
        if (blackAIDifficultyPopup != null) blackAIDifficultyPopup.SetActive(true);
        // Lưu chế độ chơi: Human (Trắng) vs AI (Đen)
        PlayerPrefs.SetString("GameMode", AIMode.HumanVsAI_Black.ToString());

        PlayerPrefs.DeleteKey("WhiteAIDifficulty");
    }

    // QUAY LẠI TỪ POPUP AI ĐEN
    public void HideBlackAIDifficultyPopup()
    {
        if (blackAIDifficultyPopup != null) blackAIDifficultyPopup.SetActive(false);
        if (playModePopup != null) playModePopup.SetActive(true);
    }

    // GỌI KHI NHẤN "AI vs AI"
    public void ShowWhiteAIDifficultyPopup_AIVsAI()
    {
        if (playModePopup != null) playModePopup.SetActive(false);
        if (whiteAIDifficultyPopup != null) whiteAIDifficultyPopup.SetActive(true);
        // Lưu chế độ chơi: AI vs AI 
        PlayerPrefs.SetString("GameMode", AIMode.AIVsAI.ToString());
    }

    // --- Hàm lưu cấp độ khó và khởi động game ---

    // HÀM LƯU CẤP ĐỘ AI TRẮNG VÀ BẮT ĐẦU HOẶC CHUYỂN TIẾP
    public void SetWhiteAIDifficultyAndContinue(int difficulty)
    {
        PlayerPrefs.SetInt("WhiteAIDifficulty", difficulty);

        string mode = PlayerPrefs.GetString("GameMode", AIMode.HumanVsHuman.ToString());

        if (mode == AIMode.AIVsAI.ToString())
        {
            // Nếu là AI vs AI, chuyển sang chọn cấp độ AI Đen
            if (whiteAIDifficultyPopup != null) whiteAIDifficultyPopup.SetActive(false);
            if (blackAIDifficultyPopup != null) blackAIDifficultyPopup.SetActive(true);
        }
        else
        {
            // Nếu là Human vs AI Trắng, bắt đầu game
            StartCoroutine(FadeThenLoad(gameSceneName));
        }
    }

    // HÀM LƯU CẤP ĐỘ AI ĐEN VÀ KHỞI ĐỘNG GAME (Dùng cho cả P vs AI Black và AI vs AI)
    public void SetBlackAIDifficultyAndStart(int difficulty)
    {
        PlayerPrefs.SetInt("BlackAIDifficulty", difficulty);
        StartCoroutine(FadeThenLoad(gameSceneName));
    }


    // --- Hàm Khởi động Game trực tiếp ---

    public void StartPlayVsPlayer()
    {
        PlayerPrefs.SetString("GameMode", AIMode.HumanVsHuman.ToString());

        PlayerPrefs.DeleteKey("WhiteAIDifficulty");
        PlayerPrefs.DeleteKey("BlackAIDifficulty");

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
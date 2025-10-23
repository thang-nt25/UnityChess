
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Thêm thư viện TextMeshPro
using System.Collections.Generic; // Thêm thư viện để dùng List

public class SettingsMenuController : MonoBehaviour
{
    public GameObject settingsPopup; 
    public GameObject historyViewPanel;

    [Header("History UI")]
    public GameObject historyItemPrefab; // Khuôn mẫu cho mỗi mục lịch sử
    public Transform historyContentParent; // "Content" của Scroll View

    void Start()
    {
        if (settingsPopup != null) settingsPopup.SetActive(false);
        if (historyViewPanel != null) historyViewPanel.SetActive(false);
    }

    public void OnSettingsButtonPressed()
    {
        if (settingsPopup != null) settingsPopup.SetActive(true);
    }

    public void OnBackButtonPressed()
    {
        if (settingsPopup != null) settingsPopup.SetActive(false);
    }

    public void OnViewHistoryButtonPressed()
    {
        if (historyViewPanel != null && settingsPopup != null)
        {
            historyViewPanel.SetActive(true);
            settingsPopup.SetActive(false);
            PopulateHistoryView(); // Gọi hàm để đổ dữ liệu vào UI
        }
    }

    public void OnHistoryBackButtonPressed()
    {
        if (historyViewPanel != null && settingsPopup != null)
        {
            historyViewPanel.SetActive(false);
            settingsPopup.SetActive(true);
            ClearHistoryView(); // Dọn dẹp danh sách khi quay lại
        }
    }

    void PopulateHistoryView()
    {
        ClearHistoryView();

        GameHistory history = HistoryManager.LoadHistory();
        if (history == null || history.allGames.Count == 0)
        {
            Debug.Log("Không có lịch sử trận đấu.");
            return;
        }

        // Đảo ngược danh sách để hiển thị trận gần nhất lên đầu
        history.allGames.Reverse();

        foreach (GameHistoryEntry entry in history.allGames)
        {
            GameObject itemGO = Instantiate(historyItemPrefab, historyContentParent);
            
            // Tìm các thành phần Text trong prefab vừa tạo
            TextMeshProUGUI resultText = itemGO.transform.Find("ResultText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI modeText = itemGO.transform.Find("ModeText").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI dateText = itemGO.transform.Find("DateText").GetComponent<TextMeshProUGUI>();

            // Gán dữ liệu
            if (resultText != null) resultText.text = entry.gameResult;
            if (modeText != null) modeText.text = entry.gameMode;
            if (dateText != null) dateText.text = entry.dateTime;
        }
    }

    void ClearHistoryView()
    {
        foreach (Transform child in historyContentParent)
        {
            Destroy(child.gameObject);
        }
    }
}

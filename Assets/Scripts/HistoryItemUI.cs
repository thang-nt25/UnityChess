using UnityEngine;
using TMPro; // Hoặc 'UnityEngine.UI' nếu bạn dùng Text thường

public class HistoryItemUI : MonoBehaviour
{
    // Kéo 3 Text GameObjects vào đây trong Prefab
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI modeText;
    public TextMeshProUGUI dateText;

    // Hàm này để 'HistoryPanel' gọi và gán dữ liệu
    public void Setup(GameHistoryEntry entry)
    {
        resultText.text = entry.gameResult;
        modeText.text = $"{entry.gameMode} ({entry.totalMoves} nước)";
        dateText.text = entry.dateTime;
    }
}
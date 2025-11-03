using UnityEngine;
using UnityEngine.UI; // Cần cho Button
using TMPro; // Cần cho TextMeshPro
using System.Collections.Generic; // Cần cho List
using UnityEngine.SceneManagement; // Cần để tải scene replay

public class HistoryPanel : MonoBehaviour
{
    // Kéo Prefab "HistoryEntry_Template" của bạn vào đây
    public GameObject historyEntryPrefab;

    // Kéo GameObject "Content" của ScrollView vào đây
    public Transform contentContainer;

    // Hàm này được Unity gọi tự động MỖI KHI panel được bật lên
    void OnEnable()
    {
        DisplayHistory();
    }

    // Đây là hàm DisplayHistory MỚI
    public void DisplayHistory()
    {
        // 1. Xóa các nút cũ đi
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. Tải toàn bộ lịch sử (dùng code HistoryManager CÓ SẴN của bạn)
        GameHistory history = HistoryManager.LoadHistory();
        history.allGames.Reverse(); // Đảo ngược để trận mới nhất ở trên

        // 3. Lặp qua từng trận đấu và tạo UI
        foreach (GameHistoryEntry entry in history.allGames)
        {
            // Tạo một đối tượng mới từ Prefab
            GameObject newEntryObject = Instantiate(historyEntryPrefab, contentContainer);

            // Lấy script 'HistoryItemUI' từ nó
            HistoryItemUI uiItem = newEntryObject.GetComponent<HistoryItemUI>();

            // GỌI HÀM SETUP (Lỗi NullReference sẽ hết ở đây)
            if (uiItem != null)
            {
                uiItem.Setup(entry);
            }
            else
            {
                Debug.LogError("Prefab 'HistoryItemTemplate' thiếu script HistoryItemUI.cs!");
            }

            // Lấy component Button và gán sự kiện click
            Button button = newEntryObject.GetComponent<Button>();
            if (button != null)
            {
                // Tạo một List<string> tạm thời để gửi đi cho an toàn
                List<string> moves = new List<string>(entry.moves);
                button.onClick.AddListener(() => {
                    StartReplay(moves);
                });
            }
        }
    }

    void StartReplay(List<string> movesToReplay)
    {
        Debug.Log($"Chuẩn bị Replay trận đấu với {movesToReplay.Count} nước đi!");

        // *** GỬI DỮ LIỆU SANG SCENE 'BOARD' ***
        // Chúng ta cần một script static trung gian, ví dụ: ReplayManager
        // ReplayManager.SetMoves(movesToReplay); 

        // Tải scene 'Board'
        // SceneManager.LoadScene("Board"); 

        // (Hiện tại chúng ta sẽ chỉ in ra, bước tiếp theo sẽ làm Replay)
        ReplayManager.movesToReplay = new List<string>(movesToReplay);
        UnityEngine.SceneManagement.SceneManager.LoadScene("Board");    
    }
}

using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class HistoryManager
{
    // Xác định đường dẫn file lưu lịch sử. Application.persistentDataPath là một thư mục an toàn trên mọi nền tảng.
    private static readonly string FilePath = Path.Combine(Application.persistentDataPath, "gameHistory.json");

    // Hàm để lưu một ván đấu vừa kết thúc
    public static void SaveGame(string result, string mode, UnityChess.Timeline<UnityChess.HalfMove> halfMoveTimeline)
    {
        GameHistory history = LoadHistory();

        // Chuyển đổi dòng thời gian của các nước đi (HalfMove) thành danh sách chuỗi đơn giản
        List<string> moveNotations = new List<string>();
        for (int i = 0; i <= halfMoveTimeline.HeadIndex; i++)
        {
            UnityChess.HalfMove halfMove = halfMoveTimeline[i];
            UnityChess.Movement move = halfMove.Move;
            // Square.ToString() sẽ cho ra ký hiệu đại số (ví dụ: "e4")
            moveNotations.Add($"{move.Start.ToString()}{move.End.ToString()}");
        }

        GameHistoryEntry newEntry = new GameHistoryEntry
        {
            gameResult = result,
            gameMode = mode,
            dateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            totalMoves = moveNotations.Count,
            moves = moveNotations
        };

        history.allGames.Add(newEntry);

        // Chuyển toàn bộ lịch sử thành chuỗi JSON và ghi ra file
        string json = JsonUtility.ToJson(history, true);
        File.WriteAllText(FilePath, json);
        Debug.Log($"Game saved to: {FilePath}");
    }

    // Hàm để đọc toàn bộ lịch sử từ file
    public static GameHistory LoadHistory()
    {
        if (File.Exists(FilePath))
        {
            string json = File.ReadAllText(FilePath);
            // Tránh lỗi nếu file trống
            if (!string.IsNullOrEmpty(json))
            {
                return JsonUtility.FromJson<GameHistory>(json);
            }
        }

        // Nếu file không tồn tại hoặc trống, trả về một đối tượng lịch sử mới
        return new GameHistory();
    }
}

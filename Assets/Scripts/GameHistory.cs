
using System;
using System.Collections.Generic;

// Dùng [Serializable] để Unity có thể chuyển đổi lớp này sang JSON và ngược lại
[Serializable]
public class GameHistoryEntry
{
    public string gameResult; // Ví dụ: "White Wins", "Black Wins", "Draw"
    public string gameMode;   // Ví dụ: "vs Player", "vs AI"
    public string dateTime;
    public int totalMoves;
    public List<string> moves; // Lưu các nước đi dưới dạng chuỗi, ví dụ: "e2e4"
}

[Serializable]
public class GameHistory
{
    public List<GameHistoryEntry> allGames;

    public GameHistory()
    {
        allGames = new List<GameHistoryEntry>();
    }
}

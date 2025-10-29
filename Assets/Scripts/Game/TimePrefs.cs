using UnityEngine;

public static class TimePrefs
{
    public const string KeySeconds = "TimeModeSeconds";
    public const int Unlimited = -1; // ∞

    public static int GetSecondsOrDefault()
        => PlayerPrefs.GetInt(KeySeconds, Unlimited);

    public static void SetSeconds(int seconds)
    {
        PlayerPrefs.SetInt(KeySeconds, seconds);
        PlayerPrefs.Save();
    }

    public static bool IsUnlimited(int seconds) => seconds < 0;

    // 👇 Thêm hàm này NGAY TRONG class, không được dán ngoài
    public static string FormatLabel(int seconds)
    {
        if (IsUnlimited(seconds)) return "Thời gian: ∞";
        int m = seconds / 60, s = seconds % 60;
        return $"Thời gian: {m:00}:{s:00}";
    }
}

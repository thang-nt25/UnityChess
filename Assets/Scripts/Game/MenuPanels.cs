using UnityEngine;
using TMPro;

public class MenuPanels : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject timePanel;

    [Header("Button Text (Hiển thị thời gian)")]
    [SerializeField] private TextMeshProUGUI selectTimeButtonText;

    private void Start()
    {
        UpdateTimeLabel();
    }

    public void OpenTimePanel()
    {
        mainPanel.SetActive(false);
        timePanel.SetActive(true);
    }

    public void OpenMainPanel()
    {
        mainPanel.SetActive(true);
        timePanel.SetActive(false);
    }

    public void UpdateTimeLabel()
    {
        int seconds = TimePrefs.GetSecondsOrDefault();

        string label;
        if (TimePrefs.IsUnlimited(seconds))
            label = "Time: ∞";
        else
            label = $"Time: {seconds / 60:00}:{seconds % 60:00}";

        if (selectTimeButtonText != null)
            selectTimeButtonText.text = label;
    }
}

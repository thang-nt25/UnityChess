using UnityEngine;

public class MenuTimeSelector : MonoBehaviour
{
    [SerializeField] private MenuPanels panels;

    public void PickUnlimited()
    {
        TimePrefs.SetSeconds(TimePrefs.Unlimited);
        panels.UpdateTimeLabel();
        panels.OpenMainPanel();
    }

    public void Pick5()
    {
        TimePrefs.SetSeconds(300);
        panels.UpdateTimeLabel();
        panels.OpenMainPanel();
    }

    public void Pick10()
    {
        TimePrefs.SetSeconds(600);
        panels.UpdateTimeLabel();
        panels.OpenMainPanel();
    }

    public void Pick20()
    {
        TimePrefs.SetSeconds(1200);
        panels.UpdateTimeLabel();
        panels.OpenMainPanel();
    }

    public void Pick30()
    {
        TimePrefs.SetSeconds(1800);
        panels.UpdateTimeLabel();
        panels.OpenMainPanel();
    }

    public void Back()
    {
        panels.OpenMainPanel();
    }
}

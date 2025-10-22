using UnityEngine;
using UnityEngine.UI;

public class UndoButtonController : MonoBehaviour
{
    private Button _undoButton;

    void Awake()
    {
        _undoButton = GetComponent<Button>();
        if (_undoButton == null)
        {
            Debug.LogError("UndoButtonController requires a Button component on the same GameObject.");
            enabled = false; // Disable script if no Button is found
        }
    }

    void Update()
    {
        if (_undoButton != null && GameManager.Instance != null)
        {
            _undoButton.interactable = GameManager.Instance.CanUndo;
        }
    }
}
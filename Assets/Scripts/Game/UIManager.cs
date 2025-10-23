using System;
using System.Collections.Generic;
using TMPro;
using UnityChess;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviourSingleton<UIManager>
{
    [SerializeField] private GameObject promotionUI = null;

    // --- KẾT QUẢ CHUNG (PvAI hoặc Hoà) ---
    [Header("Game Result Images")]
    [SerializeField] private Image winImage = null;   // You Win (PvAI)
    [SerializeField] private Image loseImage = null;  // You Lose (PvAI)
    [SerializeField] private Image drawImage = null;  // Draw (PvP & PvAI)

    // --- KẾT QUẢ RIÊNG CHO PvP ---
    [SerializeField] private Image whiteWinResultImage = null; // WHITE WIN (PvP)
    [SerializeField] private Image blackWinResultImage = null; // BLACK WIN (PvP)

    [SerializeField] private InputField GameStringInputField = null;
    [SerializeField] private Image whiteTurnIndicator = null;
    [SerializeField] private Image blackTurnIndicator = null;
    [SerializeField] private GameObject moveHistoryContentParent = null;
    [SerializeField] private Scrollbar moveHistoryScrollbar = null;
    [SerializeField] private FullMoveUI moveUIPrefab = null;
    [SerializeField] private Text[] boardInfoTexts = null;
    [SerializeField] private Color backgroundColor = new Color(0.39f, 0.39f, 0.39f);
    [SerializeField] private Color textColor = new Color(1f, 0.71f, 0.18f);
    [SerializeField, Range(-0.25f, 0.25f)] private float buttonColorDarkenAmount = 0f;
    [SerializeField, Range(-0.25f, 0.25f)] private float moveHistoryAlternateColorDarkenAmount = 0f;
    [SerializeField] private Text turnIndicatorText = null;
    [SerializeField] private Text gameStatusText = null;
    [SerializeField] private TMP_Text pauseButtonText = null;

    [Header("AI Difficulty")]
    [SerializeField] private TMP_Text aiDifficultyText = null;

    private bool isPaused = false;
    private Timeline<FullMoveUI> moveUITimeline;
    private Color buttonColor;

    // Bên người chơi cầm (chỉ dùng cho PvAI)
    private Side GetPlayerSide()
    {
        string gameMode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");
        if (gameMode == "PlayerVsAI_White") return Side.Black; // AI trắng => người chơi đen
        return Side.White; // default: PvP hoặc PvAI_Black => người chơi trắng
    }

    private void Start()
    {
        GameManager.NewGameStartedEvent += OnNewGameStarted;
        GameManager.GameEndedEvent += OnGameEnded;
        GameManager.MoveExecutedEvent += OnMoveExecuted;
        GameManager.GameResetToHalfMoveEvent += OnGameResetToHalfMove;

        moveUITimeline = new Timeline<FullMoveUI>();
        foreach (Text t in boardInfoTexts) t.color = textColor;

        buttonColor = new Color(
            backgroundColor.r - buttonColorDarkenAmount,
            backgroundColor.g - buttonColorDarkenAmount,
            backgroundColor.b - buttonColorDarkenAmount
        );

        SetResultImageActive(false, false, false);
        HideSpecificWinImages();

        if (promotionUI != null) promotionUI.SetActive(false);
        else Debug.LogWarning("[UIManager] Promotion UI reference is missing!");
    }

    // Ẩn/hiện bộ YouWin/YouLose/Draw
    private void SetResultImageActive(bool winActive, bool loseActive, bool drawActive)
    {
        if (winImage) winImage.gameObject.SetActive(winActive);
        if (loseImage) loseImage.gameObject.SetActive(loseActive);
        if (drawImage) drawImage.gameObject.SetActive(drawActive);

        if (winActive && winImage) winImage.transform.SetAsLastSibling();
        else if (loseActive && loseImage) loseImage.transform.SetAsLastSibling();
        else if (drawActive && drawImage) drawImage.transform.SetAsLastSibling();
    }

    // Ẩn ảnh WHITE/BLACK WIN (PvP)
    private void HideSpecificWinImages()
    {
        if (whiteWinResultImage) whiteWinResultImage.gameObject.SetActive(false);
        if (blackWinResultImage) blackWinResultImage.gameObject.SetActive(false);
    }

    // Hiện ảnh WHITE/BLACK WIN (PvP)
    private void ShowSpecificWinImage(Side winner)
    {
        HideSpecificWinImages();
        if (winner == Side.White && whiteWinResultImage)
        {
            whiteWinResultImage.gameObject.SetActive(true);
            whiteWinResultImage.transform.SetAsLastSibling();
        }
        else if (winner == Side.Black && blackWinResultImage)
        {
            blackWinResultImage.gameObject.SetActive(true);
            blackWinResultImage.transform.SetAsLastSibling();
        }
    }

    // Hiển thị người thắng theo GameMode
    private void ShowWinner(Side winner)
    {
        string mode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");
        bool isVsAI = mode.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!isVsAI) // PvP
        {
            SetResultImageActive(false, false, false);
            ShowSpecificWinImage(winner);
            if (gameStatusText) gameStatusText.text = $"{winner} wins by checkmate";
            Debug.Log($"[UIManager] PvP winner={winner}, mode='{mode}'");
            return;
        }

        // PvAI
        Side playerSide = GetPlayerSide();
        bool playerWin = (winner == playerSide);
        SetResultImageActive(playerWin, !playerWin, false);
        if (gameStatusText)
            gameStatusText.text = playerWin
                ? $"You Win! ({winner} wins by checkmate)"
                : $"You Lose! ({winner} wins by checkmate)";
        Debug.Log($"[UIManager] PvAI winner={winner}, playerSide={playerSide}, mode='{mode}'");
    }

    private void OnNewGameStarted()
    {
        UpdateGameStringInputField();
        ValidateIndicators();

        Side sideToMove = GameManager.Instance.SideToMove;
        if (turnIndicatorText) turnIndicatorText.text = sideToMove == Side.White ? "White's Turn" : "Black's Turn";

        if (moveHistoryContentParent != null)
        {
            for (int i = 0; i < moveHistoryContentParent.transform.childCount; i++)
                Destroy(moveHistoryContentParent.transform.GetChild(i).gameObject);
        }

        moveUITimeline.Clear();
        SetResultImageActive(false, false, false);
        HideSpecificWinImages();
        if (gameStatusText) gameStatusText.text = "";

        // Hiển thị mức độ AI
        if (aiDifficultyText != null)
        {
            string mode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");
            if (mode.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int difficulty = PlayerPrefs.GetInt("AIDifficulty", 3);
                aiDifficultyText.text = difficulty switch
                {
                    1 => "AI Level: EASY",
                    3 => "AI Level: MEDIUM",
                    5 => "AI Level: HARD",
                    _ => "AI Level: UNKNOWN"
                };
            }
            else aiDifficultyText.text = "Mode: Player vs Player";
        }

        SetBoardInteraction(true);
    }

    private void OnGameEnded()
    {
        if (GameManager.Instance.HalfMoveTimeline == null ||
            !GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove))
        {
            if (gameStatusText) gameStatusText.text = "Game Ended (Unknown Result/Error)";
            SetBoardInteraction(false);
            return;
        }

        if (latestHalfMove.CausedCheckmate)
        {
            Side winner = latestHalfMove.Piece.Owner;
            ShowWinner(winner);
        }
        else if (latestHalfMove.CausedStalemate)
        {
            HideSpecificWinImages();
            SetResultImageActive(false, false, true);
            if (gameStatusText) gameStatusText.text = "Draw (Stalemate)";
        }
        else
        {
            HideSpecificWinImages();
            SetResultImageActive(false, false, true);
            if (gameStatusText) gameStatusText.text = "Draw (Game Rule)";
        }

        SetBoardInteraction(false);
    }

    private void OnMoveExecuted()
    {
        UpdateGameStringInputField();
        Side sideToMove = GameManager.Instance.SideToMove;

        if (whiteTurnIndicator) whiteTurnIndicator.enabled = sideToMove == Side.White;
        if (blackTurnIndicator) blackTurnIndicator.enabled = sideToMove == Side.Black;

        if (turnIndicatorText)
            turnIndicatorText.text = sideToMove == Side.White ? "White's Turn" : "Black's Turn";

        if (GameManager.Instance.HalfMoveTimeline == null ||
            !GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove lastMove))
            return;

        AddMoveToHistory(lastMove, sideToMove.Complement());

        if (gameStatusText)
        {
            if (lastMove.CausedCheckmate) gameStatusText.text = $"{lastMove.Piece.Owner} is checkmated!";
            else if (lastMove.CausedStalemate) gameStatusText.text = "Draw (Stalemate)";
            else if (lastMove.CausedCheck) gameStatusText.text = "Check!";
            else gameStatusText.text = "";
        }
    }

    private void OnGameResetToHalfMove()
    {
        UpdateGameStringInputField();
        if (GameManager.Instance.HalfMoveTimeline == null) return;

        moveUITimeline.HeadIndex = GameManager.Instance.LatestHalfMoveIndex / 2;
        ValidateIndicators();
    }

    public void SetActivePromotionUI(bool value)
    {
        if (promotionUI != null) promotionUI.gameObject.SetActive(value);
    }

    public void OnElectionButton(int choice) => GameManager.Instance.ElectPiece((ElectedPiece)choice);
    public void ResetGameToFirstHalfMove() => GameManager.Instance.ResetGameToHalfMoveIndex(0);
    public void ResetGameToPreviousHalfMove() =>
        GameManager.Instance.ResetGameToHalfMoveIndex(Math.Max(0, GameManager.Instance.LatestHalfMoveIndex - 1));

    public void ResetGameToNextHalfMove()
    {
        int maxIndex = (GameManager.Instance.HalfMoveTimeline?.Count ?? 1) - 1;
        GameManager.Instance.ResetGameToHalfMoveIndex(Math.Min(GameManager.Instance.LatestHalfMoveIndex + 1, maxIndex));
    }

    public void ResetGameToLastHalfMove()
    {
        int maxIndex = (GameManager.Instance.HalfMoveTimeline?.Count ?? 1) - 1;
        GameManager.Instance.ResetGameToHalfMoveIndex(maxIndex);
    }

    public void StartNewGame() => GameManager.Instance.RestartWithCurrentMode();

    public void LoadGame()
    {
        if (GameStringInputField == null)
        {
            Debug.LogError("UIManager Error: GameStringInputField is not assigned in the Inspector. Cannot load game.");
            return;
        }
        GameManager.Instance.LoadGame(GameStringInputField.text);
    }

    private void AddMoveToHistory(HalfMove latestHalfMove, Side latestTurnSide)
    {
        RemoveAlternateHistory();
        if (moveHistoryContentParent == null) return;

        switch (latestTurnSide)
        {
            case Side.Black:
                {
                    if (moveUITimeline.HeadIndex == -1)
                    {
                        FullMoveUI newFullMoveUI = Instantiate(moveUIPrefab, moveHistoryContentParent.transform);
                        moveUITimeline.AddNext(newFullMoveUI);

                        newFullMoveUI.transform.SetSiblingIndex(GameManager.Instance.FullMoveNumber - 1);
                        newFullMoveUI.backgroundImage.color = backgroundColor;
                        newFullMoveUI.whiteMoveButtonImage.color = buttonColor;
                        newFullMoveUI.blackMoveButtonImage.color = buttonColor;

                        if (newFullMoveUI.FullMoveNumber % 2 == 0)
                        {
                            // newFullMoveUI.SetAlternateColor(moveHistoryAlternateColorDarkenAmount);
                        }

                        newFullMoveUI.MoveNumberText.text = $"{newFullMoveUI.FullMoveNumber}.";
                        newFullMoveUI.WhiteMoveButton.enabled = false;
                    }

                    moveUITimeline.TryGetCurrent(out FullMoveUI latestFullMoveUI);
                    latestFullMoveUI.BlackMoveText.text = latestHalfMove.ToAlgebraicNotation();
                    latestFullMoveUI.BlackMoveButton.enabled = true;
                    break;
                }
            case Side.White:
                {
                    FullMoveUI newFullMoveUI = Instantiate(moveUIPrefab, moveHistoryContentParent.transform);
                    newFullMoveUI.transform.SetSiblingIndex(GameManager.Instance.FullMoveNumber - 1);
                    newFullMoveUI.backgroundImage.color = backgroundColor;
                    newFullMoveUI.whiteMoveButtonImage.color = buttonColor;
                    newFullMoveUI.blackMoveButtonImage.color = buttonColor;

                    if (newFullMoveUI.FullMoveNumber % 2 == 0)
                    {
                        // newFullMoveUI.SetAlternateColor(moveHistoryAlternateColorDarkenAmount);
                    }

                    newFullMoveUI.MoveNumberText.text = $"{newFullMoveUI.FullMoveNumber}.";
                    newFullMoveUI.WhiteMoveText.text = latestHalfMove.ToAlgebraicNotation();
                    newFullMoveUI.BlackMoveText.text = "";
                    newFullMoveUI.BlackMoveButton.enabled = false;
                    newFullMoveUI.WhiteMoveButton.enabled = true;

                    moveUITimeline.AddNext(newFullMoveUI);
                    break;
                }
        }

        if (moveHistoryScrollbar != null) moveHistoryScrollbar.value = 0;
    }

    private void RemoveAlternateHistory()
    {
        if (!moveUITimeline.IsUpToDate)
        {
            if (GameManager.Instance.HalfMoveTimeline == null ||
                !GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove lastHalfMove))
            {
                HideSpecificWinImages();
                SetResultImageActive(false, false, false);
            }
            else
            {
                if (lastHalfMove.CausedCheckmate)
                {
                    ShowWinner(lastHalfMove.Piece.Owner);
                }
                else if (lastHalfMove.CausedStalemate)
                {
                    HideSpecificWinImages();
                    SetResultImageActive(false, false, true);
                }
                else
                {
                    HideSpecificWinImages();
                    SetResultImageActive(false, false, false);
                }
            }

            List<FullMoveUI> divergentFullMoveUIs = moveUITimeline.PopFuture();
            foreach (FullMoveUI divergentFullMoveUI in divergentFullMoveUIs)
                Destroy(divergentFullMoveUI.gameObject);
        }
    }

    private void ValidateIndicators()
    {
        Side sideToMove = GameManager.Instance.SideToMove;
        if (whiteTurnIndicator) whiteTurnIndicator.enabled = sideToMove == Side.White;
        if (blackTurnIndicator) blackTurnIndicator.enabled = sideToMove == Side.Black;
    }

    private void UpdateGameStringInputField()
    {
        if (GameStringInputField != null)
            GameStringInputField.text = GameManager.Instance.SerializeGame();
    }

    public void GoToMainMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    public void OnPauseButtonClicked()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;

        if (pauseButtonText) pauseButtonText.text = isPaused ? "Continue" : "Pause";
        if (gameStatusText) gameStatusText.text = isPaused ? "Game Paused" : "";

        if (isPaused)
        {
            SetResultImageActive(false, false, false);
            HideSpecificWinImages();
        }
        else
        {
            if (GameManager.Instance.HalfMoveTimeline != null &&
                GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove))
            {
                if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
                    OnGameEnded();
            }
        }

        SetBoardInteraction(!isPaused);
    }

    public void OnResignButtonClicked()
    {
        // Nếu đã có ảnh kết quả -> bỏ qua
        if ((winImage && winImage.gameObject.activeSelf) ||
            (loseImage && loseImage.gameObject.activeSelf) ||
            (drawImage && drawImage.gameObject.activeSelf) ||
            (whiteWinResultImage && whiteWinResultImage.gameObject.activeSelf) ||
            (blackWinResultImage && blackWinResultImage.gameObject.activeSelf))
        {
            return;
        }

        Side sideToResign = GameManager.Instance.SideToMove;
        Side winner = sideToResign.Complement();

        string mode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");
        bool isVsAI = mode.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!isVsAI) // PvP
        {
            SetResultImageActive(false, false, false);
            ShowSpecificWinImage(winner);
            if (gameStatusText) gameStatusText.text = $"{winner} wins (opponent resigned)";
        }
        else // PvAI
        {
            Side playerSide = GetPlayerSide();
            bool playerWin = (winner == playerSide);
            SetResultImageActive(playerWin, !playerWin, false);
            if (gameStatusText)
                gameStatusText.text = playerWin
                    ? "Opponent Resigned. Game Over (You Win)"
                    : "You Resigned. Game Over (You Lose)";
        }

        Debug.Log($"[UIManager] Resign handled: mode='{mode}', isVsAI={isVsAI}, winner={winner}");

        Time.timeScale = 0f;
        SetBoardInteraction(false);
    }

    public void OnOfferDrawButtonClicked()
    {
        if ((winImage && winImage.gameObject.activeSelf) ||
            (loseImage && loseImage.gameObject.activeSelf) ||
            (drawImage && drawImage.gameObject.activeSelf) ||
            (whiteWinResultImage && whiteWinResultImage.gameObject.activeSelf) ||
            (blackWinResultImage && blackWinResultImage.gameObject.activeSelf))
        {
            return;
        }

        HideSpecificWinImages();
        SetResultImageActive(false, false, true);
        Time.timeScale = 0f;
        if (gameStatusText) gameStatusText.text = "Game Drawn (Agreement)";
        SetBoardInteraction(false);
    }

    private void SetBoardInteraction(bool active)
    {
        if (GameManager.Instance.HalfMoveTimeline != null &&
            GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove) &&
            (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate))
        {
            BoardManager.Instance.SetActiveAllPieces(false);
        }
        else
        {
            BoardManager.Instance.SetActiveAllPieces(active);
        }
    }
}

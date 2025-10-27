using System;
using System.Collections.Generic;
using TMPro;
using UnityChess;
using UnityEngine;
using UnityEngine.UI;
using static GameManager;

public class UIManager : MonoBehaviourSingleton<UIManager>
{
    [SerializeField] private GameObject promotionUI = null;

    [Header("Game Result Screen")]
    [SerializeField] private GameObject resultPanel = null; // Panel chứa UI kết quả
    [SerializeField] private Image winImage = null;          // YOU WIN
    [SerializeField] private Image loseImage = null;         // YOU LOSE
    [SerializeField] private Image drawImage = null;
    // 2 ảnh riêng cho PvP: White/Black thắng
    [SerializeField] private Image whiteWinImage = null;
    [SerializeField] private Image blackWinImage = null;

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

    private Side GetPlayerSide()
    {
        // xác định bên của người chơi khi có AI
        string mode = PlayerPrefs.GetString("GameMode", AIMode.HumanVsHuman.ToString());

        if (!string.IsNullOrEmpty(mode))
        {
            if (mode.Contains(nameof(AIMode.HumanVsAI_White))) return Side.White;
            if (mode.Contains(nameof(AIMode.HumanVsAI_Black))) return Side.Black;
        }

        // PvP hoặc không rõ -> trả về White nhưng KHÔNG dùng để so thắng/thua PvP
        return Side.White;
    }

    private bool IsPvP()
    {
        var raw = PlayerPrefs.GetString("GameMode", AIMode.HumanVsHuman.ToString());
        return System.Enum.TryParse<AIMode>(raw, out var mode) && mode == AIMode.HumanVsHuman;
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

        // Ẩn hết kết quả khi bắt đầu
        SetResultImageActive(false, false, false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (whiteWinImage) whiteWinImage.gameObject.SetActive(false);
        if (blackWinImage) blackWinImage.gameObject.SetActive(false);

        if (promotionUI != null) promotionUI.SetActive(false);
        else Debug.LogWarning("[UIManager] Promotion UI reference is missing!");
    }

    // helper cũ: dùng cho YOU WIN / YOU LOSE / DRAW
    private void SetResultImageActive(bool winActive, bool loseActive, bool drawActive)
    {
        if (winImage) winImage.gameObject.SetActive(winActive);
        if (loseImage) loseImage.gameObject.SetActive(loseActive);
        if (drawImage) drawImage.gameObject.SetActive(drawActive);

        // tắt ảnh PvP khi dùng helper này
        if (whiteWinImage) whiteWinImage.gameObject.SetActive(false);
        if (blackWinImage) blackWinImage.gameObject.SetActive(false);

        if (resultPanel != null)
            resultPanel.SetActive(winActive || loseActive || drawActive);

        if (winActive && winImage) winImage.transform.SetAsLastSibling();
        else if (loseActive && loseImage) loseImage.transform.SetAsLastSibling();
        else if (drawActive && drawImage) drawImage.transform.SetAsLastSibling();
    }

    // hiển thị đúng ảnh theo chế độ
    private void ShowWinner(Side winner)
    {
        // Tắt hết trước
        SetResultImageActive(false, false, false);
        if (whiteWinImage) whiteWinImage.gameObject.SetActive(false);
        if (blackWinImage) blackWinImage.gameObject.SetActive(false);

        // Đọc mode đúng tên enum
        var raw = PlayerPrefs.GetString("GameMode", AIMode.HumanVsHuman.ToString());
        System.Enum.TryParse<AIMode>(raw, out var mode);

        switch (mode)
        {
            case AIMode.HumanVsHuman:
                // PvP: hiển thị White/Black thắng
                if (winner == Side.White) { if (whiteWinImage) whiteWinImage.gameObject.SetActive(true); }
                else { if (blackWinImage) blackWinImage.gameObject.SetActive(true); }
                if (gameStatusText) gameStatusText.text = $"{winner} wins by checkmate";
                break;

            case AIMode.HumanVsAI_Black:
                // nghĩa là: Human vs AI (Black) -> BẠN CẦM TRẮNG
                {
                    bool playerWin = (winner == Side.White);
                    SetResultImageActive(playerWin, !playerWin, false);
                    if (gameStatusText) gameStatusText.text = playerWin
                        ? $"You Win! ({winner} wins by checkmate)"
                        : $"You Lose! ({winner} wins by checkmate)";
                    break;
                }

            case AIMode.HumanVsAI_White:
            default:
                // nghĩa là: Human vs AI (White) -> BẠN CẦM ĐEN
                {
                    bool playerWin = (winner == Side.Black);
                    SetResultImageActive(playerWin, !playerWin, false);
                    if (gameStatusText) gameStatusText.text = playerWin
                        ? $"You Win! ({winner} wins by checkmate)"
                        : $"You Lose! ({winner} wins by checkmate)";
                    break;
                }
        }

        if (resultPanel) resultPanel.SetActive(true);
        Debug.Log($"[GameResult] Winner={winner}, Mode={mode} (Mapping: AI_White=HumanBlack, AI_Black=HumanWhite)");
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
        if (resultPanel != null) resultPanel.SetActive(false);
        if (whiteWinImage) whiteWinImage.gameObject.SetActive(false);
        if (blackWinImage) blackWinImage.gameObject.SetActive(false);

        if (gameStatusText) gameStatusText.text = "";

        if (aiDifficultyText != null)
        {
            string mode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");
            if (mode.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int whiteDifficulty = PlayerPrefs.GetInt("WhiteAIDifficulty", 3);
                int blackDifficulty = PlayerPrefs.GetInt("BlackAIDifficulty", 3);

                string whiteMode = mode.Contains("HumanVsAI_White") || mode.Contains("AIVsAI") ? $"White AI: L{whiteDifficulty}" : "White: Player";
                string blackMode = mode.Contains("HumanVsAI_Black") || mode.Contains("AIVsAI") ? $"Black AI: L{blackDifficulty}" : "Black: Player";

                aiDifficultyText.text = $"{whiteMode} | {blackMode}";
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
            if (resultPanel != null) resultPanel.SetActive(true);
            return;
        }

        if (latestHalfMove.CausedCheckmate)
        {
            Side winner = GameManager.Instance.SideToMove.Complement();
            ShowWinner(winner);
        }
        else if (latestHalfMove.CausedStalemate)
        {
            SetResultImageActive(false, false, true);
            if (gameStatusText) gameStatusText.text = "Draw (Stalemate)";
        }
        else
        {
            SetResultImageActive(false, false, true);
            if (gameStatusText) gameStatusText.text = "Draw (Game Rule)";
        }

        SetBoardInteraction(false);
        if (resultPanel != null) resultPanel.SetActive(true);
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
            if (lastMove.CausedCheckmate) gameStatusText.text = $"{lastMove.Piece.Owner.Complement()} is checkmated! ({lastMove.Piece.Owner} wins)";
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
                        newFullMoveUI.BlackMoveButton.enabled = false;
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
                SetResultImageActive(false, false, false);
            }
            else
            {
                if (lastHalfMove.CausedCheckmate)
                {
                    Side winner = GameManager.Instance.SideToMove.Complement();
                    ShowWinner(winner);
                }
                else if (lastHalfMove.CausedStalemate)
                {
                    SetResultImageActive(false, false, true);
                }
                else
                {
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

    public void CloseResultScreen()
    {
        SetResultImageActive(false, false, false);
        if (whiteWinImage) whiteWinImage.gameObject.SetActive(false);
        if (blackWinImage) blackWinImage.gameObject.SetActive(false);

        if (resultPanel != null) resultPanel.SetActive(false);

        if (gameStatusText)
        {
            if (GameManager.Instance.HalfMoveTimeline != null &&
                GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove))
            {
                if (latestHalfMove.CausedCheck)
                {
                    gameStatusText.text = "Check!";
                }
                else
                {
                    gameStatusText.text = "";
                }
            }
        }

        Time.timeScale = 1f;
    }

    public void OnPauseButtonClicked()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;

        if (pauseButtonText) pauseButtonText.text = isPaused ? "Continue" : "Pause";

        if (isPaused)
        {
            if (gameStatusText) gameStatusText.text = "Game Paused";
            SetResultImageActive(false, false, false);
            if (whiteWinImage) whiteWinImage.gameObject.SetActive(false);
            if (blackWinImage) blackWinImage.gameObject.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
        }
        else
        {
            if (GameManager.Instance.HalfMoveTimeline != null &&
                GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove))
            {
                if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
                    OnGameEnded();
                else if (gameStatusText) gameStatusText.text = latestHalfMove.CausedCheck ? "Check!" : "";
            }
        }

        SetBoardInteraction(!isPaused);
    }

    public void OnResignButtonClicked()
    {
        // chặn khi bất kỳ ảnh kết quả nào đang bật
        if ((winImage && winImage.gameObject.activeSelf) ||
            (loseImage && loseImage.gameObject.activeSelf) ||
            (drawImage && drawImage.gameObject.activeSelf) ||
            (whiteWinImage && whiteWinImage.gameObject.activeSelf) ||
            (blackWinImage && blackWinImage.gameObject.activeSelf))
        {
            return;
        }

        Side resigningSide = GameManager.Instance.SideToMove;
        Side winner = resigningSide.Complement();

        // Hiện kết quả đúng theo chế độ
        ShowWinner(winner);

        // Message phụ tùy theo người chơi
        Side playerSide = GetPlayerSide();
        bool playerWin = (winner == playerSide);

        if (gameStatusText)
            gameStatusText.text = playerWin
                ? "Opponent Resigned. Game Over (You Win)"
                : "You Resigned. Game Over (You Lose)";

        Time.timeScale = 0f;
        SetBoardInteraction(false);
        if (resultPanel != null) resultPanel.SetActive(true);
    }

    public void OnOfferDrawButtonClicked()
    {
        if ((winImage && winImage.gameObject.activeSelf) ||
            (loseImage && loseImage.gameObject.activeSelf) ||
            (drawImage && drawImage.gameObject.activeSelf) ||
            (whiteWinImage && whiteWinImage.gameObject.activeSelf) ||
            (blackWinImage && blackWinImage.gameObject.activeSelf))
        {
            return;
        }

        SetResultImageActive(false, false, true);
        Time.timeScale = 0f;
        if (gameStatusText) gameStatusText.text = "Game Drawn (Agreement)";
        SetBoardInteraction(false);
        if (resultPanel != null) resultPanel.SetActive(true);
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

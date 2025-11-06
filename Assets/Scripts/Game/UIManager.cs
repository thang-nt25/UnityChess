using System;
using System.Collections.Generic;
using TMPro;
using UnityChess;
using UnityEngine;
using UnityEngine.UI;
using static GameManager;

public class UIManager : MonoBehaviourSingleton<UIManager>
{
    [Header("Traversal/Replay")]
    [SerializeField] private GameObject leftBarGameObject = null;

    [SerializeField] private GameObject promotionUI = null;

    [Header("Game Result Screen")]
    [SerializeField] private GameObject resultPanel = null;
    [SerializeField] private Image winImage = null;
    [SerializeField] private Image loseImage = null;
    [SerializeField] private Image drawImage = null;
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

    [Header("Game Control Buttons")]
    [SerializeField] private Button pauseButton = null;
    [SerializeField] private Button resignButton = null;
    [SerializeField] private Button drawButton = null;


    [Header("AI Difficulty")]
    [SerializeField] private TMP_Text aiDifficultyText = null;

    private bool isPaused = false;
    private Timeline<FullMoveUI> moveUITimeline;
    private Color buttonColor;

    private Side GetPlayerSide()
    {
        string mode = PlayerPrefs.GetString("GameMode", AIMode.HumanVsHuman.ToString());

        if (!string.IsNullOrEmpty(mode))
        {
            if (mode.Contains(nameof(AIMode.HumanVsAI_White))) return Side.White;
            if (mode.Contains(nameof(AIMode.HumanVsAI_Black))) return Side.Black;
        }

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

        if (boardInfoTexts != null)
        {
            foreach (Text t in boardInfoTexts)
            {
                if (t != null) t.color = textColor;
            }
        }

        buttonColor = new Color(
            backgroundColor.r - buttonColorDarkenAmount,
            backgroundColor.g - buttonColorDarkenAmount,
            backgroundColor.b - buttonColorDarkenAmount
        );

        SetResultImageActive(false, false, false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (whiteWinImage) whiteWinImage.gameObject.SetActive(false);
        if (blackWinImage) blackWinImage.gameObject.SetActive(false);

        if (promotionUI != null) promotionUI.SetActive(false);
        else Debug.LogWarning("[UIManager] Promotion UI reference is missing!");

        SetTraversalBarVisibility(GameManager.Instance.isReplayMode);
        UpdateControlButtonsVisibility();
    }

    private void SetResultImageActive(bool winActive, bool loseActive, bool drawActive)
    {
        if (winImage) winImage.gameObject.SetActive(winActive);
        if (loseImage) loseImage.gameObject.SetActive(loseActive);
        if (drawImage) drawImage.gameObject.SetActive(drawActive);

        if (whiteWinImage) whiteWinImage.gameObject.SetActive(false);
        if (blackWinImage) blackWinImage.gameObject.SetActive(false);

        if (resultPanel != null)
            resultPanel.SetActive(winActive || loseActive || drawActive);

        if (winActive && winImage) winImage.transform.SetAsLastSibling();
        else if (loseActive && loseImage) loseImage.transform.SetAsLastSibling();
        else if (drawActive && drawImage) drawImage.transform.SetAsLastSibling();
    }

    private void ShowWinner(Side winner)
    {
        SetResultImageActive(false, false, false);
        if (whiteWinImage) whiteWinImage.gameObject.SetActive(false);
        if (blackWinImage) blackWinImage.gameObject.SetActive(false);

        var raw = PlayerPrefs.GetString("GameMode", AIMode.HumanVsHuman.ToString());
        System.Enum.TryParse<AIMode>(raw, out var mode);

        switch (mode)
        {
            case AIMode.HumanVsHuman:
                if (winner == Side.White) { if (whiteWinImage) whiteWinImage.gameObject.SetActive(true); }
                else { if (blackWinImage) blackWinImage.gameObject.SetActive(true); }
                if (gameStatusText) gameStatusText.text = $"{winner} wins by checkmate";
                break;

            case AIMode.HumanVsAI_Black:
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

        UpdateControlButtonsVisibility();

        GameManager.Instance.running = true;
    }


    public void OnGameEnded()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;

        SetResultImageActive(false, false, false);
        if (whiteWinImage) whiteWinImage.gameObject.SetActive(false);
        if (blackWinImage) blackWinImage.gameObject.SetActive(false);

        switch (gm.LastEndReason)
        {
            case GameManager.GameEndReason.Checkmate:
                ShowWinner(gm.LastWinner);
                if (gameStatusText) gameStatusText.text = $"{gm.LastWinner} wins by checkmate";
                break;

            case GameManager.GameEndReason.Stalemate:
                SetResultImageActive(false, false, true);
                if (gameStatusText) gameStatusText.text = "Draw (Stalemate)";
                break;

            case GameManager.GameEndReason.Timeout:
                if (IsPvP())
                {
                    if (gm.LastWinner == Side.White)
                    {
                        if (whiteWinImage) whiteWinImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        if (blackWinImage) blackWinImage.gameObject.SetActive(true);
                    }

                    if (gameStatusText) gameStatusText.text = $"{gm.LastWinner} wins (Timeout)";
                    if (resultPanel) resultPanel.SetActive(true);
                }
                else
                {
                    bool playerWin = (gm.LastWinner == GetPlayerSide());
                    SetResultImageActive(playerWin, !playerWin, false);

                    if (gameStatusText) gameStatusText.text = playerWin
                        ? $"You Win! ({gm.LastWinner} wins by Timeout)"
                        : $"You Lose! ({gm.LastWinner} wins by Timeout)";
                }
                break;

            case GameManager.GameEndReason.Draw:
            case GameManager.GameEndReason.None:
            default:
                if (GameManager.Instance.HalfMoveTimeline != null &&
                    GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove))
                {
                    if (latestHalfMove.CausedStalemate)
                    {
                        SetResultImageActive(false, false, true);
                        if (gameStatusText) gameStatusText.text = "Draw (Stalemate)";
                    }
                    else if (latestHalfMove.CausedCheckmate)
                    {
                        Side winner = GameManager.Instance.SideToMove.Complement();
                        ShowWinner(winner);
                        if (gameStatusText) gameStatusText.text = $"{winner} wins by checkmate";
                    }
                }
                break;
        }

        SetBoardInteraction(false);
        if (resultPanel != null) resultPanel.SetActive(true);
        Time.timeScale = 0f;

        GameManager.Instance.running = false;
    }



    private void OnMoveExecuted()
    {
        UpdateGameStringInputField();
        Side sideToMove = GameManager.Instance.SideToMove;

        if (turnIndicatorText)
            turnIndicatorText.text = sideToMove == Side.White ? "White's Turn" : "Black's Turn";

        if (whiteTurnIndicator) whiteTurnIndicator.enabled = sideToMove == Side.White;
        if (blackTurnIndicator) blackTurnIndicator.enabled = sideToMove == Side.Black;

        if (GameManager.Instance.HalfMoveTimeline == null ||
            !GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove lastMove))
            return;

        if (!GameManager.Instance.isReplayMode)
        {
            AddMoveToHistory(lastMove, sideToMove.Complement());
        }

        if (gameStatusText)
        {
            if (GameManager.Instance.isReplayMode && GameManager.Instance.CurrentReplayIndex < 0)
            {
                gameStatusText.text = "";
                return;
            }

            if (lastMove.CausedCheckmate)
                gameStatusText.text = $"{lastMove.Piece.Owner.Complement()} is checkmated! ({lastMove.Piece.Owner} wins)";
            else if (lastMove.CausedStalemate)
                gameStatusText.text = "Draw (Stalemate)";
            else if (lastMove.CausedCheck)
                gameStatusText.text = "Check!";
            else
                gameStatusText.text = "";
        }
    }


    private void OnGameResetToHalfMove()
    {

        if (GameManager.Instance.isReplayMode)
        {
            UpdateGameStringInputField();
            ValidateIndicators();
            return;
        }

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

    public void ResetGameToFirstHalfMove()
    {
        if (GameManager.Instance.isReplayMode)
        {
            GameManager.Instance.ReplayGoToStart();
        }
        else
        {
            GameManager.Instance.ResetGameToHalfMoveIndex(0);
        }
    }
    public void ResetGameToPreviousHalfMove()
    {
        if (GameManager.Instance.isReplayMode)
        {
            GameManager.Instance.ReplayPreviousMove();
        }
        else
        {
            GameManager.Instance.UndoLastMove();
        }
    }

    public void ResetGameToNextHalfMove()
    {
        if (GameManager.Instance.isReplayMode)
        {
            GameManager.Instance.ReplayNextMove();
        }
        else
        {
            int maxIndex = (GameManager.Instance.HalfMoveTimeline?.Count ?? 1) - 1;
            GameManager.Instance.ResetGameToHalfMoveIndex(Math.Min(GameManager.Instance.LatestHalfMoveIndex + 1, maxIndex));
        }
    }

    public void ResetGameToLastHalfMove()
    {
        if (GameManager.Instance.isReplayMode)
        {
            GameManager.Instance.ReplayGoToEnd();
        }
        else
        {
            int maxIndex = (GameManager.Instance.HalfMoveTimeline?.Count ?? 1) - 1;
            GameManager.Instance.ResetGameToHalfMoveIndex(maxIndex);
        }
    }

    public void StartNewGame()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnClick_NewGame();
        }
    }

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

                    int blackMoveIndex = GameManager.Instance.HalfMoveTimeline.HeadIndex;
                    latestFullMoveUI.BlackMoveButton.onClick.RemoveAllListeners();
                    latestFullMoveUI.BlackMoveButton.onClick.AddListener(() =>
                    {
                        GameManager.Instance.ResetGameToHalfMoveIndex(blackMoveIndex);

                        if (GameManager.Instance.isReplayMode)
                            GameManager.Instance.SetReplayIndex(blackMoveIndex);
                    });

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

                    int whiteMoveIndex = GameManager.Instance.HalfMoveTimeline.HeadIndex;
                    newFullMoveUI.WhiteMoveButton.onClick.RemoveAllListeners();
                    newFullMoveUI.WhiteMoveButton.onClick.AddListener(() =>
                    {
                        GameManager.Instance.ResetGameToHalfMoveIndex(whiteMoveIndex);

                        if (GameManager.Instance.isReplayMode)
                            GameManager.Instance.SetReplayIndex(whiteMoveIndex);
                    });

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
            if (GameManager.Instance.isReplayMode)
            {
                gameStatusText.text = "";
                return;
            }

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

        if (pauseButtonText)
            pauseButtonText.text = isPaused ? "Continue" : "Pause";

        if (isPaused)
        {
            if (gameStatusText)
                gameStatusText.text = "Game Paused";

            SetResultImageActive(false, false, false);
            if (whiteWinImage) whiteWinImage.gameObject.SetActive(false);
            if (blackWinImage) blackWinImage.gameObject.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);

            GameManager.Instance.PauseTimer();
        }
        else
        {
            if (!GameManager.Instance.unlimited && GameManager.Instance.enableTimer)
                GameManager.Instance.ResumeTimer();

            if (GameManager.Instance.HalfMoveTimeline != null &&
                GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove))
            {
                if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
                    OnGameEnded();
                else if (gameStatusText)
                    gameStatusText.text = latestHalfMove.CausedCheck ? "Check!" : "";
            }
        }

        SetBoardInteraction(!isPaused);
    }



    public void OnResignButtonClicked()
    {
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

        GameManager.Instance.LastEndReason = GameManager.GameEndReason.None;
        GameManager.Instance.LastWinner = winner;
        GameManager.Instance.TriggerGameEnded();
        string mode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");
        HistoryManager.SaveGame("Resign", mode, GameManager.Instance.HalfMoveTimeline);


        ShowWinner(winner);

        Side playerSide = GetPlayerSide();
        bool playerWin = (winner == playerSide);

        if (gameStatusText)
            gameStatusText.text = playerWin
                ? "Opponent Resigned. Game Over (You Win)"
                : "You Resigned. Game Over (You Lose)";

        Time.timeScale = 0f;
        GameManager.Instance.running = false;

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

        GameManager.Instance.LastEndReason = GameManager.GameEndReason.Draw;
        GameManager.Instance.LastWinner = Side.None;
        GameManager.Instance.TriggerGameEnded();

        string mode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");
        HistoryManager.SaveGame("Draw", mode, GameManager.Instance.HalfMoveTimeline);

        SetResultImageActive(false, false, true);
        if (gameStatusText) gameStatusText.text = "Game Drawn (Agreement)";

        Time.timeScale = 0f;
        GameManager.Instance.running = false;
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

    public void OnClick_WatchReplayButton()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnClick_WatchReplay();
            UpdateControlButtonsVisibility();
        }
    }

    public void OnClick_ReturnToMenuButton()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnClick_ReturnToMenu();
        }
    }

    public void SetTraversalBarVisibility(bool isVisible)
    {
        if (leftBarGameObject != null)
        {
            leftBarGameObject.SetActive(isVisible);
        }
    }

    private void UpdateControlButtonsVisibility()
    {
        bool isReplay = GameManager.Instance != null && GameManager.Instance.isReplayMode;

        if (pauseButton) pauseButton.gameObject.SetActive(!isReplay);
        if (resignButton) resignButton.gameObject.SetActive(!isReplay);
        if (drawButton) drawButton.gameObject.SetActive(!isReplay);
    }

    public void SetGameStatusText(string text)
    {
        if (gameStatusText != null)
            gameStatusText.text = text;
    }


}
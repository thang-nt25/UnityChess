using System;
using System.Collections.Generic;
using TMPro;
using UnityChess;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviourSingleton<UIManager>
{
    [SerializeField] private GameObject promotionUI = null;

    // --- THÊM CÁC IMAGE KẾT QUẢ ---
    [Header("Game Result Images")]
    [SerializeField] private Image winImage = null; // Hiển thị khi Người chơi thắng
    [SerializeField] private Image loseImage = null; // Hiển thị khi Người chơi thua
    [SerializeField] private Image drawImage = null; // Hiển thị khi Hòa
    // -----------------------------

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
    [SerializeField] private TMP_Text aiDifficultyText = null; // TRƯỜNG MỚI ĐỂ HIỂN THỊ CẤP ĐỘ AI


    private bool isPaused = false;
    private Timeline<FullMoveUI> moveUITimeline;
    private Color buttonColor;

    // HÀM ĐÃ SỬA: XÁC ĐỊNH BÊN MÀ NGƯỜI CHƠI CẦM
    private Side GetPlayerSide()
    {
        string gameMode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");

        if (gameMode == "PlayerVsAI_White")
        {
            // AI cầm Trắng -> Người chơi cầm Đen
            return Side.Black;
        }

        // Mặc định, PlayerVsPlayer, hoặc PlayerVsAI_Black: Người chơi cầm Trắng
        return Side.White;
    }

    private void Start()
    {
        GameManager.NewGameStartedEvent += OnNewGameStarted;
        GameManager.GameEndedEvent += OnGameEnded;
        GameManager.MoveExecutedEvent += OnMoveExecuted;
        GameManager.GameResetToHalfMoveEvent += OnGameResetToHalfMove;

        moveUITimeline = new Timeline<FullMoveUI>();
        foreach (Text boardInfoText in boardInfoTexts)
        {
            boardInfoText.color = textColor;
        }

        buttonColor = new Color(backgroundColor.r - buttonColorDarkenAmount, backgroundColor.g - buttonColorDarkenAmount, backgroundColor.b - buttonColorDarkenAmount);

        // Ẩn tất cả ảnh kết quả khi khởi tạo
        SetResultImageActive(false, false, false);

        if (promotionUI != null)
        {
            promotionUI.SetActive(false);
            Debug.Log("[UIManager] Promotion UI hidden on Start()");
        }
        else
        {
            Debug.LogWarning("[UIManager] Promotion UI reference is missing!");
        }
    }

    private void SetResultImageActive(bool winActive, bool loseActive, bool drawActive)
    {
        // Cập nhật trạng thái hiển thị
        if (winImage != null) winImage.gameObject.SetActive(winActive);
        if (loseImage != null) loseImage.gameObject.SetActive(loseActive);
        if (drawImage != null) drawImage.gameObject.SetActive(drawActive);

        // Đặt hình ảnh kết quả lên trên cùng
        if (winActive && winImage != null)
        {
            winImage.transform.SetAsLastSibling();
        }
        else if (loseActive && loseImage != null)
        {
            loseImage.transform.SetAsLastSibling();
        }
        else if (drawActive && drawImage != null)
        {
            drawImage.transform.SetAsLastSibling();
        }
    }

    private void OnNewGameStarted()
    {
        UpdateGameStringInputField();
        ValidateIndicators();

        Side sideToMove = GameManager.Instance.SideToMove;
        if (turnIndicatorText != null)
        {
            turnIndicatorText.text = sideToMove == Side.White ? "White's Turn" : "Black's Turn";
        }

        if (moveHistoryContentParent != null)
        {
            for (int i = 0; i < moveHistoryContentParent.transform.childCount; i++)
            {
                Destroy(moveHistoryContentParent.transform.GetChild(i).gameObject);
            }
        }

        moveUITimeline.Clear();
        SetResultImageActive(false, false, false);

        if (gameStatusText != null)
        {
            gameStatusText.text = "";
        }

        // ===================================================
        // THÊM LOGIC HIỂN THỊ MỨC ĐỘ KHÓ AI
        // ===================================================
        if (aiDifficultyText != null)
        {
            string mode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");
            string difficultyString = "";

            if (mode.Contains("AI"))
            {
                int difficulty = PlayerPrefs.GetInt("AIDifficulty", 3); // Mặc định là 3 (Medium)

                switch (difficulty)
                {
                    case 1:
                        difficultyString = "AI Level: EASY";
                        break;
                    case 3:
                        difficultyString = "AI Level: MEDIUM";
                        break;
                    case 5:
                        difficultyString = "AI Level: HARD";
                        break;
                    default:
                        difficultyString = "AI Level: UNKNOWN";
                        break;
                }
            }
            else
            {
                difficultyString = "Mode: Player vs Player";
            }

            aiDifficultyText.text = difficultyString;
        }
        // ===================================================

        SetBoardInteraction(true);
    }

    // ... (Giữ nguyên OnGameEnded, OnMoveExecuted, OnGameResetToHalfMove, SetActivePromotionUI, OnElectionButton, 
    // ResetGameToFirstHalfMove, ResetGameToPreviousHalfMove, ResetGameToNextHalfMove, 
    // ResetGameToLastHalfMove, StartNewGame, LoadGame, AddMoveToHistory, RemoveAlternateHistory)

    // (Bỏ qua các hàm không bị thay đổi để rút gọn code trình bày, nhưng bạn nên giữ nguyên chúng)

    private void OnGameEnded()
    {
        if (GameManager.Instance.HalfMoveTimeline == null || !GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove))
        {
            if (gameStatusText != null) gameStatusText.text = "Game Ended (Unknown Result/Error)";
            SetBoardInteraction(false);
            return;
        }

        Side playerSide = GetPlayerSide(); // Lấy bên người chơi

        if (latestHalfMove.CausedCheckmate)
        {
            // Bên vừa đi nước cuối cùng là bên thắng
            Side winner = latestHalfMove.Piece.Owner;

            if (winner == playerSide)
            {
                SetResultImageActive(true, false, false); // NGƯỜI CHƠI THẮNG
                if (gameStatusText != null) gameStatusText.text = $"You Win! ({winner} wins by checkmate)";
            }
            else
            {
                SetResultImageActive(false, true, false); // NGƯỜI CHƠI THUA
                if (gameStatusText != null) gameStatusText.text = $"You Lose! ({winner} wins by checkmate)";
            }
        }
        else if (latestHalfMove.CausedStalemate)
        {
            SetResultImageActive(false, false, true); // HÒA
            if (gameStatusText != null) gameStatusText.text = "Draw (Stalemate)";
        }
        // Giả định các kết thúc khác (ví dụ: Thiếu chất, Lặp 3 lần, 50 Nước) là Hòa theo Luật.
        else
        {
            SetResultImageActive(false, false, true); // HÒA
            if (gameStatusText != null) gameStatusText.text = "Draw (Game Rule)";
        }

        SetBoardInteraction(false);
    }

    private void OnMoveExecuted()
    {
        UpdateGameStringInputField();
        Side sideToMove = GameManager.Instance.SideToMove;

        if (whiteTurnIndicator != null)
        {
            whiteTurnIndicator.enabled = sideToMove == Side.White;
        }
        if (blackTurnIndicator != null)
        {
            blackTurnIndicator.enabled = sideToMove == Side.Black;
        }

        if (turnIndicatorText != null)
        {
            turnIndicatorText.text = sideToMove == Side.White ? "White's Turn" : "Black's Turn";
        }

        // Thêm kiểm tra null
        if (GameManager.Instance.HalfMoveTimeline == null || !GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove lastMove))
        {
            return;
        }

        AddMoveToHistory(lastMove, sideToMove.Complement());

        if (gameStatusText != null)
        {
            if (lastMove.CausedCheckmate)
            {
                gameStatusText.text = $"{lastMove.Piece.Owner} is checkmated!";
            }
            else if (lastMove.CausedStalemate)
            {
                gameStatusText.text = "Draw (Stalemate)";
            }
            else if (lastMove.CausedCheck)
            {
                gameStatusText.text = "Check!";
            }
            else
            {
                gameStatusText.text = "";
            }
        }
    }

    private void OnGameResetToHalfMove()
    {
        UpdateGameStringInputField();
        // Thêm kiểm tra null
        if (GameManager.Instance.HalfMoveTimeline == null) return;

        moveUITimeline.HeadIndex = GameManager.Instance.LatestHalfMoveIndex / 2;
        ValidateIndicators();
    }

    public void SetActivePromotionUI(bool value)
    {
        if (promotionUI != null)
        {
            promotionUI.gameObject.SetActive(value);
        }
    }

    public void OnElectionButton(int choice) => GameManager.Instance.ElectPiece((ElectedPiece)choice);

    public void ResetGameToFirstHalfMove() => GameManager.Instance.ResetGameToHalfMoveIndex(0);

    public void ResetGameToPreviousHalfMove() => GameManager.Instance.ResetGameToHalfMoveIndex(Math.Max(0, GameManager.Instance.LatestHalfMoveIndex - 1));

    public void ResetGameToNextHalfMove()
    {
        // Thêm kiểm tra null
        int maxIndex = (GameManager.Instance.HalfMoveTimeline?.Count ?? 1) - 1;
        GameManager.Instance.ResetGameToHalfMoveIndex(Math.Min(GameManager.Instance.LatestHalfMoveIndex + 1, maxIndex));
    }

    public void ResetGameToLastHalfMove()
    {
        // Thêm kiểm tra null
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
                            // Assume FullMoveUI has this method
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
                        // Assume FullMoveUI has this method
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

        if (moveHistoryScrollbar != null)
        {
            moveHistoryScrollbar.value = 0;
        }
    }

    private void RemoveAlternateHistory()
    {
        if (!moveUITimeline.IsUpToDate)
        {
            if (GameManager.Instance.HalfMoveTimeline == null || !GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove lastHalfMove))
            {
                SetResultImageActive(false, false, false);
            }
            else
            {
                if (lastHalfMove.CausedCheckmate)
                {
                    Side winner = lastHalfMove.Piece.Owner;
                    Side playerSide = GetPlayerSide();

                    if (winner == playerSide)
                    {
                        SetResultImageActive(true, false, false); // Win
                    }
                    else
                    {
                        SetResultImageActive(false, true, false); // Lose
                    }
                }
                else if (lastHalfMove.CausedStalemate)
                {
                    SetResultImageActive(false, false, true); // Draw
                }
                else
                {
                    SetResultImageActive(false, false, false); // Không hiển thị gì khác
                }
            }


            List<FullMoveUI> divergentFullMoveUIs = moveUITimeline.PopFuture();
            foreach (FullMoveUI divergentFullMoveUI in divergentFullMoveUIs)
            {
                Destroy(divergentFullMoveUI.gameObject);
            }
        }
    }

    private void ValidateIndicators()
    {
        Side sideToMove = GameManager.Instance.SideToMove;
        if (whiteTurnIndicator != null)
        {
            whiteTurnIndicator.enabled = sideToMove == Side.White;
        }
        if (blackTurnIndicator != null)
        {
            blackTurnIndicator.enabled = sideToMove == Side.Black;
        }
    }

    private void UpdateGameStringInputField()
    {
        if (GameStringInputField != null)
        {
            GameStringInputField.text = GameManager.Instance.SerializeGame();
        }
    }

    public void GoToMainMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }


    public void OnPauseButtonClicked()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;

        if (pauseButtonText != null)
        {
            pauseButtonText.text = isPaused ? "Continue" : "Pause";
        }

        if (gameStatusText != null)
        {
            gameStatusText.text = isPaused ? "Game Paused" : "";
        }

        // Đảm bảo ẩn ảnh kết quả khi tạm dừng
        if (isPaused)
        {
            SetResultImageActive(false, false, false);
        }
        else
        {
            // Khi tiếp tục, kiểm tra lại trạng thái game đã kết thúc chưa
            if (GameManager.Instance.HalfMoveTimeline != null && GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove))
            {
                if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
                {
                    OnGameEnded();
                }
            }
        }


        SetBoardInteraction(!isPaused);
    }

    public void OnResignButtonClicked()
    {
        // Kiểm tra xem đã có kết quả chưa
        if (winImage != null && winImage.gameObject.activeSelf ||
            loseImage != null && loseImage.gameObject.activeSelf ||
            drawImage != null && drawImage.gameObject.activeSelf)
        {
            return;
        }

        // Bên đang đi (sideToMove) là bên xin thua
        Side sideToResign = GameManager.Instance.SideToMove;
        Side playerSide = GetPlayerSide(); // Lấy bên người chơi

        // Bên thắng là bên còn lại
        Side winner = sideToResign.Complement();

        if (playerSide == sideToResign)
        {
            // NGƯỜI CHƠI TỰ XIN THUA
            SetResultImageActive(false, true, false); // Người chơi THUA
            if (gameStatusText != null) gameStatusText.text = $"You Resigned. Game Over (You Lose)";
        }
        else
        {
            // ĐỐI THỦ XIN THUA (Áp dụng cho PvAI)
            SetResultImageActive(true, false, false); // Người chơi THẮNG (vì đối thủ xin thua)
            if (gameStatusText != null) gameStatusText.text = $"Opponent Resigned. Game Over (You Win)";
        }

        Time.timeScale = 0f;
        SetBoardInteraction(false);
    }


    public void OnOfferDrawButtonClicked()
    {
        // Kiểm tra xem đã có kết quả chưa
        if (winImage != null && winImage.gameObject.activeSelf ||
            loseImage != null && loseImage.gameObject.activeSelf ||
            drawImage != null && drawImage.gameObject.activeSelf)
        {
            return;
        }

        SetResultImageActive(false, false, true); // Hòa: Hiển thị Draw

        Time.timeScale = 0f;

        if (gameStatusText != null)
        {
            gameStatusText.text = "Game Drawn (Agreement)";
        }

        SetBoardInteraction(false);
    }

    private void SetBoardInteraction(bool active)
    {
        if (GameManager.Instance.HalfMoveTimeline != null && GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove)
            && (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate))
        {
            // Không bật lại tương tác nếu game đã kết thúc
            BoardManager.Instance.SetActiveAllPieces(false);
        }
        else
        {
            BoardManager.Instance.SetActiveAllPieces(active);
        }
    }

}
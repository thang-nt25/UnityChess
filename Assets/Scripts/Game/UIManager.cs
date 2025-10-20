using System;
using System.Collections.Generic;
using TMPro;
using UnityChess;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviourSingleton<UIManager>
{
    [SerializeField] private GameObject promotionUI = null;
    // [SerializeField] private Text resultText = null; // Đã XÓA

    // --- THÊM CÁC IMAGE KẾT QUẢ ---
    [Header("Game Result Images")]
    [SerializeField] private Image winImage = null; // Hiển thị khi Trắng thắng
    [SerializeField] private Image loseImage = null; // Hiển thị khi Đen thắng hoặc Trắng thua
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
    [SerializeField] private TMP_Text turnIndicatorText = null;
    [SerializeField] private Text gameStatusText = null;
    [SerializeField] private TMP_Text pauseButtonText = null;


    private bool isPaused = false;
    private Timeline<FullMoveUI> moveUITimeline;
    private Color buttonColor;

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
    }

    private void SetResultImageActive(bool winActive, bool loseActive, bool drawActive)
    {
        // Cập nhật trạng thái hiển thị
        if (winImage != null) winImage.gameObject.SetActive(winActive);
        if (loseImage != null) loseImage.gameObject.SetActive(loseActive);
        if (drawImage != null) drawImage.gameObject.SetActive(drawActive);

        // THÊM: Đặt hình ảnh kết quả lên trên cùng (SetAsLastSibling)
        // để đảm bảo chúng không bị che khuất bởi các UI khác khi chúng được kích hoạt.
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
        // FIX: Thêm kiểm tra null cho turnIndicatorText
        if (turnIndicatorText != null)
        {
            turnIndicatorText.text = sideToMove == Side.White ? "White's Turn" : "Black's Turn";
        }

        // FIX: Thêm kiểm tra null cho moveHistoryContentParent
        if (moveHistoryContentParent != null)
        {
            for (int i = 0; i < moveHistoryContentParent.transform.childCount; i++)
            {
                Destroy(moveHistoryContentParent.transform.GetChild(i).gameObject);
            }
        }

        moveUITimeline.Clear();
        // Đảm bảo ẩn tất cả ảnh kết quả
        SetResultImageActive(false, false, false);

        // FIX: Thêm kiểm tra null cho gameStatusText (cũng cần cho việc khởi tạo game)
        if (gameStatusText != null)
        {
            gameStatusText.text = "";
        }
        SetBoardInteraction(true);
    }

    private void OnGameEnded()
    {
        GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);

        if (latestHalfMove.CausedCheckmate)
        {
            // Kiểm tra xem ai là người thắng (bên vừa đi nước cuối cùng)
            Side winner = latestHalfMove.Piece.Owner;

            if (winner == Side.White)
            {
                SetResultImageActive(true, false, false); // Trắng thắng: Hiển thị Win
            }
            else
            {
                SetResultImageActive(false, true, false); // Đen thắng: Hiển thị Lose
            }
            // Cập nhật trạng thái game cuối cùng (Bên thắng)
            if (gameStatusText != null)
            {
                gameStatusText.text = $"{latestHalfMove.Piece.Owner} wins by checkmate!";
            }
        }
        else if (latestHalfMove.CausedStalemate)
        {
            SetResultImageActive(false, false, true); // Hòa: Hiển thị Draw (Bế tắc)
            if (gameStatusText != null)
            {
                gameStatusText.text = "Draw (Stalemate)";
            }
        }
        // THÊM: Bắt các trường hợp hòa khác theo luật cờ vua (ví dụ: Thiếu chất, Lặp 3 lần, 50 Nước)
        // Giả định bất kỳ kết thúc nào không phải Checkmate hoặc Stalemate là Hòa theo Luật.
        else
        {
            SetResultImageActive(false, false, true); // Hòa: Hiển thị Draw (Luật chung)
            if (gameStatusText != null)
            {
                gameStatusText.text = "Draw (Game Rule)";
            }
        }
    }

    private void OnMoveExecuted()
    {
        UpdateGameStringInputField();
        Side sideToMove = GameManager.Instance.SideToMove;

        // Đã sửa: Thêm kiểm tra null để tránh NullReferenceException
        if (whiteTurnIndicator != null)
        {
            whiteTurnIndicator.enabled = sideToMove == Side.White;
        }
        if (blackTurnIndicator != null)
        {
            blackTurnIndicator.enabled = sideToMove == Side.Black;
        }

        // FIX: Thêm kiểm tra null cho turnIndicatorText
        if (turnIndicatorText != null)
        {
            turnIndicatorText.text = sideToMove == Side.White ? "White's Turn" : "Black's Turn";
        }


        GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove lastMove);
        AddMoveToHistory(lastMove, sideToMove.Complement());
        // Hiển thị trạng thái game
        // FIX: Thêm kiểm tra null cho gameStatusText để tránh NRE
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
        moveUITimeline.HeadIndex = GameManager.Instance.LatestHalfMoveIndex / 2;
        ValidateIndicators();
    }

    public void SetActivePromotionUI(bool value)
    {
        // FIX: Thêm kiểm tra null để tránh UnassignedReferenceException
        if (promotionUI != null)
        {
            promotionUI.gameObject.SetActive(value);
        }
    }

    public void OnElectionButton(int choice) => GameManager.Instance.ElectPiece((ElectedPiece)choice);

    public void ResetGameToFirstHalfMove() => GameManager.Instance.ResetGameToHalfMoveIndex(0);

    public void ResetGameToPreviousHalfMove() => GameManager.Instance.ResetGameToHalfMoveIndex(Math.Max(0, GameManager.Instance.LatestHalfMoveIndex - 1));

    public void ResetGameToNextHalfMove() => GameManager.Instance.ResetGameToHalfMoveIndex(Math.Min(GameManager.Instance.LatestHalfMoveIndex + 1, GameManager.Instance.HalfMoveTimeline.Count - 1));

    public void ResetGameToLastHalfMove() => GameManager.Instance.ResetGameToHalfMoveIndex(GameManager.Instance.HalfMoveTimeline.Count - 1);

    public void StartNewGame() => GameManager.Instance.RestartWithCurrentMode();

    public void LoadGame() => GameManager.Instance.LoadGame(GameStringInputField.text);

    private void AddMoveToHistory(HalfMove latestHalfMove, Side latestTurnSide)
    {
        RemoveAlternateHistory();

        // FIX: Thêm kiểm tra null cho moveHistoryContentParent
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
                            newFullMoveUI.SetAlternateColor(moveHistoryAlternateColorDarkenAmount);
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
                        newFullMoveUI.SetAlternateColor(moveHistoryAlternateColorDarkenAmount);
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

        // FIX: Thêm kiểm tra null cho moveHistoryScrollbar
        if (moveHistoryScrollbar != null)
        {
            moveHistoryScrollbar.value = 0;
        }
    }

    private void RemoveAlternateHistory()
    {
        if (!moveUITimeline.IsUpToDate)
        {
            GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove lastHalfMove);
            // Cập nhật trạng thái hiển thị ảnh kết quả khi quay lại lịch sử
            if (lastHalfMove.CausedCheckmate)
            {
                Side winner = lastHalfMove.Piece.Owner;
                SetResultImageActive(winner == Side.White, winner == Side.Black, false);
            }
            else
            {
                SetResultImageActive(false, false, false);
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
        // Đã sửa: Thêm kiểm tra null để tránh NullReferenceException
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

        // FIX: Thêm kiểm tra null cho pauseButtonText
        if (pauseButtonText != null)
        {
            pauseButtonText.text = isPaused ? "Continue" : "Pause";
        }
        // FIX: Thêm kiểm tra null cho gameStatusText
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
            GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
            if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
            {
                OnGameEnded();
            }
        }


        SetBoardInteraction(!isPaused);
    }

    public void OnResignButtonClicked()
    {
        // Kiểm tra xem đã có kết quả chưa, nếu có thì không làm gì
        if (winImage != null && winImage.gameObject.activeSelf ||
            loseImage != null && loseImage.gameObject.activeSelf ||
            drawImage != null && drawImage.gameObject.activeSelf)
        {
            return;
        }

        Side sideToMove = GameManager.Instance.SideToMove;

        // Bên vừa đi nước cuối cùng (bên thắng) là bên còn lại
        Side winner = sideToMove.Complement();

        if (winner == Side.White)
        {
            SetResultImageActive(true, false, false); // Trắng thắng: Hiển thị Win
        }
        else
        {
            SetResultImageActive(false, true, false); // Đen thắng: Hiển thị Lose
        }

        Time.timeScale = 0f;
        // FIX: Thêm kiểm tra null cho gameStatusText
        if (gameStatusText != null)
        {
            gameStatusText.text = $"{sideToMove} resigned. Game Over.";
        }

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
        // FIX: Thêm kiểm tra null cho gameStatusText
        if (gameStatusText != null)
        {
            gameStatusText.text = "Game Drawn (Agreement)";
        }

        SetBoardInteraction(false);
    }


    private void SetBoardInteraction(bool active)
    {
        BoardManager.Instance.SetActiveAllPieces(active);
    }

}

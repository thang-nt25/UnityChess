using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityChess;
using UnityChess.Engine;
using UnityEngine;
using UnityEngine.SceneManagement; // Đảm bảo bạn có dòng này
using static UnityChess.SquareUtil;
using Debug = UnityEngine.Debug;

public class GameManager : MonoBehaviourSingleton<GameManager>
{
    // --- REPLAY ---
    // Đã XÓA 2 biến normalGameUI và replayGameUI (vì chúng ta không cần nữa)
    // --- END REPLAY ---

    // Đã đổi 'private' thành 'public' để UIManager có thể đọc
    public bool isReplayMode { get; private set; } = false;
    private List<string> replayMoveList; // Danh sách nước đi ("e2e4", "e7e5"...)
    private int currentReplayIndex = -1; // "Con trỏ" xem lại

    public static event Action NewGameStartedEvent;
    public static event Action GameEndedEvent;
    public static event Action GameResetToHalfMoveEvent;
    public static event Action MoveExecutedEvent;
    public enum GameEndReason { None, Checkmate, Stalemate, Timeout, Draw }
    public GameEndReason LastEndReason { get; private set; } = GameEndReason.None;
    public Side LastWinner { get; private set; } = Side.None;

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip sfxMove;
    [SerializeField] private AudioClip sfxCheck;

    [Header("AI Settings")]
    [SerializeField] private int aiThinkTimeMs = 750;
    [Header("Time Control")]
    [SerializeField] private TMPro.TextMeshProUGUI whiteTimeText;
    [SerializeField] private TMPro.TextMeshProUGUI blackTimeText;
    [SerializeField] public bool enableTimer = true;

    private float whiteRemain;
    private float blackRemain;
    private float lastTickRealtime;
    public bool running { get; set; }
    public bool unlimited;

    private int WhiteAIDifficulty = 3;
    private int BlackAIDifficulty = 3;

    private TaskCompletionSource<ElectedPiece> promotionTcs = null;

    public enum AIMode
    {
        HumanVsHuman,
        HumanVsAI_White,
        HumanVsAI_Black,
        AIVsAI
    }

    [SerializeField] private AIMode aiMode = AIMode.HumanVsHuman;

    private bool isWhiteAI;
    private bool isBlackAI;
    private bool lastWhiteAI;
    private bool lastBlackAI;

    public Game Game => game;

    public Board CurrentBoard
    {
        get
        {
            game.BoardTimeline.TryGetCurrent(out Board currentBoard);
            return currentBoard;
        }
    }

    public Side SideToMove
    {
        get
        {
            game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions);
            return currentConditions.SideToMove;
        }
    }

    public bool TryGetLegalMoves(Piece piece, out ICollection<Movement> legalMoves)
    {
        if (game == null)
        {
            legalMoves = null;
            return false;
        }
        return game.TryGetLegalMovesForPiece(piece, out legalMoves);
    }

    public bool TryGetLegalMove(Square start, Square end, out Movement move)
    {
        if (game == null)
        {
            move = null;
            return false;
        }
        return game.TryGetLegalMove(start, end, out move);
    }

    public Side StartingSide => game.ConditionsTimeline[0].SideToMove;
    public Timeline<HalfMove> HalfMoveTimeline => game.HalfMoveTimeline;
    public int LatestHalfMoveIndex => game.HalfMoveTimeline.HeadIndex;
    public int FullMoveNumber => StartingSide switch
    {
        Side.White => LatestHalfMoveIndex / 2 + 1,
        Side.Black => (LatestHalfMoveIndex + 1) / 2 + 1,
        _ => -1
    };
    public bool CanUndo => _halfMoveIndicesForUndo != null && _halfMoveIndicesForUndo.Count > 1;

    public List<(Square, Piece)> CurrentPieces
    {
        get
        {
            currentPiecesBacking.Clear();
            for (int file = 1; file <= 8; file++)
            {
                for (int rank = 1; rank <= 8; rank++)
                {
                    Piece piece = CurrentBoard[file, rank];
                    if (piece != null) currentPiecesBacking.Add((new Square(file, rank), piece));
                }
            }
            return currentPiecesBacking;
        }
    }
    private readonly List<(Square, Piece)> currentPiecesBacking = new List<(Square, Piece)>();

    [SerializeField] private UnityChessDebug unityChessDebug;
    private Game game;
    private Dictionary<GameSerializationType, IGameSerializer> serializersByType;
    private GameSerializationType selectedSerializationType = GameSerializationType.FEN;
    private IUCIEngine uciEngine;
    private Stack<int> _halfMoveIndicesForUndo;


    public void Awake()
    {
        // Logic kiểm tra Replay (từ MainMenu) vẫn giữ nguyên
        if (ReplayManager.movesToReplay != null)
        {
            isReplayMode = true;
            replayMoveList = new List<string>(ReplayManager.movesToReplay);
            ReplayManager.movesToReplay = null;
        }
        else
        {
            // Đây là game bình thường
            isReplayMode = false;

            string desiredMode = PlayerPrefs.GetString("GameMode", AIMode.HumanVsHuman.ToString());
            this.WhiteAIDifficulty = PlayerPrefs.GetInt("WhiteAIDifficulty", 3);
            this.BlackAIDifficulty = PlayerPrefs.GetInt("BlackAIDifficulty", 3);

            string enumModeString = desiredMode.Replace("PlayerVs", "HumanVs");

            if (Enum.TryParse(enumModeString, out AIMode parsedMode))
            {
                this.aiMode = parsedMode;
            }
            else
            {
                this.aiMode = AIMode.HumanVsHuman;
            }

            RestartWithCurrentMode();
        }
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.DeleteKey("GameMode");
        PlayerPrefs.DeleteKey("WhiteAIDifficulty");
        PlayerPrefs.DeleteKey("BlackAIDifficulty");
    }

    private void EnsureAudio()
    {
        if (sfxSource == null)
        {
            sfxSource = gameObject.GetComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }


    private void ApplyAIModeToFlags()
    {
        switch (this.aiMode)
        {
            case AIMode.HumanVsAI_White:
                this.isWhiteAI = true;
                this.isBlackAI = false;
                break;
            case AIMode.HumanVsAI_Black:
                this.isWhiteAI = false;
                this.isBlackAI = true;
                break;
            case AIMode.AIVsAI:
                this.isWhiteAI = true;
                this.isBlackAI = true;
                break;
            case AIMode.HumanVsHuman:
            default:
                this.isWhiteAI = false;
                this.isBlackAI = false;
                break;
        }
    }

    public void Start()
    {
        VisualPiece.VisualPieceMoved += OnPieceMoved;
        EnsureAudio();

        serializersByType = new Dictionary<GameSerializationType, IGameSerializer>
        {
            [GameSerializationType.FEN] = new FENSerializer(),
            [GameSerializationType.PGN] = new PGNSerializer()
        };

        // --- REFACTOR ---
        // Đã SỬA LẠI hàm Start()
        // Xóa các hàm SetupReplayMode() và SetupNormalGameMode()
        // vì chúng ta không còn 2 UI riêng biệt nữa
        if (isReplayMode)
        {
            // Nếu là Replay (từ MainMenu), bắt đầu game rỗng và tắt input
            StartNewGame(false, false);
            Debug.Log("GameManager: Đã vào chế độ REPLAY!");
            if (BoardManager.Instance != null) BoardManager.Instance.SetActiveAllPieces(false);
            currentReplayIndex = -1;
        }
        else
        {
            // Nếu là game bình thường, không cần làm gì thêm
            // vì Awake() đã gọi RestartWithCurrentMode()
            Debug.Log("GameManager: Đã vào chế độ chơi bình thường.");
        }
        // --- END REFACTOR ---

#if DEBUG_VIEW
        unityChessDebug.gameObject.SetActive(true);
        unityChessDebug.enabled = true;
#endif
    }

    private void OnDestroy()
    {
        VisualPiece.VisualPieceMoved -= OnPieceMoved;
        uciEngine?.ShutDown();
    }

    public Side GetHumanSide()
    {
        return aiMode switch
        {
            AIMode.HumanVsAI_White => Side.Black,
            AIMode.HumanVsAI_Black => Side.White,
            AIMode.AIVsAI => Side.White,
            _ => Side.White
        };
    }
    private void InitClock()
    {
        int sec = TimePrefs.GetSecondsOrDefault();
        unlimited = TimePrefs.IsUnlimited(sec);

        if (unlimited)
        {
            whiteRemain = blackRemain = Mathf.Infinity;
            running = false;
            UpdateClockUI();
            return;
        }

        whiteRemain = blackRemain = sec;
        running = true;
        lastTickRealtime = Time.realtimeSinceStartup;
        UpdateClockUI();
    }
    private void UpdateClockUI()
    {
        if (whiteTimeText)
            whiteTimeText.text = FormatTime(whiteRemain);
        if (blackTimeText)
            blackTimeText.text = FormatTime(blackRemain);
    }

    private string FormatTime(float sec)
    {
        if (float.IsInfinity(sec)) return "∞";
        sec = Mathf.Max(0, sec);
        int m = Mathf.FloorToInt(sec / 60f);
        int s = Mathf.FloorToInt(sec % 60f);
        return $"{m:00}:{s:00}";
    }

    private void OnTimeOut(Side side)
    {
        if (isReplayMode) return;

        running = false;
        Debug.Log($"{side} hết giờ!");
        BoardManager.Instance?.SetActiveAllPieces(false);
        var winner = side.Complement();

        string mode = aiMode switch
        {
            AIMode.HumanVsHuman => "Player vs Player",
            AIMode.HumanVsAI_White => "Player vs AI (Black)",
            AIMode.HumanVsAI_Black => "Player vs AI (White)",
            AIMode.AIVsAI => "AI vs AI",
            _ => "Unknown"
        };

        HistoryManager.SaveGame($"{winner} Wins", mode, game.HalfMoveTimeline);

        LastEndReason = GameEndReason.Timeout;
        LastWinner = winner;
        UIManager.Instance?.OnGameEnded();
        GameEndedEvent?.Invoke();
    }


    private void Update()
    {
        if (isReplayMode) return;
        if (!enableTimer || !running || unlimited) return;

        float now = Time.realtimeSinceStartup;
        float delta = now - lastTickRealtime;
        lastTickRealtime = now;

        Side current = SideToMove;
        Side humanSide = GetHumanSide();

        if (humanSide == Side.Black)
        {
            if (current == Side.White)
                blackRemain -= delta;
            else
                whiteRemain -= delta;
        }
        else
        {
            if (current == Side.White)
                whiteRemain -= delta;
            else
                blackRemain -= delta;
        }

        if (whiteRemain <= 0)
            OnTimeOut(Side.White);
        else if (blackRemain <= 0)
            OnTimeOut(Side.Black);

        UpdateClockUI();
    }


    public async void StartNewGame(bool isWhiteAI = false, bool isBlackAI = false)
    {
        lastWhiteAI = isWhiteAI;
        lastBlackAI = isBlackAI;

        Side humanSide = GetHumanSide();

        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.RotateBoardForSide(humanSide);
        }

        game = new Game();
        LastEndReason = GameEndReason.None;
        LastWinner = Side.None;
        InitClock();
        _halfMoveIndicesForUndo = new Stack<int>();
        _halfMoveIndicesForUndo.Push(game.HalfMoveTimeline.HeadIndex);

        promotionTcs = null;
        if (UIManager.Instance != null)
            UIManager.Instance.SetActivePromotionUI(false);

        this.isWhiteAI = isWhiteAI;
        this.isBlackAI = isBlackAI;

        if (isWhiteAI || isBlackAI)
        {
            if (uciEngine == null)
            {
                uciEngine = new StockfishUCIEngine();
                uciEngine.Start();
            }

            await Task.Delay(300);

            if (uciEngine == null)
            {
                Debug.LogError("[GameManager] UCI Engine is null after attempted initialization. Cannot start AI game.");
                return;
            }

            await uciEngine.SetupNewGame(game);
            NewGameStartedEvent?.Invoke();

            if (BoardManager.Instance != null)
            {
                BoardManager.Instance.FixAllPieceRotations();
                if (!isReplayMode)
                    BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
            }

            bool aiTurnNow = (SideToMove == Side.White && isWhiteAI) || (SideToMove == Side.Black && isBlackAI);

            if (aiTurnNow && !isReplayMode)
            {
                try
                {
                    int currentDepth = SideToMove == Side.White ? WhiteAIDifficulty : BlackAIDifficulty;
                    Movement bestMove = await uciEngine.GetBestMove(aiThinkTimeMs, currentDepth);

                    if (bestMove != null)
                    {
                        DoAIMove(bestMove);
                    }
                    else
                    {
                        Debug.LogError("AI Move failed: Engine returned a null move.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"AI Move failed: {ex.Message}");
                }
            }
        }
        else
        {
            NewGameStartedEvent?.Invoke();
            if (BoardManager.Instance != null)
            {
                BoardManager.Instance.FixAllPieceRotations();
                if (!isReplayMode)
                    BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
            }
        }
    }

    public string SerializeGame()
    {
        return serializersByType.TryGetValue(selectedSerializationType, out IGameSerializer serializer)
        ? serializer?.Serialize(game)
        : null;
    }

    public void LoadGame(string serializedGame)
    {
        game = serializersByType[selectedSerializationType].Deserialize(serializedGame);
        NewGameStartedEvent?.Invoke();
        ApplyAIModeToFlags();
        Side humanSide = GetHumanSide();

        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.RotateBoardForSide(humanSide);
            BoardManager.Instance.FixAllPieceRotations();
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
        }

        bool aiTurnNow = (SideToMove == Side.White && isWhiteAI) || (SideToMove == Side.Black && isBlackAI);
        if (aiTurnNow && uciEngine != null)
        {
            Task.Run(async () =>
            {
                try
                {
                    int currentDepth = SideToMove == Side.White ? WhiteAIDifficulty : BlackAIDifficulty;
                    Movement bestMove = await uciEngine.GetBestMove(aiThinkTimeMs, currentDepth);

                    if (bestMove != null)
                    {
                        DoAIMove(bestMove);
                    }
                    else
                    {
                        Debug.LogError("AI Move after load failed: Engine returned a null move.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"AI Move after load failed: {ex.Message}");
                }
            });
        }
    }

    public void ResetGameToHalfMoveIndex(int halfMoveIndex)
    {
        if (game == null) return; // Bảo vệ
        if (!game.ResetGameToHalfMoveIndex(halfMoveIndex)) return;

        if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(false);

        GameResetToHalfMoveEvent?.Invoke();

        Side humanSide = GetHumanSide();
        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.RotateBoardForSide(humanSide);
            BoardManager.Instance.FixAllPieceRotations();

            if (isReplayMode)
            {
                BoardManager.Instance.SetActiveAllPieces(false);
            }
            else
            {
                BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
            }
        }
    }

    private bool TryExecuteMove(Movement move)
    {
        if (game == null) return false; // Bảo vệ
        if (!game.TryExecuteMove(move))
        {
            return false;
        }

        HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);

        if (isReplayMode)
        {
            MoveExecutedEvent?.Invoke();
            return true; // Bỏ qua phần xử lý kết thúc game
        }

        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
        {
            if (BoardManager.Instance != null) BoardManager.Instance.SetActiveAllPieces(false);
            HandleGameEnd(latestHalfMove);
            GameEndedEvent?.Invoke();
        }
        else
        {
            if (BoardManager.Instance != null) BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
        }

        MoveExecutedEvent?.Invoke();
        _halfMoveIndicesForUndo.Push(game.HalfMoveTimeline.HeadIndex);

        return true;
    }

    private void HandleGameEnd(HalfMove latestHalfMove)
    {
        if (isReplayMode) return;

        string gameResultForHistory;
        string mode;
        string reason;
        running = false;

        Side winningSide = Side.None;

        if (latestHalfMove.CausedCheckmate)
        {
            winningSide = SideToMove.Complement();
            gameResultForHistory = $"{winningSide} Wins";
            reason = "Checkmate";
            LastEndReason = GameEndReason.Checkmate;
            LastWinner = winningSide;
        }
        else
        {
            gameResultForHistory = "Draw";
            reason = "Stalemate";
            LastEndReason = GameEndReason.Stalemate;
            LastWinner = Side.None;
        }

        switch (aiMode)
        {
            case AIMode.HumanVsHuman: mode = "Player vs Player"; break;
            case AIMode.HumanVsAI_White: mode = "Player vs AI (Black)"; break;
            case AIMode.HumanVsAI_Black: mode = "Player vs AI (White)"; break;
            case AIMode.AIVsAI: mode = "AI vs AI"; break;
            default: mode = "Unknown"; break;
        }

        HistoryManager.SaveGame(gameResultForHistory, mode, game.HalfMoveTimeline);
        Debug.Log($"Game history saved: {gameResultForHistory}, Mode: {mode}");
        UIManager.Instance?.OnGameEnded();
    }


    private async Task<bool> TryHandleSpecialMoveBehaviourAsync(SpecialMove specialMove)
    {
        switch (specialMove)
        {
            case CastlingMove castlingMove:
                if (BoardManager.Instance != null)
                    BoardManager.Instance.CastleRook(castlingMove.RookSquare, castlingMove.GetRookEndSquare());
                return true;

            case EnPassantMove enPassantMove:
                if (BoardManager.Instance != null)
                    BoardManager.Instance.TryDestroyVisualPiece(enPassantMove.CapturedPawnSquare);
                return true;

            case PromotionMove { PromotionPiece: null } promotionMove:
                if (isReplayMode)
                {
                    promotionMove.SetPromotionPiece(PromotionUtil.GeneratePromotionPiece(ElectedPiece.Queen, SideToMove));

                    if (BoardManager.Instance != null)
                    {
                        BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                        BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                        BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);
                    }
                    return true;
                }
                if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(true);
                if (BoardManager.Instance != null) BoardManager.Instance.SetActiveAllPieces(false);

                promotionTcs = new TaskCompletionSource<ElectedPiece>();
                ElectedPiece choice;
                try { choice = await promotionTcs.Task; }
                catch (TaskCanceledException)
                {
                    if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(false);
                    if (BoardManager.Instance != null) BoardManager.Instance.SetActiveAllPieces(true);
                    promotionTcs = null; return false;
                }

                if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(false);
                if (BoardManager.Instance != null) BoardManager.Instance.SetActiveAllPieces(true);

                promotionMove.SetPromotionPiece(PromotionUtil.GeneratePromotionPiece(choice, SideToMove));

                if (BoardManager.Instance != null)
                {
                    BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                    BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                    BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);
                }
                promotionTcs = null;
                return true;

            case PromotionMove promotionMove:
                if (BoardManager.Instance != null)
                {
                    BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                    BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                    BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);
                }
                return true;

            default:
                return false;
        }
    }

    public void ElectPiece(ElectedPiece choice)
    {
        if (promotionTcs != null && !promotionTcs.Task.IsCompleted)
        {
            promotionTcs.TrySetResult(choice);
        }
    }

    private async void OnPieceMoved(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null)
    {
        Square endSquare = new Square(closestBoardSquareTransform.name);

        // --- REFACTOR ---
        // Thêm kiểm tra 'game' null
        if (game == null || !game.TryGetLegalMove(movedPieceInitialSquare, endSquare, out Movement move))
        // --- END REFACTOR ---
        {
            movedPieceTransform.position = movedPieceTransform.parent.position;
            return;
        }

        if (move is PromotionMove promotionMove)
        {
            promotionMove.SetPromotionPiece(promotionPiece);
        }

        if ((move is not SpecialMove specialMove || await TryHandleSpecialMoveBehaviourAsync(specialMove))
            && TryExecuteMove(move))
        {
            if (!unlimited) lastTickRealtime = Time.realtimeSinceStartup;

            if (move is not SpecialMove && BoardManager.Instance != null)
            {
                BoardManager.Instance.TryDestroyVisualPiece(move.End);
            }

            if (move is PromotionMove && BoardManager.Instance != null)
            {
                movedPieceTransform = BoardManager.Instance.GetPieceGOAtPosition(move.End).transform;
            }

            // --- REFACTOR ---
            // Thêm kiểm tra 'movedPieceTransform' null
            if (movedPieceTransform == null)
            {
                Debug.LogError($"OnPieceMoved: movedPieceTransform became null after promotion/move.");
                return;
            }
            // --- END REFACTOR ---

            Vector3 center = GetSquareWorldCenter(closestBoardSquareTransform);
            float keepWorldY = movedPieceTransform.position.y;
            movedPieceTransform.SetParent(closestBoardSquareTransform, worldPositionStays: true);
            movedPieceTransform.position = new Vector3(center.x, keepWorldY, center.z);
            movedPieceTransform.localRotation = Quaternion.identity;

            bool hasLast = game.HalfMoveTimeline.TryGetCurrent(out HalfMove lastHalfMoveAfterMove);
            bool isCheck = hasLast && lastHalfMoveAfterMove.CausedCheck;

            if (isCheck) PlaySfx(sfxCheck);
            else PlaySfx(sfxMove);
        }

        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.FixAllPieceRotations();
        }

        // --- REFACTOR ---
        // Thêm kiểm tra 'game' null
        bool gameIsOver = game != null && game.HalfMoveTimeline.TryGetCurrent(out HalfMove tailHalfMove)
            && (tailHalfMove.CausedStalemate || tailHalfMove.CausedCheckmate);
        // --- END REFACTOR ---

        if (BoardManager.Instance != null)
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);

        if (!gameIsOver
            && !isReplayMode
            && uciEngine != null
            && ((SideToMove == Side.White && isWhiteAI) || (SideToMove == Side.Black && isBlackAI)))
        {
            int currentDepth = SideToMove == Side.White ? WhiteAIDifficulty : BlackAIDifficulty;
            Movement bestMove = await uciEngine.GetBestMove(aiThinkTimeMs, currentDepth);

            if (bestMove != null)
            {
                DoAIMove(bestMove);
            }
            else
            {
                Debug.LogError("AI Move failed after human move: Engine returned a null move.");
            }
        }
    }


    private void DoAIMove(Movement move)
    {
        if (move == null || BoardManager.Instance == null)
        {
            Debug.LogError("[GameManager] DoAIMove called with null move or BoardManager is null.");
            return;
        }

        GameObject movedPiece = BoardManager.Instance.GetPieceGOAtPosition(move.Start);
        if (movedPiece == null)
        {
            Debug.LogError($"[GameManager] DoAIMove: No VisualPiece found at {move.Start}.");
            return;
        }

        GameObject endSquareGO = BoardManager.Instance.GetSquareGOByPosition(move.End);
        if (endSquareGO == null)
        {
            Debug.LogError($"[GameManager] DoAIMove: No SquareGO found for {move.End}.");
            return;
        }

        OnPieceMoved(
        move.Start,
        movedPiece.transform,
        endSquareGO.transform,
        (move as PromotionMove)?.PromotionPiece
        );
    }

    public bool HasLegalMoves(Piece piece)
    {
        if (game == null) return false; // Bảo vệ
        return game.TryGetLegalMovesForPiece(piece, out _);
    }

    public void RestartWithCurrentMode()
    {
        this.WhiteAIDifficulty = PlayerPrefs.GetInt("WhiteAIDifficulty", 3);
        this.BlackAIDifficulty = PlayerPrefs.GetInt("BlackAIDifficulty", 3);

        string savedModeString = PlayerPrefs.GetString("GameMode", AIMode.HumanVsHuman.ToString());
        string enumModeString = savedModeString.Replace("PlayerVs", "HumanVs");

        if (Enum.TryParse(enumModeString, out AIMode parsedMode))
        {
            this.aiMode = parsedMode;
        }
        else
        {
            this.aiMode = AIMode.HumanVsHuman;
            Debug.LogWarning($"[GameManager] Failed to parse saved game mode: {savedModeString}. Defaulting to HumanVsHuman.");
        }

        ApplyAIModeToFlags();

        StartNewGame(isWhiteAI, isBlackAI);
    }

    public void RestartWithLastMode()
    {
        StartNewGame(lastWhiteAI, lastBlackAI);
    }

    public void UndoLastMove()
    {
        if (isReplayMode) return;

        if (_halfMoveIndicesForUndo == null || _halfMoveIndicesForUndo.Count <= 1)
        {
            Debug.Log("Cannot undo: No moves or only initial state left.");
            return;
        }

        _halfMoveIndicesForUndo.Pop();
        int previousHalfMoveIndex = _halfMoveIndicesForUndo.Peek();

        ResetGameToHalfMoveIndex(previousHalfMoveIndex);
    }

    private static Vector3 GetSquareWorldCenter(Transform square)
    {
        var rend = square.GetComponentInChildren<Renderer>();
        if (rend != null) return rend.bounds.center;

        var col = square.GetComponentInChildren<Collider>();
        if (col != null) return col.bounds.center;

        return square.position;
    }

    public void PauseTimer()
    {
        running = false;
    }

    public void ResumeTimer()
    {
        lastTickRealtime = Time.realtimeSinceStartup;
        running = true;
    }

    // --- REFACTOR ---
    // Đã XÓA các hàm Replay/UI CŨ (SetupReplayMode, SetupNormalGameMode, ExitReplay)
    // Giữ lại các hàm OnClick và Replay MỚI (đã tích hợp)
    // --- END REFACTOR ---

    // Nút "New Game" sẽ gọi hàm này (thông qua UIManager)
    public void OnClick_NewGame()
    {
        Time.timeScale = 1f;

        // --- REFACTOR ---
        // Thêm dòng này để thoát chế độ Replay nếu đang Replay
        isReplayMode = false;
        // --- END REFACTOR ---

        RestartWithLastMode();
    }

    // Gán nút "Watch Replay" (Xem Replay) vào hàm này
    public void OnClick_WatchReplay()
    {
        Debug.Log("GameManager: Bắt đầu xem lại (chế độ tích hợp)...");
        Time.timeScale = 1f;

        // 1. Chuyển đổi Timeline thành List<string>
        List<string> moveNotations = new List<string>();
        for (int i = 0; i <= HalfMoveTimeline.HeadIndex; i++)
        {
            UnityChess.HalfMove halfMove = HalfMoveTimeline[i];
            moveNotations.Add($"{halfMove.Move.Start.ToString()}{halfMove.Move.End.ToString()}");
        }

        // 2. Cài đặt các biến Replay
        this.replayMoveList = moveNotations;
        this.isReplayMode = true;
        this.currentReplayIndex = -1; // Reset con trỏ

        // 3. Bảo UIManager ẩn màn hình kết quả ("BLACK WIN")
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseResultScreen(); // Dùng hàm có sẵn của UIManager
        }

        // 4. Reset bàn cờ về vị trí ban đầu
        ResetGameToHalfMoveIndex(-1);

        // 5. Tắt khả năng người chơi tự click
        if (BoardManager.Instance != null) BoardManager.Instance.SetActiveAllPieces(false);
    }

    // Nút "Return To Menu" sẽ gọi hàm này (thông qua UIManager)
    public void OnClick_ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }


    // --- CÁC HÀM ĐIỀU KHIỂN REPLAY MỚI ---
    // (Đây là các hàm DÙNG CHUNG cho cả Replay (từ MainMenu) và Replay (tức thì))

    // UIManager sẽ gọi hàm này cho nút "Next" (>)
    public void ReplayNextMove()
    {
        if (!isReplayMode || replayMoveList == null || currentReplayIndex >= replayMoveList.Count - 1) return;

        currentReplayIndex++;
        string moveString = replayMoveList[currentReplayIndex];
        Square start = new Square(moveString.Substring(0, 2));
        Square end = new Square(moveString.Substring(2, 2));

        if (game.TryGetLegalMove(start, end, out Movement move))
        {
            DoAIMove(move); // Dùng hàm có sẵn để vẽ quân cờ
        }
        else
        {
            Debug.LogError($"ReplayNextMove: Nước đi không hợp lệ? {moveString}. Thử nước đi tiếp theo.");
            ReplayNextMove(); // Tự động thử nước đi tiếp theo
        }
    }

    // UIManager sẽ gọi hàm này cho nút "Previous" (<)
    public void ReplayPreviousMove()
    {
        if (!isReplayMode || currentReplayIndex < 0) return;

        currentReplayIndex--;
        ResetGameToHalfMoveIndex(currentReplayIndex);
    }

    // UIManager sẽ gọi hàm này cho nút "Tua đến đầu" (|<)
    public void ReplayGoToStart()
    {
        if (!isReplayMode) return;
        currentReplayIndex = -1;
        ResetGameToHalfMoveIndex(currentReplayIndex);
    }

    // UIManager sẽ gọi hàm này cho nút "Tua đến cuối" (>)
    public void ReplayGoToEnd()
    {
        if (!isReplayMode || replayMoveList == null) return;

        // Reset về ban đầu
        ResetGameToHalfMoveIndex(-1);

        // Chạy tất cả các nước đi thật nhanh
        for (int i = 0; i < replayMoveList.Count; i++)
        {
            string moveString = replayMoveList[i];
            Square start = new Square(moveString.Substring(0, 2));
            Square end = new Square(moveString.Substring(2, 2));
            if (game.TryGetLegalMove(start, end, out Movement move))
            {
                // --- REFACTOR ---
                // Chúng ta không thể gọi DoAIMove ở đây vì nó là 'async void'
                // Chúng ta cần một hàm đồng bộ (synchronous) để thực hiện nước đi
                // Tạm thời, chúng ta sẽ gọi TryExecuteMove (dù nó không di chuyển hình ảnh)
                // Hoặc tốt hơn là gọi ResetGameToHalfMoveIndex cho mỗi bước
                // *** Sửa lại logic: ***
                game.TryExecuteMove(move); // Chỉ thực hiện logic
                // --- END REFACTOR ---
            }
        }

        // --- REFACTOR ---
        // Sau khi chạy hết logic, gọi ResetGameToHalfMoveIndex một lần
        // để "vẽ" lại bàn cờ ở trạng thái cuối cùng.
        currentReplayIndex = replayMoveList.Count - 1;
        ResetGameToHalfMoveIndex(currentReplayIndex);
        // --- END REFACTOR ---
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityChess;
using UnityChess.Engine;
using UnityEngine;
using static UnityChess.SquareUtil;

public class GameManager : MonoBehaviourSingleton<GameManager>
{
    public static event Action NewGameStartedEvent;
    public static event Action GameEndedEvent;
    public static event Action GameResetToHalfMoveEvent;
    public static event Action MoveExecutedEvent;

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip sfxMove;
    [SerializeField] private AudioClip sfxCheck;

    [Header("AI Settings")]
    [SerializeField] private int aiThinkTimeMs = 750;

    private int WhiteAIDifficulty = 3;
    private int BlackAIDifficulty = 3;

    private TaskCompletionSource<ElectedPiece> promotionTcs = null;

    public enum AIMode
    {
        HumanVsHuman,
        HumanVsAI_White, // Người chơi cầm Đen (AI cầm Trắng) -> Board xoay 180 độ
        HumanVsAI_Black, // Người chơi cầm Trắng (AI cầm Đen) -> Board xoay 0 độ
        AIVsAI
    }

    [SerializeField] private AIMode aiMode = AIMode.HumanVsHuman;

    private bool isWhiteAI;
    private bool isBlackAI;
    private bool lastWhiteAI;
    private bool lastBlackAI;

    // ... (Các properties như CurrentBoard, SideToMove, TryGetLegalMoves...)
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


    // *** ĐIỀU CHỈNH CHÍNH Ở ĐÂY: GỌI RestartWithCurrentMode() TẠI AWAKE ***
    public void Awake()
    {
        // Đọc chế độ chơi từ PlayerPrefs (được MainMenu lưu lại)
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

        // Khởi động game ngay sau khi đọc chế độ
        RestartWithCurrentMode();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.DeleteKey("GameMode");
        PlayerPrefs.DeleteKey("WhiteAIDifficulty");
        PlayerPrefs.DeleteKey("BlackAIDifficulty");
    }

    // ... (Các hàm EnsureAudio, PlaySfx)
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

    // *** ĐIỀU CHỈNH CHÍNH Ở ĐÂY: XÓA logic khởi tạo game đã chuyển lên Awake() ***
    public void Start()
    {
        VisualPiece.VisualPieceMoved += OnPieceMoved;
        EnsureAudio();

        serializersByType = new Dictionary<GameSerializationType, IGameSerializer>
        {
            [GameSerializationType.FEN] = new FENSerializer(),
            [GameSerializationType.PGN] = new PGNSerializer()
        };

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

    // HÀM CHÍNH: Xác định bên cờ của người chơi dựa trên AIMode (Logic này đã đúng)
    public Side GetHumanSide()
    {
        return aiMode switch
        {
            AIMode.HumanVsAI_White => Side.Black, // Người chơi cầm Đen -> Board xoay 180
            AIMode.HumanVsAI_Black => Side.White, // Người chơi cầm Trắng -> Board xoay 0
            _ => Side.White // Mặc định là Trắng trong HumanVsHuman
        };
    }

    public async void StartNewGame(bool isWhiteAI = false, bool isBlackAI = false)
    {
        lastWhiteAI = isWhiteAI;
        lastBlackAI = isBlackAI;

        Side humanSide = GetHumanSide();

        // KÍCH HOẠT XOAY BÀN CỜ THEO BÊN NGƯỜI CHƠI (Logic này đã đúng)
        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.RotateBoardForSide(humanSide);
        }

        game = new Game();
        _halfMoveIndicesForUndo = new Stack<int>();
        _halfMoveIndicesForUndo.Push(game.HalfMoveTimeline.HeadIndex);

        if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(false);
        promotionTcs = null;

        this.isWhiteAI = isWhiteAI;
        this.isBlackAI = isBlackAI;

        if (isWhiteAI || isBlackAI)
        {
            if (uciEngine == null)
            {
                uciEngine = new MockUCIEngine();
                uciEngine.Start();
            }

            await uciEngine.SetupNewGame(game);
            NewGameStartedEvent?.Invoke();

            if (BoardManager.Instance != null)
            {
                BoardManager.Instance.FixAllPieceRotations();
                BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
            }

            bool aiTurnNow = (SideToMove == Side.White && isWhiteAI) || (SideToMove == Side.Black && isBlackAI);
            if (aiTurnNow)
            {
                try
                {
                    int currentDepth = SideToMove == Side.White ? WhiteAIDifficulty : BlackAIDifficulty;
                    Movement bestMove = await uciEngine.GetBestMove(aiThinkTimeMs, currentDepth);
                    DoAIMove(bestMove);
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
                BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
            }
        }
    }

    // ... (Các hàm SerializeGame, LoadGame, ResetGameToHalfMoveIndex, TryExecuteMove, TryHandleSpecialMoveBehaviourAsync, ElectPiece, OnPieceMoved, DoAIMove, HasLegalMoves, UndoLastMove, GetSquareWorldCenter)

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
        if (aiTurnNow)
        {
            Task.Run(async () =>
            {
                try
                {
                    int currentDepth = SideToMove == Side.White ? WhiteAIDifficulty : BlackAIDifficulty;
                    Movement bestMove = await uciEngine.GetBestMove(aiThinkTimeMs, currentDepth);
                    DoAIMove(bestMove);
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
        if (!game.ResetGameToHalfMoveIndex(halfMoveIndex)) return;

        if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(false);

        GameResetToHalfMoveEvent?.Invoke();

        Side humanSide = GetHumanSide();
        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.RotateBoardForSide(humanSide);
            BoardManager.Instance.FixAllPieceRotations();
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
        }
    }

    private bool TryExecuteMove(Movement move)
    {
        if (!game.TryExecuteMove(move))
        {
            return false;
        }

        HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
        {
            if (BoardManager.Instance != null) BoardManager.Instance.SetActiveAllPieces(false);
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

        if (!game.TryGetLegalMove(movedPieceInitialSquare, endSquare, out Movement move))
        {
            movedPieceTransform.position = movedPieceTransform.parent.position;
            return;
        }

        if (move is PromotionMove promotionMove)
        {
            promotionMove.SetPromotionPiece(promotionPiece);
        }

        if ((move is not SpecialMove specialMove || await TryHandleSpecialMoveBehaviourAsync(specialMove))
          && TryExecuteMove(move)
        )
        {
            if (move is not SpecialMove && BoardManager.Instance != null) { BoardManager.Instance.TryDestroyVisualPiece(move.End); }

            if (move is PromotionMove && BoardManager.Instance != null)
            {
                movedPieceTransform = BoardManager.Instance.GetPieceGOAtPosition(move.End).transform;
            }

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

        bool gameIsOver = game.HalfMoveTimeline.TryGetCurrent(out HalfMove tailHalfMove)
          && (tailHalfMove.CausedStalemate || tailHalfMove.CausedCheckmate);

        if (BoardManager.Instance != null)
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);

        if (!gameIsOver
          && (SideToMove == Side.White && isWhiteAI
            || SideToMove == Side.Black && isBlackAI)
        )
        {
            int currentDepth = SideToMove == Side.White ? WhiteAIDifficulty : BlackAIDifficulty;
            Movement bestMove = await uciEngine.GetBestMove(aiThinkTimeMs, currentDepth);
            DoAIMove(bestMove);
        }
    }

    private void DoAIMove(Movement move)
    {
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

        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.FixAllPieceRotations();
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
        }
    }

    public bool HasLegalMoves(Piece piece)
    {
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
}
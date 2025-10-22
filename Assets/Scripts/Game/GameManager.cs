using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityChess;
using UnityChess.Engine;
using UnityEngine;

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
    [SerializeField] private bool whiteIsAI = false;
    [SerializeField] private bool blackIsAI = false;
    private bool lastWhiteAI;
    private bool lastBlackAI;
    [SerializeField] private int aiThinkTimeMs = 5000;
    // === Promotion flow controller ===
    private TaskCompletionSource<ElectedPiece> promotionTcs = null;

    // --- ĐÃ XÓA BIẾN cameraRigTransform VÀ HEADER CAMERA ORIENTATION ---

    public enum AIMode
    {
        HumanVsHuman,
        HumanVsAI_White, // Người chơi Đen, AI Trắng
        HumanVsAI_Black, // Người chơi Trắng, AI Đen
        AIVsAI
    }

    [SerializeField] private AIMode aiMode = AIMode.HumanVsHuman;

    public void Awake()
    {
        string desiredMode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");

        switch (desiredMode)
        {
            case "PlayerVsAI_White":
                this.aiMode = AIMode.HumanVsAI_White;
                break;
            case "PlayerVsAI_Black":
            case "PlayerVsAI": // Nếu có chế độ PlayerVsAI chung mà không rõ bên
                this.aiMode = AIMode.HumanVsAI_Black;
                break;
            case "PlayerVsPlayer":
            default:
                this.aiMode = AIMode.HumanVsHuman;
                break;
        }
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.DeleteKey("GameMode");
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
        // Phương thức mới được thêm vào để VisualPiece có thể truy cập
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

    private bool isWhiteAI;
    private bool isBlackAI;

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
    private FENSerializer fenSerializer;
    private PGNSerializer pgnSerializer;
    private CancellationTokenSource promotionUITaskCancellationTokenSource;
    private ElectedPiece userPromotionChoice = ElectedPiece.None;
    private Dictionary<GameSerializationType, IGameSerializer> serializersByType;
    private GameSerializationType selectedSerializationType = GameSerializationType.FEN;

    private IUCIEngine uciEngine;
    private Stack<int> _halfMoveIndicesForUndo;

    private void ApplyAIModeToFlags()
    {
        switch (this.aiMode)
        {
            case AIMode.HumanVsAI_White:
                this.isWhiteAI = true;
                this.isBlackAI = false; // Người chơi cầm Đen
                break;
            case AIMode.HumanVsAI_Black:
                this.isWhiteAI = false; // Người chơi cầm Trắng
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

        ApplyAIModeToFlags();
        RestartWithCurrentMode();

        // --- ĐÃ XÓA OrientCameraForPlayerSide() ---

#if DEBUG_VIEW
		unityChessDebug.gameObject.SetActive(true);
		unityChessDebug.enabled = true;
#endif
    }

    // --- ĐÃ XÓA HÀM OrientCameraForPlayerSide() ---

    private void OnDestroy()
    {
        VisualPiece.VisualPieceMoved -= OnPieceMoved;
        uciEngine?.ShutDown();
    }

    // HÀM MỚI: XÁC ĐỊNH BÊN NGƯỜI CHƠI
    public Side GetHumanSide()
    {
        return aiMode switch
        {
            AIMode.HumanVsAI_White => Side.Black, // Người chơi cầm Đen
            AIMode.HumanVsAI_Black => Side.White, // Người chơi cầm Trắng
            AIMode.HumanVsHuman => Side.White,    // Mặc định góc Trắng
            _ => Side.White
        };
    }

#if AI_TEST
	public async void StartNewGame(bool isWhiteAI = true, bool isBlackAI = true) {
#else
    public async void StartNewGame(bool isWhiteAI = false, bool isBlackAI = false)
    {
#endif
        lastWhiteAI = isWhiteAI;
        lastBlackAI = isBlackAI;

        // --- THÊM LOGIC XOAY BÀN CỜ ---
        Side humanSide = GetHumanSide();
        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.RotateBoardForSide(humanSide);
        }
        // -----------------------------

        game = new Game();
        _halfMoveIndicesForUndo = new Stack<int>();
        _halfMoveIndicesForUndo.Push(game.HalfMoveTimeline.HeadIndex);

        if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(false);
        promotionUITaskCancellationTokenSource?.Cancel();
        promotionUITaskCancellationTokenSource = null;

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
            if (BoardManager.Instance != null) BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);

            bool aiTurnNow = (SideToMove == Side.White && isWhiteAI) || (SideToMove == Side.Black && isBlackAI);
            if (aiTurnNow)
            {
                try
                {
                    Movement bestMove = await uciEngine.GetBestMove(aiThinkTimeMs);
                    DoAIMove(bestMove);
                }
                catch (Exception ex)
                {
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
    }

    public void ResetGameToHalfMoveIndex(int halfMoveIndex)
    {
        if (!game.ResetGameToHalfMoveIndex(halfMoveIndex)) return;

        if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(false);
        promotionUITaskCancellationTokenSource?.Cancel();
        GameResetToHalfMoveEvent?.Invoke();
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
                Debug.Log("[GameManager] Showing promotion UI");

                if (UIManager.Instance != null)
                    UIManager.Instance.SetActivePromotionUI(true);

                if (BoardManager.Instance != null)
                    BoardManager.Instance.SetActiveAllPieces(false);

                // Dọn TCS cũ nếu còn
                if (promotionTcs != null && !promotionTcs.Task.IsCompleted)
                    promotionTcs.TrySetCanceled();

                promotionTcs = new TaskCompletionSource<ElectedPiece>();

                ElectedPiece choice;
                try
                {
                    choice = await promotionTcs.Task; // chờ người chơi chọn
                }
                catch (TaskCanceledException)
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.SetActivePromotionUI(false);
                    if (BoardManager.Instance != null)
                        BoardManager.Instance.SetActiveAllPieces(true);

                    promotionTcs = null;
                    return false;
                }

                Debug.Log($"[GameManager] Player selected promotion: {choice}");

                if (UIManager.Instance != null)
                    UIManager.Instance.SetActivePromotionUI(false);
                if (BoardManager.Instance != null)
                    BoardManager.Instance.SetActiveAllPieces(true);

                promotionMove.SetPromotionPiece(
                    PromotionUtil.GeneratePromotionPiece(choice, SideToMove)
                );

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


    private ElectedPiece GetUserPromotionPieceChoice()
    {
        while (userPromotionChoice == ElectedPiece.None) { }

        ElectedPiece result = userPromotionChoice;
        userPromotionChoice = ElectedPiece.None;
        return result;
    }

    public void ElectPiece(ElectedPiece choice)
    {
        Debug.Log($"[GameManager] ElectPiece called: {choice}");

        if (promotionTcs != null && !promotionTcs.Task.IsCompleted)
        {
            promotionTcs.TrySetResult(choice);
            return;
        }

        // Nếu chưa tới bước promotion
        userPromotionChoice = choice;
    }

    private async void OnPieceMoved(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null)
    {
        Square endSquare = new Square(closestBoardSquareTransform.name);

        if (!game.TryGetLegalMove(movedPieceInitialSquare, endSquare, out Movement move))
        {
            movedPieceTransform.position = movedPieceTransform.parent.position;
#if DEBUG_VIEW
			Piece movedPiece = CurrentBoard[movedPieceInitialSquare];
			game.TryGetLegalMovesForPiece(movedPiece, out ICollection<Movement> legalMoves);
			UnityChessDebug.ShowLegalMovesInLog(legalMoves);
#endif
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

            if (isCheck)
                PlaySfx(sfxCheck);
            else
                PlaySfx(sfxMove);

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
            Movement bestMove = await uciEngine.GetBestMove(aiThinkTimeMs);
            DoAIMove(bestMove);
        }
    }

    private void DoAIMove(Movement move)
    {
        GameObject movedPiece = BoardManager.Instance.GetPieceGOAtPosition(move.Start);
        GameObject endSquareGO = BoardManager.Instance.GetSquareGOByPosition(move.End);
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

    public void SetAIMode(int modeIndex)
    {
        aiMode = (AIMode)modeIndex;
        ApplyAIModeToFlags();
    }

    public void SetAIThinkTimeMs(int ms)
    {
        aiThinkTimeMs = Mathf.Max(500, ms);
    }

    public void RestartWithCurrentMode()
    {
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

        _halfMoveIndicesForUndo.Pop(); // Remove the current state's index
        int previousHalfMoveIndex = _halfMoveIndicesForUndo.Peek(); // Get the index of the state before the last move

        ResetGameToHalfMoveIndex(previousHalfMoveIndex);

        // Ensure visual board is updated and pieces are enabled for the correct side
        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.FixAllPieceRotations();
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
        }

        Debug.Log($"Undo successful. Reset to half-move index: {previousHalfMoveIndex}");
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
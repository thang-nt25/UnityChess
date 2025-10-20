
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityChess;
using UnityChess.Engine;
using UnityEngine;

public class GameManager : MonoBehaviourSingleton<GameManager> {
	public static event Action NewGameStartedEvent;
	public static event Action GameEndedEvent;
	public static event Action GameResetToHalfMoveEvent;
	public static event Action MoveExecutedEvent;

    // --- ÂM THANH CHO DI CHUYỂN & CHIẾU ---
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip sfxMove;   // âm khi di chuyển
    [SerializeField] private AudioClip sfxCheck;  // âm khi chiếu
    [Header("AI Settings")]
    [SerializeField] private bool whiteIsAI = false;
    [SerializeField] private bool blackIsAI = false;
    private bool lastWhiteAI;
    private bool lastBlackAI;


    // Milliseconds cho mỗi nước đi của AI (tương đương mức độ khó)
    [SerializeField] private int aiThinkTimeMs = 5000;

    public enum AIMode
    {
        HumanVsHuman,
        HumanVsAI_White, // AI cầm Trắng
        HumanVsAI_Black, // AI cầm Đen
        AIVsAI
    }

    [SerializeField] private AIMode aiMode = AIMode.HumanVsHuman;

    public void Awake() {
        // Read the game mode from PlayerPrefs, which was set in the MainMenu scene.
        string desiredMode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");
        if (desiredMode == "PlayerVsAI")
        {
            this.aiMode = AIMode.HumanVsAI_Black;
        }
        else
        {
            this.aiMode = AIMode.HumanVsHuman;
        }
        
        Debug.Log($"[GameManager] Awake: Game mode set to {this.aiMode} from PlayerPrefs.");

        //// Clean up the PlayerPrefs key so it doesn't affect the next game launch.
        //PlayerPrefs.DeleteKey("GameMode");
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
            sfxSource.spatialBlend = 0f; // 2D
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    public Board CurrentBoard {
		get {
			game.BoardTimeline.TryGetCurrent(out Board currentBoard);
			return currentBoard;
		}
	}

	public Side SideToMove {
		get {
			game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions);
			return currentConditions.SideToMove;
		}
	}

	public Side StartingSide => game.ConditionsTimeline[0].SideToMove;
	public Timeline<HalfMove> HalfMoveTimeline => game.HalfMoveTimeline;
	public int LatestHalfMoveIndex => game.HalfMoveTimeline.HeadIndex;
	public int FullMoveNumber => StartingSide switch {
		Side.White => LatestHalfMoveIndex / 2 + 1,
		Side.Black => (LatestHalfMoveIndex + 1) / 2 + 1,
		_ => -1
	};

	private bool isWhiteAI;
	private bool isBlackAI;

	public List<(Square, Piece)> CurrentPieces {
		get {
			currentPiecesBacking.Clear();
			for (int file = 1; file <= 8; file++) {
				for (int rank = 1; rank <= 8; rank++) {
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
	
	public void Start() {
		VisualPiece.VisualPieceMoved += OnPieceMoved;
        EnsureAudio();

        serializersByType = new Dictionary<GameSerializationType, IGameSerializer> {
			[GameSerializationType.FEN] = new FENSerializer(),
			[GameSerializationType.PGN] = new PGNSerializer()
		};

        ApplyAIModeToFlags();
        RestartWithCurrentMode();

#if DEBUG_VIEW
		unityChessDebug.gameObject.SetActive(true);
		unityChessDebug.enabled = true;
#endif
    }

	private void OnDestroy() {
        VisualPiece.VisualPieceMoved -= OnPieceMoved;
        uciEngine?.ShutDown();
	}
	
#if AI_TEST
	public async void StartNewGame(bool isWhiteAI = true, bool isBlackAI = true) {
#else
	public async void StartNewGame(bool isWhiteAI = false, bool isBlackAI = false) {
#endif
        lastWhiteAI = isWhiteAI;
        lastBlackAI = isBlackAI;

        game = new Game();
        // HỦY UI/CTS còn treo từ ván trước
        if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(false);
        promotionUITaskCancellationTokenSource?.Cancel();
        promotionUITaskCancellationTokenSource = null;

        this.isWhiteAI = isWhiteAI;
		this.isBlackAI = isBlackAI;

		if (isWhiteAI || isBlackAI) {
			if (uciEngine == null) {
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
            // Bật đúng bên đang tới lượt
            if (BoardManager.Instance != null) BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);

            // Nếu bên tới lượt là AI, cho AI đi ngay
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
                    Debug.LogError($"AI move failed at start: {ex}");
                }
            }

        }
        else {
			NewGameStartedEvent?.Invoke();
            if (BoardManager.Instance != null)
            {
                BoardManager.Instance.FixAllPieceRotations();
                BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
            }
        }
	}

	public string SerializeGame() {
		return serializersByType.TryGetValue(selectedSerializationType, out IGameSerializer serializer)
			? serializer?.Serialize(game)
			: null;
	}
	
	public void LoadGame(string serializedGame) {
		game = serializersByType[selectedSerializationType].Deserialize(serializedGame);
		NewGameStartedEvent?.Invoke();
	}

	public void ResetGameToHalfMoveIndex(int halfMoveIndex) {
		if (!game.ResetGameToHalfMoveIndex(halfMoveIndex)) return;
		
		if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(false);
		promotionUITaskCancellationTokenSource?.Cancel();
		GameResetToHalfMoveEvent?.Invoke();
	}

	private bool TryExecuteMove(Movement move) {
		if (!game.TryExecuteMove(move)) {
			return false;
		}

		HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
		if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate) {
			if (BoardManager.Instance != null) BoardManager.Instance.SetActiveAllPieces(false);
			GameEndedEvent?.Invoke();
		} else {
			if (BoardManager.Instance != null) BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
		}

		MoveExecutedEvent?.Invoke();

		return true;
	}
	
	private async Task<bool> TryHandleSpecialMoveBehaviourAsync(SpecialMove specialMove) {
		switch (specialMove) {
			case CastlingMove castlingMove:
				if (BoardManager.Instance != null) BoardManager.Instance.CastleRook(castlingMove.RookSquare, castlingMove.GetRookEndSquare());
				return true;
			case EnPassantMove enPassantMove:
				if (BoardManager.Instance != null) BoardManager.Instance.TryDestroyVisualPiece(enPassantMove.CapturedPawnSquare);
				return true;
			case PromotionMove { PromotionPiece: null } promotionMove:
				if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(true);
				if (BoardManager.Instance != null) BoardManager.Instance.SetActiveAllPieces(false);

				promotionUITaskCancellationTokenSource?.Cancel();
				promotionUITaskCancellationTokenSource = new CancellationTokenSource();
				
				ElectedPiece choice = await Task.Run(GetUserPromotionPieceChoice, promotionUITaskCancellationTokenSource.Token);
				
				if (UIManager.Instance != null) UIManager.Instance.SetActivePromotionUI(false);
				if (BoardManager.Instance != null) BoardManager.Instance.SetActiveAllPieces(true);

				if (promotionUITaskCancellationTokenSource == null
				    || promotionUITaskCancellationTokenSource.Token.IsCancellationRequested
				) { return false; }

				promotionMove.SetPromotionPiece(
					PromotionUtil.GeneratePromotionPiece(choice, SideToMove)
				);
				if (BoardManager.Instance != null) {
                    BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
				    BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
				    BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);
                }

				promotionUITaskCancellationTokenSource = null;
				return true;
			case PromotionMove promotionMove:
				if (BoardManager.Instance != null) {
    				BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
	    			BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
		    		BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);
                }
				return true;
			default:
				return false;
		}
	}
	
	private ElectedPiece GetUserPromotionPieceChoice() {
		while (userPromotionChoice == ElectedPiece.None) { }

		ElectedPiece result = userPromotionChoice;
		userPromotionChoice = ElectedPiece.None;
		return result;
	}
	
	public void ElectPiece(ElectedPiece choice) {
		userPromotionChoice = choice;
	}

	private async void OnPieceMoved(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null) {
		Square endSquare = new Square(closestBoardSquareTransform.name);

		if (!game.TryGetLegalMove(movedPieceInitialSquare, endSquare, out Movement move)) {
			movedPieceTransform.position = movedPieceTransform.parent.position;
#if DEBUG_VIEW
			Piece movedPiece = CurrentBoard[movedPieceInitialSquare];
			game.TryGetLegalMovesForPiece(movedPiece, out ICollection<Movement> legalMoves);
			UnityChessDebug.ShowLegalMovesInLog(legalMoves);
#endif
			return;
		}

		if (move is PromotionMove promotionMove) {
			promotionMove.SetPromotionPiece(promotionPiece);
		}

		if ((move is not SpecialMove specialMove || await TryHandleSpecialMoveBehaviourAsync(specialMove))
		    && TryExecuteMove(move)
		) {
			if (move is not SpecialMove && BoardManager.Instance != null) { BoardManager.Instance.TryDestroyVisualPiece(move.End); }

			if (move is PromotionMove && BoardManager.Instance != null) {
				movedPieceTransform = BoardManager.Instance.GetPieceGOAtPosition(move.End).transform;
			}

            // --- Đặt quân đúng TÂM ô (3D) ---
            Vector3 center = GetSquareWorldCenter(closestBoardSquareTransform);

            // Giữ lại chiều cao hiện tại của quân nếu bạn dùng Y là trục cao
            float keepWorldY = movedPieceTransform.position.y;

            // Giữ world pos khi set parent (để không nhảy theo offset của parent)
            movedPieceTransform.SetParent(closestBoardSquareTransform, worldPositionStays: true);

            // Đặt đúng tâm ô (X,Z theo tâm; Y giữ nguyên)
            movedPieceTransform.position = new Vector3(center.x, keepWorldY, center.z);

            // Không xoay theo parent (tránh lệch do rotation)
            movedPieceTransform.localRotation = Quaternion.identity;

            // (tuỳ) nếu parent có scale lạ, chuẩn hoá lại
            // movedPieceTransform.localScale = Vector3.one;

            bool hasLast = game.HalfMoveTimeline.TryGetCurrent(out HalfMove lastHalfMoveAfterMove);
            bool isCheck = hasLast && lastHalfMoveAfterMove.CausedCheck; // nếu có cờ CausedCheck

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
		) {
			Movement bestMove = await uciEngine.GetBestMove(aiThinkTimeMs);
			DoAIMove(bestMove);
		}
	}

	private void DoAIMove(Movement move) {
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

	public bool HasLegalMoves(Piece piece) {
		return game.TryGetLegalMovesForPiece(piece, out _);
	}
    private void ApplyAIModeToFlags()
    {
        switch (aiMode)
        {
            case AIMode.HumanVsHuman:
                whiteIsAI = false; blackIsAI = false; break;
            case AIMode.HumanVsAI_White:
                whiteIsAI = true; blackIsAI = false; break;
            case AIMode.HumanVsAI_Black:
                whiteIsAI = false; blackIsAI = true; break;
            case AIMode.AIVsAI:
                whiteIsAI = true; blackIsAI = true; break;
        }

        lastWhiteAI = whiteIsAI;
        lastBlackAI = blackIsAI;
    }

    public void SetAIMode(int modeIndex)
    {
        aiMode = (AIMode)modeIndex;
        ApplyAIModeToFlags();
    }

    public void SetAIThinkTimeMs(int ms)
    {
        aiThinkTimeMs = Mathf.Max(500, ms); // tránh quá thấp
    }

    public void RestartWithCurrentMode()
    {
        // ✅ In ra log để xác minh giá trị thật trong PlayerPrefs
        string desiredMode = PlayerPrefs.GetString("GameMode", "PlayerVsPlayer");
        Debug.Log($"[GameManager] RestartWithCurrentMode() reading GameMode = {desiredMode}");

        // ✅ Áp dụng đúng mode dựa trên PlayerPrefs
        if (desiredMode == "PlayerVsAI")
        {
            aiMode = AIMode.HumanVsAI_Black;
        }
        else
        {
            aiMode = AIMode.HumanVsHuman;
        }

        ApplyAIModeToFlags();

        Debug.Log($"[GameManager] Restarting New Game as {aiMode} (whiteAI={whiteIsAI}, blackAI={blackIsAI})");

        // ✅ Bắt đầu lại ván đúng chế độ
        StartNewGame(whiteIsAI, blackIsAI);
    }



    // Lấy tâm world của một ô, KHÔNG phụ thuộc pivot
    private static Vector3 GetSquareWorldCenter(Transform square)
    {
        // ưu tiên Mesh/SkinnedMesh
        var rend = square.GetComponentInChildren<Renderer>();
        if (rend != null) return rend.bounds.center;

        // fallback: Collider
        var col = square.GetComponentInChildren<Collider>();
        if (col != null) return col.bounds.center;

        // cuối cùng: vị trí transform (nếu ô chỉ là empty)
        return square.position;
    }


}

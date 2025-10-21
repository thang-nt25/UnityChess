using System;
using System.Collections.Generic;
using UnityChess;
using UnityEngine;
using static UnityChess.SquareUtil;

public class BoardManager : MonoBehaviourSingleton<BoardManager> {
	private readonly GameObject[] allSquaresGO = new GameObject[64];
	private Dictionary<Square, GameObject> positionMap;
	private const float BoardPlaneSideLength = 14f; // measured from corner square center to corner square center, on same side.
	private const float BoardPlaneSideHalfLength = BoardPlaneSideLength * 0.5f;
	private const float BoardHeight = 1.6f;
	private readonly System.Random rng = new System.Random();
    [Header("Captured Pieces UI")]
    public CapturedPiecesUI capturedPiecesUI; // Kéo UIManager vào đây trong Inspector


    private void Awake() {
		GameManager.NewGameStartedEvent += OnNewGameStarted;
		GameManager.GameResetToHalfMoveEvent += OnGameResetToHalfMove;

        if (capturedPiecesUI != null)
        {
            foreach (Transform child in capturedPiecesUI.whiteArea)
                Destroy(child.gameObject);
            foreach (Transform child in capturedPiecesUI.blackArea)
                Destroy(child.gameObject);
        }

        positionMap = new Dictionary<Square, GameObject>(64);
		Transform boardTransform = transform;
		Vector3 boardPosition = boardTransform.position;
		
		for (int file = 1; file <= 8; file++) {
			for (int rank = 1; rank <= 8; rank++) {
				GameObject squareGO = new GameObject(SquareToString(file, rank)) {
					transform = {
						position = new Vector3(boardPosition.x + FileOrRankToSidePosition(file), boardPosition.y + BoardHeight, boardPosition.z + FileOrRankToSidePosition(rank)),
						parent = boardTransform
					},
					tag = "Square"
				};

				positionMap.Add(new Square(file, rank), squareGO);
				allSquaresGO[(file - 1) * 8 + (rank - 1)] = squareGO;
			}
		}
	}

    private void OnNewGameStarted()
    {
        if (this == null) return;
        // 1. Clear all VisualPieces
        ClearBoard();

        // 2. Clear captured pieces UI
        if (capturedPiecesUI != null)
        {
            foreach (Transform child in capturedPiecesUI.whiteArea)
                Destroy(child.gameObject);
            foreach (Transform child in capturedPiecesUI.blackArea)
                Destroy(child.gameObject);
        }

        foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
        {
            CreateAndPlacePieceGO(piece, square);
        }

        EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    }


    private void OnGameResetToHalfMove()
    {
        if (this == null) return;
        ClearBoard();

        if (capturedPiecesUI != null)
        {
            foreach (Transform child in capturedPiecesUI.whiteArea)
                Destroy(child.gameObject);
            foreach (Transform child in capturedPiecesUI.blackArea)
                Destroy(child.gameObject);
        }

        foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
        {
            CreateAndPlacePieceGO(piece, square);
        }

        GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate) SetActiveAllPieces(false);
        else EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    }


    public void CastleRook(Square rookPosition, Square endSquare) {
		GameObject rookGO = GetPieceGOAtPosition(rookPosition);
		rookGO.transform.parent = GetSquareGOByPosition(endSquare).transform;
		rookGO.transform.localPosition = Vector3.zero;
	}

    public void CreateAndPlacePieceGO(Piece piece, Square position)
    {
        string modelName = $"{piece.Owner} {piece.GetType().Name}";
        GameObject pieceGO = Instantiate(
            Resources.Load("PieceSets/Marble/" + modelName) as GameObject,
            positionMap[position].transform
        );

        // Tuỳ chọn: xoay ngẫu nhiên nếu muốn cho tự nhiên
        /*
        if (!(piece is Knight) && !(piece is King))
        {
            pieceGO.transform.Rotate(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        }
        */
        pieceGO.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        VisualPiece visualPiece = pieceGO.GetComponent<VisualPiece>();
        if (visualPiece != null)
        {
            visualPiece.piece = piece;
            visualPiece.PieceColor = piece.Owner;
            visualPiece.PieceTypeManual = piece.GetType().Name; // Rook, Knight, Bishop, Queen, Pawn
        }
    }


    public void GetSquareGOsWithinRadius(List<GameObject> squareGOs, Vector3 positionWS, float radius) {
		float radiusSqr = radius * radius;
		foreach (GameObject squareGO in allSquaresGO) {
			if ((squareGO.transform.position - positionWS).sqrMagnitude < radiusSqr)
				squareGOs.Add(squareGO);
		}
	}

	public void SetActiveAllPieces(bool active) {
		VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);
		foreach (VisualPiece pieceBehaviour in visualPiece) pieceBehaviour.enabled = active;
	}

	public void EnsureOnlyPiecesOfSideAreEnabled(Side side) {
		VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);
		foreach (VisualPiece pieceBehaviour in visualPiece) {
			// Temporarily enable all pieces of the current side to move, regardless of legal moves
			pieceBehaviour.enabled = pieceBehaviour.PieceColor == side; 
		}
	}

    public void TryDestroyVisualPiece(Square position)
    {
        if (this == null) return;
        if (!positionMap.TryGetValue(position, out GameObject squareGO) || squareGO == null) return;
        VisualPiece visualPiece = squareGO.GetComponentInChildren<VisualPiece>();
        if (visualPiece != null)
        {
            VisualPiece visualPiece2 = positionMap[position].GetComponentInChildren<VisualPiece>();
            if (visualPiece2 != null)
            {
                if (capturedPiecesUI != null)
                {
                    // Sử dụng PieceTypeManual để xác định sprite
                    capturedPiecesUI.AddCapturedPiece(
                        visualPiece2.PieceType, // <-- dùng PieceType hoặc PieceTypeManual
                        visualPiece2.PieceColor == Side.White
                    );
                }

                // Xoá quân cờ khỏi board
                DestroyImmediate(visualPiece2.gameObject);
            }
        }
    }

    public void FixAllPieceRotations()
    {
        foreach (var vp in GetComponentsInChildren<VisualPiece>(true))
        {
            vp.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }
    }



    public GameObject GetPieceGOAtPosition(Square position) {
		GameObject square = GetSquareGOByPosition(position);
		return square.transform.childCount == 0 ? null : square.transform.GetChild(0).gameObject;
	}
	
	private static float FileOrRankToSidePosition(int index) {
		float t = (index - 1) / 7f;
		return Mathf.Lerp(-BoardPlaneSideHalfLength, BoardPlaneSideHalfLength, t);
	}
	
	private void ClearBoard() {
        if (this == null) return;
        VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);

		foreach (VisualPiece pieceBehaviour in visualPiece) {
			DestroyImmediate(pieceBehaviour.gameObject);
		}
	}

    private void OnDestroy()
    {
        GameManager.NewGameStartedEvent -= OnNewGameStarted;
        GameManager.GameResetToHalfMoveEvent -= OnGameResetToHalfMove;
    }


    public GameObject GetSquareGOByPosition(Square position) => Array.Find(allSquaresGO, go => go.name == SquareToString(position));
}
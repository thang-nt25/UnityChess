using System;
using System.Collections.Generic;
using UnityChess;
using UnityEngine;
using static UnityChess.SquareUtil;

public class BoardManager : MonoBehaviourSingleton<BoardManager>
{
    private readonly GameObject[] allSquaresGO = new GameObject[64];
    private Dictionary<Square, GameObject> positionMap;

    private const float BoardPlaneSideLength = 14f;
    private const float BoardPlaneSideHalfLength = BoardPlaneSideLength * 0.5f;
    private const float BoardHeight = 1.6f;
    private readonly System.Random rng = new System.Random();
    public bool IsUserInputEnabled { get; private set; } = true;

    [Header("Captured Pieces UI")]
    public CapturedPiecesUI capturedPiecesUI;


    private void Awake()
    {
        GameManager.NewGameStartedEvent += OnNewGameStarted;
        GameManager.GameResetToHalfMoveEvent += OnGameResetToHalfMove;

        InitializeSquareGameObjects();

        if (capturedPiecesUI != null)
        {
            foreach (Transform child in capturedPiecesUI.whiteArea)
                Destroy(child.gameObject);
            foreach (Transform child in capturedPiecesUI.blackArea)
                Destroy(child.gameObject);
        }
    }

    private void InitializeSquareGameObjects()
    {
        positionMap = new Dictionary<Square, GameObject>(64);
        Transform boardTransform = transform;
        Vector3 boardPosition = boardTransform.position;

        for (int file = 1; file <= 8; file++)
        {
            for (int rank = 1; rank <= 8; rank++)
            {
                Vector3 squarePosition = new Vector3(
                    boardPosition.x + FileOrRankToSidePosition(file),
                    boardPosition.y + BoardHeight,
                    boardPosition.z + FileOrRankToSidePosition(rank)
                );

                GameObject squareGO = new GameObject(SquareToString(file, rank))
                {
                    transform = {
                        position = squarePosition,
                        parent = boardTransform
                    },
                    tag = "Square"
                };

                positionMap.Add(new Square(file, rank), squareGO);
                allSquaresGO[(file - 1) * 8 + (rank - 1)] = squareGO;
            }
        }
    }


    private Side GetPlayerSide()
    {
        return GameManager.Instance.GetHumanSide();
    }

    // ĐÃ SỬA: CHỈ XOAY BOARD CHA, KHÔNG XOAY CAMERA.
    // Camera phải là con của Board cha để xoay theo.
    public void RotateBoardForSide(Side side)
    {
        float targetYRotation = side == Side.White ? 0f : 180f;

        // Xoay đối tượng Board (Board Manager gắn vào đây)
        transform.rotation = Quaternion.Euler(0f, targetYRotation, 0f);
    }


    private void OnNewGameStarted()
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

        // CHỈ DÙNG LOGICAL SQUARE (vì bàn cờ đã xoay vật lý)
        List<(Square logicalSquare, Piece piece)> currentPieces = GameManager.Instance.CurrentPieces;

        foreach ((Square logicalSquare, Piece piece) in currentPieces)
        {
            CreateAndPlacePieceGOAtPhysicalSquare(piece, logicalSquare);
        }

        EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    }

    private void OnGameResetToHalfMove()
    {
        if (this == null) return;

        OnNewGameStarted();

        GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate) SetActiveAllPieces(false);
        else EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    }


    public void CastleRook(Square rookPosition, Square endSquare)
    {
        GameObject rookGO = GetPieceGOAtPosition(rookPosition);
        rookGO.transform.parent = GetSquareGOByPosition(endSquare).transform;
        rookGO.transform.localPosition = Vector3.zero;
    }

    private void CreateAndPlacePieceGOAtPhysicalSquare(Piece piece, Square physicalPosition)
    {
        if (!positionMap.TryGetValue(physicalPosition, out GameObject squareGO) || squareGO == null)
        {
            Debug.LogError($"[BoardManager] Could not find physical square GO for: {physicalPosition}");
            return;
        }

        string modelName = $"{piece.Owner} {piece.GetType().Name}";
        GameObject pieceGO = Instantiate(
            Resources.Load("PieceSets/Marble/" + modelName) as GameObject,
            squareGO.transform
        );

        pieceGO.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        VisualPiece visualPiece = pieceGO.GetComponent<VisualPiece>();
        if (visualPiece != null)
        {
            visualPiece.piece = piece;
            visualPiece.PieceColor = piece.Owner;
            visualPiece.PieceTypeManual = piece.GetType().Name;
        }
    }

    public void CreateAndPlacePieceGO(Piece piece, Square logicalPosition)
    {
        CreateAndPlacePieceGOAtPhysicalSquare(piece, logicalPosition);
    }


    public void GetSquareGOsWithinRadius(List<GameObject> squareGOs, Vector3 positionWS, float radius)
    {
        float radiusSqr = radius * radius;
        foreach (GameObject squareGO in allSquaresGO)
        {
            if ((squareGO.transform.position - positionWS).sqrMagnitude < radiusSqr)
                squareGOs.Add(squareGO);
        }
    }

    public void SetActiveAllPieces(bool active)
    {
        VisualPiece[] allPieces = GetComponentsInChildren<VisualPiece>(true);
        foreach (var vp in allPieces)
        {
            if (vp == null) continue;

            Collider col = vp.GetComponent<Collider>();
            if (col != null) col.enabled = active;

            vp.enabled = active;
        }

        Debug.Log($"[BoardManager] Pieces active = {active}");
    }




    public void EnsureOnlyPiecesOfSideAreEnabled(Side side)
    {
        VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);
        foreach (VisualPiece pieceBehaviour in visualPiece)
        {
            pieceBehaviour.enabled = pieceBehaviour.PieceColor == side;
        }
    }

    public void TryDestroyVisualPiece(Square logicalPosition)
    {
        if (this == null) return;

        Square physicalSquare = logicalPosition;

        if (!positionMap.TryGetValue(physicalSquare, out GameObject squareGO) || squareGO == null) return;

        VisualPiece visualPiece = squareGO.GetComponentInChildren<VisualPiece>();

        if (visualPiece != null)
        {
            VisualPiece visualPiece2 = squareGO.GetComponentInChildren<VisualPiece>();
            if (visualPiece2 != null)
            {
                if (capturedPiecesUI != null)
                {
                    capturedPiecesUI.AddCapturedPiece(
                        visualPiece2.PieceType,
                        visualPiece2.PieceColor == Side.White
                    );
                }

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


    public GameObject GetPieceGOAtPosition(Square logicalPosition)
    {
        GameObject square = GetSquareGOByPosition(logicalPosition);
        return square != null && square.transform.childCount > 0 ? square.transform.GetChild(0).gameObject : null;
    }

    private static float FileOrRankToSidePosition(int index)
    {
        float t = (index - 1) / 7f;
        return Mathf.Lerp(-BoardPlaneSideHalfLength, BoardPlaneSideHalfLength, t);
    }

    private void ClearBoard()
    {
        if (this == null) return;
        VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);

        foreach (VisualPiece pieceBehaviour in visualPiece)
        {
            DestroyImmediate(pieceBehaviour.gameObject);
        }
    }

    private void OnDestroy()
    {
        GameManager.NewGameStartedEvent -= OnNewGameStarted;
        GameManager.GameResetToHalfMoveEvent -= OnGameResetToHalfMove;
    }


    public GameObject GetSquareGOByPosition(Square logicalPosition)
    {
        Square physicalSquare = logicalPosition;

        if (positionMap.TryGetValue(physicalSquare, out GameObject squareGO))
        {
            return squareGO;
        }

        Debug.LogError($"Could not find mapped SquareGO for physical position: {SquareToString(physicalSquare)} (Logical: {SquareToString(logicalPosition)})");
        return null;
    }

    public void SetUserInputEnabled(bool enabled)
    {
        IsUserInputEnabled = enabled;
        Debug.Log($"[BoardManager] User input enabled = {enabled}");
    }
}
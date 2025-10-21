using System.Collections.Generic;
using UnityChess;
using UnityEngine;
using static UnityChess.SquareUtil;

public class VisualPiece : MonoBehaviour
{
    public delegate void VisualPieceMovedAction(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null);
    public static event VisualPieceMovedAction VisualPieceMoved;

    public Side PieceColor;
    public Piece piece;
    public string PieceTypeManual;

    public Square CurrentSquare => StringToSquare(transform.parent.name);

    private const float SquareCollisionRadius = 9f;
    private Camera boardCamera;
    private Vector3 piecePositionSS;
    private List<GameObject> potentialLandingSquares;
    private Transform thisTransform;

    private void Start()
    {
        potentialLandingSquares = new List<GameObject>();
        thisTransform = transform;
        boardCamera = Camera.main;
        thisTransform.rotation = Quaternion.Euler(-90f, thisTransform.rotation.eulerAngles.y, 0f);
    }

    public void OnMouseDown()
    {
        Debug.Log($"[VisualPiece] OnMouseDown triggered. Enabled: {enabled}");
        if (enabled)
        {
            if (this.piece == null)
            {
                Debug.LogError($"[VisualPiece] Piece object is null for {gameObject.name}");
                return;
            }
            if (GameManager.Instance == null)
            {
                Debug.LogError("[VisualPiece] GameManager.Instance is null.");
                return;
            }

            if (GameManager.Instance.CurrentBoard == null)
            {
                Debug.LogError("[VisualPiece] Game is not properly initialized. CurrentBoard is null.");
                return;
            }

            Debug.Log($"[VisualPiece] OnMouseDown called for {this.piece.GetType().Name} at {CurrentSquare}. PieceColor: {PieceColor}, SideToMove: {GameManager.Instance.SideToMove}");
            HighlightManager.Instance.ClearHighlights();

            Piece currentPieceOnBoard = GameManager.Instance.CurrentBoard[CurrentSquare];
            ICollection<Movement> legalMoves = null; // Khởi tạo để sửa lỗi CS0165

            // Sửa lỗi CS1061: Truy cập logic game thông qua phương thức public TryGetLegalMoves
            if (currentPieceOnBoard != null && GameManager.Instance.TryGetLegalMoves(currentPieceOnBoard, out legalMoves))
            {
                Debug.Log($"[VisualPiece] Found {legalMoves.Count} legal moves for {currentPieceOnBoard.GetType().Name} at {CurrentSquare}.");
                HighlightManager.Instance.ShowHighlights(legalMoves, currentPieceOnBoard.Owner);
            }
            else
            {
                Debug.Log($"[VisualPiece] No legal moves found for {this.piece.GetType().Name} at {CurrentSquare} (or piece is null on board).");
            }

            piecePositionSS = boardCamera.WorldToScreenPoint(transform.position);

        }
    }

    private void OnMouseDrag()
    {
        if (enabled)
        {
            Vector3 nextPiecePositionSS = new Vector3(Input.mousePosition.x, Input.mousePosition.y, piecePositionSS.z);
            thisTransform.position = boardCamera.ScreenToWorldPoint(nextPiecePositionSS);
        }
    }

    public void OnMouseUp()
    {
        if (enabled)
        {
            HighlightManager.Instance.ClearHighlights();

            potentialLandingSquares.Clear();
            BoardManager.Instance.GetSquareGOsWithinRadius(potentialLandingSquares, thisTransform.position, SquareCollisionRadius);

            if (potentialLandingSquares.Count == 0)
            {
                thisTransform.position = thisTransform.parent.position;
                return;
            }

            Transform closestSquareTransform = potentialLandingSquares[0].transform;
            float shortestDistanceFromPieceSquared = (closestSquareTransform.position - thisTransform.position).sqrMagnitude;

            for (int i = 1; i < potentialLandingSquares.Count; i++)
            {
                GameObject potentialLandingSquare = potentialLandingSquares[i];
                float distanceFromPieceSquared = (potentialLandingSquare.transform.position - thisTransform.position).sqrMagnitude;

                if (distanceFromPieceSquared < shortestDistanceFromPieceSquared)
                {
                    shortestDistanceFromPieceSquared = distanceFromPieceSquared;
                    closestSquareTransform = potentialLandingSquare.transform;
                }
            }

            VisualPieceMoved?.Invoke(CurrentSquare, thisTransform, closestSquareTransform);

            thisTransform.rotation = Quaternion.Euler(-90f, thisTransform.rotation.eulerAngles.y, 0f);
        }
    }

    public string PieceType
    {
        get
        {
            if (piece != null)
            {
                if (piece is Pawn) return "Pawn";
                if (piece is Rook) return "Rook";
                if (piece is Knight) return "Knight";
                if (piece is Bishop) return "Bishop";
                if (piece is Queen) return "Queen";
                if (piece is King) return "King";
            }
            return PieceTypeManual ?? "Unknown";
        }
    }
}
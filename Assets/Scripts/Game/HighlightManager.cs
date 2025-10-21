using System.Collections.Generic;
using UnityChess;
using UnityEngine;

public class HighlightManager : MonoBehaviourSingleton<HighlightManager>
{
    public GameObject highlightMarkerPrefab;
    public GameObject captureHighlightMarkerPrefab;
    private List<GameObject> activeHighlights = new List<GameObject>();

    public void ShowHighlights(ICollection<Movement> legalMoves, Side movingPieceSide)
    {
        Debug.Log($"[HighlightManager] ShowHighlights called with {legalMoves.Count} legal moves for {movingPieceSide} piece.");
        ClearHighlights(); // Clear existing highlights first

        if (highlightMarkerPrefab == null)
        {
            Debug.LogError("HighlightManager: Normal HighlightMarker Prefab is not assigned.");
            // Don't return, allow capture highlights to still show if assigned
        }

        foreach (Movement move in legalMoves)
        {
            Square targetSquare = move.End;
            GameObject squareGO = BoardManager.Instance.GetSquareGOByPosition(targetSquare);

            if (squareGO != null)
            {
                Piece pieceAtTarget = GameManager.Instance.CurrentBoard[targetSquare];
                bool isCapture = (pieceAtTarget != null && pieceAtTarget.Owner != movingPieceSide);

                GameObject markerPrefabToUse = isCapture ? captureHighlightMarkerPrefab : highlightMarkerPrefab;

                if (markerPrefabToUse == null)
                {
                    Debug.LogError($"HighlightManager: {(isCapture ? "Capture" : "Normal")} HighlightMarker Prefab is not assigned. Skipping highlight for {targetSquare}.");
                    continue; // Skip this highlight if prefab is missing
                }

                GameObject marker = Instantiate(markerPrefabToUse, squareGO.transform);
                marker.transform.localPosition = Vector3.zero; // Position marker at the center of the square
                activeHighlights.Add(marker);
            }
        }
        Debug.Log($"[HighlightManager] Instantiated {activeHighlights.Count} highlight markers.");
    }

    public void ClearHighlights()
    {
        Debug.Log("[HighlightManager] ClearHighlights called.");
        foreach (GameObject marker in activeHighlights)
        {
            Destroy(marker);
        }
        activeHighlights.Clear();
        Debug.Log("[HighlightManager] All highlights cleared.");
    }
}

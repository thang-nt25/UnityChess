using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CapturedPiecesUI : MonoBehaviour
{
    public Transform whiteArea;
    public Transform blackArea;

    // This will be our prefab for displaying a captured piece icon and its count
    public GameObject capturedPieceItemPrefab; 

    // Sprites for each piece
    public Sprite whitePawnSprite;
    public Sprite whiteRookSprite;
    public Sprite whiteKnightSprite;
    public Sprite whiteBishopSprite;
    public Sprite whiteQueenSprite;

    public Sprite blackPawnSprite;
    public Sprite blackRookSprite;
    public Sprite blackKnightSprite;
    public Sprite blackBishopSprite;
    public Sprite blackQueenSprite;

    // Dictionaries to count captured pieces
    private Dictionary<string, int> whiteCapturedPieces = new Dictionary<string, int>();
    private Dictionary<string, int> blackCapturedPieces = new Dictionary<string, int>();

    // This method will be called from your GameManager or BoardManager when a piece is captured
    public void AddCapturedPiece(string pieceType, bool isWhite)
    {
        if (pieceType == "King") return; // Kings aren't captured

        Dictionary<string, int> capturedDict = isWhite ? whiteCapturedPieces : blackCapturedPieces;

        if (capturedDict.ContainsKey(pieceType))
        {
            capturedDict[pieceType]++;
        }
        else
        {
            capturedDict[pieceType] = 1;
        }

        UpdateUI(isWhite);
    }

    // Resets the UI for a new game
    public void ResetUI()
    {
        whiteCapturedPieces.Clear();
        blackCapturedPieces.Clear();

        foreach (Transform child in whiteArea) {
            Destroy(child.gameObject);
        }
        foreach (Transform child in blackArea) {
            Destroy(child.gameObject);
        }
    }

    private void UpdateUI(bool isWhite)
    {
        Transform area = isWhite ? whiteArea : blackArea;
        Dictionary<string, int> capturedDict = isWhite ? whiteCapturedPieces : blackCapturedPieces;

        // Clear the old UI for the specific color
        foreach (Transform child in area)
        {
            Destroy(child.gameObject);
        }

        // Re-create the UI based on the dictionary
        foreach (var capturedPiece in capturedDict)
        {
            string pieceType = capturedPiece.Key;
            int count = capturedPiece.Value;

            if (count == 0) continue;

            GameObject itemGO = Instantiate(capturedPieceItemPrefab, area);
            
            // Find the Image and Text components within the prefab instance
            Image pieceImage = itemGO.GetComponentInChildren<Image>();
            if (pieceImage != null)
            {
                pieceImage.sprite = GetSprite(pieceType, isWhite);
                pieceImage.preserveAspect = true;
            }

            TextMeshProUGUI countText = itemGO.GetComponentInChildren<TextMeshProUGUI>();
            if (countText != null)
            {
                if (count > 1)
                {
                    countText.text = "x" + count;
                    countText.gameObject.SetActive(true);
                }
                else
                {
                    countText.gameObject.SetActive(false);
                }
            }
        }
    }

    public void ToggleVisibility()
    {
        // Check the current state of one of the areas and toggle both
        bool isActive = whiteArea.gameObject.activeSelf;
        whiteArea.gameObject.SetActive(!isActive);
        blackArea.gameObject.SetActive(!isActive);
    }

    private Sprite GetSprite(string pieceType, bool isWhite)
    {
        switch (pieceType)
        {
            case "Pawn": return isWhite ? whitePawnSprite : blackPawnSprite;
            case "Rook": return isWhite ? whiteRookSprite : blackRookSprite;
            case "Knight": return isWhite ? whiteKnightSprite : blackKnightSprite;
            case "Bishop": return isWhite ? whiteBishopSprite : blackBishopSprite;
            case "Queen": return isWhite ? whiteQueenSprite : blackQueenSprite;
            default: return null;
        }
    }
}

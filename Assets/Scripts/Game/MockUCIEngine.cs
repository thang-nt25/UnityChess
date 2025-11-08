// Assets/Scripts/Game/MockUCIEngine.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityChess;
using UnityEngine;
using static UnityChess.SquareUtil;

namespace UnityChess.Engine
{
    public class MockUCIEngine : IUCIEngine
    {
        private Game _game;
        private bool _running;
        private readonly System.Random _rng = new System.Random();

        private Board _currentBoard => GetCurrentBoard();

        // Chỉ dùng max depth 5
        public enum Difficulty { Easy = 1, Medium = 5, Hard = 10 }
        private Difficulty _difficulty = Difficulty.Medium;

        public void SetDifficulty(Difficulty diff) => _difficulty = diff;

        // Giá trị quân (Giữ nguyên)
        private const float PAWN_VALUE = 1.0f;
        private const float KNIGHT_VALUE = 3.2f;
        private const float BISHOP_VALUE = 3.4f;
        private const float ROOK_VALUE = 5.0f;
        private const float QUEEN_VALUE = 9.0f;
        private const float KING_VALUE = 5000f;

        private static readonly Square[] CENTER_SQUARES = {
            new Square(4, 4), new Square(5, 4), new Square(4, 5), new Square(5, 5)
        };

        private const float BISHOP_PAIR_BONUS = 0.5f;

        private Board GetCurrentBoard()
        {
            if (_game == null) return null;
            _game.BoardTimeline.TryGetCurrent(out Board board);
            return board;
        }

        private static Side GetOppositeSide(Side side) => side == Side.White ? Side.Black : Side.White;

        public void Start()
        {
            _running = true;
            Debug.Log("[MOCK UCI] Started.");
        }

        public void ShutDown()
        {
            _running = false;
            Debug.Log("[MOCK UCI] Shut down.");
        }

        public Task SetupNewGame(Game game)
        {
            _game = game;
            Debug.Log("[MOCK UCI] New game set.");
            return Task.CompletedTask;
        }

        public async Task<Movement> GetBestMove(int thinkTimeMs, int depth)
        {
            if (!_running) throw new InvalidOperationException("[MOCK UCI] Engine not started.");
            if (_game == null) throw new InvalidOperationException("[MOCK UCI] Game is null.");

            int currentDepth = (int)_difficulty;
            int baseDelay = Mathf.Clamp(thinkTimeMs, 100, 500);

            // Tăng thời gian chờ dựa trên độ khó
            int adjustedDelay = baseDelay + (currentDepth * 200);
            if (adjustedDelay > 0) await Task.Delay(adjustedDelay);

            var allLegalMoves = CollectAllLegalMoves(_game);
            if (allLegalMoves.Count == 0) return null;

            if (_difficulty == Difficulty.Easy)
            {
                int idx = _rng.Next(allLegalMoves.Count);
                return allLegalMoves[idx];
            }

            // MINIMAX VỚI ALPHA-BETA PRUNING (MEDIUM VÀ HARD)
            float bestScore = float.MinValue;
            Movement bestMove = null;
            float alpha = float.MinValue;
            float beta = float.MaxValue;

            foreach (var move in allLegalMoves)
            {
                // Simulate move
                Board simulatedBoard = SimulateMove(_currentBoard, move);
                if (simulatedBoard == null) continue;

                // Đánh giá với minimax
                float score = Minimax(simulatedBoard, GetOppositeSide(GetPieceOwner(_currentBoard, move)), currentDepth - 1, false, alpha, beta);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }

                alpha = Math.Max(alpha, bestScore);
            }

            return bestMove;
        }

        private float Minimax(Board board, Side sideToMove, int depth, bool isMaximizing, float alpha, float beta)
        {
            if (depth == 0)
            {
                return EvaluateBoard(board, sideToMove);
            }

            var legalMoves = CollectAllLegalMovesFromBoard(board, sideToMove);
            if (legalMoves.Count == 0)
            {
                // Checkmate hoặc stalemate
                return isMaximizing ? float.MinValue : float.MaxValue;
            }

            if (isMaximizing)
            {
                float maxEval = float.MinValue;
                foreach (var move in legalMoves)
                {
                    Board newBoard = SimulateMove(board, move);
                    if (newBoard == null) continue;

                    float eval = Minimax(newBoard, GetOppositeSide(sideToMove), depth - 1, false, alpha, beta);
                    maxEval = Math.Max(maxEval, eval);
                    alpha = Math.Max(alpha, eval);
                    if (beta <= alpha) break; // Prune
                }
                return maxEval;
            }
            else
            {
                float minEval = float.MaxValue;
                foreach (var move in legalMoves)
                {
                    Board newBoard = SimulateMove(board, move);
                    if (newBoard == null) continue;

                    float eval = Minimax(newBoard, GetOppositeSide(sideToMove), depth - 1, true, alpha, beta);
                    minEval = Math.Min(minEval, eval);
                    beta = Math.Min(beta, eval);
                    if (beta <= alpha) break; // Prune
                }
                return minEval;
            }
        }

        // --- HÀM HỖ TRỢ BÀN CỜ ---

        private Board SimulateMove(Board original, Movement move)
        {
            if (original == null) return null;

            Board clone = new Board();

            for (int f = 1; f <= 8; f++)
            {
                for (int r = 1; r <= 8; r++)
                {
                    Piece p = original[f, r];
                    if (p != null)
                        clone[f, r] = Activator.CreateInstance(p.GetType(), p.Owner) as Piece;
                }
            }

            Side side = GetPieceOwner(original, move);

            clone[move.End] = clone[move.Start];
            clone[move.Start] = null;

            if (move is CastlingMove)
            {
                int rank = (side == Side.White) ? 1 : 8;
                if (move.End.File == 7)
                {
                    clone[6, rank] = clone[8, rank];
                    clone[8, rank] = null;
                }
                else if (move.End.File == 3)
                {
                    clone[4, rank] = clone[1, rank];
                    clone[1, rank] = null;
                }
            }

            if (move is PromotionMove promotionMove)
            {
                clone[move.End] = new Queen(side);
            }

            return clone;
        }

        private static Side GetPieceOwner(Board board, Movement move)
        {
            var piece = board[move.Start];
            return piece != null ? piece.Owner : Side.White;
        }

        // --- HÀM KIỂM TRA CHIẾN THUẬT ---

        // Kiểm tra Tốt thông
        private bool IsPassedPawn(Board board, Square square, Side side)
        {
            if (!(board[square] is Pawn)) return false;
            int direction = (side == Side.White) ? 1 : -1;
            int file = square.File;

            for (int f = Math.Max(1, file - 1); f <= Math.Min(8, file + 1); f++)
            {
                for (int r = square.Rank + direction; r != (side == Side.White ? 9 : 0); r += direction)
                {
                    Piece p = board[f, r];
                    if (p is Pawn && p.Owner == GetOppositeSide(side))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // Kiểm tra Cặp Tượng
        private bool HasBishopPair(Board board, Side side)
        {
            int bishopCount = 0;
            for (int f = 1; f <= 8; f++)
            {
                for (int r = 1; r <= 8; r++)
                {
                    Piece p = board[f, r];
                    if (p is Bishop && p.Owner == side)
                    {
                        bishopCount++;
                    }
                }
            }
            return bishopCount == 2;
        }

        private float EvaluateBoard(Board board, Side sideToMove)
        {
            float score = 0f;

            // Material evaluation
            for (int f = 1; f <= 8; f++)
            {
                for (int r = 1; r <= 8; r++)
                {
                    Piece p = board[f, r];
                    if (p != null)
                    {
                        float value = GetPieceValue(p);
                        if (p.Owner == sideToMove)
                        {
                            score += value;
                        }
                        else
                        {
                            score -= value;
                        }

                        // Position bonus
                        if (p is Pawn)
                        {
                            int rankBonus = (p.Owner == Side.White) ? r : 9 - r;
                            score += (p.Owner == sideToMove ? 1 : -1) * rankBonus * 0.1f;
                        }
                        else if (p is Knight || p is Bishop)
                        {
                            // Center control
                            if ((f >= 4 && f <= 5) && (r >= 4 && r <= 5))
                            {
                                score += (p.Owner == sideToMove ? 1 : -1) * 0.2f;
                            }
                        }
                    }
                }
            }

            // Bishop pair bonus
            if (HasBishopPair(board, sideToMove)) score += 0.5f;
            if (HasBishopPair(board, GetOppositeSide(sideToMove))) score -= 0.5f;

            // Mobility bonus
            int myMobility = CollectAllLegalMovesFromBoard(board, sideToMove).Count;
            int oppMobility = CollectAllLegalMovesFromBoard(board, GetOppositeSide(sideToMove)).Count;
            score += (myMobility - oppMobility) * 0.01f;

            return score;
        }

        // --- CÁC HÀM CƠ BẢN GIỮ NGUYÊN ---

        private bool IsLineOpen(Board board, Square square, Side side)
        {
            for (int r = 1; r <= 8; r++)
            {
                Piece piece = board[square.File, r];
                if (piece != null && piece.Owner == side && r != square.Rank) return false;
            }

            for (int f = 1; f <= 8; f++)
            {
                Piece piece = board[f, square.Rank];
                if (piece != null && piece.Owner == side && f != square.File) return false;
            }
            return true;
        }

        private bool IsSquareAttacked(Board board, Square square, Side attackerSide)
        {
            for (int f = 1; f <= 8; f++)
            {
                for (int r = 1; r <= 8; r++)
                {
                    Piece p = board[f, r];
                    if (p == null || p.Owner != attackerSide) continue;

                    if (_game.TryGetLegalMovesForPiece(p, out ICollection<Movement> legalMoves) && legalMoves != null)
                    {
                        foreach (var mv in legalMoves)
                        {
                            if (mv.End.Equals(square)) return true;
                        }
                    }
                }
            }
            return false;
        }

        private float GetPieceValue(Piece p)
        {
            return p switch
            {
                Pawn => PAWN_VALUE,
                Knight => KNIGHT_VALUE,
                Bishop => BISHOP_VALUE,
                Rook => ROOK_VALUE,
                Queen => QUEEN_VALUE,
                King => KING_VALUE,
                _ => 0f
            };
        }

        private static List<Movement> CollectAllLegalMoves(Game game)
        {
            var result = new List<Movement>();
            game.BoardTimeline.TryGetCurrent(out Board board);
            if (board == null) return result;

            game.ConditionsTimeline.TryGetCurrent(out GameConditions cond);

            for (int f = 1; f <= 8; f++)
            {
                for (int r = 1; r <= 8; r++)
                {
                    Piece p = board[f, r];
                    if (p == null || p.Owner != cond.SideToMove) continue;

                    if (game.TryGetLegalMovesForPiece(p, out ICollection<Movement> legal) && legal != null)
                        result.AddRange(legal);
                }
            }

            return result;
        }

        private List<Movement> CollectAllLegalMovesFromBoard(Board board, Side side)
        {
            var result = new List<Movement>();
            if (board == null || _game == null) return result;

            for (int f = 1; f <= 8; f++)
            {
                for (int r = 1; r <= 8; r++)
                {
                    Piece p = board[f, r];
                    if (p == null || p.Owner != side) continue;

                    if (_game.TryGetLegalMovesForPiece(p, out ICollection<Movement> legal) && legal != null)
                        result.AddRange(legal);
                }
            }

            return result;
        }
    }
}
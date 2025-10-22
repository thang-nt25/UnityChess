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
        public enum Difficulty { Easy = 1, Medium = 3, Hard = 5 }
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
            int adjustedDelay = baseDelay + (currentDepth * 700);
            if (adjustedDelay > 0) await Task.Delay(adjustedDelay);

            var allLegalMoves = CollectAllLegalMoves(_game);
            if (allLegalMoves.Count == 0) return null;

            if (_difficulty == Difficulty.Easy)
            {
                int idx = _rng.Next(allLegalMoves.Count);
                return allLegalMoves[idx];
            }

            // TÌM KIẾM SÂU (MEDIUM VÀ HARD)
            float bestScore = float.MinValue;
            Movement bestMove = null;

            // HARD (depth=5) sẽ lặp 50 lần. MEDIUM (depth=3) lặp 10 lần.
            int searchIterations = (currentDepth == 5) ? 50 : 10;

            for (int i = 0; i < searchIterations; i++)
            {
                Movement move = allLegalMoves[_rng.Next(allLegalMoves.Count)];

                // Đánh giá nước đi của AI (Maximize)
                float score = EvaluateMove(move, currentDepth);

                // MINIMAX 2-PLY GIẢ LẬP: Tính toán LỢI/HẠI khi đối thủ phản công
                var simulatedBoard = SimulateMove(_currentBoard, move);
                float worstReplyScore = 0;

                if (simulatedBoard != null && currentDepth >= 3) // Chỉ tính phản công từ Medium trở lên
                {
                    Side opponentSide = GetOppositeSide(GetPieceOwner(simulatedBoard, move));
                    var replyMoves = CollectAllLegalMovesFromBoard(simulatedBoard, opponentSide);

                    if (replyMoves.Count > 0)
                    {
                        // Tìm nước đi tốt nhất của đối thủ (sẽ tạo ra điểm số cao nhất)
                        worstReplyScore = float.MinValue;
                        int replySearchCount = Math.Min(5, replyMoves.Count); // Kiểm tra 5 nước phản công hàng đầu

                        for (int k = 0; k < replySearchCount; k++)
                        {
                            Movement reply = replyMoves[_rng.Next(replyMoves.Count)];

                            // Đánh giá nước reply TỪ GÓC ĐỘ CỦA ĐỐI THỦ
                            float replyEvaluation = EvaluateMove(reply, currentDepth);

                            worstReplyScore = Math.Max(worstReplyScore, replyEvaluation);
                        }
                    }
                }

                // Công thức Minimax: Ta muốn tối đa hóa (score - worstReplyScore)
                // Hình phạt rất nặng cho nước đi dẫn đến phản công mạnh của đối thủ
                float total = score - 1.0f * worstReplyScore;

                if (total > bestScore)
                {
                    bestScore = total;
                    bestMove = move;
                }
            }

            return bestMove;
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

        // --- HÀM ĐÁNH GIÁ CHÍNH (Độ sâu 5) ---

        private float EvaluateMove(Movement move, int depth)
        {
            float score = 0f;
            Board board = _currentBoard;
            if (board == null) return 0;

            Piece movedPiece = board[move.Start];
            if (movedPiece == null) return float.MinValue;

            Piece capturedPiece = board[move.End];
            Side currentSide = movedPiece.Owner;
            Side attackerSide = GetOppositeSide(currentSide);

            float movedValue = GetPieceValue(movedPiece);
            float capturedValue = capturedPiece != null ? GetPieceValue(capturedPiece) : 0f;

            // 1. TÍNH TOÁN LỢI/HẠI VẬT CHẤT (ĂN CÁI NÀY THÌ MẤT GÌ?)
            if (capturedValue > 0)
            {
                float net = capturedValue - movedValue;
                // Nếu lời hoặc hòa, thưởng cực mạnh (200x)
                if (net >= 0) score += net * 200f;
                // Nếu lỗ (thí quân ăn chốt), phạt mạnh (100x)
                else score += net * 100f;
            }

            // 2. PHẠT RỦI RO VÀ AN TOÀN TUYỆT ĐỐI (Mình sẽ yếu đi?)
            if (depth >= 3 && !(movedPiece is King))
            {
                // Phạt cực nặng nếu quân di chuyển đến ô bị tấn công
                if (IsSquareAttacked(board, move.End, attackerSide))
                {
                    bool defended = IsSquareAttacked(board, move.End, currentSide);
                    if (!defended)
                    {
                        // Thí quân miễn phí: Phạt 500% giá trị quân
                        score -= movedValue * 500f;
                    }
                    else
                    {
                        // Đổi quân không cần thiết/bị tấn công: Phạt 50% giá trị quân
                        score -= movedValue * 50f;
                    }
                }
            }

            // 3. CHIẾN THUẬT GÂY DỰNG

            if (move is CastlingMove) score += 60f; // Tăng điểm nhập thành

            // Phát triển/Trung tâm
            if (movedPiece is Knight || movedPiece is Bishop)
            {
                if (move.Start.Rank == (currentSide == Side.White ? 1 : 8)) score += 20f; // Phát triển sớm
            }
            if (Array.Exists(CENTER_SQUARES, s => s.Equals(move.End))) score += 15f; // Kiểm soát trung tâm

            // Xe và Cột Mở
            if (movedPiece is Rook)
            {
                if (IsLineOpen(board, move.End, currentSide)) score += 25f;
            }

            // Cặp Tượng
            if (HasBishopPair(board, currentSide)) score += BISHOP_PAIR_BONUS * 15f;


            // 4. TÀN CUỘC & TỐT THÔNG (depth 5)
            if (depth == 5)
            {
                if (movedPiece is Pawn)
                {
                    if (IsPassedPawn(board, move.End, currentSide))
                    {
                        float rankProgress = currentSide == Side.White ? move.End.Rank : 9 - move.End.Rank;
                        score += rankProgress * 150f; // Điểm thưởng siêu cao cho Tốt thông
                    }
                    else
                    {
                        float rankProgress = currentSide == Side.White ? move.End.Rank : 9 - move.End.Rank;
                        score += rankProgress * 10f;
                    }
                }
                if (movedPiece is King) score += 20f; // King tham gia tàn cuộc
            }
            else if (movedPiece is King) score -= 15f;

            if (move is PromotionMove) score += KING_VALUE * 4f;

            // Ngẫu nhiên (rất nhỏ ở cấp độ Hard)
            score += (float)_rng.NextDouble() * 0.05f;

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
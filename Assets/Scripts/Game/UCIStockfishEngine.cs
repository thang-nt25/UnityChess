using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityChess;
using UnityEngine;
using Debug = UnityEngine.Debug;
using ChessDotNet;  // Thêm using cho ChessDotNet
using ChessPiece = ChessDotNet.Piece;  // Alias để tránh conflict với UnityChess.Piece

#pragma warning disable 0618 // Suppress obsolete constructor warnings from ChessDotNet library

namespace UnityChess.Engine
{
    public class ChessDotNetEngine : IUCIEngine  // Đổi tên class
    {
        private ChessGame game;  // Game từ ChessDotNet
        private bool isReady = false;
        private const int maxDepth = 7; // Tăng depth lên 7 với các tối ưu hóa

        // Transposition table for caching
        private Dictionary<string, TranspositionEntry> transpositionTable = new Dictionary<string, TranspositionEntry>();

        // Killer moves heuristic
        private Move[][] killerMoves = new Move[50][]; // Max 50 plies

        // History heuristic
        private int[,,,] historyTable = new int[2, 8, 8, 8]; // [color, fromRow, fromCol, toCol]

        // Simple opening book
        private Dictionary<string, string[]> openingBook = new Dictionary<string, string[]>()
        {
            // Italian Game
            {"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", new[] {"e2e4", "d2d4"}},
            {"rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", new[] {"e7e5", "c7c5", "e7e6"}},
            {"rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e6 0 2", new[] {"g1f3", "f1c4"}},
            {"rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2", new[] {"b8c6", "g8f6"}},
            {"r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3", new[] {"f1b5", "f1c4", "d2d4"}},
            {"r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 3 3", new[] {"g8f6", "f8c5"}},
            
            // Sicilian Defense
            {"rnbqkbnr/pppppppp/8/8/3P4/8/PPP1PPPP/RNBQKBNR b KQkq d3 0 1", new[] {"g8f6", "d7d5"}},
            {"rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 2", new[] {"g1f3", "b1c3"}},
            {"rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2", new[] {"d7d6", "b8c6", "e7e6"}},
            
            // French Defense
            {"rnbqkbnr/pppp1ppp/4p3/8/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2", new[] {"d2d4", "b1c3"}},
            
            // Caro-Kann
            {"rnbqkbnr/pp1ppppp/2p5/8/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2", new[] {"d2d4", "b1c3"}},
            
            // Queen's Gambit
            {"rnbqkbnr/ppp1pppp/8/3p4/3P4/8/PPP1PPPP/RNBQKBNR w KQkq d6 0 2", new[] {"c2c4", "b1c3"}},
        };

        private class TranspositionEntry
        {
            public int Value;
            public int Depth;
            public int Flag; // 0 = exact, 1 = lower bound, 2 = upper bound
        }

        // Piece-square tables for evaluation
        private readonly int[][] pawnTable = new int[][] {
            new int[] {0,  0,  0,  0,  0,  0,  0,  0},
            new int[] {50, 50, 50, 50, 50, 50, 50, 50},
            new int[] {10, 10, 20, 30, 30, 20, 10, 10},
            new int[] {5,  5, 10, 25, 25, 10,  5,  5},
            new int[] {0,  0,  0, 20, 20,  0,  0,  0},
            new int[] {5, -5,-10,  0,  0,-10, -5,  5},
            new int[] {5, 10, 10,-20,-20, 10, 10,  5},
            new int[] {0,  0,  0,  0,  0,  0,  0,  0}
        };

        private readonly int[][] knightTable = new int[][] {
            new int[] {-50,-40,-30,-30,-30,-30,-40,-50},
            new int[] {-40,-20,  0,  0,  0,  0,-20,-40},
            new int[] {-30,  0, 10, 15, 15, 10,  0,-30},
            new int[] {-30,  5, 15, 20, 20, 15,  5,-30},
            new int[] {-30,  0, 15, 20, 20, 15,  0,-30},
            new int[] {-30,  5, 10, 15, 15, 10,  5,-30},
            new int[] {-40,-20,  0,  5,  5,  0,-20,-40},
            new int[] {-50,-40,-30,-30,-30,-30,-40,-50}
        };

        private readonly int[][] bishopTable = new int[][] {
            new int[] {-20,-10,-10,-10,-10,-10,-10,-20},
            new int[] {-10,  0,  0,  0,  0,  0,  0,-10},
            new int[] {-10,  0,  5, 10, 10,  5,  0,-10},
            new int[] {-10,  5,  5, 10, 10,  5,  5,-10},
            new int[] {-10,  0, 10, 10, 10, 10,  0,-10},
            new int[] {-10, 10, 10, 10, 10, 10, 10,-10},
            new int[] {-10,  5,  0,  0,  0,  0,  5,-10},
            new int[] {-20,-10,-10,-10,-10,-10,-10,-20}
        };

        private readonly int[][] rookTable = new int[][] {
            new int[] {0,  0,  0,  0,  0,  0,  0,  0},
            new int[] {5, 10, 10, 10, 10, 10, 10,  5},
            new int[] {-5,  0,  0,  0,  0,  0,  0, -5},
            new int[] {-5,  0,  0,  0,  0,  0,  0, -5},
            new int[] {-5,  0,  0,  0,  0,  0,  0, -5},
            new int[] {-5,  0,  0,  0,  0,  0,  0, -5},
            new int[] {-5,  0,  0,  0,  0,  0,  0, -5},
            new int[] {0,  0,  0,  5,  5,  0,  0,  0}
        };

        private readonly int[][] queenTable = new int[][] {
            new int[] {-20,-10,-10, -5, -5,-10,-10,-20},
            new int[] {-10,  0,  0,  0,  0,  0,  0,-10},
            new int[] {-10,  0,  5,  5,  5,  5,  0,-10},
            new int[] {-5,  0,  5,  5,  5,  5,  0, -5},
            new int[] {0,  0,  5,  5,  5,  5,  0, -5},
            new int[] {-10,  5,  5,  5,  5,  5,  0,-10},
            new int[] {-10,  0,  5,  0,  0,  0,  0,-10},
            new int[] {-20,-10,-10, -5, -5,-10,-10,-20}
        };

        private readonly int[][] kingTable = new int[][] {
            new int[] {-30,-40,-40,-50,-50,-40,-40,-30},
            new int[] {-30,-40,-40,-50,-50,-40,-40,-30},
            new int[] {-30,-40,-40,-50,-50,-40,-40,-30},
            new int[] {-30,-40,-40,-50,-50,-40,-40,-30},
            new int[] {-20,-30,-30,-40,-40,-30,-30,-20},
            new int[] {-10,-20,-20,-20,-20,-20,-20,-10},
            new int[] {20, 20,  0,  0,  0,  0, 20, 20},
            new int[] {20, 30, 10,  0,  0, 10, 30, 20}
        };

        public ChessDotNetEngine()
        {
            game = new ChessGame();  // Khởi tạo game mặc định
            isReady = true;

            // Initialize killer moves
            for (int i = 0; i < killerMoves.Length; i++)
            {
                killerMoves[i] = new Move[2]; // 2 killer moves per ply
            }
        }

        public void Start()
        {
            Debug.Log("[ChessDotNet] Engine started.");
        }

        public void ShutDown()
        {
            isReady = false;
            Debug.Log("[ChessDotNet] Engine shut down.");
        }

        public Task SetupNewGame(Game game)
        {
            // Reset game từ FEN nếu cần
            string initialFen = GameManager.Instance.SerializeGame();
            this.game = new ChessGame(initialFen);  // Load từ FEN
            // Clear transposition table for new game
            transpositionTable.Clear();
            // Clear history table
            Array.Clear(historyTable, 0, historyTable.Length);
            // Clear killer moves
            for (int i = 0; i < killerMoves.Length; i++)
            {
                killerMoves[i] = new Move[2];
            }
            return Task.CompletedTask;
        }

        public async Task<Movement> GetBestMove(int timeoutMS, int depth)
        {
            if (!isReady)
            {
                Debug.LogWarning("[ChessDotNet] Engine not ready.");
                return null;
            }

            string currentFen = GameManager.Instance.SerializeGame();
            game = new ChessGame(currentFen);  // Update board từ FEN

            // Check opening book first
            Move bookMove = GetOpeningBookMove(currentFen);
            if (bookMove != null)
            {
                Debug.Log("[ChessDotNet] Using opening book move");
                return ConvertToMovement(bookMove);
            }

            // Use iterative deepening with time control
            var bestMove = await Task.Run(() => GetBestMoveIterativeDeepening(timeoutMS, depth));

            if (bestMove == null)
            {
                Debug.LogError("[ChessDotNet] Best move is null, cannot proceed");
                return null;
            }

            // Chuyển sang Movement của UnityChess
            return ConvertToMovement(bestMove);
        }

        private Move GetBestMoveIterativeDeepening(int timeoutMS, int maxDepth)
        {
            Move bestMove = null;
            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            for (int depth = 1; depth <= maxDepth; depth++)
            {
                if (timer.ElapsedMilliseconds > timeoutMS * 0.8) // Use 80% of time to avoid timeout
                    break;

                Move currentBest = GetBestMoveMinimax(game, depth);
                if (currentBest != null)
                {
                    bestMove = currentBest;
                    Debug.Log($"[ChessDotNet] Completed depth {depth}, time: {timer.ElapsedMilliseconds}ms");
                }
            }

            timer.Stop();
            return bestMove;
        }

        private Move GetOpeningBookMove(string fen)
        {
            // Extract just the position part (before move counters)
            string[] fenParts = fen.Split(' ');
            string positionKey = fenParts[0] + " " + fenParts[1] + " " + fenParts[2] + " " + fenParts[3];

            if (openingBook.ContainsKey(positionKey))
            {
                string[] possibleMoves = openingBook[positionKey];
                // Randomly select one of the book moves for variety
                string moveStr = possibleMoves[UnityEngine.Random.Range(0, possibleMoves.Length)];
                return ParseAlgebraicMove(moveStr, game);
            }
            return null;
        }

        private Move ParseAlgebraicMove(string moveStr, ChessGame game)
        {
            if (moveStr.Length < 4) return null;

            string fromSquare = moveStr.Substring(0, 2);
            string toSquare = moveStr.Substring(2, 2);

            var moves = game.GetValidMoves(game.WhoseTurn);
            foreach (var move in moves)
            {
                if (move.OriginalPosition.ToString().ToLower() == fromSquare &&
                    move.NewPosition.ToString().ToLower() == toSquare)
                {
                    return move;
                }
            }
            return null;
        }

        private Move GetBestMoveMinimax(ChessGame game, int depth)
        {
            var moves = game.GetValidMoves(game.WhoseTurn);
            if (moves.Count == 0) return null;

            // Move ordering: captures first, then killer moves, then history
            var orderedMoves = OrderMoves(moves, game, 0);

            Move bestMove = null;
            bool isMaximizing = game.WhoseTurn == Player.White;
            int bestValue = isMaximizing ? int.MinValue : int.MaxValue;

            foreach (var move in orderedMoves)
            {
                string fenBefore = game.GetFen();
                ChessGame gameCopy = new ChessGame(fenBefore);
                gameCopy.MakeMove(move, true);
                int value = Minimax(gameCopy, depth - 1, !isMaximizing, int.MinValue, int.MaxValue, 1, true);

                if ((isMaximizing && value > bestValue) || (!isMaximizing && value < bestValue))
                {
                    bestValue = value;
                    bestMove = move;
                }
            }

            // Fallback: if no move was selected, just pick the first valid move
            if (bestMove == null && orderedMoves.Count > 0)
            {
                Debug.LogWarning("[ChessDotNet] No best move found, using first available move");
                bestMove = orderedMoves[0];
            }

            return bestMove;
        }

        private List<Move> OrderMoves(System.Collections.ObjectModel.ReadOnlyCollection<Move> moves, ChessGame game, int ply)
        {
            var scoredMoves = new List<(Move move, int score)>();

            foreach (var move in moves)
            {
                int score = GetMoveScore(move, game);

                // Add killer move bonus
                if (ply < killerMoves.Length)
                {
                    if (killerMoves[ply][0] != null && MovesEqual(move, killerMoves[ply][0]))
                        score += 9000;
                    else if (killerMoves[ply][1] != null && MovesEqual(move, killerMoves[ply][1]))
                        score += 8000;
                }

                // Add history heuristic bonus
                int colorIdx = game.WhoseTurn == Player.White ? 0 : 1;
                int fromRow = 8 - move.OriginalPosition.Rank;
                int fromCol = (int)move.OriginalPosition.File;
                int toCol = (int)move.NewPosition.File;
                score += historyTable[colorIdx, fromRow, fromCol, toCol] / 10;

                scoredMoves.Add((move, score));
            }

            return scoredMoves.OrderByDescending(x => x.score).Select(x => x.move).ToList();
        }

        private bool MovesEqual(Move a, Move b)
        {
            return a.OriginalPosition.File == b.OriginalPosition.File &&
                   a.OriginalPosition.Rank == b.OriginalPosition.Rank &&
                   a.NewPosition.File == b.NewPosition.File &&
                   a.NewPosition.Rank == b.NewPosition.Rank;
        }

        private int GetMoveScore(Move move, ChessGame game)
        {
            try
            {
                int score = 0;

                // Prioritize captures
                var board = game.GetBoard();
                int targetRow = 8 - move.NewPosition.Rank;
                int targetCol = (int)move.NewPosition.File;
                ChessPiece capturedPiece = board[targetRow][targetCol];

                if (capturedPiece != null)
                {
                    int victimValue = char.ToLower(capturedPiece.GetFenCharacter()) switch
                    {
                        'p' => 100,
                        'n' => 320,
                        'b' => 330,
                        'r' => 500,
                        'q' => 900,
                        'k' => 20000,
                        _ => 0
                    };

                    // Get attacker piece from original position
                    int attackerRow = 8 - move.OriginalPosition.Rank;
                    int attackerCol = (int)move.OriginalPosition.File;
                    ChessPiece attackerPiece = board[attackerRow][attackerCol];

                    int attackerValue = attackerPiece != null ? char.ToLower(attackerPiece.GetFenCharacter()) switch
                    {
                        'p' => 100,
                        'n' => 320,
                        'b' => 330,
                        'r' => 500,
                        'q' => 900,
                        'k' => 20000,
                        _ => 0
                    } : 0;

                    // MVV-LVA with safety check
                    score = victimValue * 10 - attackerValue / 10;

                    // Penalty if capturing with more valuable piece and target is defended
                    if (attackerValue > victimValue)
                    {
                        // Check if target square is defended
                        if (IsSquareDefended(game, move.NewPosition, game.WhoseTurn == Player.White ? Player.Black : Player.White))
                        {
                            // Dangerous capture - penalize heavily
                            score -= (attackerValue - victimValue) * 2;
                        }
                    }
                }
                else
                {
                    // For quiet moves, check if we're moving to a defended square
                    int attackerRow = 8 - move.OriginalPosition.Rank;
                    int attackerCol = (int)move.OriginalPosition.File;
                    ChessPiece movingPiece = board[attackerRow][attackerCol];

                    if (movingPiece != null)
                    {
                        // Penalty if moving to an attacked square
                        if (IsSquareDefended(game, move.NewPosition, game.WhoseTurn == Player.White ? Player.Black : Player.White))
                        {
                            int pieceValue = char.ToLower(movingPiece.GetFenCharacter()) switch
                            {
                                'p' => 100,
                                'n' => 320,
                                'b' => 330,
                                'r' => 500,
                                'q' => 900,
                                _ => 0
                            };

                            // Check if target square is also defended by us
                            if (!IsSquareDefended(game, move.NewPosition, game.WhoseTurn))
                            {
                                score -= pieceValue; // Heavy penalty for hanging piece
                            }
                        }
                    }
                }

                // Prioritize promotions
                if (move.Promotion.HasValue)
                    score += 8000;

                return score;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChessDotNet] Error in GetMoveScore: {ex.Message}");
                return 0;
            }
        }

        private bool IsSquareDefended(ChessGame game, Position pos, Player defender)
        {
            try
            {
                // Check if any of defender's pieces can attack this square
                var moves = game.GetValidMoves(defender);
                foreach (var move in moves)
                {
                    if (move.NewPosition.File == pos.File && move.NewPosition.Rank == pos.Rank)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false; // Safe default
            }
        }

        private int Minimax(ChessGame game, int depth, bool isMaximizing, int alpha, int beta, int ply, bool allowNull)
        {
            // Check transposition table
            string fen = game.GetFen();
            if (transpositionTable.ContainsKey(fen))
            {
                var entry = transpositionTable[fen];
                if (entry.Depth >= depth)
                {
                    if (entry.Flag == 0) return entry.Value; // exact
                    if (entry.Flag == 1 && entry.Value >= beta) return beta; // lower bound
                    if (entry.Flag == 2 && entry.Value <= alpha) return alpha; // upper bound
                }
            }

            if (depth == 0)
            {
                return QuiescenceSearch(game, isMaximizing, alpha, beta);
            }

            var moves = game.GetValidMoves(isMaximizing ? Player.White : Player.Black);
            if (moves.Count == 0)
            {
                if (game.IsCheckmated(isMaximizing ? Player.White : Player.Black))
                    return isMaximizing ? int.MinValue + 1000 : int.MaxValue - 1000;
                return 0; // stalemate
            }

            // Null move pruning
            bool inCheck = game.IsInCheck(isMaximizing ? Player.White : Player.Black);
            if (allowNull && !inCheck && depth >= 3)
            {
                // Make a null move (pass the turn)
                string fenBefore = game.GetFen();
                ChessGame nullGame = new ChessGame(fenBefore);
                // Skip turn by evaluating with reduced depth
                int nullScore = -Minimax(nullGame, depth - 3, !isMaximizing, -beta, -beta + 1, ply + 1, false);
                if (nullScore >= beta)
                    return beta; // Beta cutoff
            }

            int originalAlpha = alpha;
            int bestValue = isMaximizing ? int.MinValue : int.MaxValue;
            Move bestMove = null;

            var orderedMoves = OrderMoves(moves, game, ply);
            int moveCount = 0;

            foreach (var move in orderedMoves)
            {
                string fenBefore = game.GetFen();
                ChessGame gameCopy = new ChessGame(fenBefore);
                gameCopy.MakeMove(move, true);

                int value;
                moveCount++;

                // Late Move Reduction (LMR)
                if (moveCount > 4 && depth >= 3 && !inCheck && GetMoveScore(move, game) < 100)
                {
                    // Reduce depth for late moves that don't look promising
                    value = Minimax(gameCopy, depth - 2, !isMaximizing, alpha, beta, ply + 1, true);

                    // If it looks good, re-search with full depth
                    if ((isMaximizing && value > alpha) || (!isMaximizing && value < beta))
                    {
                        value = Minimax(gameCopy, depth - 1, !isMaximizing, alpha, beta, ply + 1, true);
                    }
                }
                else
                {
                    value = Minimax(gameCopy, depth - 1, !isMaximizing, alpha, beta, ply + 1, true);
                }

                if (isMaximizing)
                {
                    if (value > bestValue)
                    {
                        bestValue = value;
                        bestMove = move;
                    }
                    alpha = Math.Max(alpha, bestValue);
                }
                else
                {
                    if (value < bestValue)
                    {
                        bestValue = value;
                        bestMove = move;
                    }
                    beta = Math.Min(beta, bestValue);
                }

                if (beta <= alpha)
                {
                    // Update killer moves for non-capture moves
                    if (GetMoveScore(move, game) < 100 && ply < killerMoves.Length)
                    {
                        if (killerMoves[ply][0] == null || !MovesEqual(move, killerMoves[ply][0]))
                        {
                            killerMoves[ply][1] = killerMoves[ply][0];
                            killerMoves[ply][0] = move;
                        }
                    }

                    // Update history table
                    int colorIdx = game.WhoseTurn == Player.White ? 0 : 1;
                    int fromRow = 8 - move.OriginalPosition.Rank;
                    int fromCol = (int)move.OriginalPosition.File;
                    int toCol = (int)move.NewPosition.File;
                    historyTable[colorIdx, fromRow, fromCol, toCol] += depth * depth;

                    break;  // Alpha-beta cutoff
                }
            }

            // Store in transposition table
            var newEntry = new TranspositionEntry();
            newEntry.Value = bestValue;
            newEntry.Depth = depth;
            if (bestValue <= originalAlpha) newEntry.Flag = 2; // upper bound
            else if (bestValue >= beta) newEntry.Flag = 1; // lower bound
            else newEntry.Flag = 0; // exact
            transpositionTable[fen] = newEntry;

            return bestValue;
        }

        private int QuiescenceSearch(ChessGame game, bool isMaximizing, int alpha, int beta)
        {
            int standPat = EvaluateBoard(game, isMaximizing ? Player.White : Player.Black);

            if (isMaximizing)
            {
                if (standPat >= beta) return beta;
                if (standPat > alpha) alpha = standPat;
            }
            else
            {
                if (standPat <= alpha) return alpha;
                if (standPat < beta) beta = standPat;
            }

            // Only consider captures and promotions
            var moves = game.GetValidMoves(isMaximizing ? Player.White : Player.Black)
                .Where(move => IsCaptureOrPromotion(game, move)).ToList();

            foreach (var move in moves)
            {
                string fenBefore = game.GetFen();
                ChessGame gameCopy = new ChessGame(fenBefore);
                gameCopy.MakeMove(move, true);
                int value = QuiescenceSearch(gameCopy, !isMaximizing, alpha, beta);

                if (isMaximizing)
                {
                    if (value >= beta) return beta;
                    if (value > alpha) alpha = value;
                }
                else
                {
                    if (value <= alpha) return alpha;
                    if (value < beta) beta = value;
                }
            }

            return isMaximizing ? alpha : beta;
        }

        private bool IsCaptureOrPromotion(ChessGame game, Move move)
        {
            // Check if move is a capture
            var board = game.GetBoard();
            int targetRow = 8 - move.NewPosition.Rank; // ChessDotNet uses different coordinate system
            int targetCol = (int)move.NewPosition.File;
            return board[targetRow][targetCol] != null || move.Promotion.HasValue;
        }

        private int EvaluateBoard(ChessGame game, Player perspective)
        {
            try
            {
                int score = 0;
                ChessDotNet.Piece[][] board = game.GetBoard();

                // Tính điểm từ piece values và position
                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        ChessPiece piece = board[row][col];
                        if (piece != null)
                        {
                            int baseValue = char.ToLower(piece.GetFenCharacter()) switch
                            {
                                'p' => 100,
                                'n' => 320,
                                'b' => 330,
                                'r' => 500,
                                'q' => 900,
                                'k' => 20000,
                                _ => 0
                            };

                            int positionBonus = 0;
                            int tableRow = piece.Owner == Player.White ? 7 - row : row; // Flip for black

                            switch (char.ToLower(piece.GetFenCharacter()))
                            {
                                case 'p': positionBonus = pawnTable[tableRow][col]; break;
                                case 'n': positionBonus = knightTable[tableRow][col]; break;
                                case 'b': positionBonus = bishopTable[tableRow][col]; break;
                                case 'r': positionBonus = rookTable[tableRow][col]; break;
                                case 'q': positionBonus = queenTable[tableRow][col]; break;
                                case 'k': positionBonus = kingTable[tableRow][col]; break;
                            }

                            int totalValue = baseValue + positionBonus;
                            score += piece.Owner == perspective ? totalValue : -totalValue;
                        }
                    }
                }

                // Bonus cho kiểm soát trung tâm
                int centerControl = 0;
                for (int r = 3; r <= 4; r++)
                {
                    for (int c = 3; c <= 4; c++)
                    {
                        ChessPiece piece = board[r][c];
                        if (piece != null)
                        {
                            int bonus = char.ToLower(piece.GetFenCharacter()) switch
                            {
                                'p' => 10,
                                'n' => 20,
                                'b' => 15,
                                'r' => 5,
                                'q' => 5,
                                'k' => 0,
                                _ => 0
                            };
                            centerControl += piece.Owner == perspective ? bonus : -bonus;
                        }
                    }
                }
                score += centerControl;

                // King safety evaluation
                int kingSafety = EvaluateKingSafety(board, game, perspective);
                score += kingSafety;

                // Pawn structure analysis
                int pawnStructure = EvaluatePawnStructure(board, perspective);
                score += pawnStructure;

                // Rook on open/semi-open files
                int rookPlacement = EvaluateRookPlacement(board);
                score += perspective == Player.White ? rookPlacement : -rookPlacement;

                // Bishop pair bonus
                int bishopPair = EvaluateBishopPair(board);
                score += perspective == Player.White ? bishopPair : -bishopPair;

                // Bonus cho tính di động (mobility)
                int mobility = game.GetValidMoves(perspective).Count - game.GetValidMoves(perspective == Player.White ? Player.Black : Player.White).Count;
                score += mobility * 5;

                // Penalty cho vua bị chiếu
                if (game.IsInCheck(perspective))
                    score -= 50;
                if (game.IsInCheck(perspective == Player.White ? Player.Black : Player.White))
                    score += 50;

                // Penalty for hanging (undefended) pieces
                score -= EvaluateHangingPieces(game, board, perspective);

                // Endgame detection and king activity
                int totalMaterial = CountTotalMaterial(board);
                if (totalMaterial < 2500) // Endgame threshold
                {
                    int kingActivity = EvaluateKingActivity(board, perspective);
                    score += kingActivity;
                }

                return score;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChessDotNet] Error in EvaluateBoard: {ex.Message}\n{ex.StackTrace}");
                return 0; // Return neutral score
            }
        }

        private int CountTotalMaterial(ChessDotNet.Piece[][] board)
        {
            int total = 0;
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    ChessPiece piece = board[row][col];
                    if (piece != null)
                    {
                        int value = char.ToLower(piece.GetFenCharacter()) switch
                        {
                            'p' => 100,
                            'n' => 320,
                            'b' => 330,
                            'r' => 500,
                            'q' => 900,
                            _ => 0
                        };
                        total += value;
                    }
                }
            }
            return total;
        }

        private int EvaluateKingSafety(ChessDotNet.Piece[][] board, ChessGame game, Player perspective)
        {
            int score = 0;

            // Find king positions
            (int row, int col) whiteKing = (-1, -1);
            (int row, int col) blackKing = (-1, -1);

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    ChessPiece piece = board[row][col];
                    if (piece != null && char.ToLower(piece.GetFenCharacter()) == 'k')
                    {
                        if (piece.Owner == Player.White)
                            whiteKing = (row, col);
                        else
                            blackKing = (row, col);
                    }
                }
            }

            // Evaluate pawn shield for white king
            if (whiteKing.row >= 6) // King on back ranks
            {
                int pawnShield = 0;
                for (int c = Math.Max(0, whiteKing.col - 1); c <= Math.Min(7, whiteKing.col + 1); c++)
                {
                    if (whiteKing.row > 0)
                    {
                        var p = board[whiteKing.row - 1][c];
                        if (p != null && char.ToLower(p.GetFenCharacter()) == 'p' && p.Owner == Player.White)
                            pawnShield += 15;
                    }
                    if (whiteKing.row > 1)
                    {
                        var p = board[whiteKing.row - 2][c];
                        if (p != null && char.ToLower(p.GetFenCharacter()) == 'p' && p.Owner == Player.White)
                            pawnShield += 10;
                    }
                }
                score += perspective == Player.White ? pawnShield : -pawnShield;
            }

            // Evaluate pawn shield for black king
            if (blackKing.row <= 1) // King on back ranks
            {
                int pawnShield = 0;
                for (int c = Math.Max(0, blackKing.col - 1); c <= Math.Min(7, blackKing.col + 1); c++)
                {
                    if (blackKing.row < 7)
                    {
                        var p = board[blackKing.row + 1][c];
                        if (p != null && char.ToLower(p.GetFenCharacter()) == 'p' && p.Owner == Player.Black)
                            pawnShield += 15;
                    }
                    if (blackKing.row < 6)
                    {
                        var p = board[blackKing.row + 2][c];
                        if (p != null && char.ToLower(p.GetFenCharacter()) == 'p' && p.Owner == Player.Black)
                            pawnShield += 10;
                    }
                }
                score += perspective == Player.Black ? pawnShield : -pawnShield;
            }

            return score;
        }

        private int EvaluateRookPlacement(ChessDotNet.Piece[][] board)
        {
            int score = 0;

            // Check each file for pawns
            bool[] filePawns = new bool[8];
            for (int col = 0; col < 8; col++)
            {
                for (int row = 0; row < 8; row++)
                {
                    var piece = board[row][col];
                    if (piece != null && char.ToLower(piece.GetFenCharacter()) == 'p')
                    {
                        filePawns[col] = true;
                        break;
                    }
                }
            }

            // Evaluate rook placement
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var piece = board[row][col];
                    if (piece != null && char.ToLower(piece.GetFenCharacter()) == 'r')
                    {
                        int bonus = 0;
                        if (!filePawns[col]) // Open file
                            bonus = 25;
                        // Check if semi-open (only enemy pawns)
                        else
                        {
                            bool ownPawn = false;
                            for (int r = 0; r < 8; r++)
                            {
                                var p = board[r][col];
                                if (p != null && char.ToLower(p.GetFenCharacter()) == 'p' && p.Owner == piece.Owner)
                                {
                                    ownPawn = true;
                                    break;
                                }
                            }
                            if (!ownPawn)
                                bonus = 15; // Semi-open file
                        }

                        score += piece.Owner == Player.White ? bonus : -bonus;
                    }
                }
            }

            return score;
        }

        private int EvaluateBishopPair(ChessDotNet.Piece[][] board)
        {
            int whiteBishops = 0;
            int blackBishops = 0;

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var piece = board[row][col];
                    if (piece != null && char.ToLower(piece.GetFenCharacter()) == 'b')
                    {
                        if (piece.Owner == Player.White)
                            whiteBishops++;
                        else
                            blackBishops++;
                    }
                }
            }

            int score = 0;
            if (whiteBishops >= 2) score += 50; // Bishop pair bonus
            if (blackBishops >= 2) score -= 50;

            return score;
        }

        private int EvaluateKingActivity(ChessDotNet.Piece[][] board, Player perspective)
        {
            int score = 0;

            // Find king
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var piece = board[row][col];
                    if (piece != null && char.ToLower(piece.GetFenCharacter()) == 'k' && piece.Owner == perspective)
                    {
                        // In endgame, king should be active (closer to center)
                        int distanceFromCenter = Math.Abs(row - 3) + Math.Abs(row - 4) + Math.Abs(col - 3) + Math.Abs(col - 4);
                        score -= distanceFromCenter * 5; // Bonus for being near center
                        return score;
                    }
                }
            }

            return score;
        }

        private int EvaluatePawnStructure(ChessDotNet.Piece[][] board, Player perspective)
        {
            try
            {
                int score = 0;

                // Track pawns by file and color
                bool[] whitePawns = new bool[8];
                bool[] blackPawns = new bool[8];

                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        ChessPiece piece = board[row][col];
                        if (piece != null && char.ToLower(piece.GetFenCharacter()) == 'p')
                        {
                            if (piece.Owner == Player.White)
                                whitePawns[col] = true;
                            else
                                blackPawns[col] = true;
                        }
                    }
                }

                // Evaluate for both sides
                score += EvaluatePawnStructureForColor(board, whitePawns, blackPawns, Player.White, perspective);
                score += EvaluatePawnStructureForColor(board, blackPawns, whitePawns, Player.Black, perspective);

                return score;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChessDotNet] Error in EvaluatePawnStructure: {ex.Message}");
                return 0;
            }
        }

        private int EvaluatePawnStructureForColor(ChessDotNet.Piece[][] board, bool[] ownPawns, bool[] enemyPawns, Player color, Player perspective)
        {
            try
            {
                int score = 0;

                for (int file = 0; file < 8; file++)
                {
                    if (ownPawns[file])
                    {
                        // Doubled pawns penalty
                        int doubledCount = 0;
                        for (int r = 0; r < 8; r++)
                        {
                            ChessPiece piece = board[r][file];
                            if (piece != null && piece.Owner == color && char.ToLower(piece.GetFenCharacter()) == 'p')
                                doubledCount++;
                        }
                        if (doubledCount > 1)
                            score += (color == perspective ? -20 : 20) * (doubledCount - 1);

                        // Isolated pawns penalty
                        bool hasNeighbor = (file > 0 && ownPawns[file - 1]) || (file < 7 && ownPawns[file + 1]);
                        if (!hasNeighbor)
                            score += color == perspective ? -15 : 15;

                        // Passed pawns bonus - simplified check
                        bool isPassed = true;
                        for (int f = Math.Max(0, file - 1); f <= Math.Min(7, file + 1); f++)
                        {
                            if (enemyPawns[f])
                            {
                                isPassed = false;
                                break;
                            }
                        }
                        if (isPassed)
                            score += color == perspective ? 30 : -30;
                    }
                }

                return score;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChessDotNet] Error in EvaluatePawnStructureForColor: {ex.Message}");
                return 0;
            }
        }

        private int EvaluateHangingPieces(ChessGame game, ChessDotNet.Piece[][] board, Player perspective)
        {
            int penalty = 0;
            Player opponent = perspective == Player.White ? Player.Black : Player.White;

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    ChessPiece piece = board[row][col];
                    if (piece != null && piece.Owner == perspective)
                    {
                        // Convert board coordinates to Position
                        int rank = 8 - row;
                        ChessDotNet.File file = (ChessDotNet.File)col;
                        Position pos = new Position(file, rank);

                        // Check if this square is attacked by opponent
                        bool isAttacked = IsSquareDefended(game, pos, opponent);

                        if (isAttacked)
                        {
                            // Check if it's defended by us
                            bool isDefended = IsSquareDefended(game, pos, perspective);

                            if (!isDefended)
                            {
                                // Piece is hanging (attacked but not defended)
                                int pieceValue = char.ToLower(piece.GetFenCharacter()) switch
                                {
                                    'p' => 100,
                                    'n' => 320,
                                    'b' => 330,
                                    'r' => 500,
                                    'q' => 900,
                                    'k' => 0, // King can't really hang
                                    _ => 0
                                };
                                penalty += pieceValue;
                            }
                        }
                    }
                }
            }

            return penalty;
        }

        private Movement ConvertToMovement(Move chessDotNetMove)
        {
            if (chessDotNetMove == null) return null;

            // Lấy start/end từ Move (ChessDotNet có OriginalPosition, NewPosition)
            string startStr = chessDotNetMove.OriginalPosition.ToString().ToLower();  // e.g., "e2"
            string endStr = chessDotNetMove.NewPosition.ToString().ToLower();

            Debug.Log($"[ChessDotNet] Converting move: {startStr} -> {endStr}");

            Square startSquare = new Square(startStr);
            Square endSquare = new Square(endStr);

            if (GameManager.Instance.Game.TryGetLegalMove(startSquare, endSquare, out Movement move))
            {
                // Handle promotion nếu ChessDotNet hỗ trợ (kiểm tra Promotion)
                if (move is PromotionMove promotionMove && chessDotNetMove.Promotion.HasValue)
                {
                    Side side = GameManager.Instance.SideToMove;
                    UnityChess.Piece promotionPiece = chessDotNetMove.Promotion.Value.ToString().ToLower() switch
                    {
                        "q" => new Queen(side),
                        "r" => new Rook(side),
                        "b" => new Bishop(side),
                        "n" => new Knight(side),
                        _ => null
                    };
                    if (promotionPiece != null)
                    {
                        promotionMove.SetPromotionPiece(promotionPiece);
                    }
                }
                return move;
            }

            Debug.LogError($"[ChessDotNet] Failed to convert move {startStr}-{endStr} to Legal Move. StartSquare: {startSquare}, EndSquare: {endSquare}");
            return null;
        }
    }
}
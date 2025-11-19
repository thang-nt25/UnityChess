using System.Threading.Tasks;
using UnityChess.Engine;
using StockFishPortApp_5._0;
using System.Collections.Generic;
using System;

namespace UnityChess.Engine
{
    public class StockfishUCIEngine : IUCIEngine
    {
        private Position _currentPosition;
        private Game _game;
        private bool _isInitialized = false;

        public void Start()
        {
            if (!_isInitialized)
            {
                try
                {
                    UnityEngine.Debug.Log("[Stockfish] Initializing Stockfish components...");

                    // Initialize Stockfish components
                    StockFishPortApp_5._0.Uci.init(StockFishPortApp_5._0.Engine.Options);
                    UnityEngine.Debug.Log("[Stockfish] UCI initialized");

                    StockFishPortApp_5._0.BitBoard.init();
                    UnityEngine.Debug.Log("[Stockfish] BitBoard initialized");

                    StockFishPortApp_5._0.Position.init();
                    UnityEngine.Debug.Log("[Stockfish] Position initialized");

                    StockFishPortApp_5._0.Bitbases.init_kpk();
                    UnityEngine.Debug.Log("[Stockfish] Bitbases initialized");

                    StockFishPortApp_5._0.Search.init();
                    UnityEngine.Debug.Log("[Stockfish] Search initialized");

                    StockFishPortApp_5._0.Pawns.init();
                    UnityEngine.Debug.Log("[Stockfish] Pawns initialized");

                    StockFishPortApp_5._0.Eval.init();
                    UnityEngine.Debug.Log("[Stockfish] Eval initialized");

                    StockFishPortApp_5._0.Engine.Threads.init();
                    UnityEngine.Debug.Log("[Stockfish] Threads initialized");

                    StockFishPortApp_5._0.Engine.TT.resize((ulong)StockFishPortApp_5._0.Engine.Options["Hash"].getInt());
                    UnityEngine.Debug.Log("[Stockfish] TT resized");

                    _currentPosition = new StockFishPortApp_5._0.Position(StockFishPortApp_5._0.Uci.StartFEN, 0, StockFishPortApp_5._0.Engine.Threads.main());
                    UnityEngine.Debug.Log("[Stockfish] Initial position set");

                    _isInitialized = true;
                    UnityEngine.Debug.Log("[Stockfish] Initialization completed successfully");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[Stockfish] Exception during initialization: {ex.Message}\n{ex.StackTrace}");
                    _isInitialized = false;
                }
            }
            else
            {
                UnityEngine.Debug.Log("[Stockfish] Already initialized");
            }
        }

        public void ShutDown()
        {
            if (_isInitialized)
            {
                StockFishPortApp_5._0.Engine.Threads.exit();
                _isInitialized = false;
            }
        }

        public async Task SetupNewGame(Game game)
        {
            _game = game;
            // Set initial position
            _currentPosition.set(StockFishPortApp_5._0.Uci.StartFEN, 0, StockFishPortApp_5._0.Engine.Threads.main());
            StockFishPortApp_5._0.Uci.SetupStates = new StockFishPortApp_5._0.StateStackPtr();
        }



        public async Task<Movement> GetBestMove(int timeoutMS, int depth)
        {
            try
            {
                UnityEngine.Debug.Log($"[Stockfish] Starting GetBestMove with timeout={timeoutMS}ms, depth={depth}");

                // Update position to current game state
                FENSerializer fenSerializer = new FENSerializer();
                string fen = fenSerializer.Serialize(_game);
                // Remove en passant and castling to avoid Stockfish bugs
                string[] parts = fen.Split(' ');
                if (parts.Length >= 4)
                {
                    parts[2] = "-"; // castling
                    parts[3] = "-"; // en passant
                }
                fen = string.Join(" ", parts);
                UnityEngine.Debug.Log($"[Stockfish] FEN used: {fen}");

                _currentPosition.set(fen, 0, StockFishPortApp_5._0.Engine.Threads.main());
                StockFishPortApp_5._0.Uci.SetupStates = new StockFishPortApp_5._0.StateStackPtr();

                // Set limits
                StockFishPortApp_5._0.LimitsType limits = new StockFishPortApp_5._0.LimitsType();
                limits.movetime = timeoutMS;
                if (depth > 0)
                {
                    limits.depth = depth;
                }

                UnityEngine.Debug.Log("[Stockfish] Starting search...");

                // Start search
                StockFishPortApp_5._0.Search.RootMoves.Clear();
                StockFishPortApp_5._0.Engine.Threads.start_thinking(_currentPosition, limits, StockFishPortApp_5._0.Uci.SetupStates);

                // Wait for search to finish
                StockFishPortApp_5._0.Engine.Threads.wait_for_think_finished();

                UnityEngine.Debug.Log($"[Stockfish] Search finished. RootMoves count: {StockFishPortApp_5._0.Search.RootMoves.Count}");

                // Get best move from RootMoves
                if (StockFishPortApp_5._0.Search.RootMoves.Count > 0 && StockFishPortApp_5._0.Search.RootMoves[0].pv.Count > 0)
                {
                    int bestMove = StockFishPortApp_5._0.Search.RootMoves[0].pv[0];
                    UnityEngine.Debug.Log($"[Stockfish] Best move int: {bestMove}");

                    // Convert to Movement
                    Movement move = ConvertMove(bestMove);
                    if (move != null)
                    {
                        UnityEngine.Debug.Log($"[Stockfish] Converted move: {move}");

                        // Validate move
                        _game.BoardTimeline.TryGetCurrent(out Board board);
                        Piece piece = board[move.Start];
                        if (piece == null)
                        {
                            UnityEngine.Debug.LogError($"[Stockfish] No piece at {move.Start}");
                            return null;
                        }
                        _game.ConditionsTimeline.TryGetCurrent(out GameConditions cond);
                        if (piece.Owner != cond.SideToMove)
                        {
                            UnityEngine.Debug.LogError($"[Stockfish] Piece at {move.Start} is not of side to move");
                            return null;
                        }
                        if (_game.TryGetLegalMovesForPiece(piece, out ICollection<Movement> legalMoves) && legalMoves != null)
                        {
                            if (!legalMoves.Contains(move))
                            {
                                UnityEngine.Debug.LogError($"[Stockfish] Move {move} is not legal. Legal moves: {string.Join(", ", legalMoves)}");
                                return null;
                            }
                        }
                        else
                        {
                            UnityEngine.Debug.LogError($"[Stockfish] Could not get legal moves for piece at {move.Start}");
                            return null;
                        }
                        UnityEngine.Debug.Log($"[Stockfish] Move validated successfully: {move}");
                        return move;
                    }
                    else
                    {
                        UnityEngine.Debug.LogError("[Stockfish] ConvertMove returned null");
                        return null;
                    }
                }

                UnityEngine.Debug.LogError("[Stockfish] No moves found by Stockfish");
                return null; // No move found
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[Stockfish] Exception in GetBestMove: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private Movement ConvertMove(int stockfishMove)
        {
            try
            {
                // Convert Stockfish move to UnityChess Movement
                int fromSquare = StockFishPortApp_5._0.Types.from_sq(stockfishMove);
                int toSquare = StockFishPortApp_5._0.Types.to_sq(stockfishMove);
                int moveType = StockFishPortApp_5._0.Types.type_of_move(stockfishMove);

                UnityEngine.Debug.Log($"Converting move: stockfishMove={stockfishMove}, fromSquare={fromSquare}, toSquare={toSquare}, moveType={moveType}");

                // Convert to UnityChess Square
                Square startSquare = new Square(fromSquare % 8 + 1, fromSquare / 8 + 1);
                Square endSquare = new Square(toSquare % 8 + 1, toSquare / 8 + 1);

                UnityEngine.Debug.Log($"Converted squares: start={startSquare}, end={endSquare}");

                if (moveType == StockFishPortApp_5._0.MoveTypeS.PROMOTION)
                {
                    // It's a promotion move
                    int promotionType = StockFishPortApp_5._0.Types.promotion_type(stockfishMove);
                    ElectedPiece elected = promotionType switch
                    {
                        2 => ElectedPiece.Knight, // KNIGHT
                        3 => ElectedPiece.Bishop, // BISHOP
                        4 => ElectedPiece.Rook,   // ROOK
                        5 => ElectedPiece.Queen,  // QUEEN
                        _ => ElectedPiece.Queen   // Default to Queen
                    };

                    Side side = _currentPosition.side_to_move() == StockFishPortApp_5._0.ColorS.WHITE ? Side.White : Side.Black;
                    Piece promotionPiece = PromotionUtil.GeneratePromotionPiece(elected, side);

                    PromotionMove promotionMove = new PromotionMove(startSquare, endSquare);
                    promotionMove.SetPromotionPiece(promotionPiece);
                    UnityEngine.Debug.Log($"Returning promotion move: {promotionMove}");
                    return promotionMove;
                }
                else if (moveType == StockFishPortApp_5._0.MoveTypeS.CASTLING)
                {
                    // It's a castling move
                    // In Stockfish, castling move has to_sq as rook start square
                    Square kingEndSquare;
                    Square rookSquare = endSquare; // endSquare is rook start

                    if (startSquare.File == 5) // e-file
                    {
                        if (endSquare.File == 8) // h-file, kingside
                        {
                            kingEndSquare = new Square(7, startSquare.Rank); // g1 or g8
                        }
                        else if (endSquare.File == 1) // a-file, queenside
                        {
                            kingEndSquare = new Square(3, startSquare.Rank); // c1 or c8
                        }
                        else
                        {
                            UnityEngine.Debug.LogError($"Invalid castling rook square: {endSquare}");
                            return null;
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"Invalid castling king square: {startSquare}");
                        return null;
                    }

                    CastlingMove castlingMove = new CastlingMove(startSquare, kingEndSquare, rookSquare);
                    UnityEngine.Debug.Log($"Returning castling move: {castlingMove}");
                    return castlingMove;
                }
                else
                {
                    // Normal move
                    Movement normalMove = new Movement(startSquare, endSquare);
                    UnityEngine.Debug.Log($"Returning normal move: {normalMove}");
                    return normalMove;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Exception in ConvertMove: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
    }
}
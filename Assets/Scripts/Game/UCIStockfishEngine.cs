using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityChess;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityChess.Engine
{
    public class StockfishUCIEngine : IUCIEngine
    {
        private Process stockfishProcess;
        private string stockfishPath;
        private FENSerializer fenSerializer = new FENSerializer();
        private bool isReady = false;
        private TaskCompletionSource<Movement> bestMoveTcs;

        public StockfishUCIEngine()
        {
            string engineFileName = "stockfish-windows-x86-64-avx2.exe";

#if UNITY_EDITOR
            // Đường dẫn tuyệt đối an toàn trong Editor
            stockfishPath = Path.Combine(Application.dataPath, "StreamingAssets", "Stockfish", engineFileName);
#else
            // Đường dẫn tuyệt đối an toàn sau khi Build (trỏ vào thư mục data)
            stockfishPath = Path.Combine(Application.streamingAssetsPath, "Stockfish", engineFileName);
#endif

            if (!File.Exists(stockfishPath))
            {
                Debug.LogError($"[SF] Stockfish engine not found at: {stockfishPath}.");
            }
        }

        public void Start()
        {
            if (stockfishProcess != null && !stockfishProcess.HasExited) return;

            stockfishProcess = new Process
            {
                StartInfo =
                {
                    FileName = stockfishPath,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            try
            {
                stockfishProcess.Start();
                Debug.Log("[SF] Stockfish process started.");

                Task.Run(ReadOutputAsync);

                SendUciCommand("uci");
                SendUciCommand("isready");

            }
            catch (Exception e)
            {
                Debug.LogError($"[SF] Failed to start Stockfish (Path: {stockfishPath}): {e.Message}");
                stockfishProcess = null;
            }
        }

        public void ShutDown()
        {
            if (stockfishProcess != null && !stockfishProcess.HasExited)
            {
                SendUciCommand("quit");
                try
                {
                    if (!stockfishProcess.WaitForExit(1000))
                    {
                        stockfishProcess.Kill();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SF] Error shutting down Stockfish: {e.Message}");
                }
                finally
                {
                    stockfishProcess.Dispose();
                    stockfishProcess = null;
                    isReady = false;
                }
            }
        }

        public Task SetupNewGame(Game game)
        {
            string initialFen = fenSerializer.Serialize(game);
            SendUciCommand("ucinewgame");
            SendUciCommand($"position fen {initialFen}");
            return Task.CompletedTask;
        }

        public async Task<Movement> GetBestMove(int timeoutMS, int depth)
        {
            if (!isReady || stockfishProcess == null || stockfishProcess.HasExited)
            {
                Debug.LogWarning("[SF] Engine not ready or shut down.");
                return null;
            }

            int skillLevel = (depth / 2) * 8 + (depth % 2) * 2;
            skillLevel = Math.Clamp(skillLevel, 0, 20);

            SendUciCommand($"setoption name Skill Level value {skillLevel}");

            string currentFen = GameManager.Instance.SerializeGame();
            SendUciCommand($"position fen {currentFen}");

            int goTime = Mathf.Clamp(timeoutMS, 500, 5000);

            bestMoveTcs = new TaskCompletionSource<Movement>();
            SendUciCommand($"go movetime {goTime}");

            try
            {
                Movement bestMove = await bestMoveTcs.Task.TimeoutAfter(goTime + 500);
                return bestMove;
            }
            catch (TimeoutException)
            {
                SendUciCommand("stop");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SF] Error getting best move: {ex.Message}");
                return null;
            }
        }

        private void SendUciCommand(string command)
        {
            if (stockfishProcess != null && !stockfishProcess.HasExited)
            {
                try
                {
                    stockfishProcess.StandardInput.WriteLine(command);
                    stockfishProcess.StandardInput.Flush();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SF] Failed to send command '{command}': {e.Message}");
                }
            }
        }

        private async Task ReadOutputAsync()
        {
            if (stockfishProcess == null) return;

            while (stockfishProcess.HasExited == false)
            {
                try
                {
                    string line = await stockfishProcess.StandardOutput.ReadLineAsync();
                    if (line == null) continue;

                    if (line.Contains("readyok"))
                    {
                        isReady = true;
                        continue;
                    }

                    if (line.StartsWith("bestmove"))
                    {
                        string[] parts = line.Split(' ');
                        string uciMoveString = parts.Length > 1 ? parts[1] : null;

                        if (!string.IsNullOrEmpty(uciMoveString))
                        {
                            Movement bestMove = ConvertUciToMovement(uciMoveString);

                            if (bestMoveTcs != null && !bestMoveTcs.Task.IsCompleted)
                            {
                                bestMoveTcs.TrySetResult(bestMove);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SF] Error reading output: {e.Message}");
                    break;
                }
            }
        }

        private Movement ConvertUciToMovement(string uciMoveString)
        {
            if (uciMoveString.Length < 4) return null;

            string startStr = uciMoveString.Substring(0, 2);
            string endStr = uciMoveString.Substring(2, 2);
            string promotionStr = uciMoveString.Length > 4 ? uciMoveString.Substring(4, 1) : null;

            Square startSquare = new Square(startStr);
            Square endSquare = new Square(endStr);

            if (GameManager.Instance == null || GameManager.Instance.Game == null) return null;

            if (GameManager.Instance.Game.TryGetLegalMove(startSquare, endSquare, out Movement move))
            {
                if (move is PromotionMove promotionMove && !string.IsNullOrEmpty(promotionStr))
                {
                    Side side = GameManager.Instance.SideToMove;

                    Piece promotionPiece = promotionStr.ToLower() switch
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

            Debug.LogError($"[SF] Failed to convert UCI '{uciMoveString}' to Legal Move in game state.");
            return null;
        }
    }
}
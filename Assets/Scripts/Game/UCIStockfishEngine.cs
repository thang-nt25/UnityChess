using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityChess;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityChess.Engine
{
    public class StockfishEngine : IUCIEngine
    {
        private Process stockfishProcess;
        private StreamWriter inputWriter;
        private StreamReader outputReader;
        private bool isReady = false;
        private Queue<string> outputQueue = new Queue<string>();
        private object outputLock = new object();

        public StockfishEngine()
        {
            InitializeStockfish();
        }

        private void InitializeStockfish()
        {
            try
            {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
                // Use executable on Windows/Editor
                string stockfishPath = Path.Combine(Application.streamingAssetsPath, "Stockfish", "stockfish-windows-x86-64-avx2.exe");
                Debug.Log("[Stockfish] StreamingAssets path: " + Application.streamingAssetsPath);
                Debug.Log("[Stockfish] Stockfish path: " + stockfishPath);
                Debug.Log("[Stockfish] File exists: " + File.Exists(stockfishPath));

                // If file doesn't exist in StreamingAssets (can happen in Editor), try to find it in the source folder
                if (!File.Exists(stockfishPath))
                {
                    string sourcePath = Path.Combine(Application.dataPath, "StreamingAssets", "Stockfish", "stockfish-windows-x86-64-avx2.exe");
                    Debug.Log("[Stockfish] Trying source path: " + sourcePath);
                    if (File.Exists(sourcePath))
                    {
                        // Copy to StreamingAssets if it exists
                        Directory.CreateDirectory(Path.GetDirectoryName(stockfishPath));
                        File.Copy(sourcePath, stockfishPath, true);
                        Debug.Log("[Stockfish] Copied file to StreamingAssets");
                    }
                }

                if (!File.Exists(stockfishPath))
                {
                    Debug.LogError("[Stockfish] Stockfish executable not found at: " + stockfishPath);
                    return;
                }
                stockfishProcess = new Process();
                stockfishProcess.StartInfo.FileName = stockfishPath;
                stockfishProcess.StartInfo.UseShellExecute = false;
                stockfishProcess.StartInfo.RedirectStandardInput = true;
                stockfishProcess.StartInfo.RedirectStandardOutput = true;
                stockfishProcess.StartInfo.RedirectStandardError = true;
                stockfishProcess.StartInfo.CreateNoWindow = true;

                stockfishProcess.Start();
                inputWriter = stockfishProcess.StandardInput;
                outputReader = stockfishProcess.StandardOutput;
                
                // Start async output reader
                System.Threading.Tasks.Task.Run(() => ReadOutputAsync());
                
                // Enable async error reader
                stockfishProcess.ErrorDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Debug.Log("[Stockfish Error] " + e.Data);
                };
                stockfishProcess.BeginErrorReadLine();
                
                Debug.Log("[Stockfish] Process started successfully");
#elif UNITY_ANDROID
                // Use Android JNI bridge
                StockfishAndroidBridge.Initialize();
                isReady = true;
                Debug.Log("[Stockfish Android] Engine initialized successfully");
#endif

                // Initialize UCI (only for desktop)
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
                SendCommand("uci");
                string response = ReadResponse("uciok");
                Debug.Log("[Stockfish] UCI Response: " + response);

                if (string.IsNullOrEmpty(response) || !response.Contains("uciok"))
                {
                    Debug.LogError("[Stockfish] Failed to get UCI response");
                    return;
                }

                // Set options if needed
                SendCommand("setoption name Threads value 1");
                SendCommand("setoption name Hash value 16");

                // Ready
                SendCommand("isready");
                response = ReadResponse("readyok");
                if (response.Contains("readyok"))
                {
                    isReady = true;
                    Debug.Log("[Stockfish] Engine initialized successfully");
                }
                else
                {
                    Debug.LogError("[Stockfish] Engine failed to initialize - no readyok response");
                }
#endif
            }
            catch (Exception e)
            {
                Debug.LogError("[Stockfish] Failed to initialize: " + e.Message);
                Debug.LogError("[Stockfish] Stack trace: " + e.StackTrace);
                // Don't set isReady to true if initialization actually failed
                isReady = false;
            }
        }

        private void ReadOutputAsync()
        {
            try
            {
                while (outputReader != null && !outputReader.EndOfStream)
                {
                    string line = outputReader.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        lock (outputLock)
                        {
                            outputQueue.Enqueue(line);
                        }
                        Debug.Log("[Stockfish] Received: " + line);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Stockfish] Output reader error: " + e.Message);
            }
        }

        private void SendCommand(string command)
        {
            if (inputWriter != null && stockfishProcess != null && !stockfishProcess.HasExited)
            {
                try
                {
                    Debug.Log("[Stockfish] Sending: " + command);
                    inputWriter.WriteLine(command);
                    inputWriter.Flush();
                }
                catch (Exception e)
                {
                    Debug.LogError("[Stockfish] Error sending command '" + command + "': " + e.Message);
                }
            }
            else
            {
                Debug.LogWarning("[Stockfish] Cannot send command - writer or process not available");
            }
        }

        private string ReadResponse(string expectedKeyword = null)
        {
            string response = "";

            try
            {
                // Wait for response with timeout
                int timeoutMs = 5000; // 5 seconds
                int elapsed = 0;
                int sleepInterval = 50;
                bool foundKeyword = false;

                while (elapsed < timeoutMs && !foundKeyword)
                {
                    lock (outputLock)
                    {
                        while (outputQueue.Count > 0)
                        {
                            string line = outputQueue.Dequeue();
                            response += line + "\n";

                            // Check if we got the expected response
                            if (expectedKeyword != null && line.Contains(expectedKeyword))
                            {
                                foundKeyword = true;
                                break;
                            }
                            // Also break on standard keywords if no specific keyword provided
                            if (expectedKeyword == null &&
                                (line.Contains("uciok") || line.Contains("readyok") || line.Contains("bestmove")))
                            {
                                foundKeyword = true;
                                break;
                            }
                        }
                    }

                    if (!foundKeyword)
                    {
                        System.Threading.Thread.Sleep(sleepInterval);
                        elapsed += sleepInterval;
                    }
                }

                if (!foundKeyword && elapsed >= timeoutMs)
                {
                    Debug.LogWarning("[Stockfish] Response timeout after " + timeoutMs + "ms. Expected: " + expectedKeyword);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Stockfish] Error reading response: " + e.Message);
            }

            return response;
        }

        public void Start()
        {
            Debug.Log("[Stockfish] Engine started.");
        }

        public void ShutDown()
        {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            if (stockfishProcess != null)
            {
                try
                {
                    if (!stockfishProcess.HasExited)
                    {
                        SendCommand("quit");
                        stockfishProcess.WaitForExit(1000);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Stockfish] Error during shutdown: " + e.Message);
                }
                finally
                {
                    if (outputReader != null)
                    {
                        outputReader.Close();
                        outputReader = null;
                    }
                    if (inputWriter != null)
                    {
                        inputWriter.Close();
                        inputWriter = null;
                    }
                    stockfishProcess.Close();
                    stockfishProcess = null;
                }
            }
#elif UNITY_ANDROID
            StockfishAndroidBridge.Shutdown();
#endif
            isReady = false;
            Debug.Log("[Stockfish] Engine shut down.");
        }

        public Task SetupNewGame(Game game)
        {
            // Send new game command
            SendCommand("ucinewgame");
            return Task.CompletedTask;
        }

        public async Task<Movement> GetBestMove(int timeoutMS, int depth)
        {
#if UNITY_ANDROID
            // Use Android JNI bridge
            string currentFen = GameManager.Instance.SerializeGame();
            string bestMoveStr = StockfishAndroidBridge.Instance.GetBestMove(currentFen, depth, timeoutMS);
            if (!string.IsNullOrEmpty(bestMoveStr))
            {
                Movement movement = ParseUciMoveToMovement(bestMoveStr, currentFen);
                return movement;
            }
            return null;
#else
            if (!isReady || stockfishProcess == null)
            {
                Debug.LogWarning("[Stockfish] Engine not ready.");
                return null;
            }

            try
            {
                if (stockfishProcess.HasExited)
                {
                    Debug.LogWarning("[Stockfish] Stockfish process has exited.");
                    return null;
                }
                string currentFen = GameManager.Instance.SerializeGame();

                // Send position
                SendCommand("position fen " + currentFen);

                // Send go command with time and depth limits
                string goCommand = "go";
                if (depth > 0)
                    goCommand += " depth " + depth;
                if (timeoutMS > 0)
                    goCommand += " movetime " + timeoutMS;

                SendCommand(goCommand);

                // Read response with expected keyword
                string response = ReadResponse("bestmove");
                Debug.Log("[Stockfish] Go response: " + response);

                // Parse best move
                string bestMoveStr = ParseBestMove(response);
                if (!string.IsNullOrEmpty(bestMoveStr))
                {
                    Movement movement = ParseUciMoveToMovement(bestMoveStr, currentFen);
                    return movement;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Stockfish] Error getting best move: " + e.Message);
            }

            return null;
#endif
        }

        private string ParseBestMove(string response)
        {
            string[] lines = response.Split('\n');
            foreach (string line in lines)
            {
                if (line.Trim().StartsWith("bestmove"))
                {
                    string[] parts = line.Trim().Split(' ');
                    if (parts.Length >= 2)
                    {
                        return parts[1];
                    }
                }
            }
            return null;
        }

        private Movement ParseUciMoveToMovement(string uciMove, string fen)
        {
            // UCI move format: e2e4, e7e8q, etc.
            if (uciMove.Length < 4)
            {
                Debug.LogError("[Stockfish] Invalid UCI move length: " + uciMove);
                return null;
            }

            try
            {
                // UCI uses 0-indexed notation (a=0, 1=0) but Square uses 1-indexed (a=1, 1=1)
                int startFile = (uciMove[0] - 'a') + 1;  // a=1, b=2, ..., h=8
                int startRank = (uciMove[1] - '1') + 1;  // 1=1, 2=2, ..., 8=8
                int endFile = (uciMove[2] - 'a') + 1;
                int endRank = (uciMove[3] - '1') + 1;

                Debug.Log($"[Stockfish] Parsing UCI move '{uciMove}': File({startFile},{startRank}) -> File({endFile},{endRank})");

                Square startSquare = new Square(startFile, startRank);
                Square endSquare = new Square(endFile, endRank);

                // Promotion will be handled by GameManager based on board state
                return new Movement(startSquare, endSquare);
            }
            catch (Exception e)
            {
                Debug.LogError("[Stockfish] Error parsing UCI move '" + uciMove + "': " + e.Message);
                return null;
            }
        }
    }
}
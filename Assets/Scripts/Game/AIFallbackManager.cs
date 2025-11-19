using System;
using System.Threading.Tasks;
using UnityChess.Engine;
using UnityEngine;

namespace UnityChess.Engine
{
    /// <summary>
    /// Manages AI engine with automatic fallback from Stockfish to Mock AI when errors occur
    /// </summary>
    public class AIFallbackManager : IUCIEngine
    {
        private IUCIEngine currentEngine;
        private bool useStockfish = true;
        private int consecutiveStockfishErrors = 0;
        private const int MAX_CONSECUTIVE_ERRORS = 2;
        private bool isInitialized = false;

        public enum AIEngineType
        {
            Stockfish,
            Mock
        }

        public AIEngineType CurrentEngineType => useStockfish ? AIEngineType.Stockfish : AIEngineType.Mock;

        public void Start()
        {
            if (!isInitialized)
            {
                TrySwitchToStockfish();
                isInitialized = true;
            }
        }

        public void ShutDown()
        {
            if (currentEngine != null)
            {
                currentEngine.ShutDown();
                currentEngine = null;
            }
            isInitialized = false;
        }

        public async Task SetupNewGame(Game game)
        {
            if (currentEngine != null)
            {
                await currentEngine.SetupNewGame(game);
            }
        }

        public async Task<Movement> GetBestMove(int timeoutMS, int depth)
        {
            if (currentEngine == null)
            {
                Debug.LogError("[AIFallback] No AI engine available!");
                return null;
            }

            try
            {
                Movement move = await currentEngine.GetBestMove(timeoutMS, depth);

                if (move != null)
                {
                    // Move successful - reset error counter if using Stockfish
                    if (useStockfish)
                    {
                        consecutiveStockfishErrors = 0;
                        Debug.Log("[AIFallback] Stockfish move successful, resetting error counter");
                    }
                    return move;
                }
                else
                {
                    // No move found - this is an error
                    throw new Exception("AI returned null move");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIFallback] AI engine error ({CurrentEngineType}): {ex.Message}");

                if (useStockfish)
                {
                    consecutiveStockfishErrors++;
                    Debug.Log($"[AIFallback] Stockfish error count: {consecutiveStockfishErrors}/{MAX_CONSECUTIVE_ERRORS}");

                    if (consecutiveStockfishErrors >= MAX_CONSECUTIVE_ERRORS)
                    {
                        Debug.Log("[AIFallback] Switching to Mock AI due to repeated Stockfish errors");
                        SwitchToMockAI();
                        consecutiveStockfishErrors = 0;
                    }
                    else
                    {
                        // Try switching back to Stockfish for next move
                        Debug.Log("[AIFallback] Attempting to switch back to Stockfish");
                        TrySwitchToStockfish();
                    }
                }
                else
                {
                    // Already using Mock AI, but it failed - this shouldn't happen normally
                    Debug.LogWarning("[AIFallback] Mock AI also failed, this is unexpected");
                }

                // Try again with current engine
                try
                {
                    Movement retryMove = await currentEngine.GetBestMove(timeoutMS, depth);
                    if (retryMove != null)
                    {
                        Debug.Log($"[AIFallback] Retry successful with {CurrentEngineType}");
                        return retryMove;
                    }
                }
                catch (Exception retryEx)
                {
                    Debug.LogError($"[AIFallback] Retry also failed: {retryEx.Message}");
                }

                return null;
            }
        }

        private void TrySwitchToStockfish()
        {
            try
            {
                if (currentEngine != null)
                {
                    currentEngine.ShutDown();
                }

                currentEngine = new StockfishUCIEngine();
                currentEngine.Start();
                useStockfish = true;
                Debug.Log("[AIFallback] Successfully switched to Stockfish AI");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIFallback] Failed to initialize Stockfish: {ex.Message}");
                SwitchToMockAI();
            }
        }

        private void SwitchToMockAI()
        {
            try
            {
                if (currentEngine != null)
                {
                    currentEngine.ShutDown();
                }

                currentEngine = new MockUCIEngine();
                currentEngine.Start();
                useStockfish = false;
                Debug.Log("[AIFallback] Switched to Mock AI (fallback)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIFallback] Failed to initialize Mock AI: {ex.Message}");
                currentEngine = null;
            }
        }

        /// <summary>
        /// Force switch to a specific AI engine type
        /// </summary>
        public void ForceSwitchTo(AIEngineType engineType)
        {
            if (engineType == AIEngineType.Stockfish)
            {
                TrySwitchToStockfish();
            }
            else
            {
                SwitchToMockAI();
            }
        }

        /// <summary>
        /// Get current engine status for debugging
        /// </summary>
        public string GetStatus()
        {
            return $"Current: {CurrentEngineType}, Stockfish Errors: {consecutiveStockfishErrors}";
        }
    }
}
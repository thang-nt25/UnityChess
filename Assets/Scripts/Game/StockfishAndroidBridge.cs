using UnityEngine;

namespace UnityChess.Engine
{
    public class StockfishAndroidBridge
    {
        private AndroidJavaObject stockfishEngine;
        private static StockfishAndroidBridge instance;
        private bool isDisposed = false;

        public static StockfishAndroidBridge Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new StockfishAndroidBridge();
                }
                return instance;
            }
        }

        private StockfishAndroidBridge()
        {
            try
            {
                if (Application.platform == RuntimePlatform.Android)
                {
                    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    {
                        using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                        {
                            stockfishEngine = new AndroidJavaObject("com.unitychess.StockfishEngine");
                            if (stockfishEngine == null)
                            {
                                Debug.LogError("[StockfishBridge] Failed to create StockfishEngine object");
                                return;
                            }
                            var instance = stockfishEngine.CallStatic<AndroidJavaObject>("getInstance", activity);
                            if (instance == null)
                            {
                                Debug.LogError("[StockfishBridge] Failed to get StockfishEngine instance");
                                return;
                            }
                            stockfishEngine = instance;
                            Debug.Log("[StockfishBridge] Successfully initialized");
                        }
                    }
                }
                else
                {
                    Debug.Log("[StockfishBridge] Not running on Android, bridge not initialized");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StockfishBridge] Error initializing: {e.Message}\\n{e.StackTrace}");
            }
        }

        public string InitEngine()
        {
            try
            {
                if (stockfishEngine == null)
                {
                    Debug.LogError("[StockfishBridge] Engine not initialized");
                    return "Engine not initialized";
                }

                string result = stockfishEngine.Call<string>("initEngine");
                Debug.Log($"[StockfishBridge] Engine initialized: {result}");
                return result;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StockfishBridge] Error in InitEngine: {e.Message}\\n{e.StackTrace}");
                return $"Error: {e.Message}";
            }
        }

        public string GetBestMove(string fen, int depth, int timeMs)
        {
            try
            {
                if (stockfishEngine == null)
                {
                    Debug.LogError("[StockfishBridge] Engine not initialized");
                    return string.Empty;
                }

                if (string.IsNullOrEmpty(fen))
                {
                    Debug.LogError("[StockfishBridge] Invalid FEN string");
                    return string.Empty;
                }

                string result = stockfishEngine.Call<string>("getBestMove", fen, depth, timeMs);
                if (string.IsNullOrEmpty(result))
                {
                    Debug.LogWarning("[StockfishBridge] Engine returned empty move");
                }
                else
                {
                    Debug.Log($"[StockfishBridge] Got move: {result}");
                }
                return result;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StockfishBridge] Error in GetBestMove: {e.Message}\\n{e.StackTrace}");
                return string.Empty;
            }
        }

        public void CloseEngine()
        {
            try
            {
                if (stockfishEngine != null)
                {
                    stockfishEngine.Call("closeEngine");
                    Debug.Log("[StockfishBridge] Engine closed");
                }
                else
                {
                    Debug.LogWarning("[StockfishBridge] Engine already closed or not initialized");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StockfishBridge] Error in CloseEngine: {e.Message}\\n{e.StackTrace}");
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                try
                {
                    if (stockfishEngine != null)
                    {
                        stockfishEngine.Dispose();
                        stockfishEngine = null;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[StockfishBridge] Error disposing: {e.Message}");
                }
                finally
                {
                    isDisposed = true;
                }
            }
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Threading.Tasks;


public class MultiplayerSystem : MonoBehaviourSingleton<MultiplayerSystem>
{
    private Socket serverSocket;
    private bool isConnected = false;
    private readonly int localPort = 23001;
    private readonly int remotePort = 23000;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (serverSocket != null || isConnected)
        {
            Debug.Log("[MultiplayerSystem] Already initialized, skipping InitSocket.");
            return;
        }

        InitSocket();
    }

    private void InitSocket()
    {
        try
        {
            // Đóng socket cũ nếu còn
            if (serverSocket != null)
            {
                try { serverSocket.Shutdown(SocketShutdown.Both); } catch { }
                serverSocket.Close();
                serverSocket = null;
            }

            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Cho phép reuse address (giúp giảm khả năng lỗi bind do TIME_WAIT)
            serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            try
            {
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, localPort));
            }
            catch (SocketException bindEx)
            {
                Debug.LogWarning($"[MultiplayerSystem] Bind failed: {bindEx.Message}. Will not attempt connect.");
                // Nếu Bind thất bại, đóng socket và thoát im lặng (không throw)
                try { serverSocket.Close(); } catch { }
                serverSocket = null;
                return;
            }

            // Kết nối bất đồng bộ (nếu bạn cần connect)
            Task.Run(async () =>
            {
                try
                {
                    await serverSocket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, remotePort));
                    isConnected = true;
                    Debug.Log("[MultiplayerSystem] Connected successfully.");
                }
                catch (SocketException ex)
                {
                    Debug.LogWarning($"[MultiplayerSystem] ConnectAsync failed: {ex.Message}");
                    // Không throw tiếp, server vẫn đã bind (nếu bind ok); xử lý kết nối sau nếu cần.
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[MultiplayerSystem] Socket init error: {e.Message}");
            try { serverSocket?.Close(); } catch { }
            serverSocket = null;
        }
    }

    private void OnDestroy()
    {
        CloseSocket();
    }

    private void OnApplicationQuit()
    {
        CloseSocket();
    }

    private void CloseSocket()
    {
        if (serverSocket != null)
        {
            try
            {
                if (serverSocket.Connected)
                {
                    try { serverSocket.Shutdown(SocketShutdown.Both); } catch { }
                }
                serverSocket.Close();
                Debug.Log("[MultiplayerSystem] Socket closed.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MultiplayerSystem] Socket close error: {e.Message}");
            }
            finally
            {
                serverSocket = null;
                isConnected = false;
            }
        }
    }
}
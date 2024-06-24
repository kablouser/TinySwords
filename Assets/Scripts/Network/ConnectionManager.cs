using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using static Unity.Netcode.Transports.UTP.UnityTransport;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    public event EventHandler OnFailedToFindConnection;
    public event EventHandler OnDisconnected;
    public event EventHandler OnConnecting;
    public event EventHandler OnConnected;

    const int k_MaxConnectPayload = 1024;
    private string password;

    private bool HasConnected = false;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
    }

    public void StartHost(string password, int port)
    {
        this.password = password;
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.ConnectionData = new ConnectionAddressData { Port = (ushort)port, ServerListenAddress = string.Empty };
        NetworkManager.Singleton.StartHost();
        SceneLoader.NetworkLoadScene(SceneLoader.Scene.Lobby);
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        byte[] connectionData = request.Payload;
        if (connectionData.Length > k_MaxConnectPayload)
        {
            response.Approved = false;
            return;
        }

        response.CreatePlayerObject = false;
        response.Approved = System.Text.Encoding.ASCII.GetString(connectionData) == password;
    }

    public void StartClient(string ipAddress, int port, string password)
    {
        OnConnecting?.Invoke(this, EventArgs.Empty);
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.ConnectionData = new ConnectionAddressData { Address = ipAddress, Port = (ushort)port, ServerListenAddress = string.Empty };
        NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.ASCII.GetBytes(password);
        NetworkManager.Singleton.StartClient();
    }

    private void OnClientDisconnectCallback(ulong obj)
    {
        if(!NetworkManager.Singleton.IsServer)
        {
            if(NetworkManager.Singleton.DisconnectReason != string.Empty)
            {
                print($"Approval Declined Reason: {NetworkManager.Singleton.DisconnectReason}");
            }

            if(!HasConnected)
            {
                OnFailedToFindConnection?.Invoke(this, EventArgs.Empty);
                print("Failed To Find Server");
            }
            else
            {
                OnDisconnected?.Invoke(this, EventArgs.Empty);
                print("Disconnected");
            }

            HasConnected = false;
        }
    }

    private void OnClientConnectedCallback(ulong obj)
    {
        if(!NetworkManager.Singleton.IsServer)
        {
            OnConnected?.Invoke(this, EventArgs.Empty);
            HasConnected = true;
        }
    }

    public void DisconnectSelf()
    {
        NetworkManager.Singleton.Shutdown();
        SceneLoader.LoadScene(SceneLoader.Scene.MainMenu);
    }
}

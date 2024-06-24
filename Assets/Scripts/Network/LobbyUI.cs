using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : NetworkBehaviour
{
    public LobbyPlayerRow rowPrefab;
    public Button readyButton;
    public Button disconnectButton;

    private Dictionary<ulong, LobbyPlayerRow> lobbyPlayerRowDictionary = new Dictionary<ulong, LobbyPlayerRow>();
    private List<ulong> playersReady = new List<ulong>();

    private void Start()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;

        readyButton.onClick.AddListener(() =>
        {
            PlayerReadyServerRpc();
        });

        disconnectButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.DisconnectSelf();
        });

        if (IsHost)
        {
            InstatiateNewRow(OwnerClientId, false);
            print("Client Id" + OwnerClientId);
        }
    }

    private void OnClientConnectedCallback(ulong clientId)
    {
        if(IsServer)
        {
            InstatiateNewRow(clientId, false);
            print("Client Connect, Id: " + clientId);

            StartLobbyUIClientRpc(playersReady.ToArray(), RpcTarget.Single(clientId, RpcTargetUse.Temp));
            AddPlayerRowClientRpc(clientId);
        }
    }

    private void OnClientDisconnectCallback(ulong obj)
    {
        if(IsServer)
        {
            playersReady.Remove(obj);

            if(lobbyPlayerRowDictionary.ContainsKey(obj))
            {
                Destroy(lobbyPlayerRowDictionary[obj].gameObject);
                lobbyPlayerRowDictionary.Remove(obj);
                RemovePlayerRowClientRpc(obj);
                print("Client Disconnect, Id: " + obj);
            }
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void StartLobbyUIClientRpc(ulong[] playerReady, RpcParams rpcParams)
    {
        print("Reciving Lobby Start UI");
        foreach(ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            InstatiateNewRow(clientId, playerReady.Contains(clientId));
        }
    }

    [Rpc(SendTo.NotServer)]
    private void UpdatePlayerReadyClientRpc(ulong clientId)
    {
        if(lobbyPlayerRowDictionary.ContainsKey(clientId))
        {
            lobbyPlayerRowDictionary[clientId].readyToggle.SetIsOnWithoutNotify(true);
        }
    }

    [Rpc(SendTo.NotServer)]
    private void RemovePlayerRowClientRpc(ulong clientId)
    {
        if(lobbyPlayerRowDictionary.ContainsKey(clientId))
        {
            Destroy(lobbyPlayerRowDictionary[clientId].gameObject);
            lobbyPlayerRowDictionary.Remove(clientId);
        }
    }

    [Rpc(SendTo.NotServer)]
    private void AddPlayerRowClientRpc(ulong clientId)
    {
        if(!lobbyPlayerRowDictionary.ContainsKey(clientId))
        {
            InstatiateNewRow(clientId, false);
        }
    }

    [Rpc(SendTo.Server)]
    private void PlayerReadyServerRpc(RpcParams rpcParams = default)
    {
        playersReady.Add(rpcParams.Receive.SenderClientId);

        if(lobbyPlayerRowDictionary.ContainsKey(rpcParams.Receive.SenderClientId))
        {
            lobbyPlayerRowDictionary[rpcParams.Receive.SenderClientId].readyToggle.SetIsOnWithoutNotify(true);
        }

        UpdatePlayerReadyClientRpc(rpcParams.Receive.SenderClientId);

        print("Client Ready, Id: " + rpcParams.Receive.SenderClientId);

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if(!playersReady.Contains(clientId))
            {
                print("All Players Not Ready");
                break;
            }
        }
    }

    private void InstatiateNewRow(ulong clientId, bool ready)
    {
        LobbyPlayerRow newRow = Instantiate(rowPrefab, transform);
        newRow.Initialise("Player " + clientId, ready);
        lobbyPlayerRowDictionary[clientId] = newRow;
    }

    override public void OnDestroy()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;

        base.OnDestroy();
    }
}

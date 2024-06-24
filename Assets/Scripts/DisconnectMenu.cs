using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisconnectMenu : MonoBehaviour
{
    public GameObject LoadingScreen;
    public GameObject DisconnectScreen;
    public Button DisconnectOKButton;
    public TMP_Text DisconnectReasonText;

    private void Start()
    {
        LoadingScreen.SetActive(false);
        DisconnectScreen.SetActive(false);
        DisconnectOKButton.onClick.AddListener(() =>
        {
            DisconnectScreen.SetActive(false);
            SceneLoader.LoadScene(SceneLoader.Scene.MainMenu);
        });
        ConnectionManager.Instance.OnFailedToFindConnection += OnFailedToFindConnection;
        ConnectionManager.Instance.OnDisconnected += OnDisconnected;
        ConnectionManager.Instance.OnConnecting += OnConnecting;
        ConnectionManager.Instance.OnConnected += OnConnected;
    }

    public void OnFailedToFindConnection(object sender, EventArgs e)
    {
        DisconnectReasonText.SetText("Failed To Find Server");
        LoadingScreen.SetActive(false);
        DisconnectScreen.SetActive(true);
    }

    public void OnDisconnected(object sender, EventArgs e)
    {
        DisconnectReasonText.SetText("Disconnected");
        LoadingScreen.SetActive(false);
        DisconnectScreen.SetActive(true);
    }

    //Probably better to have another scene for this (not sure)
    public void OnConnecting(object sender, EventArgs e)
    {
        LoadingScreen.SetActive(true);
    }

    public void OnConnected(object sender, EventArgs e)
    {
        LoadingScreen.SetActive(false);
    }

    private void OnDestroy()
    {
        ConnectionManager.Instance.OnFailedToFindConnection -= OnFailedToFindConnection;
        ConnectionManager.Instance.OnDisconnected -= OnDisconnected;
        ConnectionManager.Instance.OnConnecting -= OnConnecting;
        ConnectionManager.Instance.OnConnected -= OnConnected;
    }
}

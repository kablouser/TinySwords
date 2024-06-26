using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public Button hostGameButton;
    public Button joinGameButton;
    public TMP_InputField ipAdressInput;
    public TMP_InputField portInput;
    public TMP_InputField passwordInput;

    private string ipAddress = "127.0.0.1";
    private string port = "7777";
    private string password = "Password";

    private void Awake()
    {
        ipAdressInput.SetTextWithoutNotify(ipAddress);
        portInput.SetTextWithoutNotify(port);
        passwordInput.SetTextWithoutNotify(password);

        hostGameButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.StartHost(Convert.ToInt32(port), password);
        });

        joinGameButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.StartClient(ipAddress, Convert.ToInt32(port), password);
        });

        ipAdressInput.onValueChanged.AddListener((string Input) =>
        {
            this.ipAddress = Input;
        });

        portInput.onValueChanged.AddListener((string Input) =>
        {
            this.port = Input;
        });

        passwordInput.onValueChanged.AddListener((string Input) =>
        {
            this.password = Input;
        });
    }

    private void OnDestroy()
    {
        hostGameButton.onClick.RemoveAllListeners();
        joinGameButton.onClick.RemoveAllListeners();
        ipAdressInput.onValueChanged.RemoveAllListeners();
        portInput.onValueChanged.RemoveAllListeners();
        passwordInput.onValueChanged.RemoveAllListeners();
    }
}

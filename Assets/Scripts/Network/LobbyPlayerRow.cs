using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerRow : MonoBehaviour
{
    public TMP_Text playerName;
    public Toggle readyToggle;

    public void Initialise(string name, bool ready)
    {
        playerName.SetText(name);
        readyToggle.isOn = ready;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boot : MonoBehaviour
{
    void Start()
    {
        SceneLoader.LoadScene(SceneLoader.Scene.MainMenu);
    }
}

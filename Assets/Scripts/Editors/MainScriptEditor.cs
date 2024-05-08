#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MainScript))]
public class MainScriptEditor : Editor
{
    // Draw lines between a chosen GameObject
    // and a selection of added GameObjects

    void OnSceneGUI()
    {
        // Get the chosen GameObject
        MainScript mainScript = target as MainScript;

        if (mainScript == null)
            return;

        mainScript.navigationGrid.OnSceneGUI();


        Vector2 halfElementSize = mainScript.navigationGrid.GetElementSize() / 2.0f;
        Vector2 boundsSize = mainScript.navigationGrid.bounds.size;

/*        if (mainScript.pathfindingScores != null)
        {
            foreach (var kvp in mainScript.pathfindingScores)
            {
                Handles.Label(mainScript.overlapGrid.GetElementWorldPosition(boundsSize, halfElementSize, kvp.index),
                    $"{kvp.score}");
            }
        }*/
    }
}
#endif
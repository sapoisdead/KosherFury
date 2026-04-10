using UnityEngine;
using UnityEditor;

public class CreateCryptStructure
{
    public static void Execute()
    {
        // Wall con 4 figli
        GameObject wall = new GameObject("Wall");
        new GameObject("Front_wall").transform.SetParent(wall.transform);
        new GameObject("Back_wall").transform.SetParent(wall.transform);
        new GameObject("Left_wall").transform.SetParent(wall.transform);
        new GameObject("Right_wall").transform.SetParent(wall.transform);

        // Floor e Stairs
        new GameObject("Floor");
        new GameObject("Stairs");

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("Struttura Crypt creata.");
    }
}

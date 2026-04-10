using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class ApplyWallMaterial
{
    public static void Execute()
    {
        GameObject wall = GameObject.Find("Wall");
        if (wall == null) { Debug.LogError("Wall not found in scene."); return; }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WallMaterial.mat");
        if (mat == null) { Debug.LogError("WallMaterial.mat not found."); return; }

        MeshRenderer[] renderers = wall.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers)
            r.sharedMaterial = mat;

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"WallMaterial applicato a {renderers.Length} oggetti dentro Wall.");
    }
}

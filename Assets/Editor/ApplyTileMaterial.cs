using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class ApplyTileMaterial
{
    public static void Execute()
    {
        GameObject floor = GameObject.Find("Floor");
        if (floor == null) { Debug.LogError("Floor not found in scene."); return; }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileMaterial.mat");
        if (mat == null) { Debug.LogError("TileMaterial.mat not found."); return; }

        MeshRenderer[] renderers = floor.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers)
            r.sharedMaterial = mat;

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"TileMaterial applicato a {renderers.Length} oggetti dentro Floor.");
    }
}

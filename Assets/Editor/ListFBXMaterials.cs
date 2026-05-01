using UnityEngine;
using UnityEditor;

public class ListFBXMaterials
{
    public static void Execute()
    {
        string path = "Assets/Art/Environment/Models/Crypt/Env_Catacombs.fbx";
        AssetImporter importer = AssetImporter.GetAtPath(path);
        ModelImporter mi = importer as ModelImporter;
        if (mi == null) { Debug.LogError("Not a ModelImporter"); return; }

        var map = mi.GetExternalObjectMap();
        Debug.Log($"Current externalObjects count: {map.Count}");

        // List all sub-assets to find embedded material names
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var a in assets)
        {
            if (a is Material mat)
                Debug.Log($"Embedded material: '{mat.name}'");
        }
    }
}

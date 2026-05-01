using UnityEngine;
using UnityEditor;

public class FixCatacombsRemap
{
    public static void Execute()
    {
        string fbxPath = "Assets/Art/Environment/Models/Crypt/Env_Catacombs.fbx";
        string matPath = "Assets/Art/Environment/Textures/Crypt/DefaultMaterial.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { Debug.LogError("Material not found"); return; }

        ModelImporter mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (mi == null) { Debug.LogError("ModelImporter not found"); return; }

        // Remove old wrong remap
        mi.RemoveRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "DefaultMaterial"));

        // Add correct remap with actual FBX material name
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "No Name"), mat);
        mi.SaveAndReimport();

        Debug.Log("Remap fixed: 'No Name' -> DefaultMaterial.mat");
    }
}

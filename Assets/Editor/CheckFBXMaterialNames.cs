using UnityEngine;
using UnityEditor;

public class CheckFBXMaterialNames
{
    public static void Execute()
    {
        string fbxPath = "Assets/Art/Environment/Models/Crypt/Env_Catacombs.fbx";
        ModelImporter mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;

        // Temporarily switch to internal to expose embedded material names
        var prev = mi.materialImportMode;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        mi.SaveAndReimport();

        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (a is Material m) Debug.Log($"FBX material name: '{m.name}'");
        }

        // Restore
        mi.materialLocation = ModelImporterMaterialLocation.External;
        mi.SaveAndReimport();
    }
}

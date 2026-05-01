using UnityEngine;
using UnityEditor;

public class SetupCatacombsMaterial
{
    public static void Execute()
    {
        string matFolder = "Assets/Art/Environment/Textures/Crypt/";
        string matOut    = "Assets/Art/Environment/Textures/Crypt/DefaultMaterial.mat";
        string fbxPath   = "Assets/Art/Environment/Models/Crypt/Env_Catacombs.fbx";

        // Load textures
        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(matFolder + "DefaultMaterial_Base_color.png");
        Texture2D normal    = AssetDatabase.LoadAssetAtPath<Texture2D>(matFolder + "DefaultMaterial_Normal.png");
        Texture2D metallic  = AssetDatabase.LoadAssetAtPath<Texture2D>(matFolder + "DefaultMaterial_Metallic.png");
        Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(matFolder + "DefaultMaterial_Roughness.png");
        Texture2D ao        = AssetDatabase.LoadAssetAtPath<Texture2D>(matFolder + "DefaultMaterial_Mixed_AO.png");

        // Create URP Lit material
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) { Debug.LogError("URP Lit shader not found"); return; }

        Material mat = new Material(urpLit);
        mat.name = "DefaultMaterial";

        if (baseColor != null) mat.SetTexture("_BaseMap", baseColor);
        if (metallic  != null) mat.SetTexture("_MetallicGlossMap", metallic);
        if (ao        != null) mat.SetTexture("_OcclusionMap", ao);

        // Normal map - ensure texture import type is Normal
        if (normal != null)
        {
            var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }

        // URP: smoothness comes from metallic alpha; roughness = 1 - smoothness
        // Set smoothness low since we have a roughness map (user can tune in Inspector)
        mat.SetFloat("_Smoothness", 0.3f);

        AssetDatabase.CreateAsset(mat, matOut);
        AssetDatabase.SaveAssets();

        // Remap in FBX importer
        ModelImporter mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (mi == null) { Debug.LogError("ModelImporter not found"); return; }

        var sourceId = new AssetImporter.SourceAssetIdentifier(typeof(Material), "DefaultMaterial");
        mi.AddRemap(sourceId, mat);
        mi.SaveAndReimport();

        Debug.Log($"DefaultMaterial created and remapped to Env_Catacombs.fbx. baseColor={baseColor != null}, normal={normal != null}, metallic={metallic != null}, ao={ao != null}");
    }
}

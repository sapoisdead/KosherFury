using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

public class AddHeightFog
{
    public static void Execute()
    {
        // Disabilita la fog built-in
        RenderSettings.fog = false;

        // Carica il PC renderer
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        if (renderer == null)
        {
            Debug.LogError("[HeightFog] PC_Renderer.asset not found.");
            return;
        }

        // Controlla se già presente
        foreach (var f in renderer.rendererFeatures)
        {
            if (f is HeightFogFeature)
            {
                Debug.Log("[HeightFog] Already added.");
                return;
            }
        }

        // Crea e aggiunge il feature
        var feature = ScriptableObject.CreateInstance<HeightFogFeature>();
        feature.name = "HeightFog";

        AssetDatabase.AddObjectToAsset(feature, renderer);
        renderer.rendererFeatures.Add(feature);
        renderer.SetDirty();

        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();

        Debug.Log("[HeightFog] HeightFogFeature added to PC_Renderer.");
    }
}

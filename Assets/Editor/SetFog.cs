using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SetFog
{
    public static void Execute()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.45f, 0.45f, 0.45f);
        RenderSettings.fogDensity = 0.04f;

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Fog enabled: ExponentialSquared, gray, dense.");
    }
}

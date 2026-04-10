using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class AutoSave
{
    private static double lastSaveTime;
    private const double intervalMinutes = 5.0;

    static AutoSave()
    {
        EditorApplication.update += OnUpdate;
        lastSaveTime = EditorApplication.timeSinceStartup;
    }

    private static void OnUpdate()
    {
        if (EditorApplication.isPlaying) return;

        double elapsed = EditorApplication.timeSinceStartup - lastSaveTime;
        if (elapsed >= intervalMinutes * 60.0)
        {
            lastSaveTime = EditorApplication.timeSinceStartup;
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[AutoSave] Scena salvata automaticamente alle {System.DateTime.Now:HH:mm:ss}");
        }
    }
}

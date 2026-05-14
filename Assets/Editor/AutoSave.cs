using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class AutoSave
{
    private static double lastSaveTime;
    private const double intervalMinutes = 5.0;
    private const string EnabledKey = "AutoSave_Enabled";

    static AutoSave()
    {
        EditorApplication.update += OnUpdate;
        lastSaveTime = EditorApplication.timeSinceStartup;
    }

    private static bool IsEnabled => EditorPrefs.GetBool(EnabledKey, false);

    private static void OnUpdate()
    {
        if (!IsEnabled) return;
        if (EditorApplication.isPlaying) return;

        double elapsed = EditorApplication.timeSinceStartup - lastSaveTime;
        if (elapsed >= intervalMinutes * 60.0)
        {
            lastSaveTime = EditorApplication.timeSinceStartup;
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[AutoSave] Scena salvata automaticamente alle {System.DateTime.Now:HH:mm:ss}");
        }
    }

    [MenuItem("Edit/AutoSave/Attivo", false, 200)]
    private static void Toggle() => EditorPrefs.SetBool(EnabledKey, !IsEnabled);

    [MenuItem("Edit/AutoSave/Attivo", true, 200)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked("Edit/AutoSave/Attivo", IsEnabled);
        return true;
    }
}

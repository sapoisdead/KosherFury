using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

public class CreateSaveFeedbackUI
{
    public static void Execute()
    {
        // Canvas
        GameObject canvasGO = new GameObject("GameUI");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Text
        GameObject textGO = new GameObject("SaveFeedbackText");
        textGO.transform.SetParent(canvasGO.transform, false);
        Text text = textGO.AddComponent<Text>();
        text.text = "Partita salvata.";
        text.fontSize = 32;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 100);
        rt.sizeDelta = new Vector2(600, 60);

        textGO.SetActive(false);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("GameUI con SaveFeedbackText creato.");
    }
}

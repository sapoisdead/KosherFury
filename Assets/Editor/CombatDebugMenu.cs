using UnityEditor;
using UnityEngine;

// Comandi di prova del combattimento, solo in Play Mode.
public static class CombatDebugMenu
{
    private const string SimpPrefab = "Assets/Prefabs/Enemies/Simps/Valerio_Simp.prefab";

    // Un simp fermo a portata di pugno davanti al player, che attacca ogni 2 secondi.
    // Ferma le ondate e spegne gli altri nemici, cosi' la prova e' pulita.
    [MenuItem("KosherFury/Combat/Parry Trainer (Play Mode)")]
    public static void SpawnParryTrainer()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ParryTrainer] Va lanciato in Play Mode.");
            return;
        }

        var player = PlayerManager.Instance != null ? PlayerManager.Instance.PlayerTransform : null;
        if (player == null)
        {
            Debug.LogWarning("[ParryTrainer] Nessun player in scena.");
            return;
        }

        foreach (var spawner in Object.FindObjectsByType<WaveSpawner>(FindObjectsSortMode.None))
            spawner.enabled = false;
        foreach (var other in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            if (other.transform != player) other.gameObject.SetActive(false);

        // parte a 0.9 m fra i centri, la distanza a cui poi si riporta prima di ogni colpo
        Vector3 forward = player.forward; forward.y = 0f; forward.Normalize();
        Vector3 position = player.position + forward * 0.9f;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimpPrefab);
        var simp = Object.Instantiate(prefab, position, Quaternion.LookRotation(-forward));
        simp.name = "[ParryTrainer] Valerio_Simp";
        simp.AddComponent<ParryTrainer>().Setup(2f);

        Debug.Log("[ParryTrainer] Simp davanti al player: attacca ogni 2 s. Ogni colpo scrive nel log se e' stato parato, preso o a vuoto.");
    }
}

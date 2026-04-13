using UnityEngine;

/// <summary>
/// Solo per debug in editor: se mancano i manager persistenti (perché si è
/// avviata la scena direttamente senza passare dal MainMenu), li crea al volo.
/// </summary>
public class DebugBootstrap : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject cameraPrefab;

    private void Awake()
    {
        if (PersistenceManager.Instance == null)
        {
            GameObject pm = new GameObject("PersistenceManager");
            pm.AddComponent<PersistenceManager>();
        }

        if (PlayerManager.Instance == null)
        {
            GameObject pmGO = new GameObject("PlayerManager");
            PlayerManager playerManager = pmGO.AddComponent<PlayerManager>();
            playerManager.SetPrefabs(playerPrefab, cameraPrefab);
            playerManager.Spawn();
        }
    }
#endif
}

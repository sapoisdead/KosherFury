using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject cameraPrefab;

    public Transform PlayerTransform { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetPrefabs(GameObject player, GameObject camera)
    {
        playerPrefab = player;
        cameraPrefab = camera;
    }

    public void Spawn()
    {
        if (PlayerTransform != null) return; // già spawnato

        GameObject player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        DontDestroyOnLoad(player);
        PlayerTransform = player.transform;

        GameObject cam = Instantiate(cameraPrefab, Vector3.zero, Quaternion.identity);
        DontDestroyOnLoad(cam);

        CameraController cc = cam.GetComponentInChildren<CameraController>();
        if (cc != null)
            cc.SetTarget(PlayerTransform);
    }
}

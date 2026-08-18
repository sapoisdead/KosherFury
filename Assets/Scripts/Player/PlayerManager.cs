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

        // Se la scena dichiara un punto di spawn lo si usa, altrimenti si ricade
        // sull'origine come prima. L'origine va bene su un piano di prova, ma in una
        // scena costruita ci finisce dentro l'arredamento.
        PlayerSpawnPoint spawn = PlayerSpawnPoint.Find();
        Vector3 spawnPos = spawn != null ? spawn.transform.position : Vector3.zero;
        Quaternion spawnRot = spawn != null ? spawn.transform.rotation : Quaternion.identity;

        GameObject player = Instantiate(playerPrefab, spawnPos, spawnRot);
        DontDestroyOnLoad(player);
        PlayerTransform = player.transform;

        GameObject cam = Instantiate(cameraPrefab, Vector3.zero, Quaternion.identity);
        DontDestroyOnLoad(cam);

        CameraController cc = cam.GetComponentInChildren<CameraController>();
        if (cc != null)
            cc.SetTarget(PlayerTransform);
    }
}

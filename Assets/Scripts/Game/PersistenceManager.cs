using UnityEngine;
using System.IO;

public class PersistenceManager : MonoBehaviour
{
    public static PersistenceManager Instance { get; private set; }

    private GameData currentData;
    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    private void Awake()
    {
        // Se esiste già un'istanza, distruggi questo duplicato
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // sopravvive al cambio scena
    }

    public void Save(Vector3 playerPosition)
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Preserva il progresso delle scene precedenti
        if (currentData == null)
            currentData = new GameData();

        currentData.sceneName = currentScene;
        currentData.posX = playerPosition.x;
        currentData.posY = playerPosition.y;
        currentData.posZ = playerPosition.z;

        // Registra le candele raccolte nella scena corrente
        if (MenorahManager.Instance != null)
            currentData.SetCandlesCollected(currentScene, MenorahManager.Instance.CollectedCount);

        // Registra le armi raccolte
        if (PlayerManager.Instance != null)
        {
            WeaponManager wm = PlayerManager.Instance.PlayerTransform?.GetComponent<WeaponManager>();
            if (wm != null)
            {
                var (names, equipped) = wm.GetSaveData();
                currentData.collectedWeapons = names;
                currentData.equippedWeapon = equipped;
            }
        }

        // Registra lo stato dei nemici in scena (vivi e morti)
        currentData.enemyList.Clear();
        foreach (EnemyID enemy in Object.FindObjectsByType<EnemyID>(FindObjectsInactive.Include))
        {
            Health h = enemy.GetComponent<Health>();
            bool alive = enemy.gameObject.activeSelf;
            float hp = h != null && alive ? h.CurrentHealth : 0f;
            currentData.SetEnemyData(enemy.SpawnPosition, alive, hp);
        }

        string json = JsonUtility.ToJson(currentData, prettyPrint: true);
        File.WriteAllText(SavePath, json);

    }

    public GameData Load()
    {
        if (!File.Exists(SavePath))
            return null;

        string json = File.ReadAllText(SavePath);
        currentData = JsonUtility.FromJson<GameData>(json);

        // Ripristina le armi
        if (PlayerManager.Instance != null)
        {
            WeaponManager wm = PlayerManager.Instance.PlayerTransform?.GetComponent<WeaponManager>();
            wm?.LoadFromSave(currentData.collectedWeapons, currentData.equippedWeapon);
        }

        return currentData;
    }

    public bool HasSave() => File.Exists(SavePath);

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);

        currentData = null;
    }
}

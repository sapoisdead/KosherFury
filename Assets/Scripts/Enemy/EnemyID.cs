using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ripristina lo stato del nemico dal salvataggio usando la posizione di spawn come chiave.
/// </summary>
public class EnemyID : MonoBehaviour
{
    public Vector3 SpawnPosition { get; private set; }

    private void Awake()
    {
        SpawnPosition = transform.position;
    }

    private void Start()
    {
        if (PersistenceManager.Instance == null || !PersistenceManager.Instance.HasSave()) return;

        GameData data = PersistenceManager.Instance.Load();
        if (data == null) return;

        if (data.sceneName != SceneManager.GetActiveScene().name) return;

        EnemyData saved = data.GetEnemyByPosition(transform.position);
        if (saved == null) return;

        if (!saved.isAlive)
        {
            gameObject.SetActive(false);
            return;
        }

        Health health = GetComponent<Health>();
        if (health != null)
            health.SetHealth(saved.health);
    }
}

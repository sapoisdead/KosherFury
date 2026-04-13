using UnityEngine;
using UnityEngine.SceneManagement;

public class MenorahManager : MonoBehaviour
{
    public static MenorahManager Instance { get; private set; }

    [SerializeField] private int totalCandles = 7;
    [SerializeField] private GameObject candlesParent; // MenorahCandles

    private int collectedCount = 0;
    private bool hasSaved = false;

    public bool CanSave => collectedCount >= totalCandles && !hasSaved;
    public int CollectedCount => collectedCount;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Se questa scena è già stata completata nel salvataggio, disattiva le candele
        if (PersistenceManager.Instance == null || !PersistenceManager.Instance.HasSave()) return;

        GameData data = PersistenceManager.Instance.Load();
        if (data == null) return;

        string currentScene = SceneManager.GetActiveScene().name;
        int saved = data.GetCandlesCollected(currentScene);

        if (saved >= totalCandles)
        {
            collectedCount = totalCandles;
            hasSaved = true;

            if (candlesParent != null)
                candlesParent.SetActive(false);

        }
    }

    public void OnCandleCollected()
    {
        collectedCount++;
    }

    public void OnSaved()
    {
        hasSaved = true;
    }
}

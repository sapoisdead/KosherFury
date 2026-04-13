using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;

    private void Start()
    {
        // Disabilita Load Game se non esiste un salvataggio
        bool hasSave = PersistenceManager.Instance != null && PersistenceManager.Instance.HasSave();
        loadGameButton.interactable = hasSave;
    }

    public void OnNewGame()
    {
        PersistenceManager.Instance?.DeleteSave();
        PlayerManager.Instance?.Spawn();
        SceneManager.LoadScene("Crypt");
    }

    public void OnLoadGame()
    {
        if (PersistenceManager.Instance == null) return;

        GameData data = PersistenceManager.Instance.Load();
        if (data == null) return;

        PlayerManager.Instance?.Spawn();
        SceneManager.LoadScene(data.sceneName);
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;

    private void Start()
    {
        if (PlayerManager.Instance == null) return;

        Transform playerTransform = PlayerManager.Instance.PlayerTransform;
        CharacterController cc = playerTransform.GetComponent<CharacterController>();

        // Se esiste un salvataggio per questa scena, usa la posizione salvata
        if (PersistenceManager.Instance != null && PersistenceManager.Instance.HasSave())
        {
            GameData data = PersistenceManager.Instance.Load();
            if (data != null && data.sceneName == SceneManager.GetActiveScene().name)
            {
                MovePlayer(playerTransform, cc, data.PlayerPosition);
                return;
            }
        }

        // Altrimenti usa lo spawn point della scena
        if (spawnPoint != null)
        {
            MovePlayer(playerTransform, cc, spawnPoint.position);
        }
    }

    private void MovePlayer(Transform player, CharacterController cc, Vector3 position)
    {
        if (cc != null) cc.enabled = false;
        player.position = position;
        if (cc != null) cc.enabled = true;
    }
}

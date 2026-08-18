using UnityEngine;

// Segnaposto: dove nasce il player in questa scena, e verso dove guarda.
//
// Senza, il PlayerManager lo faceva comparire all'origine, che va bene solo in una
// scena di prova con un piano piatto: nell'arena l'origine cade sopra il sarcofago.
// Basta piazzarne uno in scena, non serve collegarlo a niente.
public class PlayerSpawnPoint : MonoBehaviour
{
    public static PlayerSpawnPoint Find()
    {
        return Object.FindFirstObjectByType<PlayerSpawnPoint>();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.8f);
        // la freccia mostra l'orientamento iniziale, che conta quanto la posizione
        Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward * 1.2f);
    }
}

using UnityEngine;

// Dati di una singola mossa del player, uno per stato d'attacco dell'Animator.
//
// Esiste per poter tarare i colpi uno per uno: prima tutte le hitbox di pugni e
// calci avevano gli stessi valori serializzati, e il danno per colpo stava in array
// paralleli su PlayerAnimator, legati all'ordine della combo.
//
// Copre solo il colpo NORMALE: congelamento, scuotimento, spinta e reazione su
// parata o esecuzione restano sulla Hitbox, perche' dipendono da come il colpo
// viene ricevuto e non da quale mossa e'.
//
// Una mossa senza MoveData si comporta come prima: moltiplicatori di PlayerAnimator
// e valori serializzati sulla Hitbox.
[CreateAssetMenu(fileName = "Move_", menuName = "KosherFury/MoveData")]
public class MoveData : ScriptableObject
{
    [Tooltip("Nome dello stato nel Base Layer dell'Animator del Rabbi (es. Punch_Right, Slash_2, Area_Slash). E' cosi' che PlayerAnimator trova la mossa in corso.")]
    public string stateName;

    [Header("Danno")]
    [Tooltip("Moltiplica il danno base dell'arma equipaggiata (WeaponData.damage).")]
    public float damageMultiplier = 1f;

    [Header("Impatto (colpo andato a segno)")]
    [Tooltip("Congelamento all'impatto, in secondi. Sostituisce Hitbox.hitStop per questa mossa.")]
    public float hitStop = 0.07f;
    [Tooltip("Scuotimento della camera (0-1). Sostituisce Hitbox.cameraShake.")]
    [Range(0f, 1f)] public float cameraShake = 0.35f;
    [Tooltip("Spinta impressa al bersaglio, in m/s. distanza = v^2 / (2 * decay), con decay 9: a 1.5 fa ~0.125 m. Sostituisce Hitbox.knockback.")]
    public float knockback = 3.5f;
    [Tooltip("Forza passata a HitImpactReaction del bersaglio, cioe' quanto il colpo scompone il rig. Sostituisce Hitbox.impactReaction.")]
    public float impactReaction = 2.5f;

    [Header("Lunge")]
    [Tooltip("Metri percorsi in avanti durante la mossa. 0 = nessun lunge (resta l'avanzamento agganciato di PlayerMovement).")]
    public float lungeDistance;
    [Tooltip("Frazione di lungeDistance gia' percorsa (asse Y, 0..1) in funzione del tempo normalizzato dello stato (asse X, 0..1). Deve partire da 0 e arrivare a 1: la forma decide QUANDO ci si muove, la distanza la decide lungeDistance.")]
    public AnimationCurve lungeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public int StateHash => Animator.StringToHash(stateName);
}

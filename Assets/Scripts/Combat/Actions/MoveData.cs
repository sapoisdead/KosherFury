using System;
using UnityEngine;

// Una mossa come dati: clip, tempi, impatto, lunge, collegamenti. Un asset per mossa,
// in Assets/ScriptableObjects/Moves, con lo stesso nome dell'action di Blender.
//
// I tempi sono in frame a 60 Hz di tempo di GIOCO dall'inizio della mossa (vedi
// HitWindow): per le clip nuove arrivano dai marker di Blender (importer in A.5),
// per quelle vecchie dal Frame Data Report. Un valore negativo vuol dire "assente".
//
// Fasi e hitbox si aprono confrontando il tempo della mossa con questi numeri: niente
// animation event.
[CreateAssetMenu(fileName = "Move_", menuName = "KosherFury/Combat/MoveData")]
public class MoveData : ScriptableObject
{
    public const float FrameRate = 60f;

    [Header("Clip")]
    public MoveKind kind = MoveKind.Attack;
    public AnimationClip clip;
    [Tooltip("Velocita' di riproduzione. 1 per le clip nuove; le vecchie vanno accelerate come nel controller di oggi (es. 1.96).")]
    public float speed = 1f;
    [Tooltip("Crossfade d'ingresso, in secondi.")]
    public float fadeIn = 0.05f;
    [Tooltip("Crossfade d'uscita verso la locomozione, in secondi, preso dalla coda della mossa.")]
    public float fadeOut = 0.1f;
    [Tooltip("Fine della mossa in frame. Negativo = fine della clip.")]
    public float endFrame = -1f;

    [Header("Finestre (frame a 60 Hz)")]
    public HitWindow[] hitWindows = Array.Empty<HitWindow>();
    [Tooltip("Da qui parte il colpo successivo (nextLight/nextHeavy), se e' gia' in coda. Negativo = nessuna catena.")]
    public float comboFrame = -1f;
    [Tooltip("Da qui schivata e parata annullano la mossa. Negativo = non annullabile.")]
    public float cancelFrame = -1f;

    [Header("Parata")]
    [Tooltip("Finestra in cui un colpo subito viene parato.")]
    public float parryStartFrame = -1f;
    public float parryEndFrame = -1f;
    [Tooltip("Frame in cui non si puo' agire, a fine mossa, se la parata non ha intercettato niente. 0 = nessuna penalita'.")]
    public float whiffRecoveryFrames;

    [Header("Schivata")]
    public float iframeStartFrame = -1f;
    public float iframeEndFrame = -1f;
    [Tooltip("Lo spostamento della clip arriva al personaggio come root motion.")]
    public bool rootMotion;
    [Tooltip("Moltiplica il root motion. Le clip di dash vecchie coprono ~1 m, e il gioco di oggi le scala x4.")]
    public float rootMotionScale = 1f;

    [Header("Impatto (colpo andato a segno)")]
    [Tooltip("Moltiplica il danno base dell'arma.")]
    public float damageMultiplier = 1f;
    [Tooltip("Congelamento all'impatto, in secondi.")]
    public float hitStop = 0.07f;
    [Range(0f, 1f)] public float cameraShake = 0.35f;
    [Tooltip("Spinta al bersaglio in m/s. distanza = v^2 / (2 * 9): a 1.5 fa ~0.125 m.")]
    public float knockback = 1f;
    [Tooltip("Forza passata a HitImpactReaction del bersaglio (quanto scompone il rig).")]
    public float impactReaction = 2.5f;
    public ReactionType reactionType = ReactionType.Light;
    [Tooltip("Sui frame attivi si incassa il danno senza essere interrotti (armor).")]
    public bool armorWhileActive = true;

    [Header("Lunge")]
    [Tooltip("Metri in avanti durante la mossa. 0 = nessun lunge.")]
    public float lungeDistance;
    [Tooltip("Frazione di lungeDistance gia' percorsa (Y, 0..1) in funzione del tempo della mossa normalizzato sulla sua durata (X, 0..1).")]
    public AnimationCurve lungeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Collegamenti")]
    public MoveData nextLight;
    public MoveData nextHeavy;

    // Durata in secondi di gioco
    public float Duration => endFrame >= 0f
        ? endFrame / FrameRate
        : (clip != null && speed > 0f ? clip.length / speed : 0f);

    public static bool InWindow(float frame, float start, float end) =>
        start >= 0f && end >= 0f && frame >= start && frame < end;
}

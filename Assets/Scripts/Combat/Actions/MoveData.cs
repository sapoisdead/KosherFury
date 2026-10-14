using System;
using UnityEngine;

// Una mossa come dati: clip, tempi, impatto, lunge, collegamenti. Un asset per mossa,
// in Assets/ScriptableObjects/Moves, con lo stesso nome dell'action di Blender.
//
// I tempi sono in frame a 60 Hz della CLIP, cioe' i frame di Blender: per le clip nuove li
// scrive l'importer dai marker, per quelle vecchie vengono dal Frame Data Report. Un valore
// negativo vuol dire "assente". In tempo di gioco un frame dura 1 / (60 * velocita'
// effettiva), dove la velocita' effettiva e' speed per il moltiplicatore dell'ActionPlayer:
// cambiando speed, anche a mano dopo l'import, finestre, combo, cancel, parata e
// invulnerabilita' si spostano insieme alla lama.
//
// Fasi e hitbox si aprono confrontando il tempo della clip con questi numeri: niente
// animation event.
[CreateAssetMenu(fileName = "Move_", menuName = "KosherFury/Combat/MoveData")]
public class MoveData : ScriptableObject
{
    public const float FrameRate = 60f;

    [Header("Clip")]
    public MoveKind kind = MoveKind.Attack;
    public AnimationClip clip;
    [Tooltip("Velocita' di riproduzione: sotto 1 la mossa rallenta, e tutti i tempi qui sotto (frame della clip) la seguono. Le clip vecchie vanno accelerate come nel controller di oggi (es. 1.96).")]
    public float speed = 1f;
    [Tooltip("Crossfade d'ingresso, in secondi.")]
    public float fadeIn = 0.05f;
    [Tooltip("Crossfade d'uscita verso la locomozione, in secondi, preso dalla coda della mossa.")]
    public float fadeOut = 0.1f;
    [Tooltip("Fine della mossa in frame della clip. Negativo = fine della clip.")]
    public float endFrame = -1f;

    [Header("Finestre (frame a 60 Hz della clip)")]
    public HitWindow[] hitWindows = Array.Empty<HitWindow>();
    [Tooltip("Da qui parte il colpo successivo (nextLight/nextHeavy), se e' gia' in coda. Negativo = nessuna catena.")]
    public float comboFrame = -1f;
    [Tooltip("Da qui schivata e parata annullano la mossa. Negativo = non annullabile.")]
    public float cancelFrame = -1f;

    [Header("Parata")]
    [Tooltip("Finestra in cui un colpo subito viene parato.")]
    public float parryStartFrame = -1f;
    public float parryEndFrame = -1f;
    [Tooltip("Frame (di GIOCO, a 60 Hz: e' una penalita', non un punto della clip, e non segue speed) in cui non si puo' agire, a fine mossa, se la parata non ha intercettato niente. 0 = nessuna penalita'.")]
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

    // Durata della mossa in secondi di clip (a velocita' 1)
    public float ClipDuration => endFrame >= 0f
        ? endFrame / FrameRate
        : (clip != null ? clip.length : 0f);

    // Durata in secondi di gioco, alla velocita' della mossa per un moltiplicatore
    public float DurationAt(float speedMultiplier = 1f)
    {
        float effective = speed * speedMultiplier;
        return effective > 0f ? ClipDuration / effective : 0f;
    }

    public static bool InWindow(float frame, float start, float end) =>
        start >= 0f && end >= 0f && frame >= start && frame < end;
}

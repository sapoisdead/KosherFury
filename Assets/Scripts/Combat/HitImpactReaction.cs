using UnityEngine;

// Flinch procedurale additivo sopra l'animazione esistente: un offset di
// rotazione che decade nel tempo, applicato in LateUpdate dopo che l'Animator
// ha gia' scritto la posa del frame (per questo va sullo stesso GameObject
// dell'Animator, stessa convenzione degli animation event OnRightHandStart
// ecc., vedi CLAUDE.md). A offset zero il risultato e' identico a oggi.
//
// Nessun Rigidbody, nessun Collider per osso: solo una rotazione locale
// aggiunta ai bone del rig Mixamo indicati in Inspector.
[DisallowMultipleComponent]
public class HitImpactReaction : MonoBehaviour
{
    [System.Serializable]
    private struct BoneKick
    {
        [Tooltip("Bone Mixamo da flinchare (es. mixamorig:Spine1, mixamorig:Head).")]
        public Transform bone;
        [Tooltip("Peso relativo di questo bone rispetto agli altri (1 = pieno effetto).")]
        public float weight;
    }

    [Tooltip("Bone coinvolti nel flinch, tipicamente dal busto verso le estremita'.")]
    [SerializeField] private BoneKick[] bones;

    [Tooltip("Gradi di offset per unita' di force (stessa scala usata da Knockback, m/s).")]
    [SerializeField] private float degreesPerForce = 6f;
    [Tooltip("Offset massimo in gradi, qualunque sia la force del colpo.")]
    [SerializeField] private float maxDegrees = 25f;
    [Tooltip("Tempo perche' l'offset si riassorba quasi del tutto, in secondi.")]
    [SerializeField] private float recoveryTime = 0.2f;

    private Vector3 axisWorld;
    private float degrees;

    // direction e' la direzione orizzontale del colpo (da chi attacca verso il
    // bersaglio, come gia' calcolata in Hitbox.ApplyKnockback), force nella
    // stessa scala usata da Knockback.Apply.
    public void Kick(Vector3 direction, float force)
    {
        if (bones == null || bones.Length == 0 || force <= 0f) return;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        // asse orizzontale perpendicolare alla direzione del colpo: il busto si
        // piega all'indietro rispetto a chi ha colpito
        axisWorld = Vector3.Cross(Vector3.up, direction);
        degrees = Mathf.Min(force * degreesPerForce, maxDegrees);
    }

    private void LateUpdate()
    {
        if (degrees <= 0.01f) return;

        foreach (var b in bones)
        {
            if (b.bone == null) continue;

            // l'asse va riproiettato ogni frame nello spazio locale del bone: il
            // parent ruota per conto suo (e' l'Animator a guidarlo), quindi un
            // asse locale calcolato una sola volta all'impatto andrebbe storto
            Vector3 localAxis = b.bone.parent != null
                ? b.bone.parent.InverseTransformDirection(axisWorld)
                : axisWorld;

            b.bone.localRotation = Quaternion.AngleAxis(degrees * b.weight, localAxis) * b.bone.localRotation;
        }

        // decadimento esponenziale: si riassorbe in ~recoveryTime indipendentemente
        // dall'ampiezza di partenza (un rate fisso in gradi/secondo impiegherebbe
        // piu' tempo per i colpi forti)
        float decayRate = recoveryTime > 0f ? 5f / recoveryTime : 1000f;
        degrees *= Mathf.Exp(-decayRate * Time.deltaTime);
    }
}

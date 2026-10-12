using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

// Strumento di misura, non di gioco: fa partire gli stati d'attacco del player
// uno alla volta e ne misura startup, finestra attiva e durata in tempo vero.
//
// Il tempo si accumula da Time.deltaTime in LateUpdate, cioe' dopo l'aggiornamento
// dell'Animator dello stesso frame: e' li' che gli animation event sono gia'
// scattati, quindi ogni misura e' in ritardo al piu' di un frame. Contare i frame
// non servirebbe a niente (il gioco gira a ~260 fps).
//
// Il PlayerInput viene spento per tutta la prova: l'Input System vivo
// interferirebbe con i comandi dati da qui. Lo lancia FrameDataReport (editor).
//
// Per tutta la prova il tempo avanza a passo fisso (Time.captureDeltaTime): con
// l'editor in secondo piano Unity scende a ~10 fps, e a 100 ms per frame l'errore
// di quantizzazione e' piu' grande delle finestre che si vogliono misurare. A passo
// fisso Animator, eventi e deltaTime avanzano tutti dello stesso quanto, quindi la
// misura e' ripetibile e non dipende da quanto e' carico l'editor.
[DisallowMultipleComponent]
public class FrameDataProbe : MonoBehaviour
{
    [Serializable]
    public class Measurement
    {
        public string stateName;
        public bool completed;
        public string error;
        public float startup = -1f;
        public float active = -1f;
        public float total = -1f;
    }

    public class Result
    {
        public readonly List<Measurement> attacks = new();
        public float commandLatency = -1f;
        public float commandToActive = -1f;
        public float parryWindow = -1f;
        public string parryClosedBy = "-";
        public float meanFrameTime;
    }

    private const float StateTimeout = 5f;
    private const float SettleTime = 0.4f;
    private const float MeasureStep = 1f / 240f;

    private Animator animator;
    private PlayerAnimator playerAnimator;
    private CombatState combat;
    private Health health;

    // campionamento in LateUpdate, guidato dalla coroutine
    private Func<bool> sampler;
    private int frameCount;
    private float frameTimeSum;

    public bool IsRunning { get; private set; }

    public void Run(string[] stateNames, Action<Result> onDone)
    {
        if (IsRunning) return;

        animator = GetComponent<Animator>();
        playerAnimator = GetComponent<PlayerAnimator>();
        combat = GetComponent<CombatState>();
        health = GetComponent<Health>();
        StartCoroutine(RunAll(stateNames, onDone));
    }

    private void LateUpdate()
    {
        if (sampler == null) return;
        frameCount++;
        frameTimeSum += Time.deltaTime;
        if (sampler()) sampler = null;
    }

    private IEnumerator RunAll(string[] stateNames, Action<Result> onDone)
    {
        IsRunning = true;
        var input = GetComponent<PlayerInput>();
        bool inputWasEnabled = input != null && input.enabled;
        if (input != null) input.enabled = false;
        float previousCapture = Time.captureDeltaTime;
        Time.captureDeltaTime = MeasureStep;

        var result = new Result();

        foreach (var name in stateNames)
        {
            yield return Settle();
            var m = new Measurement { stateName = name };
            yield return MeasureState(name, m);
            result.attacks.Add(m);
        }

        yield return Settle();
        yield return MeasureCommandLatency(result);

        yield return Settle();
        yield return MeasureParry(result);

        result.meanFrameTime = frameCount > 0 ? frameTimeSum / frameCount : 0f;

        Time.captureDeltaTime = previousCapture;
        if (input != null) input.enabled = inputWasEnabled;
        IsRunning = false;
        onDone?.Invoke(result);
    }

    // Aspetta che il player sia fermo fuori dagli attacchi, poi ancora un po'.
    private IEnumerator Settle()
    {
        float calm = 0f;
        float guard = 0f;
        while (calm < SettleTime && guard < 10f)
        {
            yield return null;
            guard += Time.deltaTime;
            bool busy = combat.IsAttacking || animator.IsInTransition(0) || health.IsBlocking;
            calm = busy ? 0f : calm + Time.deltaTime;
        }
    }

    private IEnumerator MeasureState(string stateName, Measurement m)
    {
        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, hash))
        {
            m.error = "stato assente nel controller";
            yield break;
        }

        playerAnimator.ResetCombo();
        animator.Play(hash, 0, 0f);

        float t = 0f;
        bool sawState = false;
        bool done = false;

        sampler = () =>
        {
            t += Time.deltaTime;
            var cur = animator.GetCurrentAnimatorStateInfo(0);
            bool inState = cur.shortNameHash == hash;
            bool leaving = inState && animator.IsInTransition(0)
                           && animator.GetNextAnimatorStateInfo(0).shortNameHash != hash;

            if (inState) sawState = true;

            if (m.startup < 0f && (combat.Phase == AttackPhase.Active || combat.Phase == AttackPhase.Recovery))
                m.startup = t;
            if (m.startup >= 0f && m.active < 0f && combat.Phase == AttackPhase.Recovery)
                m.active = t - m.startup;   // finestra piu' corta di un frame: viene ~0

            if (sawState && (!inState || leaving))
            {
                m.total = t;
                m.completed = true;
                done = true;
            }
            else if (t > StateTimeout)
            {
                m.error = "timeout";
                done = true;
            }
            return done;
        };

        while (!done) yield return null;
    }

    // Stesso colpo, ma dal comando: comprende il ritardo del trigger e la transizione.
    private IEnumerator MeasureCommandLatency(Result r)
    {
        int hash = Animator.StringToHash("Punch_Right");
        playerAnimator.ResetCombo();
        playerAnimator.TriggerPunch();

        float t = 0f;
        bool done = false;
        sampler = () =>
        {
            t += Time.deltaTime;
            bool entering = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash
                            || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == hash);
            if (r.commandLatency < 0f && entering) r.commandLatency = t;
            if (r.commandToActive < 0f && combat.Phase != AttackPhase.None && combat.Phase != AttackPhase.Startup)
                r.commandToActive = t;

            done = (r.commandToActive >= 0f) || t > StateTimeout;
            return done;
        };
        while (!done) yield return null;
    }

    // Finestra di parata vera: dal comando a quando Health smette di bloccare.
    private IEnumerator MeasureParry(Result r)
    {
        playerAnimator.TriggerParry();
        float maxWindow = ReadFloat(playerAnimator, "parryMaxWindow", float.MaxValue);

        float t = 0f;
        bool done = false;
        sampler = () =>
        {
            t += Time.deltaTime;
            if (!health.IsBlocking)
            {
                r.parryWindow = t;
                r.parryClosedBy = t >= maxWindow - 0.02f
                    ? $"parryMaxWindow ({maxWindow:0.00}s)"
                    : "OnParryEnd (animation event)";
                done = true;
            }
            else if (t > StateTimeout)
            {
                r.parryClosedBy = "timeout: la parata non si e' chiusa";
                done = true;
            }
            return done;
        };
        while (!done) yield return null;
    }

    private static float ReadFloat(object target, string field, float fallback)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return f != null && f.FieldType == typeof(float) ? (float)f.GetValue(target) : fallback;
    }
}

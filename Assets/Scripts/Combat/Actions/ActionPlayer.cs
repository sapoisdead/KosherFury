using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Riproduce le mosse (colpi, schivate, parate) fuori dall'Animator Controller, con un
// PlayableGraph proprio. Generico: non sa chi lo usa ne' perche', riceve solo Play().
// Chi decide QUALE mossa e QUANDO e' CombatActor.
//
// Il grafo ha una propria uscita sullo stesso Animator, che si SOVRAPPONE al controller
// con un peso: 0 quando non c'e' mossa (si vede il controller, cioe' la locomozione),
// 1 durante la mossa. Il controller continua a girare sotto. Quando il controller sara'
// ridotto alla sola locomozione potra' restare cosi' o diventare un input del grafo.
//
//   [AnimationClipPlayable slot A] \
//                                   > AnimationMixerPlayable -> AnimationPlayableOutput (peso) -> Animator
//   [AnimationClipPlayable slot B] /
//
// Due slot alternati: una mossa nuova entra nello slot libero e prende peso mentre
// quella vecchia lo perde, cosi' due colpi in catena si fondono invece di scattare.
//
// Le finestre attive si aprono confrontando il tempo della mossa con il MoveData, non
// con animation event: gli eventi sono spenti finche' la mossa ha peso (vedi
// fireEvents), o le clip vecchie, che li hanno ancora, aprirebbero le hitbox due volte.
//
// HitStop congela l'Animator mettendone la velocita' a 0, ma un grafo proprio non la
// guarda: per questo la velocita' della clip viene azzerata a mano finche' il
// personaggio e' congelato. Il tempo della mossa e' quello della clip, quindi si
// ferma con lei, e con lui tutto cio' che ne dipende (finestre, lunge).
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class ActionPlayer : MonoBehaviour
{
    public const float FrameRate = MoveData.FrameRate;

    private Animator animator;
    private CombatState combat;
    private IHitboxSource hitboxSource;

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationPlayableOutput output;
    private readonly AnimationClipPlayable[] slots = new AnimationClipPlayable[2];
    private readonly float[] slotWeights = new float[2];
    private int activeSlot = -1;

    private MoveData current;
    private bool[] windowOpened;
    private bool[] windowClosed;
    private bool finished = true;

    private float outputWeight;
    private float fadeTime;          // durata del crossfade in corso
    private bool fadingOut;
    private bool eventsWereOn = true;
    private bool eventsSuppressed;

    public bool IsPlaying => !finished;
    public MoveData Current => finished ? null : current;

    // Tempo di gioco dall'inizio della mossa: tempo della clip / velocita'
    public float Elapsed { get; private set; }
    public float ElapsedFrames => Elapsed * FrameRate;
    public float NormalizedTime => IsPlaying && current.Duration > 0f ? Mathf.Clamp01(Elapsed / current.Duration) : 0f;

    // La mossa in corso governa CombatState: chi altrimenti lo aggiornerebbe (il vecchio
    // PlayerAnimator, che lo deriva dagli stati del controller) deve astenersi.
    public bool OwnsCombatState => IsPlaying && current.kind == MoveKind.Attack;

    // Root motion della mossa: chi applica lo spostamento (PlayerMovement, in
    // OnAnimatorMove) lo prende solo quando la mossa lo vuole, gia' scalato.
    public bool WantsRootMotion => IsPlaying && current.rootMotion && outputWeight > 0f;
    public float RootMotionScale => IsPlaying ? current.rootMotionScale : 1f;

    public event Action<MoveData> Finished;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        combat = GetComponent<CombatState>();
        if (combat == null) combat = gameObject.AddComponent<CombatState>();
        hitboxSource = GetComponent<IHitboxSource>();

        graph = PlayableGraph.Create($"{name}.Actions");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        output = AnimationPlayableOutput.Create(graph, "Actions", animator);
        output.SetSourcePlayable(mixer);
        output.SetWeight(0f);
        graph.Play();
    }

    private void OnDestroy()
    {
        RestoreEvents();
        if (graph.IsValid()) graph.Destroy();
    }

    // ---------------------------------------------------------------- API

    public void Play(MoveData move)
    {
        if (move == null || move.clip == null) return;

        // la mossa interrotta chiude le sue finestre: una hitbox lasciata aperta
        // continuerebbe a colpire durante quella nuova
        CloseAllWindows();

        int slot = activeSlot < 0 ? 0 : 1 - activeSlot;
        if (slots[slot].IsValid())
        {
            graph.Disconnect(mixer, slot);
            slots[slot].Destroy();
        }

        var clipPlayable = AnimationClipPlayable.Create(graph, move.clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetSpeed(move.speed);
        // niente SetTime(0): il playable nasce gia' a 0, e dopo un SetTime Unity non fa
        // avanzare il tempo nel frame successivo, che diventava un frame fermo a ogni mossa
        clipPlayable.SetDuration(move.clip.length);
        graph.Connect(clipPlayable, 0, mixer, slot);
        slots[slot] = clipPlayable;

        // senza una mossa visibile il nuovo slot parte gia' pieno: il crossfade lo fa
        // il peso dell'uscita, dalla locomozione
        if (outputWeight <= 0f)
        {
            slotWeights[slot] = 1f;
            slotWeights[1 - slot] = 0f;
        }

        activeSlot = slot;
        current = move;
        Elapsed = 0f;
        windowOpened = new bool[move.hitWindows.Length];
        windowClosed = new bool[move.hitWindows.Length];
        finished = false;
        fadingOut = false;
        fadeTime = Mathf.Max(move.fadeIn, 0.0001f);

        SuppressEvents();

        // None e poi Startup: cosi' AttackId cambia anche se la stessa mossa si ripete.
        // Una mossa che non e' un attacco riporta la fase a None (una schivata che
        // annulla un colpo non deve lasciarlo in Recovery).
        combat.UpdatePhase(false, 0);
        combat.ArmorInActive = true;
        if (move.kind == MoveKind.Attack)
        {
            combat.ArmorInActive = move.armorWhileActive;
            combat.UpdatePhase(true, Animator.StringToHash(move.name));
        }
    }

    public void Stop(float fade = 0.1f)
    {
        if (finished) return;
        Finish(fade);
    }

    // ---------------------------------------------------------------- tempo

    // Prima dell'animazione: pesi e velocita' per l'aggiornamento di questo frame.
    private void Update()
    {
        UpdateWeights(Time.deltaTime);

        if (finished || !slots[activeSlot].IsValid()) return;

        // HitStop arriva dalla Hitbox in FixedUpdate, quindi e' gia' noto qui
        bool frozen = HitStop.IsFrozen(gameObject);
        slots[activeSlot].SetSpeed(frozen ? 0f : current.speed);
    }

    // Dopo l'animazione: il tempo della clip e' quello appena valutato, cioe' la posa
    // che si vede in questo frame. Leggerlo in Update darebbe quello del frame prima,
    // e ogni finestra si aprirebbe e chiuderebbe con un frame di ritardo.
    private void LateUpdate()
    {
        if (finished || !slots[activeSlot].IsValid()) return;

        Elapsed = (float)slots[activeSlot].GetTime() / Mathf.Max(current.speed, 0.0001f);
        UpdateWindows();

        float duration = current.Duration;
        if (!fadingOut && Elapsed >= duration - current.fadeOut)
        {
            fadingOut = true;
            fadeTime = Mathf.Max(current.fadeOut, 0.0001f);
        }
        if (Elapsed >= duration) Finish(0f);
    }

    private void UpdateWeights(float dt)
    {
        float step = dt / Mathf.Max(fadeTime, 0.0001f);
        float targetOutput = (!finished && !fadingOut) ? 1f : 0f;
        outputWeight = Mathf.MoveTowards(outputWeight, targetOutput, step);
        output.SetWeight(outputWeight);

        if (activeSlot >= 0)
        {
            slotWeights[activeSlot] = Mathf.MoveTowards(slotWeights[activeSlot], 1f, step);
            slotWeights[1 - activeSlot] = Mathf.MoveTowards(slotWeights[1 - activeSlot], 0f, step);
            mixer.SetInputWeight(0, slotWeights[0]);
            mixer.SetInputWeight(1, slotWeights[1]);
        }

        if (outputWeight <= 0f) RestoreEvents();
    }

    // Una finestra piu' corta di un frame si apre e si chiude nello stesso frame:
    // Hitbox.Activate fa comunque una lettura immediata, quindi il colpo viene testato.
    private void UpdateWindows()
    {
        float frame = ElapsedFrames;
        var windows = current.hitWindows;
        for (int i = 0; i < windows.Length; i++)
        {
            if (!windowOpened[i] && frame >= windows[i].startFrame)
            {
                windowOpened[i] = true;
                OpenWindow(windows[i].slot);
            }
            if (windowOpened[i] && !windowClosed[i] && frame >= windows[i].endFrame)
            {
                windowClosed[i] = true;
                CloseWindow(windows[i].slot);
            }
        }
    }

    private void OpenWindow(HitboxSlot slot)
    {
        if (current.kind == MoveKind.Attack) combat.OpenActiveWindow();
        Hitbox hitbox = hitboxSource?.GetHitbox(slot);
        float damage = (hitboxSource?.BaseDamage ?? 0f) * current.damageMultiplier;
        if (hitbox != null) hitbox.Activate(damage, current);
    }

    private void CloseWindow(HitboxSlot slot)
    {
        if (current.kind == MoveKind.Attack && !AnyWindowOpen()) combat.CloseActiveWindow();
        Hitbox hitbox = hitboxSource?.GetHitbox(slot);
        if (hitbox != null) hitbox.Deactivate();
    }

    private bool AnyWindowOpen()
    {
        for (int i = 0; i < windowOpened.Length; i++)
            if (windowOpened[i] && !windowClosed[i]) return true;
        return false;
    }

    private void CloseAllWindows()
    {
        if (finished || current == null) return;
        for (int i = 0; i < current.hitWindows.Length; i++)
        {
            if (windowOpened[i] && !windowClosed[i])
            {
                windowClosed[i] = true;
                CloseWindow(current.hitWindows[i].slot);
            }
        }
    }

    private void Finish(float fade)
    {
        CloseAllWindows();
        if (current.kind == MoveKind.Attack) combat.UpdatePhase(false, 0);
        combat.ArmorInActive = true;
        finished = true;
        fadingOut = true;
        if (fade > 0f) fadeTime = fade;
        Finished?.Invoke(current);
    }

    // ---------------------------------------------------------------- eventi

    private void SuppressEvents()
    {
        if (eventsSuppressed) return;
        eventsWereOn = animator.fireEvents;
        animator.fireEvents = false;
        eventsSuppressed = true;
    }

    private void RestoreEvents()
    {
        if (!eventsSuppressed) return;
        if (animator != null) animator.fireEvents = eventsWereOn;
        eventsSuppressed = false;
    }
}

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Frame data statico del Rabbi, letto dagli asset e non misurato: animation event
// delle clip e velocita' degli stati nell'Animator Controller.
//
// Gli eventi si leggono dal ModelImporter, dove il campo time e' NORMALIZZATO 0..1
// sulla durata della clip (al contrario di AnimationUtility.GetAnimationEvents, che
// li da' in secondi). La durata reale di uno stato e' la durata della clip divisa per
// la velocita' dello stato, quindi un evento a tempo normalizzato t cade a
// t * durata reale dall'ingresso nello stato.
//
// "Verify Frame Data (Play Mode)" fa partire gli stessi stati sul player vivo,
// misura i tempi accumulando Time.deltaTime (vedi FrameDataProbe) e li confronta
// con questa tabella.
public static class FrameDataReport
{
    private const string ControllerPath = "Assets/Prefabs/Player/Rabbi_AnimatorController.controller";
    private const float ReferenceFps = 60f;

    // Coppie di eventi che aprono e chiudono la finestra attiva, nominate sull'arto
    private static readonly string[] StartEvents =
        { "OnRightHandStart", "OnLeftHandStart", "OnRightFootStart", "OnLeftFootStart", "OnWeaponStart" };
    private static readonly string[] EndEvents =
        { "OnRightHandEnd", "OnLeftHandEnd", "OnRightFootEnd", "OnLeftFootEnd", "OnWeaponEnd" };

    private const string ParryEndEvent = "OnParryEnd";
    private const string ComboWindowEvent = "OnComboWindow";

    // Soglia fissa di PlayerAnimator per far partire il colpo in coda, usata
    // quando la clip non ha OnComboWindow
    private const float FallbackComboGate = 0.5f;

    public class Entry
    {
        public string stateName;
        public string clipName;
        public bool isParry;
        public float clipLength;      // secondi, a velocita' 1
        public float stateSpeed;
        public float duration;        // secondi reali: clipLength / stateSpeed
        public float startN = -1f;    // apertura hitbox, normalizzato (-1 = assente)
        public float endN = -1f;      // chiusura hitbox, normalizzato
        public float parryEndN = -1f; // OnParryEnd, normalizzato
        public float comboWindowN = -1f;
        public float chainExitN = -1f;   // exit time della transizione di catena (trigger + exit time)
        public float exitN = -1f;        // exit time della transizione di ritorno senza trigger
        public string notes = "";

        public float Startup  => startN  >= 0f ? startN * duration : -1f;
        public float Active   => startN  >= 0f && endN >= 0f ? (endN - startN) * duration : -1f;
        public float Recovery => endN    >= 0f ? (1f - endN) * duration : -1f;
        public float ParryWindow => parryEndN >= 0f ? parryEndN * duration : -1f;
    }

    [MenuItem("KosherFury/Combat/Frame Data Report")]
    public static void PrintReport()
    {
        var entries = Build();
        Debug.Log(Format(entries));
    }

    public static List<Entry> Build()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[FrameDataReport] Controller non trovato: {ControllerPath}");
            return new List<Entry>();
        }

        var result = new List<Entry>();
        foreach (var state in AllStates(controller.layers[0].stateMachine))
        {
            foreach (var clip in ClipsOf(state.motion))
            {
                var events = NormalizedEvents(clip);
                bool isAttack = events.Any(e => StartEvents.Contains(e.functionName));
                bool isParry  = events.Any(e => e.functionName == ParryEndEvent)
                                || clip.name.ToLowerInvariant().Contains("parry");
                if (!isAttack && !isParry) continue;

                var entry = new Entry
                {
                    stateName = state.name,
                    clipName = clip.name,
                    isParry = isParry && !isAttack,
                    clipLength = clip.length,
                    stateSpeed = state.speed,
                };

                if (state.speedParameterActive)
                    entry.notes += $"velocita' moltiplicata dal parametro '{state.speedParameter}' (qui assunto 1). ";

                entry.duration = entry.stateSpeed > 0f ? entry.clipLength / entry.stateSpeed : 0f;

                foreach (var e in events)
                {
                    if (StartEvents.Contains(e.functionName) && entry.startN < 0f) entry.startN = e.time;
                    else if (EndEvents.Contains(e.functionName)) entry.endN = e.time;
                    else if (e.functionName == ParryEndEvent) entry.parryEndN = e.time;
                    else if (e.functionName == ComboWindowEvent) entry.comboWindowN = e.time;
                }

                foreach (var t in state.transitions)
                {
                    if (!t.hasExitTime) continue;
                    bool hasTrigger = t.conditions.Any(c => IsTrigger(controller, c.parameter));
                    if (hasTrigger) entry.chainExitN = entry.chainExitN < 0f ? t.exitTime : Mathf.Min(entry.chainExitN, t.exitTime);
                    else entry.exitN = entry.exitN < 0f ? t.exitTime : Mathf.Min(entry.exitN, t.exitTime);
                }

                result.Add(entry);
            }
        }

        return result;
    }

    // Eventi della clip con time normalizzato. Per le clip importate da FBX la fonte e'
    // il ModelImporter (time gia' 0..1); per le clip .anim si ripiega sugli eventi della
    // clip, che sono in secondi, e li si normalizza qui.
    private static List<AnimationEvent> NormalizedEvents(AnimationClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        if (AssetImporter.GetAtPath(path) is ModelImporter importer)
        {
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            var match = clips.FirstOrDefault(c => c.name == clip.name);
            if (match != null)
                return match.events.Select(e => new AnimationEvent { functionName = e.functionName, time = e.time })
                                   .OrderBy(e => e.time).ToList();
        }

        float len = Mathf.Max(clip.length, 0.0001f);
        return AnimationUtility.GetAnimationEvents(clip)
                               .Select(e => new AnimationEvent { functionName = e.functionName, time = e.time / len })
                               .OrderBy(e => e.time).ToList();
    }

    private static bool IsTrigger(AnimatorController c, string param) =>
        c.parameters.Any(p => p.name == param && p.type == AnimatorControllerParameterType.Trigger);

    private static IEnumerable<AnimatorState> AllStates(AnimatorStateMachine sm)
    {
        foreach (var s in sm.states) yield return s.state;
        foreach (var sub in sm.stateMachines)
            foreach (var s in AllStates(sub.stateMachine)) yield return s;
    }

    // Uno stato puo' avere un blend tree (es. Block: Hand_parry/Sword_parry su Stance):
    // ogni clip foglia diventa una riga a se'.
    private static IEnumerable<AnimationClip> ClipsOf(Motion motion)
    {
        if (motion is AnimationClip clip) { yield return clip; yield break; }
        if (motion is BlendTree tree)
            foreach (var child in tree.children)
                foreach (var c in ClipsOf(child.motion)) yield return c;
    }

    // ---------------------------------------------------------------- formato

    public static string Sec(float s) => s < 0f ? "   -   " : s.ToString("0.000", CultureInfo.InvariantCulture) + "s";
    public static string Frames(float s) => s < 0f ? "  - " : (s * ReferenceFps).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(5) + "f";
    private static string Both(float s) => $"{Sec(s)} {Frames(s)}";

    public static string Format(List<Entry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== KF FRAME DATA REPORT ===");
        sb.AppendLine($"(statico: eventi dal ModelImporter, velocita' dagli stati; frame a {ReferenceFps:0} Hz)");
        sb.AppendLine();
        sb.AppendLine("stato           clip             speed  totale          startup         attiva          recovery        combo-gate  catena@  uscita@");

        foreach (var e in entries.Where(e => !e.isParry))
        {
            string gate = e.comboWindowN >= 0f
                ? $"{e.comboWindowN:0.00}(ev)"
                : $"{FallbackComboGate:0.00}(fix)";
            sb.AppendLine(
                $"{e.stateName,-15} {e.clipName,-16} {e.stateSpeed,5:0.00}  {Both(e.duration)}  {Both(e.Startup)}  {Both(e.Active)}  {Both(e.Recovery)}  {gate,-10}  " +
                $"{(e.chainExitN >= 0f ? e.chainExitN.ToString("0.00", CultureInfo.InvariantCulture) : "-"),-7}  " +
                $"{(e.exitN >= 0f ? e.exitN.ToString("0.00", CultureInfo.InvariantCulture) : "-")}");
            if (!string.IsNullOrEmpty(e.notes)) sb.AppendLine("    nota: " + e.notes);
        }

        sb.AppendLine();
        sb.AppendLine("parata          clip             speed  totale          OnParryEnd (dall'ingresso nello stato)");
        foreach (var e in entries.Where(e => e.isParry))
            sb.AppendLine($"{e.stateName,-15} {e.clipName,-16} {e.stateSpeed,5:0.00}  {Both(e.duration)}  {Both(e.ParryWindow)}");

        sb.AppendLine();
        sb.AppendLine("Legenda: startup = ingresso nello stato -> apertura hitbox; attiva = finestra aperta;");
        sb.AppendLine("recovery = chiusura hitbox -> fine clip. combo-gate = punto (normalizzato) da cui PlayerAnimator");
        sb.AppendLine("fa partire il colpo in coda; catena@ = exit time della transizione al colpo successivo nel");
        sb.AppendLine("controller, che il trigger deve comunque aspettare; uscita@ = exit time del ritorno a idle.");
        sb.AppendLine("Non comprende la latenza fra comando e ingresso nello stato: quella la misura la verifica in Play Mode.");
        sb.AppendLine("=== END KF FRAME DATA REPORT ===");
        return sb.ToString();
    }

    // ---------------------------------------------------------------- verifica

    [MenuItem("KosherFury/Combat/Verify Frame Data (Play Mode)")]
    public static void VerifyInPlayMode()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[FrameDataReport] La verifica va lanciata in Play Mode, con il player in scena.");
            return;
        }

        var player = Object.FindFirstObjectByType<PlayerAnimator>();
        if (player == null)
        {
            Debug.LogWarning("[FrameDataReport] Nessun PlayerAnimator in scena.");
            return;
        }

        var entries = Build();
        // una riga per stato: Block ha due clip (mani nude / spada) ma uno stato solo,
        // e a mani nude e' Hand_parry a girare
        var attackStates = entries.Where(e => !e.isParry).Select(e => e.stateName).Distinct().ToArray();

        var probe = player.GetComponent<FrameDataProbe>() ?? player.gameObject.AddComponent<FrameDataProbe>();
        probe.Run(attackStates, measurements => Debug.Log(FormatComparison(entries, measurements)));
    }

    private static string FormatComparison(List<Entry> entries, FrameDataProbe.Result result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== KF FRAME DATA VERIFY ===");
        sb.AppendLine($"(misurato in Play Mode accumulando Time.deltaTime, frame medio {result.meanFrameTime * 1000f:0.00} ms;");
        sb.AppendLine(" errore atteso entro un frame. Delta = misurato - statico)");
        sb.AppendLine();
        sb.AppendLine("stato           startup stat/mis/delta          attiva stat/mis/delta           totale stat/mis/delta");

        foreach (var m in result.attacks)
        {
            var e = entries.FirstOrDefault(x => x.stateName == m.stateName && !x.isParry);
            if (e == null) continue;
            if (!m.completed)
            {
                sb.AppendLine($"{m.stateName,-15} NON COMPLETATO: {m.error}");
                continue;
            }
            sb.AppendLine($"{m.stateName,-15} {Cmp(e.Startup, m.startup)}  {Cmp(e.Active, m.active)}  {Cmp(e.duration, m.total)}");
        }

        sb.AppendLine();
        sb.AppendLine($"Latenza comando -> ingresso nello stato (Punch_Right via TriggerPunch): {Sec(result.commandLatency)} {Frames(result.commandLatency)}");
        sb.AppendLine($"Comando -> hitbox aperta (stesso colpo): {Sec(result.commandToActive)} {Frames(result.commandToActive)}");

        var hand = entries.FirstOrDefault(x => x.isParry && x.clipName.ToLowerInvariant().Contains("hand"));
        sb.AppendLine();
        sb.AppendLine($"Parata a mani nude: finestra effettiva dal comando = {Sec(result.parryWindow)} {Frames(result.parryWindow)}" +
                      (hand != null ? $"  (statico, dall'ingresso nello stato: {Sec(hand.ParryWindow)})" : ""));
        sb.AppendLine($"  chiusa da: {result.parryClosedBy}");
        sb.AppendLine("=== END KF FRAME DATA VERIFY ===");
        return sb.ToString();
    }

    private static string Cmp(float stat, float measured)
    {
        if (stat < 0f || measured < 0f) return $"{Sec(stat)}/{Sec(measured)}/   -     ";
        float d = measured - stat;
        return $"{Sec(stat)}/{Sec(measured)}/{(d >= 0 ? "+" : "")}{d * 1000f:0.0}ms";
    }
}

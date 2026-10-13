using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Importer dei marker di Blender (contratto in Docs/Combat/PIPELINE.md).
//
// Legge <Personaggio>_anim_events.json e, per il <Personaggio>_anim.fbx accanto:
//   1. imposta le clip dell'FBX dalle take: nome (la parte dopo "|"), range di frame
//      dal JSON, loop per le action cicliche, root motion libero solo per schivate e
//      dash, compressione Off. Senza questo passo una riesportazione piu' lunga
//      verrebbe troncata al range della volta prima.
//   2. aggiorna SOLO i tempi dei MoveData con lo stesso nome dell'action (finestre
//      attive, combo, cancel, parata, invulnerabilita', fine), e la clip se manca.
//      Hitbox, impatto, lunge e collegamenti restano quelli scritti a mano.
//   3. segnala action senza MoveData, MoveData senza action e marker mancanti.
//
// Parte da solo quando il JSON o l'FBX vengono reimportati (cioe' a ogni esportazione),
// oppure dal menu KosherFury/Combat/Import Move Markers.
public class MoveMarkerImporter : AssetPostprocessor
{
    private const string JsonSuffix = "_anim_events.json";
    private const string FbxSuffix = "_anim.fbx";

    // JSON da importare. Sta in SessionState e non in un campo statico: un import seguito
    // da una ricarica del dominio (script ricompilati nello stesso refresh) azzererebbe i
    // campi statici e perderebbe la chiamata rimandata. Al caricamento si riprende da qui.
    private const string PendingKey = "KosherFury.MoveMarkerImporter.Pending";

    [MenuItem("KosherFury/Combat/Import Move Markers")]
    public static void ImportAll()
    {
        foreach (var guid in AssetDatabase.FindAssets("_anim_events t:TextAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(JsonSuffix)) Debug.Log(Import(path));
        }
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
        {
            string json = path.EndsWith(JsonSuffix) ? path
                        : path.EndsWith(FbxSuffix) ? path.Substring(0, path.Length - FbxSuffix.Length) + JsonSuffix
                        : null;
            if (json == null || !File.Exists(json)) continue;
            AddPending(json);
        }
    }

    [InitializeOnLoadMethod]
    private static void ResumeAfterReload() => SchedulePending();

    private static void AddPending(string json)
    {
        var pending = new HashSet<string>(SessionState.GetString(PendingKey, "").Split('|').Where(x => x.Length > 0));
        if (pending.Add(json)) SessionState.SetString(PendingKey, string.Join("|", pending));
        SchedulePending();
    }

    // Fuori dall'import in corso: reimportare l'FBX da dentro un import non e' permesso.
    private static void SchedulePending()
    {
        EditorApplication.delayCall -= ProcessPending;
        EditorApplication.delayCall += ProcessPending;
    }

    private static void ProcessPending()
    {
        string list = SessionState.GetString(PendingKey, "");
        SessionState.EraseString(PendingKey);
        foreach (var json in list.Split('|').Where(x => x.Length > 0).Distinct())
            if (File.Exists(json)) Debug.Log(Import(json));
    }

    // ---------------------------------------------------------------- import

    public static string Import(string jsonPath)
    {
        var report = new StringBuilder();
        report.AppendLine($"=== KF MOVE MARKERS === {jsonPath}");

        if (MiniJson.Parse(File.ReadAllText(jsonPath)) is not Dictionary<string, object> root
            || root.GetValueOrDefault("actions") is not Dictionary<string, object> actions)
        {
            report.AppendLine("JSON non valido: manca 'actions'.");
            return report.ToString();
        }

        float fps = root.TryGetValue("fps", out var f) ? (float)(double)f : MoveData.FrameRate;
        if (Mathf.Abs(fps - MoveData.FrameRate) > 0.01f)
            report.AppendLine($"ATTENZIONE: il JSON e' a {fps} fps, il contratto vuole {MoveData.FrameRate}.");

        string fbxPath = jsonPath.Substring(0, jsonPath.Length - JsonSuffix.Length) + FbxSuffix;
        var clips = ImportClips(fbxPath, actions, report);

        var moves = AssetDatabase.FindAssets("t:MoveData")
            .Select(g => AssetDatabase.LoadAssetAtPath<MoveData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(m => m != null)
            .ToDictionary(m => m.name, m => m);

        foreach (var (name, value) in actions)
        {
            if (value is not Dictionary<string, object> action) continue;
            if (!moves.TryGetValue(name, out var move))
            {
                report.AppendLine($"{name}: nessun MoveData con questo nome (normale per le locomozioni; per una mossa va creato).");
                continue;
            }
            UpdateMove(move, name, action, clips, report);
        }

        // MoveData che usano una clip di questo FBX ma non hanno piu' l'action
        foreach (var move in moves.Values)
            if (move.clip != null && AssetDatabase.GetAssetPath(move.clip) == fbxPath && !actions.ContainsKey(move.name))
                report.AppendLine($"{move.name}: il MoveData usa {fbxPath} ma l'action non c'e' piu' nel JSON.");

        AssetDatabase.SaveAssets();
        report.Append("=== END KF MOVE MARKERS ===");
        return report.ToString();
    }

    // Le clip dell'FBX dalle take, coi range del JSON. Reimporta solo se cambia qualcosa,
    // o l'import dell'FBX richiamerebbe questo all'infinito.
    private static Dictionary<string, AnimationClip> ImportClips(string fbxPath, Dictionary<string, object> actions, StringBuilder report)
    {
        var result = new Dictionary<string, AnimationClip>();
        if (AssetImporter.GetAtPath(fbxPath) is not ModelImporter importer)
        {
            report.AppendLine($"FBX non trovato: {fbxPath}");
            return result;
        }

        var takes = importer.importedTakeInfos;
        var wanted = new List<ModelImporterClipAnimation>();
        foreach (var take in takes)
        {
            string name = take.name.Contains("|") ? take.name.Substring(take.name.LastIndexOf('|') + 1) : take.name;
            if (actions.GetValueOrDefault(name) is not Dictionary<string, object> action)
            {
                report.AppendLine($"take {take.name}: nessuna action nel JSON, la clip resta fuori.");
                continue;
            }

            float start = Num(action, "frame_start", 0f), end = Num(action, "frame_end", (float)(take.stopTime * take.sampleRate));
            bool cyclic = action.GetValueOrDefault("cyclic") is bool b && b;
            bool rootMotion = name.StartsWith("Dodge") || name.StartsWith("Dash");

            wanted.Add(new ModelImporterClipAnimation
            {
                name = name, takeName = take.name,
                firstFrame = start, lastFrame = end,
                loopTime = cyclic, loopPose = false,
                lockRootRotation = true, keepOriginalOrientation = true,
                lockRootHeightY = true, keepOriginalPositionY = true,
                lockRootPositionXZ = !rootMotion, keepOriginalPositionXZ = true,
            });
        }

        bool changed = importer.animationCompression != ModelImporterAnimationCompression.Off
                       || !SameClips(importer.clipAnimations, wanted);
        if (changed)
        {
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.clipAnimations = wanted.ToArray();
            importer.SaveAndReimport();
            report.AppendLine($"clip aggiornate in {Path.GetFileName(fbxPath)}: " +
                              string.Join(", ", wanted.Select(c => $"{c.name} {c.firstFrame:0}-{c.lastFrame:0}{(c.loopTime ? " loop" : "")}")));
        }

        foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>())
            if (!clip.name.StartsWith("__preview__")) result[clip.name] = clip;
        return result;
    }

    private static bool SameClips(ModelImporterClipAnimation[] a, List<ModelImporterClipAnimation> b)
    {
        if (a.Length != b.Count) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].name != b[i].name || a[i].takeName != b[i].takeName
                || !Mathf.Approximately(a[i].firstFrame, b[i].firstFrame) || !Mathf.Approximately(a[i].lastFrame, b[i].lastFrame)
                || a[i].loopTime != b[i].loopTime || a[i].lockRootPositionXZ != b[i].lockRootPositionXZ)
                return false;
        }
        return true;
    }

    // Solo i tempi. I frame del JSON sono assoluti: si sottrae frame_start, e si
    // dividono per la velocita' (i tempi del MoveData sono in tempo di gioco).
    private static void UpdateMove(MoveData move, string name, Dictionary<string, object> action,
                                   Dictionary<string, AnimationClip> clips, StringBuilder report)
    {
        var markers = action.GetValueOrDefault("markers") as Dictionary<string, object> ?? new Dictionary<string, object>();
        float start = Num(action, "frame_start", 0f);
        float speed = move.speed > 0f ? move.speed : 1f;
        float Frame(string marker) => markers.TryGetValue(marker, out var v) ? ((float)(double)v - start) / speed : -1f;

        Undo.RecordObject(move, "Import Move Markers");

        if (move.clip == null && clips.TryGetValue(name, out var clip)) move.clip = clip;

        // finestre: hit, hit2, hit3, ... finche' ci sono
        var windows = new List<HitWindow>();
        for (int i = 1; ; i++)
        {
            string prefix = i == 1 ? "hit" : $"hit{i}";
            float s = Frame(prefix + "_start"), e = Frame(prefix + "_end");
            if (s < 0f && e < 0f) break;
            if (s < 0f || e < 0f) { report.AppendLine($"{name}: {prefix}_start o {prefix}_end mancante."); break; }
            var slot = i - 1 < move.hitWindows.Length ? move.hitWindows[i - 1].slot
                     : move.hitWindows.Length > 0 ? move.hitWindows[0].slot : HitboxSlot.Weapon;
            windows.Add(new HitWindow { slot = slot, startFrame = s, endFrame = e });
        }
        if (windows.Count > move.hitWindows.Length && move.hitWindows.Length == 0 && windows.Count > 0)
            report.AppendLine($"{name}: finestre nuove senza hitbox scelta, messe su Weapon: controllare.");
        move.hitWindows = windows.ToArray();

        move.comboFrame = Frame("combo");
        move.cancelFrame = Frame("cancel");
        move.parryStartFrame = Frame("parry_start");
        move.parryEndFrame = Frame("parry_end");
        move.iframeStartFrame = Frame("iframe_start");
        move.iframeEndFrame = Frame("iframe_end");
        move.endFrame = (Num(action, "frame_end", start) - start) / speed;

        // marker obbligatori per tipo di mossa, come nello script di Blender
        string[] required = move.kind switch
        {
            MoveKind.Attack => new[] { "hit_start", "hit_end" },
            MoveKind.Parry => new[] { "parry_start", "parry_end" },
            MoveKind.Dodge => new[] { "iframe_start", "iframe_end" },
            _ => new string[0],
        };
        var missing = required.Where(r => !markers.ContainsKey(r)).ToArray();
        if (missing.Length > 0) report.AppendLine($"{name}: mancano i marker {string.Join(", ", missing)}.");

        EditorUtility.SetDirty(move);
        report.AppendLine($"{name}: " +
            string.Join(" ", move.hitWindows.Select(w => $"{w.slot} {w.startFrame:0.#}-{w.endFrame:0.#}")) +
            $" combo {Fmt(move.comboFrame)} cancel {Fmt(move.cancelFrame)}" +
            (move.kind == MoveKind.Parry ? $" parata {Fmt(move.parryStartFrame)}-{Fmt(move.parryEndFrame)}" : "") +
            (move.kind == MoveKind.Dodge ? $" iframe {Fmt(move.iframeStartFrame)}-{Fmt(move.iframeEndFrame)}" : "") +
            $" fine {move.endFrame:0.#}");
    }

    private static string Fmt(float frame) => frame < 0f ? "-" : frame.ToString("0.#", CultureInfo.InvariantCulture);

    private static float Num(Dictionary<string, object> d, string key, float fallback) =>
        d.TryGetValue(key, out var v) && v is double x ? (float)x : fallback;

    // ---------------------------------------------------------------- JSON

    // Parser minimo: oggetti, array, numeri, stringhe, booleani, null. JsonUtility non
    // legge dizionari, e le action del JSON hanno come chiave il proprio nome.
    private static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            try { return Value(text, ref i); }
            catch { return null; }
        }

        private static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            char c = s[i];
            if (c == '{') return Object(s, ref i);
            if (c == '[') return Array(s, ref i);
            if (c == '"') return String(s, ref i);
            if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, object> Object(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++; Skip(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i);
                string key = String(s, ref i);
                Skip(s, ref i); i++;           // ':'
                d[key] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i++] == '}') return d;   // altrimenti ','
            }
        }

        private static List<object> Array(string s, ref int i)
        {
            var l = new List<object>();
            i++; Skip(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i++] == ']') return l;
            }
        }

        private static string String(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (s[i] != '"')
            {
                if (s[i] == '\\') { i++; sb.Append(s[i] switch { 'n' => '\n', 't' => '\t', _ => s[i] }); }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }
    }
}

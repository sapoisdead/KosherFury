using System;
using UnityEngine;

// Quale hitbox apre una finestra. Nominate sull'arto, come gli animation event di
// oggi: un gomito riusa RightHand.
public enum HitboxSlot { RightHand, LeftHand, RightFoot, LeftFoot, Weapon }

// Una finestra attiva della mossa: da startFrame incluso a endFrame escluso.
//
// I frame sono a 60 Hz e contano il tempo di GIOCO dall'inizio dell'azione, cioe'
// gia' divisi per la velocita' della clip. Con le clip nuove (speed 1) coincidono
// con i frame di Blender; con quelle vecchie (25 fps, accelerate) coincidono con i
// numeri del Frame Data Report. Possono essere frazionari per le clip vecchie, i cui
// eventi non cadono su frame interi.
[Serializable]
public struct HitWindow
{
    public HitboxSlot slot;
    public float startFrame;
    public float endFrame;
}

// PROTOTIPO (Fase A.2): la descrizione minima di un'azione per provare l'ActionPlayer.
// In A.3 diventa il MoveData del piano (asset, combo, cancel, impatto, lunge).
[Serializable]
public class ActionSpec
{
    public string name;
    public AnimationClip clip;
    [Tooltip("Velocita' di riproduzione della clip. 1 per le clip nuove; le vecchie vanno accelerate come nel controller (es. 1.96).")]
    public float speed = 1f;
    [Tooltip("Crossfade d'ingresso, in secondi.")]
    public float fadeIn = 0.05f;
    [Tooltip("Crossfade d'uscita verso la locomozione, in secondi, preso dalla coda della clip.")]
    public float fadeOut = 0.12f;
    [Tooltip("Se vero l'azione e' un attacco: alimenta CombatState (Startup -> Active -> Recovery).")]
    public bool isAttack = true;
    [Tooltip("Se vero lo spostamento della clip arriva al personaggio come root motion (scatti, schivate).")]
    public bool rootMotion;
    [Tooltip("Moltiplica il danno base di chi tira il colpo (l'arma, per il player).")]
    public float damageMultiplier = 1f;
    public HitWindow[] windows = Array.Empty<HitWindow>();

    // Durata in secondi di gioco
    public float Duration => clip != null && speed > 0f ? clip.length / speed : 0f;
}

// Chi possiede le hitbox e il danno base. Il player lo risolve con le hitbox degli
// arti e l'arma in mano; un nemico migrato in Fase C fornira' le sue.
public interface IHitboxSource
{
    Hitbox GetHitbox(HitboxSlot slot);
    float BaseDamage { get; }
}

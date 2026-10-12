using System;
using UnityEngine;

// Tipi comuni del sistema d'azioni. Generici: valgono per il player e, dalla Fase C,
// per i nemici migrati.

// Quale hitbox apre una finestra. Nominate sull'arto, come gli animation event di
// oggi: un gomito riusa RightHand.
public enum HitboxSlot { RightHand, LeftHand, RightFoot, LeftFoot, Weapon }

// Che cosa fa una mossa: decide come la tratta la macchina a stati.
public enum MoveKind { Attack, Parry, Dodge, Reaction }

// Come reagisce chi viene colpito. Per ora e' solo un dato: la reazione del
// bersaglio e' ancora la clip unica di oggi (Take_hit). Servira' con le hit reaction nuove.
public enum ReactionType { Light, Heavy, Launch, Knockdown }

public enum DodgeDirection { Forward, Back, Left, Right }

// Una finestra attiva della mossa: da startFrame incluso a endFrame escluso, in frame
// a 60 Hz di tempo di gioco dall'inizio della mossa (cioe' gia' divisi per la velocita'
// della clip). Con le clip nuove (speed 1) coincidono con i frame di Blender. Possono
// essere frazionari per le clip vecchie, i cui eventi non cadono su frame interi.
[Serializable]
public struct HitWindow
{
    public HitboxSlot slot;
    public float startFrame;
    public float endFrame;
}

// Chi possiede le hitbox e il danno base. Il player lo risolve con le hitbox degli
// arti e l'arma in mano; un nemico migrato fornira' le sue.
public interface IHitboxSource
{
    Hitbox GetHitbox(HitboxSlot slot);
    float BaseDamage { get; }
}

// Chi sposta fisicamente il personaggio: CharacterController per il player,
// NavMeshAgent per i nemici. Il lunge passa da qui, cosi' rispetta le collisioni.
public interface IActorMotor
{
    void MoveBy(Vector3 delta);
}

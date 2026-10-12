using UnityEngine;

// Le mosse di un'arma (o di un nemico). WeaponData punta al proprio Moveset: spada e
// coltello hanno moveset distinti. Un campo vuoto vuol dire "questa arma non lo fa".
//
// La locomozione non sta ancora qui: finche' esiste il controller di oggi la gestisce
// lui (Fase A.4).
[CreateAssetMenu(fileName = "Moveset_", menuName = "KosherFury/Combat/Moveset")]
public class Moveset : ScriptableObject
{
    [Tooltip("Primo colpo della catena leggera; i successivi si seguono con nextLight.")]
    public MoveData light;
    public MoveData heavy;
    public MoveData parry;
    [Tooltip("Contrattacco dopo una parata riuscita. Vuoto = la catena leggera normale.")]
    public MoveData riposte;

    [Header("Schivate")]
    public MoveData dodgeForward;
    public MoveData dodgeBack;
    public MoveData dodgeLeft;
    public MoveData dodgeRight;

    [Header("Reazioni")]
    [Tooltip("Reazione al colpo. Vuoto = resta quella del controller di oggi, e lo stordimento dura quanto Health.")]
    public MoveData hitReaction;

    public MoveData Dodge(DodgeDirection direction) => direction switch
    {
        DodgeDirection.Forward => dodgeForward,
        DodgeDirection.Back    => dodgeBack,
        DodgeDirection.Left    => dodgeLeft,
        DodgeDirection.Right   => dodgeRight,
        _ => null,
    };
}

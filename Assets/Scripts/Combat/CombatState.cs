using UnityEngine;

// Le tre fasi in cui si divide un attacco. Sono la stessa suddivisione che usano
// i picchiaduro: leggerle permette di distinguere un colpo "letto" da uno subito.
public enum AttackPhase
{
    None,      // non sta attaccando
    Startup,   // colpo partito, hitbox non ancora aperta: e' qui che lo si anticipa
    Active,    // hitbox aperta: i frame in cui il colpo fa male
    Recovery   // hitbox chiusa ma animazione non finita: e' qui che lo si punisce
}

// Espone a che punto del proprio attacco e' un personaggio.
//
// La fase NON e' descritta da tempi propri: viene mossa dagli stessi animation
// event che aprono e chiudono le hitbox. Se sposti un evento nella finestra
// Animation, la fase si sposta con lui e non c'e' niente da tenere allineato.
public class CombatState : MonoBehaviour
{
    public AttackPhase Phase { get; private set; } = AttackPhase.None;

    public bool IsAttacking => Phase != AttackPhase.None;

    // Sui frame attivi si incassa il danno ma non si viene interrotti: e' cio' che
    // rende possibili gli scambi colpo su colpo invece del solo "chi parte prima vince".
    public bool IsArmored => Phase == AttackPhase.Active;

    // Colpo gia' esaurito ma animazione ancora in corso: non puo' reagire.
    public bool IsPunishable => Phase == AttackPhase.Recovery;

    // Cambia a ogni nuovo attacco. Serve a chi osserva per reagire UNA volta per
    // colpo: senza un'identita' del colpo, un'IA che decide se schivare tirerebbe il
    // dado a ogni frame e finirebbe per schivare sempre.
    public int AttackId { get; private set; }

    private int currentStateHash;

    // Chiamato ogni frame dall'animator del personaggio. Il cambio di stato azzera
    // la fase: serve per le combo, dove si passa da un attacco all'altro senza mai
    // uscire dagli stati d'attacco, e senza questo il secondo colpo resterebbe
    // marcato come Recovery del primo.
    public void UpdatePhase(bool inAttackState, int stateHash)
    {
        if (!inAttackState)
        {
            Phase = AttackPhase.None;
            currentStateHash = 0;
            return;
        }

        if (stateHash != currentStateHash)
        {
            currentStateHash = stateHash;
            Phase = AttackPhase.Startup;
            AttackId++;
        }
        // dentro allo stesso stato la fase la muovono gli animation event
    }

    public void OpenActiveWindow()  => Phase = AttackPhase.Active;

    public void CloseActiveWindow()
    {
        if (Phase == AttackPhase.Active)
            Phase = AttackPhase.Recovery;
    }
}

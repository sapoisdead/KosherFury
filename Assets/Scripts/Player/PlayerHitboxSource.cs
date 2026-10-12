using UnityEngine;

// Le hitbox del player per l'ActionPlayer: quelle degli arti, trovate per nome come fa
// PlayerAnimator, e quella dell'arma in mano, che cambia a ogni cambio d'arma e quindi
// si chiede a WeaponManager ogni volta. Il danno base e' quello dell'arma equipaggiata.
[DisallowMultipleComponent]
public class PlayerHitboxSource : MonoBehaviour, IHitboxSource
{
    [SerializeField] private Hitbox rightHand;
    [SerializeField] private Hitbox leftHand;
    [SerializeField] private Hitbox rightFoot;
    [SerializeField] private Hitbox leftFoot;

    private WeaponManager weaponManager;

    private void Awake()
    {
        weaponManager = GetComponent<WeaponManager>();

        foreach (var hb in GetComponentsInChildren<Hitbox>(true))
        {
            bool isRight = hb.name.Contains("Right");
            bool isFoot  = hb.name.Contains("Kick") || hb.name.Contains("Foot");

            if (isFoot && isRight)       { if (rightFoot == null) rightFoot = hb; }
            else if (isFoot && !isRight) { if (leftFoot == null)  leftFoot = hb; }
            else if (isRight)            { if (rightHand == null) rightHand = hb; }
            else                         { if (leftHand == null)  leftHand = hb; }
        }
    }

    public Hitbox GetHitbox(HitboxSlot slot) => slot switch
    {
        HitboxSlot.RightHand => rightHand,
        HitboxSlot.LeftHand  => leftHand,
        HitboxSlot.RightFoot => rightFoot,
        HitboxSlot.LeftFoot  => leftFoot,
        HitboxSlot.Weapon    => weaponManager != null ? weaponManager.CurrentHitbox : null,
        _ => null,
    };

    public float BaseDamage =>
        weaponManager != null && weaponManager.CurrentWeapon != null ? weaponManager.CurrentWeapon.damage : 0f;
}

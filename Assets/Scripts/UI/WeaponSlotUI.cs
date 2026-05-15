using UnityEngine;
using UnityEngine.UI;

public class WeaponSlotUI : MonoBehaviour
{
    [SerializeField] private Image weaponIcon;

    private WeaponManager wm;

    void Start()
    {
        wm = PlayerManager.Instance.PlayerTransform.GetComponent<WeaponManager>();
        wm.OnWeaponChanged += UpdateDisplay;
        UpdateDisplay();
    }

    void OnDestroy()
    {
        if (wm != null)
            wm.OnWeaponChanged -= UpdateDisplay;
    }

    void UpdateDisplay()
    {
        weaponIcon.sprite = wm.CurrentWeapon?.iconSprite;
    }
}

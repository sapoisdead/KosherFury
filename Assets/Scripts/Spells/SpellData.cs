using UnityEngine;

[CreateAssetMenu(fileName = "SpellData", menuName = "KosherFury/SpellData")]
public class SpellData : ScriptableObject
{
    public string spellName;
    public Sprite icon;

    public float manaCost;
}

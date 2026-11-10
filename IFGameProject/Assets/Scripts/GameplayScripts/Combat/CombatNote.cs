using UnityEngine;

[System.Serializable]
public class CombatNote
{
    public double hitTime;          // seconds from chart start
    public float telegraphLead = 0.6f;
    public NoteType type;
    public AttackDirection direction;
    public int damage = 10;
    [Tooltip("Shield lost when this attack is blocked, separate from its damage. -1 uses the Response Rules default for its type.")]
    [Min(-1)] public int shieldDamage = -1;
    public string animTrigger;      // enemy wind-up animation
    [Tooltip("Can be parried by a well-timed swing matching its direction: Right Swing for Right, Left Swing for Left, Rising Swing for Up, Overhead Swing for Center.")]
    public bool canBeParried = true;

    [Header("Break")]
    [Tooltip("A successful parry of this attack interrupts the chart and staggers the enemy.")]
    public bool canParryBreak = true;
    [Tooltip("A successful dodge of this attack interrupts the chart and staggers the enemy.")]
    public bool canDodgeBreak;
    [Tooltip("A successful block of this attack interrupts the chart and staggers the enemy.")]
    public bool canBlockBreak;

    public double TelegraphTime => hitTime - telegraphLead;

    public bool BreaksOn(CombatAction action)
    {
        if (action.IsSwing()) return canParryBreak;

        switch (action)
        {
            case CombatAction.DodgeLeft:
            case CombatAction.DodgeRight: return canDodgeBreak;
            case CombatAction.Block: return canBlockBreak;
            default: return false;
        }
    }
}

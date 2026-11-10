public enum NoteType
{
    Slash,
    Overhead,
    Thrust,
    Heavy,
    Unblockable,
    Feint
}

public enum AttackDirection
{
    Left,
    Right,
    Up,
    Center
}

public enum CombatAction
{
    Block,
    DodgeLeft,
    DodgeRight,
    RightSwing,
    LeftSwing,
    RisingSwing,
    OverheadSwing
}

public static class CombatActionExtensions
{
    /// <summary>Swings parry attacks from their direction while the enemy is attacking, and hit it while it's staggered.</summary>
    public static bool IsSwing(this CombatAction action) => action.ParriesDirection().HasValue;

    public static AttackDirection? ParriesDirection(this CombatAction action)
    {
        switch (action)
        {
            case CombatAction.RightSwing: return AttackDirection.Right;
            case CombatAction.LeftSwing: return AttackDirection.Left;
            case CombatAction.RisingSwing: return AttackDirection.Up;
            case CombatAction.OverheadSwing: return AttackDirection.Center;
            default: return null;
        }
    }
}

public enum Judgment
{
    Perfect,
    Good,
    Miss,
    WrongAction
}

public enum ResponseEffect
{
    None,
    Dodged,
    Blocked,
    Parried
}

public enum PlayerCombatState
{
    Idle,
    Blocking,
    LoweringGuard,
    DodgingLeft,
    DodgingRight,
    Parrying,
    Attacking,
    Hurt,
    GuardBroken,
    Recovering,
    Dead
}

public enum EnemyCombatState
{
    Idle,
    Attacking,
    Staggered,
    Defeated
}

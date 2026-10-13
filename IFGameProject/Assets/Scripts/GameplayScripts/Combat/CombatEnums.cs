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
    Parry,
    Attack
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
    Staggered
}

public enum EnemyCombatState
{
    Idle,
    Attacking,
    Staggered,
    Defeated
}

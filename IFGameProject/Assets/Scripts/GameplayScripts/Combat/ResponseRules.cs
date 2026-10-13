using UnityEngine;

public struct ResponseResult
{
    public bool valid;
    public ResponseEffect effect;
    public float shieldCost;

    public static ResponseResult Invalid => new ResponseResult { valid = false, effect = ResponseEffect.None };

    public static ResponseResult Valid(ResponseEffect effect, float shieldCost = 0f) =>
        new ResponseResult { valid = true, effect = effect, shieldCost = shieldCost };
}

[CreateAssetMenu(fileName = "ResponseRules", menuName = "Combat/Response Rules")]
public class ResponseRules : ScriptableObject
{
    [Header("Block")]
    [Min(0f)] public float blockShieldCost = 10f;
    public bool heavyIsBlockable = true;
    [Min(0f)] public float heavyBlockShieldCost = 30f;

    [Header("Feint")]
    [Tooltip("If true, any reaction to a feint counts as a WrongAction. If false, reactions to feints are harmless.")]
    public bool punishFeintReactions = true;

    public ResponseResult Evaluate(CombatAction action, NoteType type, AttackDirection direction)
    {
        if (type == NoteType.Feint)
        {
            return punishFeintReactions ? ResponseResult.Invalid : ResponseResult.Valid(ResponseEffect.None);
        }

        switch (action)
        {
            case CombatAction.Block:
                return EvaluateBlock(type);

            case CombatAction.DodgeLeft:
            case CombatAction.DodgeRight:
                return IsDodgeDirectionValid(action, type, direction)
                    ? ResponseResult.Valid(ResponseEffect.Dodged)
                    : ResponseResult.Invalid;

            case CombatAction.Parry:
                return type == NoteType.Heavy || type == NoteType.Unblockable
                    ? ResponseResult.Invalid
                    : ResponseResult.Valid(ResponseEffect.Staggered);

            default:
                return ResponseResult.Invalid;
        }
    }

    private ResponseResult EvaluateBlock(NoteType type)
    {
        switch (type)
        {
            case NoteType.Slash:
            case NoteType.Overhead:
            case NoteType.Thrust:
                return ResponseResult.Valid(ResponseEffect.Blocked, blockShieldCost);
            case NoteType.Heavy:
                return heavyIsBlockable
                    ? ResponseResult.Valid(ResponseEffect.Blocked, heavyBlockShieldCost)
                    : ResponseResult.Invalid;
            default:
                return ResponseResult.Invalid;
        }
    }

    private static bool IsDodgeDirectionValid(CombatAction dodge, NoteType type, AttackDirection direction)
    {
        if (type == NoteType.Overhead) return true;

        switch (direction)
        {
            case AttackDirection.Left: return dodge == CombatAction.DodgeRight;
            case AttackDirection.Right: return dodge == CombatAction.DodgeLeft;
            default: return true;
        }
    }
}

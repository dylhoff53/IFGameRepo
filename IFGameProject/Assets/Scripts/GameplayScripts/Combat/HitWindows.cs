using System;
using UnityEngine;

[Serializable]
public class HitWindows
{
    [Tooltip("Max seconds from impact for a Perfect.")]
    [Min(0f)] public float perfect = 0.05f;

    [Tooltip("Max seconds from impact for a Good.")]
    [Min(0f)] public float good = 0.12f;

    [Tooltip("Max seconds from impact for a Parry to land at all.")]
    [Min(0f)] public float parryWindow = 0.08f;

    public float Largest => Mathf.Max(good, parryWindow);

    public float WindowFor(CombatAction action) => action == CombatAction.Parry ? parryWindow : good;

    public bool IsInWindow(double offset, CombatAction action) => Math.Abs(offset) <= WindowFor(action);

    /// <summary>Timing judgment for an input made <paramref name="offset"/> seconds from impact (negative = early).</summary>
    public Judgment Evaluate(double offset, CombatAction action)
    {
        double abs = Math.Abs(offset);
        if (abs > WindowFor(action)) return Judgment.Miss;
        return abs <= Mathf.Min(perfect, WindowFor(action)) ? Judgment.Perfect : Judgment.Good;
    }
}

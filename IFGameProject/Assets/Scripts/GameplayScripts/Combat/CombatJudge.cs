using System;
using System.Collections.Generic;

/// <summary>A note that has been telegraphed and is waiting to be answered or to expire.</summary>
public class ActiveNote
{
    public CombatNote Note { get; }

    /// <summary>Absolute impact time on the <see cref="CombatClock"/>, not the chart-relative hitTime.</summary>
    public double HitTime { get; }

    public bool Resolved { get; internal set; }

    public ActiveNote(CombatNote note, double hitTime)
    {
        Note = note;
        HitTime = hitTime;
    }
}

public struct JudgeResult
{
    public ActiveNote note;

    /// <summary>Null when the note expired without any input.</summary>
    public CombatAction? action;

    public Judgment judgment;
    public ResponseResult response;

    /// <summary>Input time minus impact time, in seconds. Negative means early.</summary>
    public double offset;

    public bool Success => judgment == Judgment.Perfect || judgment == Judgment.Good;

    public bool PlayerHit => !Success && note.Note.type != NoteType.Feint;
}

/// <summary>
/// Matches player actions against incoming notes. Has no Unity dependencies beyond the data it is given,
/// so it can be driven by any clock and unit tested directly.
/// </summary>
public class CombatJudge
{
    private readonly List<ActiveNote> active = new List<ActiveNote>();

    public HitWindows Windows { get; set; }
    public ResponseRules Rules { get; set; }

    public IReadOnlyList<ActiveNote> ActiveNotes => active;

    public event Action<JudgeResult> OnNoteJudged;

    /// <summary>Fired when an action is submitted with no note in range.</summary>
    public event Action<CombatAction, double> OnWhiff;

    public CombatJudge(HitWindows windows, ResponseRules rules)
    {
        Windows = windows ?? throw new ArgumentNullException(nameof(windows));
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public ActiveNote AddNote(CombatNote note, double absoluteHitTime)
    {
        var activeNote = new ActiveNote(note, absoluteHitTime);

        int index = active.Count;
        while (index > 0 && active[index - 1].HitTime > absoluteHitTime) index--;
        active.Insert(index, activeNote);

        return activeNote;
    }

    public bool Submit(CombatAction action, double time) => Submit(action, time, out _);

    /// <summary>
    /// Answers the earliest unresolved note in range. Returns false (a whiff) if no note is in range.
    /// A mistimed or invalid answer still consumes the note.
    /// </summary>
    public bool Submit(CombatAction action, double time, out JudgeResult result)
    {
        ActiveNote target = FindTarget(time);
        if (target == null)
        {
            result = default;
            OnWhiff?.Invoke(action, time);
            return false;
        }

        double offset = time - target.HitTime;
        ResponseResult response = Rules.Evaluate(action, target.Note.type, target.Note.direction);

        Judgment judgment;
        if (!response.valid) judgment = Judgment.WrongAction;
        else judgment = Windows.Evaluate(offset, action);

        result = new JudgeResult
        {
            note = target,
            action = action,
            judgment = judgment,
            response = response,
            offset = offset
        };

        Resolve(target, result);
        return true;
    }

    /// <summary>Resolves every note whose window has fully passed as a Miss.</summary>
    public void Tick(double now)
    {
        double window = Windows.Largest;

        while (active.Count > 0 && now - active[0].HitTime > window)
        {
            ActiveNote expired = active[0];
            Resolve(expired, new JudgeResult
            {
                note = expired,
                action = null,
                judgment = Judgment.Miss,
                response = ResponseResult.Invalid,
                offset = now - expired.HitTime
            });
        }
    }

    /// <summary>Drops all pending notes without judging them, e.g. when the enemy is staggered.</summary>
    public void Clear()
    {
        foreach (ActiveNote note in active) note.Resolved = true;
        active.Clear();
    }

    private ActiveNote FindTarget(double time)
    {
        double window = Windows.Largest;

        foreach (ActiveNote note in active)
        {
            if (Math.Abs(time - note.HitTime) <= window) return note;
            if (note.HitTime - time > window) break;
        }

        return null;
    }

    private void Resolve(ActiveNote note, JudgeResult result)
    {
        note.Resolved = true;
        active.Remove(note);
        OnNoteJudged?.Invoke(result);
    }
}

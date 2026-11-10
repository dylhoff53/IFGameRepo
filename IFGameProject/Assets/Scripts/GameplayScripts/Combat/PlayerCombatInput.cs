using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Turns Combat action map callbacks into <see cref="CombatJudge"/> submissions.
/// Wire the On* methods up through a PlayerInput component set to Invoke Unity Events.
/// </summary>
public class PlayerCombatInput : MonoBehaviour
{
    [SerializeField] private CombatClock clock;

    [Tooltip("Seconds a press is held and retried if an attack is incoming but not in range yet. Presses with nothing incoming, like swings during a stagger, go through immediately.")]
    [Min(0f)] [SerializeField] private float inputBuffer = 0.08f;

    [SerializeField] private bool logInputs;

    /// <summary>Set by whoever owns the judge, e.g. the enemy controller running the current fight.</summary>
    public CombatJudge Judge { get; set; }

    /// <summary>Optional gate checked on each press, e.g. for stamina or lockouts. Rejected presses are dropped, not buffered.</summary>
    public Func<CombatAction, bool> CanAct { get; set; }

    /// <summary>Fired for every press that reaches the judge, just before it is judged, whether it lands on a note or whiffs.</summary>
    public event Action<CombatAction, double> OnActionSubmitted;

    /// <summary>Fired when the player lets go of a held action, but not when the hold was cancelled by <see cref="CancelHold"/>.</summary>
    public event Action<CombatAction> OnHoldReleased;

    public bool IsHolding { get; private set; }

    private bool hasBuffered;
    private CombatAction bufferedAction;
    private double bufferedTime;
    private CombatAction heldAction;

    /// <summary>Block is held rather than tapped, so the action needs to report release (a plain Button with no Press Only interaction).</summary>
    public void OnBlock(InputAction.CallbackContext context) => HandleHold(context, CombatAction.Block);

    public void OnDodgeLeft(InputAction.CallbackContext context) => Handle(context, CombatAction.DodgeLeft);

    public void OnDodgeRight(InputAction.CallbackContext context) => Handle(context, CombatAction.DodgeRight);

    public void OnRightSwing(InputAction.CallbackContext context) => Handle(context, CombatAction.RightSwing);

    public void OnLeftSwing(InputAction.CallbackContext context) => Handle(context, CombatAction.LeftSwing);

    public void OnRisingSwing(InputAction.CallbackContext context) => Handle(context, CombatAction.RisingSwing);

    public void OnOverheadSwing(InputAction.CallbackContext context) => Handle(context, CombatAction.OverheadSwing);

    public void ClearBuffer() => hasBuffered = false;

    /// <summary>Drops the current hold, e.g. when the guard breaks. The button has to be pressed again to resume.</summary>
    public void CancelHold()
    {
        if (!IsHolding) return;
        IsHolding = false;
        Judge?.EndHold();
    }

    private void Awake()
    {
        if (clock == null) clock = FindFirstObjectByType<CombatClock>();
        if (clock == null) Debug.LogError($"{nameof(PlayerCombatInput)} needs a {nameof(CombatClock)} in the scene.", this);
    }

    private void Update()
    {
        if (!hasBuffered || Judge == null || clock == null) return;

        double now = clock.Now;
        if (HasNoteInRange(now))
        {
            hasBuffered = false;
            Submit(bufferedAction, now);
        }
        else if (now - bufferedTime > inputBuffer)
        {
            hasBuffered = false;
            Submit(bufferedAction, bufferedTime);
        }
    }

    private void Handle(InputAction.CallbackContext context, CombatAction action)
    {
        if (!context.performed || clock == null) return;

        double now = clock.Now;
        if (logInputs) Debug.Log($"[Combat] {action} pressed at {now:F3}");
        if (Judge == null) return;
        if (CanAct != null && !CanAct(action)) return;

        bool bufferable = inputBuffer > 0f && Judge.ActiveNotes.Count > 0;
        if (!bufferable || HasNoteInRange(now))
        {
            hasBuffered = false;
            Submit(action, now);
            return;
        }

        if (hasBuffered) Submit(bufferedAction, bufferedTime);

        hasBuffered = true;
        bufferedAction = action;
        bufferedTime = now;
    }

    private void HandleHold(InputAction.CallbackContext context, CombatAction action)
    {
        if (context.canceled)
        {
            if (!IsHolding || heldAction != action) return;
            CancelHold();
            OnHoldReleased?.Invoke(action);
            return;
        }

        if (!context.performed || clock == null || IsHolding) return;

        double now = clock.Now;
        if (logInputs) Debug.Log($"[Combat] {action} held at {now:F3}");
        if (Judge == null) return;
        if (CanAct != null && !CanAct(action)) return;

        if (hasBuffered)
        {
            hasBuffered = false;
            Submit(bufferedAction, bufferedTime);
        }

        IsHolding = true;
        heldAction = action;
        OnActionSubmitted?.Invoke(action, now);
        Judge.BeginHold(action, now);
    }

    private void Submit(CombatAction action, double time)
    {
        OnActionSubmitted?.Invoke(action, time);
        Judge?.Submit(action, time);
    }

    private bool HasNoteInRange(double time)
    {
        double window = Judge.Windows.Largest;
        foreach (ActiveNote note in Judge.ActiveNotes)
        {
            if (Math.Abs(time - note.HitTime) <= window) return true;
            if (note.HitTime - time > window) break;
        }
        return false;
    }
}

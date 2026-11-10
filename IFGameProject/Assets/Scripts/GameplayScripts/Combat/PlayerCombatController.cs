using System;
using UnityEngine;

/// <summary>
/// The player's side of a fight: health, shield durability and stamina, defensive animations,
/// attack combos during the enemy's stagger window, and judgment feedback messages.
/// </summary>
public class PlayerCombatController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CombatClock clock;
    [SerializeField] private EnemyCombatController enemy;
    [SerializeField] private PlayerCombatInput input;
    [SerializeField] private Animator animator;

    [Header("Health")]
    [Min(1)] [SerializeField] private int maxHealth = 100;

    [Header("Shield")]
    [Min(0f)] [SerializeField] private float maxShield = 100f;
    [Tooltip("Shield cost multiplier for a Perfect block, rounded up, never below 1.")]
    [Range(0f, 1f)] [SerializeField] private float perfectBlockCostMultiplier = 0.5f;
    [Tooltip("Fraction of an attack's damage taken when it is blocked without enough shield left.")]
    [Range(0f, 1f)] [SerializeField] private float guardBreakDamageMultiplier = 0.75f;

    [Header("Stamina")]
    [Min(0f)] [SerializeField] private float maxStamina = 100f;
    [Min(0f)] [SerializeField] private float dodgeCost = 15f;
    [Min(0f)] [SerializeField] private float blockCost;
    [Tooltip("Extra stamina lost when a dodge is pressed during the enemy's attacks with no attack in range.")]
    [Min(0f)] [SerializeField] private float whiffCost = 5f;
    [Tooltip("Fraction of the action's stamina cost refunded on a Perfect.")]
    [Range(0f, 1f)] [SerializeField] private float perfectStaminaRefund = 1f;
    [Min(0f)] [SerializeField] private float staminaRegenPerSecond = 25f;
    [Tooltip("Combat seconds after spending stamina before it starts regenerating.")]
    [Min(0f)] [SerializeField] private float staminaRegenDelay = 0.5f;

    [Header("Lockouts")]
    [Tooltip("Combat seconds the player can't act after a defensive whiff. Stops button mashing.")]
    [Min(0f)] [SerializeField] private float whiffLockout = 0.25f;
    [Tooltip("Combat seconds the player can't act after scratching an enemy that isn't staggered.")]
    [Min(0f)] [SerializeField] private float scratchLockout = 0.3f;
    [Tooltip("Combat seconds the player can't act after reacting to a feint.")]
    [Min(0f)] [SerializeField] private float feintLockout = 0.5f;

    [Header("State Durations")]
    [Tooltip("Blocking lasts as long as the button is held. This is the combat seconds after letting go before the player can act again.")]
    [Min(0f)] [SerializeField] private float blockReleaseDelay = 0.15f;
    [Min(0f)] [SerializeField] private float dodgeDuration = 0.35f;
    [Tooltip("A swing while the enemy is attacking.")]
    [Min(0f)] [SerializeField] private float parryDuration = 0.3f;
    [Tooltip("A swing while the enemy is staggered. Also limits how fast the player can hit.")]
    [Min(0f)] [SerializeField] private float attackDuration = 0.25f;
    [Min(0f)] [SerializeField] private float hurtDuration = 0.2f;
    [Min(0f)] [SerializeField] private float guardBrokenDuration = 0.5f;
    [Tooltip("Combat seconds before an action ends during which a new press is accepted.")]
    [Min(0f)] [SerializeField] private float cancelWindow = 0.08f;

    [Header("Attacks")]
    [Min(0)] [SerializeField] private int attackDamage = 5;
    [Tooltip("Fraction of base damage dealt by a swing that hits an enemy that isn't staggered. Rounded up, at least 1, never boosted by combos.")]
    [Range(0f, 1f)] [SerializeField] private float scratchDamageFraction = 0.1f;
    [Tooltip("Max combat seconds between hits for the combo to continue.")]
    [Min(0f)] [SerializeField] private float comboWindow = 0.6f;
    [Tooltip("Extra damage per combo step, as a fraction of base damage.")]
    [Min(0f)] [SerializeField] private float comboDamageBonus = 0.25f;
    [Tooltip("Combo steps past this no longer add damage.")]
    [Min(1)] [SerializeField] private int maxComboSteps = 5;

    [Header("Animator Triggers")]
    [SerializeField] private string blockTrigger = "Block";
    [SerializeField] private string dodgeLeftTrigger = "DodgeLeft";
    [SerializeField] private string dodgeRightTrigger = "DodgeRight";
    [SerializeField] private string rightSwingTrigger = "RightSwing";
    [SerializeField] private string leftSwingTrigger = "LeftSwing";
    [SerializeField] private string risingSwingTrigger = "RisingSwing";
    [SerializeField] private string overheadSwingTrigger = "OverheadSwing";
    [SerializeField] private string hurtTrigger = "Hurt";
    [SerializeField] private string guardBreakTrigger = "GuardBreak";
    [SerializeField] private string deathTrigger = "Death";

    [Header("Feedback Colors")]
    [SerializeField] private Color perfectColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color goodColor = Color.white;
    [SerializeField] private Color failColor = new Color(1f, 0.3f, 0.3f);

    public int Health { get; private set; }
    public int MaxHealth => maxHealth;
    public float Shield { get; private set; }
    public float MaxShield => maxShield;
    public float Stamina { get; private set; }
    public float MaxStamina => maxStamina;
    public int Combo { get; private set; }
    public bool IsDead { get; private set; }
    public PlayerCombatState State { get; private set; }

    /// <summary>Combat seconds until the current state returns to Idle. Zero while Idle or Dead.</summary>
    public float StateTimeRemaining =>
        clock == null || State == PlayerCombatState.Idle || State == PlayerCombatState.Dead
            ? 0f
            : (float)Math.Max(0, stateEndTime - clock.Now);

    public event Action<PlayerCombatState> OnStateChanged;
    public event Action<int, int> OnHealthChanged;
    public event Action<float, float> OnShieldChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action<int> OnComboChanged;
    public event Action OnGuardBroken;
    public event Action OnDefeated;

    /// <summary>Fired for every feedback message, so other UI or audio can react even without a text component.</summary>
    public event Action<string, Color> OnFeedback;

    private CombatJudge judge;
    private double lastClockTime;
    private double stateEndTime;
    private double lastComboHitTime;
    private double staminaRegenStartTime;
    private float lastActionCost;

    private void Awake()
    {
        if (clock == null) clock = FindFirstObjectByType<CombatClock>();
        if (clock == null) Debug.LogError($"{nameof(PlayerCombatController)} needs a {nameof(CombatClock)} in the scene.", this);

        if (enemy == null) enemy = FindFirstObjectByType<EnemyCombatController>();
        if (input == null) input = FindFirstObjectByType<PlayerCombatInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        ResetStats();
    }

    private void Start()
    {
        if (enemy != null)
        {
            judge = enemy.Judge;
            enemy.OnRecovered += ResetCombo;
            enemy.OnDefeated += HandleEnemyDefeated;
        }
        else
        {
            Debug.LogWarning($"{nameof(PlayerCombatController)} has no {nameof(EnemyCombatController)} to fight.", this);
        }

        if (judge != null)
        {
            judge.OnNoteJudged += HandleNoteJudged;
            judge.OnWhiff += HandleWhiff;
        }

        if (input != null)
        {
            input.CanAct = CanAct;
            input.OnActionSubmitted += HandleActionSubmitted;
            input.OnHoldReleased += HandleHoldReleased;
        }
    }

    private void OnDestroy()
    {
        if (enemy != null)
        {
            enemy.OnRecovered -= ResetCombo;
            enemy.OnDefeated -= HandleEnemyDefeated;
        }

        if (judge != null)
        {
            judge.OnNoteJudged -= HandleNoteJudged;
            judge.OnWhiff -= HandleWhiff;
        }

        if (input != null)
        {
            if (input.CanAct == (Func<CombatAction, bool>)CanAct) input.CanAct = null;
            input.OnActionSubmitted -= HandleActionSubmitted;
            input.OnHoldReleased -= HandleHoldReleased;
        }
    }

    private void Update()
    {
        if (clock == null) return;
        double now = clock.Now;
        float dt = (float)Math.Max(0, now - lastClockTime);
        lastClockTime = now;

        if (IsDead) return;
        if (State != PlayerCombatState.Idle && now >= stateEndTime) SetState(PlayerCombatState.Idle, 0f);
        if (dt <= 0f) return;

        if (Stamina < maxStamina && now >= staminaRegenStartTime) SetStamina(Stamina + staminaRegenPerSecond * dt);
    }

    /// <summary>Restores health, shield and stamina, e.g. before a new fight.</summary>
    public void ResetStats()
    {
        IsDead = false;
        Health = maxHealth;
        Shield = maxShield;
        Stamina = maxStamina;
        Combo = 0;

        double now = clock != null ? clock.Now : 0;
        lastClockTime = now;
        staminaRegenStartTime = now;
        SetState(PlayerCombatState.Idle, 0f);

        OnHealthChanged?.Invoke(Health, maxHealth);
        OnShieldChanged?.Invoke(Shield, maxShield);
        OnStaminaChanged?.Invoke(Stamina, maxStamina);
        OnComboChanged?.Invoke(Combo);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;

        Health = Mathf.Max(0, Health - amount);
        OnHealthChanged?.Invoke(Health, maxHealth);
        ResetCombo();

        if (Health > 0)
        {
            SetTrigger(hurtTrigger);
            if (State != PlayerCombatState.GuardBroken) SetState(PlayerCombatState.Hurt, hurtDuration);
            return;
        }

        SetState(PlayerCombatState.Dead, 0f);
        IsDead = true;
        SetTrigger(deathTrigger);
        ShowFeedback("DEFEATED", failColor);
        if (enemy != null) enemy.StopFight();
        OnDefeated?.Invoke();
    }

    private bool CanAct(CombatAction action)
    {
        if (IsDead || clock == null) return false;
        if (!IsStateCancelable()) return false;

        if (Stamina < CostOf(action))
        {
            ShowFeedback("NO STAMINA", failColor);
            return false;
        }

        return true;
    }

    /// <summary>Idle, or an action state close enough to its end that an early press still counts.</summary>
    private bool IsStateCancelable()
    {
        switch (State)
        {
            case PlayerCombatState.Idle:
                return true;
            case PlayerCombatState.Blocking:
            case PlayerCombatState.DodgingLeft:
            case PlayerCombatState.DodgingRight:
            case PlayerCombatState.Parrying:
            case PlayerCombatState.Attacking:
                return StateTimeRemaining <= cancelWindow;
            default:
                return false;
        }
    }

    /// <summary>A swing is a hit on a staggered enemy, and a parry attempt otherwise.</summary>
    private bool IsHit(CombatAction action) => action.IsSwing() && enemy != null && enemy.IsVulnerable;

    private float CostOf(CombatAction action)
    {
        switch (action)
        {
            case CombatAction.Block: return blockCost;
            case CombatAction.DodgeLeft:
            case CombatAction.DodgeRight: return dodgeCost;
            default: return 0f;
        }
    }

    private void HandleActionSubmitted(CombatAction action, double time)
    {
        if (IsDead) return;

        lastActionCost = CostOf(action);
        SpendStamina(lastActionCost);
        SetTrigger(TriggerFor(action));
        SetState(StateFor(action), DurationFor(action));

        if (IsHit(action)) TryAttack();
    }

    private void TryAttack()
    {
        double now = clock.Now;

        if (Combo > 0 && now - lastComboHitTime > comboWindow) Combo = 0;

        int step = Mathf.Min(Combo, maxComboSteps);
        int damage = Mathf.RoundToInt(attackDamage * (1f + comboDamageBonus * step));
        if (!enemy.TakeDamage(damage)) return;

        Combo++;
        lastComboHitTime = now;
        OnComboChanged?.Invoke(Combo);
        ShowFeedback(Combo > 1 ? $"HIT x{Combo}" : "HIT", Combo > maxComboSteps ? perfectColor : goodColor);
    }

    private void HandleWhiff(CombatAction action, double time)
    {
        if (IsDead || enemy == null) return;

        if (action.IsSwing())
        {
            if (!enemy.IsVulnerable) Scratch();
            return;
        }

        if (enemy.State != EnemyCombatState.Attacking) return;

        SpendStamina(whiffCost);
        Lockout(whiffLockout);
    }

    /// <summary>A swing with no incoming attack to parry still lands, for a small flat amount that combos don't boost.</summary>
    private void Scratch()
    {
        int damage = Mathf.Max(1, Mathf.CeilToInt(attackDamage * scratchDamageFraction));
        if (!enemy.Scratch(damage)) return;

        Lockout(scratchLockout);
        if (enemy.State != EnemyCombatState.Defeated) ShowFeedback("SCRATCH", goodColor);
    }

    private void HandleNoteJudged(JudgeResult result)
    {
        if (IsDead) return;

        CombatNote note = result.note.Note;
        bool perfect = result.judgment == Judgment.Perfect;

        if (result.Success)
        {
            if (perfect && result.action.HasValue) RefundStamina();

            switch (result.response.effect)
            {
                case ResponseEffect.Blocked:
                    ApplyBlock(note, note.shieldDamage >= 0 ? note.shieldDamage : result.response.shieldCost, perfect);
                    break;
                case ResponseEffect.Dodged:
                    ShowFeedback(perfect ? "PERFECT DODGE" : "DODGE", perfect ? perfectColor : goodColor);
                    break;
                case ResponseEffect.Parried:
                    ShowFeedback(perfect ? "PERFECT PARRY" : "PARRY", perfect ? perfectColor : goodColor);
                    break;
            }

            if (!IsDead && enemy != null && enemy.IsVulnerable && result.action.HasValue && note.BreaksOn(result.action.Value))
            {
                ShowFeedback("BREAK!", perfectColor);
            }
            return;
        }

        if (note.type == NoteType.Feint)
        {
            if (result.action.HasValue)
            {
                Lockout(feintLockout);
                ShowFeedback("FEINTED!", failColor);
            }
            return;
        }

        TakeDamage(note.damage);
        if (!IsDead) ShowFeedback(FailureText(result), failColor);
    }

    /// <summary>Drains the shield. If the cost is more than what's left, the guard breaks and most of the hit goes through.</summary>
    private void ApplyBlock(CombatNote note, float cost, bool perfect)
    {
        if (perfect && cost > 0f) cost = Mathf.Max(1, Mathf.CeilToInt(cost * perfectBlockCostMultiplier));

        if (cost <= Shield)
        {
            SetShield(Shield - cost);
            ShowFeedback(perfect ? "PERFECT BLOCK" : "BLOCK", perfect ? perfectColor : goodColor);
            return;
        }

        SetShield(0f);
        SetTrigger(guardBreakTrigger);
        OnGuardBroken?.Invoke();

        TakeDamage(Mathf.CeilToInt(note.damage * guardBreakDamageMultiplier));
        if (IsDead) return;

        SetState(PlayerCombatState.GuardBroken, guardBrokenDuration);
        ShowFeedback("GUARD BROKEN", failColor);
    }

    private static string FailureText(JudgeResult result)
    {
        if (!result.action.HasValue) return "MISS";
        if (result.judgment == Judgment.WrongAction) return "WRONG MOVE";
        return result.offset < 0 ? "TOO EARLY" : "TOO LATE";
    }

    private void HandleEnemyDefeated()
    {
        RestoreShield();
        ShowFeedback("VICTORY", perfectColor);
    }

    /// <summary>The shield doesn't regenerate during a fight; it only comes back when one ends.</summary>
    public void RestoreShield() => SetShield(maxShield);

    private void HandleHoldReleased(CombatAction action)
    {
        if (action == CombatAction.Block && State == PlayerCombatState.Blocking) SetState(PlayerCombatState.LoweringGuard, blockReleaseDelay);
    }

    private void ResetCombo()
    {
        if (Combo == 0) return;
        Combo = 0;
        OnComboChanged?.Invoke(Combo);
    }

    private void Lockout(float duration)
    {
        if (duration <= 0f) return;
        if (State == PlayerCombatState.Recovering) duration = Mathf.Max(duration, StateTimeRemaining);
        SetState(PlayerCombatState.Recovering, duration);
        if (input != null) input.ClearBuffer();
    }

    private void SetState(PlayerCombatState state, float duration)
    {
        if (IsDead) return;

        stateEndTime = (clock != null ? clock.Now : 0) + duration;
        if (state == State) return;

        if (State == PlayerCombatState.Blocking && input != null) input.CancelHold();
        State = state;
        OnStateChanged?.Invoke(State);
    }

    private PlayerCombatState StateFor(CombatAction action)
    {
        if (action.IsSwing()) return IsHit(action) ? PlayerCombatState.Attacking : PlayerCombatState.Parrying;

        switch (action)
        {
            case CombatAction.Block: return PlayerCombatState.Blocking;
            case CombatAction.DodgeLeft: return PlayerCombatState.DodgingLeft;
            case CombatAction.DodgeRight: return PlayerCombatState.DodgingRight;
            default: return PlayerCombatState.Idle;
        }
    }

    private float DurationFor(CombatAction action)
    {
        if (action.IsSwing()) return IsHit(action) ? attackDuration : parryDuration;

        switch (action)
        {
            case CombatAction.Block: return float.PositiveInfinity;
            case CombatAction.DodgeLeft:
            case CombatAction.DodgeRight: return dodgeDuration;
            default: return 0f;
        }
    }

    private void SpendStamina(float amount)
    {
        if (amount <= 0f) return;
        SetStamina(Stamina - amount);
        staminaRegenStartTime = clock.Now + staminaRegenDelay;
    }

    private void RefundStamina()
    {
        float refund = lastActionCost * perfectStaminaRefund;
        if (refund > 0f) SetStamina(Stamina + refund);
    }

    private void SetShield(float value)
    {
        value = Mathf.Clamp(value, 0f, maxShield);
        if (Mathf.Approximately(value, Shield)) return;
        Shield = value;
        OnShieldChanged?.Invoke(Shield, maxShield);
    }

    private void SetStamina(float value)
    {
        value = Mathf.Clamp(value, 0f, maxStamina);
        if (Mathf.Approximately(value, Stamina)) return;
        Stamina = value;
        OnStaminaChanged?.Invoke(Stamina, maxStamina);
    }

    private string TriggerFor(CombatAction action)
    {
        switch (action)
        {
            case CombatAction.Block: return blockTrigger;
            case CombatAction.DodgeLeft: return dodgeLeftTrigger;
            case CombatAction.DodgeRight: return dodgeRightTrigger;
            case CombatAction.RightSwing: return rightSwingTrigger;
            case CombatAction.LeftSwing: return leftSwingTrigger;
            case CombatAction.RisingSwing: return risingSwingTrigger;
            case CombatAction.OverheadSwing: return overheadSwingTrigger;
            default: return null;
        }
    }

    private void SetTrigger(string trigger)
    {
        if (animator != null && !string.IsNullOrEmpty(trigger)) animator.SetTrigger(trigger);
    }

    private void ShowFeedback(string message, Color color) => OnFeedback?.Invoke(message, color);
}

using System;
using TMPro;
using UnityEngine;

/// <summary>
/// The player's side of a fight: health, shield durability and stamina, defensive animations,
/// attack combos during the enemy's stagger window, and judgment feedback text.
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
    [Tooltip("Shield cost multiplier for a Perfect block.")]
    [Range(0f, 1f)] [SerializeField] private float perfectBlockCostMultiplier = 0.5f;
    [Min(0f)] [SerializeField] private float shieldRegenPerSecond = 5f;
    [Tooltip("Combat seconds after the last block before the shield starts regenerating.")]
    [Min(0f)] [SerializeField] private float shieldRegenDelay = 2f;

    [Header("Stamina")]
    [Min(0f)] [SerializeField] private float maxStamina = 100f;
    [Min(0f)] [SerializeField] private float dodgeCost = 15f;
    [Min(0f)] [SerializeField] private float parryCost = 10f;
    [Min(0f)] [SerializeField] private float blockCost;
    [Min(0f)] [SerializeField] private float attackCost = 5f;
    [Tooltip("Extra stamina lost when a defensive action is pressed with no attack in range.")]
    [Min(0f)] [SerializeField] private float whiffCost = 5f;
    [Tooltip("Fraction of the action's stamina cost refunded on a Perfect.")]
    [Range(0f, 1f)] [SerializeField] private float perfectStaminaRefund = 1f;
    [Min(0f)] [SerializeField] private float staminaRegenPerSecond = 25f;
    [Tooltip("Combat seconds after spending stamina before it starts regenerating.")]
    [Min(0f)] [SerializeField] private float staminaRegenDelay = 0.5f;

    [Header("Lockouts")]
    [Tooltip("Combat seconds the player can't act after a defensive whiff. Stops button mashing.")]
    [Min(0f)] [SerializeField] private float whiffLockout = 0.25f;
    [Tooltip("Combat seconds the player can't act after reacting to a feint.")]
    [Min(0f)] [SerializeField] private float feintLockout = 0.5f;

    [Header("Attacks")]
    [Min(0)] [SerializeField] private int attackDamage = 5;
    [Tooltip("Minimum combat seconds between swings.")]
    [Min(0f)] [SerializeField] private float attackCooldown = 0.15f;
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
    [SerializeField] private string parryTrigger = "Parry";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string hurtTrigger = "Hurt";
    [SerializeField] private string guardBreakTrigger = "GuardBreak";
    [SerializeField] private string deathTrigger = "Death";

    [Header("Feedback")]
    [Tooltip("Optional. Judgments like PERFECT PARRY are shown here and faded out.")]
    [SerializeField] private TMP_Text feedbackText;
    [Tooltip("Real seconds the text stays fully visible before fading.")]
    [Min(0f)] [SerializeField] private float feedbackHold = 0.5f;
    [Min(0.01f)] [SerializeField] private float feedbackFade = 0.3f;
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
    public bool IsLockedOut => clock != null && clock.Now < lockoutEndTime;

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
    private double lockoutEndTime;
    private double nextAttackTime;
    private double lastComboHitTime;
    private double shieldRegenStartTime;
    private double staminaRegenStartTime;
    private float feedbackTimer;
    private Color feedbackColor;

    private void Awake()
    {
        if (clock == null) clock = FindFirstObjectByType<CombatClock>();
        if (clock == null) Debug.LogError($"{nameof(PlayerCombatController)} needs a {nameof(CombatClock)} in the scene.", this);

        if (enemy == null) enemy = FindFirstObjectByType<EnemyCombatController>();
        if (input == null) input = FindFirstObjectByType<PlayerCombatInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (feedbackText != null) feedbackText.alpha = 0f;

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
        }
    }

    private void Update()
    {
        UpdateFeedback();

        if (clock == null) return;
        double now = clock.Now;
        float dt = (float)Math.Max(0, now - lastClockTime);
        lastClockTime = now;

        if (IsDead || dt <= 0f) return;

        if (Shield < maxShield && now >= shieldRegenStartTime) SetShield(Shield + shieldRegenPerSecond * dt);
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
        lockoutEndTime = now;
        nextAttackTime = now;
        shieldRegenStartTime = now;
        staminaRegenStartTime = now;

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
            return;
        }

        IsDead = true;
        SetTrigger(deathTrigger);
        ShowFeedback("DEFEATED", failColor);
        if (enemy != null) enemy.StopFight();
        OnDefeated?.Invoke();
    }

    private bool CanAct(CombatAction action)
    {
        if (IsDead || clock == null) return false;

        double now = clock.Now;
        if (now < lockoutEndTime) return false;
        if (action == CombatAction.Attack && now < nextAttackTime) return false;

        if (Stamina < CostOf(action))
        {
            ShowFeedback("NO STAMINA", failColor);
            return false;
        }

        return true;
    }

    private float CostOf(CombatAction action)
    {
        switch (action)
        {
            case CombatAction.Block: return blockCost;
            case CombatAction.DodgeLeft:
            case CombatAction.DodgeRight: return dodgeCost;
            case CombatAction.Parry: return parryCost;
            case CombatAction.Attack: return attackCost;
            default: return 0f;
        }
    }

    private void HandleActionSubmitted(CombatAction action, double time)
    {
        if (IsDead) return;

        SpendStamina(CostOf(action));
        SetTrigger(TriggerFor(action));

        if (action == CombatAction.Attack) TryAttack();
    }

    private void TryAttack()
    {
        double now = clock.Now;
        nextAttackTime = now + attackCooldown;

        if (enemy == null || !enemy.IsVulnerable) return;

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
        if (IsDead || action == CombatAction.Attack) return;
        if (enemy == null || enemy.State != EnemyCombatState.Attacking) return;

        SpendStamina(whiffCost);
        Lockout(whiffLockout);
    }

    private void HandleNoteJudged(JudgeResult result)
    {
        if (IsDead) return;

        CombatNote note = result.note.Note;
        bool perfect = result.judgment == Judgment.Perfect;

        if (result.Success)
        {
            if (perfect && result.action.HasValue) RefundStamina(result.action.Value);

            switch (result.response.effect)
            {
                case ResponseEffect.Blocked:
                    ApplyBlock(note, result.response.shieldCost, perfect);
                    break;
                case ResponseEffect.Dodged:
                    ShowFeedback(perfect ? "PERFECT DODGE" : "DODGE", perfect ? perfectColor : goodColor);
                    break;
                case ResponseEffect.Staggered:
                    ShowFeedback(perfect ? "PERFECT PARRY" : "PARRY", perfect ? perfectColor : goodColor);
                    break;
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

    /// <summary>Drains the shield. If the cost is more than what's left, the guard breaks and that share of the hit goes through.</summary>
    private void ApplyBlock(CombatNote note, float cost, bool perfect)
    {
        if (perfect) cost *= perfectBlockCostMultiplier;

        shieldRegenStartTime = clock.Now + shieldRegenDelay;

        if (cost <= Shield)
        {
            SetShield(Shield - cost);
            ShowFeedback(perfect ? "PERFECT BLOCK" : "BLOCK", perfect ? perfectColor : goodColor);
            return;
        }

        float overflow = cost > 0f ? (cost - Shield) / cost : 1f;
        SetShield(0f);
        SetTrigger(guardBreakTrigger);
        OnGuardBroken?.Invoke();

        TakeDamage(Mathf.CeilToInt(note.damage * overflow));
        if (!IsDead) ShowFeedback("GUARD BROKEN", failColor);
    }

    private static string FailureText(JudgeResult result)
    {
        if (!result.action.HasValue) return "MISS";
        if (result.judgment == Judgment.WrongAction) return "WRONG MOVE";
        return result.offset < 0 ? "TOO EARLY" : "TOO LATE";
    }

    private void HandleEnemyDefeated() => ShowFeedback("VICTORY", perfectColor);

    private void ResetCombo()
    {
        if (Combo == 0) return;
        Combo = 0;
        OnComboChanged?.Invoke(Combo);
    }

    private void Lockout(float duration)
    {
        if (duration <= 0f) return;
        lockoutEndTime = Math.Max(lockoutEndTime, clock.Now + duration);
        if (input != null) input.ClearBuffer();
    }

    private void SpendStamina(float amount)
    {
        if (amount <= 0f) return;
        SetStamina(Stamina - amount);
        staminaRegenStartTime = clock.Now + staminaRegenDelay;
    }

    private void RefundStamina(CombatAction action)
    {
        float refund = CostOf(action) * perfectStaminaRefund;
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
            case CombatAction.Parry: return parryTrigger;
            case CombatAction.Attack: return attackTrigger;
            default: return null;
        }
    }

    private void SetTrigger(string trigger)
    {
        if (animator != null && !string.IsNullOrEmpty(trigger)) animator.SetTrigger(trigger);
    }

    private void ShowFeedback(string message, Color color)
    {
        OnFeedback?.Invoke(message, color);
        if (feedbackText == null) return;

        feedbackText.text = message;
        feedbackColor = color;
        feedbackText.color = color;
        feedbackTimer = feedbackHold + feedbackFade;
    }

    /// <summary>Runs on unscaled time so the text stays readable through hit-stop and pauses.</summary>
    private void UpdateFeedback()
    {
        if (feedbackText == null || feedbackTimer <= 0f) return;

        feedbackTimer -= Time.unscaledDeltaTime;
        float alpha = Mathf.Clamp01(feedbackTimer / feedbackFade);
        feedbackText.color = new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, feedbackColor.a * alpha);
    }
}

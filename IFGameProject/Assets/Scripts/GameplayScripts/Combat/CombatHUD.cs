using TMPro;
using UnityEngine;

/// <summary>
/// Shows player health, shield and stamina, enemy health and stagger time, both sides' current state,
/// and the player's judgment feedback messages. Stats are read every frame, so nothing is missed between events.
/// </summary>
public class CombatHUD : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private PlayerCombatController player;
    [SerializeField] private EnemyCombatController enemy;

    [Header("Player")]
    [SerializeField] private CombatBar healthBar;
    [SerializeField] private CombatBar shieldBar;
    [SerializeField] private CombatBar staminaBar;

    [Header("Enemy")]
    [SerializeField] private CombatBar enemyHealthBar;
    [Tooltip("Optional. Shown only while the enemy is staggered, draining as the window closes.")]
    [SerializeField] private CombatBar staggerBar;

    [Header("Feedback")]
    [SerializeField] private TMP_Text feedbackText;
    [Tooltip("Real seconds the text stays fully visible before fading.")]
    [Min(0f)] [SerializeField] private float feedbackHold = 0.5f;
    [Min(0.01f)] [SerializeField] private float feedbackFade = 0.3f;
    [Tooltip("Scale the text pops in at before settling to normal size.")]
    [Min(1f)] [SerializeField] private float punchScale = 1.3f;
    [Min(0.01f)] [SerializeField] private float punchTime = 0.12f;

    [Header("State")]
    [Tooltip("Optional. Shows what the player is currently doing.")]
    [SerializeField] private TMP_Text playerStateText;
    [Tooltip("Optional. Shows what the enemy is currently doing.")]
    [SerializeField] private TMP_Text enemyStateText;
    [SerializeField] private Color idleStateColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color actionStateColor = new Color(0.4f, 0.8f, 1f);
    [SerializeField] private Color dangerStateColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private Color openingStateColor = new Color(0.3f, 1f, 0.3f);

    private float staggerDuration;
    private float feedbackTimer;
    private Color feedbackColor;
    private PlayerCombatState? shownPlayerState;
    private EnemyCombatState? shownEnemyState;

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<PlayerCombatController>();
        if (enemy == null) enemy = FindFirstObjectByType<EnemyCombatController>();

        if (feedbackText != null) feedbackText.text = string.Empty;
        if (staggerBar != null) staggerBar.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (player != null) player.OnFeedback += ShowFeedback;
        if (enemy != null) enemy.OnStaggered += HandleStaggered;
    }

    private void OnDisable()
    {
        if (player != null) player.OnFeedback -= ShowFeedback;
        if (enemy != null) enemy.OnStaggered -= HandleStaggered;
    }

    private void Update()
    {
        if (player != null)
        {
            if (healthBar != null) healthBar.SetValue(player.Health, player.MaxHealth);
            if (shieldBar != null) shieldBar.SetValue(player.Shield, player.MaxShield);
            if (staminaBar != null) staminaBar.SetValue(player.Stamina, player.MaxStamina);
            UpdatePlayerState();
        }

        if (enemy != null)
        {
            if (enemyHealthBar != null) enemyHealthBar.SetValue(enemy.Health, enemy.MaxHealth);
            UpdateStaggerBar();
            UpdateEnemyState();
        }

        UpdateFeedback();
    }

    private void UpdatePlayerState()
    {
        if (playerStateText == null || shownPlayerState == player.State) return;

        shownPlayerState = player.State;
        playerStateText.text = PlayerStateLabel(player.State);
        playerStateText.color = PlayerStateColor(player.State);
    }

    private void UpdateEnemyState()
    {
        if (enemyStateText == null || shownEnemyState == enemy.State) return;

        shownEnemyState = enemy.State;
        enemyStateText.text = EnemyStateLabel(enemy.State);
        enemyStateText.color = EnemyStateColor(enemy.State);
    }

    private static string PlayerStateLabel(PlayerCombatState state)
    {
        switch (state)
        {
            case PlayerCombatState.Blocking: return "BLOCKING";
            case PlayerCombatState.LoweringGuard: return "LOWERING GUARD";
            case PlayerCombatState.DodgingLeft: return "DODGING LEFT";
            case PlayerCombatState.DodgingRight: return "DODGING RIGHT";
            case PlayerCombatState.Parrying: return "PARRYING";
            case PlayerCombatState.Attacking: return "ATTACKING";
            case PlayerCombatState.Hurt: return "HURT";
            case PlayerCombatState.GuardBroken: return "GUARD BROKEN";
            case PlayerCombatState.Recovering: return "RECOVERING";
            case PlayerCombatState.Dead: return "DEAD";
            default: return "IDLE";
        }
    }

    private Color PlayerStateColor(PlayerCombatState state)
    {
        switch (state)
        {
            case PlayerCombatState.Idle:
            case PlayerCombatState.LoweringGuard:
            case PlayerCombatState.Recovering:
                return idleStateColor;
            case PlayerCombatState.Hurt:
            case PlayerCombatState.GuardBroken:
            case PlayerCombatState.Dead:
                return dangerStateColor;
            default:
                return actionStateColor;
        }
    }

    private static string EnemyStateLabel(EnemyCombatState state)
    {
        switch (state)
        {
            case EnemyCombatState.Attacking: return "ATTACKING";
            case EnemyCombatState.Staggered: return "STAGGERED";
            case EnemyCombatState.Defeated: return "DEFEATED";
            default: return "IDLE";
        }
    }

    private Color EnemyStateColor(EnemyCombatState state)
    {
        switch (state)
        {
            case EnemyCombatState.Attacking: return dangerStateColor;
            case EnemyCombatState.Staggered: return openingStateColor;
            default: return idleStateColor;
        }
    }

    private void HandleStaggered(float duration) => staggerDuration = duration;

    private void UpdateStaggerBar()
    {
        if (staggerBar == null) return;

        bool show = enemy.IsVulnerable && staggerDuration > 0f;
        if (staggerBar.gameObject.activeSelf != show) staggerBar.gameObject.SetActive(show);
        if (show) staggerBar.SetValue(enemy.StaggerTimeRemaining, staggerDuration);
    }

    private void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null) return;

        feedbackText.text = message;
        feedbackColor = color;
        feedbackTimer = feedbackHold + feedbackFade;
    }

    /// <summary>Runs on unscaled time so the text stays readable through hit-stop and pauses.</summary>
    private void UpdateFeedback()
    {
        if (feedbackText == null || feedbackTimer <= 0f) return;

        feedbackTimer -= Time.unscaledDeltaTime;

        float alpha = Mathf.Clamp01(feedbackTimer / feedbackFade);
        feedbackText.color = new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, feedbackColor.a * alpha);

        float elapsed = feedbackHold + feedbackFade - feedbackTimer;
        float scale = Mathf.Lerp(punchScale, 1f, Mathf.Clamp01(elapsed / punchTime));
        feedbackText.rectTransform.localScale = new Vector3(scale, scale, 1f);
    }
}

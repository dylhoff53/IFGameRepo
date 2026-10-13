using System;
using UnityEngine;

/// <summary>
/// Plays an enemy's attack charts into a <see cref="CombatJudge"/> it owns, fires wind-up animations,
/// and opens a stagger window (chart paused, enemy vulnerable) when the player lands a parry.
/// </summary>
[DefaultExecutionOrder(-50)]
public class EnemyCombatController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CombatClock clock;
    [SerializeField] private PlayerCombatInput playerInput;
    [SerializeField] private Animator animator;

    [Header("Charts")]
    [Tooltip("One chart per phase. Phases advance as health drops, split evenly across max health.")]
    [SerializeField] private AttackChart[] phases;
    [SerializeField] private bool autoStart = true;
    [Tooltip("Seconds after the fight starts before the first chart begins.")]
    [Min(0f)] [SerializeField] private float startDelay = 1f;

    [Header("Judging")]
    [SerializeField] private HitWindows windows = new HitWindows();
    [Tooltip("Falls back to default rules if left empty.")]
    [SerializeField] private ResponseRules rules;

    [Header("Health")]
    [Min(1)] [SerializeField] private int maxHealth = 100;

    [Header("Stagger")]
    [Tooltip("Seconds the enemy stays open to attacks after a successful parry.")]
    [Min(0f)] [SerializeField] private float staggerDuration = 2f;
    [Tooltip("Minimum seconds between recovering and the next wind-up starting.")]
    [Min(0f)] [SerializeField] private float resumeGrace = 0.5f;
    [Tooltip("Real seconds of hit-stop on a successful parry. 0 to disable.")]
    [Min(0f)] [SerializeField] private float parryHitStop = 0.08f;

    [Header("Animator Triggers")]
    [SerializeField] private string staggerTrigger = "Stagger";
    [SerializeField] private string recoverTrigger = "Recover";
    [SerializeField] private string hurtTrigger = "Hurt";
    [SerializeField] private string defeatTrigger = "Defeat";

    [Header("Debug")]
    [SerializeField] private bool logJudgments;

    public CombatJudge Judge { get; private set; }
    public EnemyCombatState State { get; private set; } = EnemyCombatState.Idle;
    public int Health { get; private set; }
    public int MaxHealth => maxHealth;
    public int PhaseIndex { get; private set; }
    public AttackChart CurrentChart => phases != null && PhaseIndex < phases.Length ? phases[PhaseIndex] : null;

    public bool IsVulnerable => State == EnemyCombatState.Staggered;
    public float StaggerTimeRemaining => IsVulnerable && clock != null ? (float)Math.Max(0, staggerEndTime - clock.Now) : 0f;

    /// <summary>Fired when a note is pushed into the judge, at the start of its wind-up.</summary>
    public event Action<ActiveNote> OnTelegraph;
    public event Action<float> OnStaggered;
    public event Action OnRecovered;
    public event Action<int, int> OnDamaged;
    public event Action OnDefeated;
    public event Action<int> OnPhaseChanged;

    /// <summary>Fired when a non-looping chart has played out and every note is resolved.</summary>
    public event Action<AttackChart> OnChartFinished;

    private double chartStartTime;
    private int nextNoteIndex;
    private double staggerStartTime;
    private double staggerEndTime;

    private void Awake()
    {
        if (clock == null) clock = FindFirstObjectByType<CombatClock>();
        if (clock == null) Debug.LogError($"{nameof(EnemyCombatController)} needs a {nameof(CombatClock)} in the scene.", this);

        if (playerInput == null) playerInput = FindFirstObjectByType<PlayerCombatInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (rules == null) rules = ScriptableObject.CreateInstance<ResponseRules>();

        Judge = new CombatJudge(windows, rules);
        Judge.OnNoteJudged += HandleNoteJudged;
        if (logJudgments) Judge.OnWhiff += (action, time) => Debug.Log($"[Combat] {action} whiffed at {time:F3}", this);

        if (playerInput != null) playerInput.Judge = Judge;

        Health = maxHealth;
    }

    private void Start()
    {
        if (autoStart) StartFight();
    }

    private void OnDestroy()
    {
        if (playerInput != null && playerInput.Judge == Judge) playerInput.Judge = null;
    }

    private void Update()
    {
        if (clock == null) return;
        double now = clock.Now;

        switch (State)
        {
            case EnemyCombatState.Attacking:
                SpawnDueNotes(now);
                Judge.Tick(now);
                if (State == EnemyCombatState.Attacking) CheckChartFinished(now);
                break;

            case EnemyCombatState.Staggered:
                if (now >= staggerEndTime) Recover(now);
                break;
        }
    }

    public void StartFight()
    {
        if (clock == null) return;
        if (phases == null || phases.Length == 0)
        {
            Debug.LogWarning($"{name} has no attack charts assigned.", this);
            return;
        }

        Health = maxHealth;
        Judge.Clear();
        ClearPlayerBuffer();
        State = EnemyCombatState.Attacking;
        StartPhase(0, clock.Now + startDelay);
    }

    public void StopFight()
    {
        Judge.Clear();
        ClearPlayerBuffer();
        State = EnemyCombatState.Idle;
    }

    /// <summary>Applies damage if the enemy is staggered. Returns false if the hit was not allowed.</summary>
    public bool TakeDamage(int amount)
    {
        if (!IsVulnerable || amount <= 0) return false;

        Health = Mathf.Max(0, Health - amount);
        OnDamaged?.Invoke(amount, Health);

        if (Health == 0) Defeat();
        else SetTrigger(hurtTrigger);

        return true;
    }

    private void SpawnDueNotes(double now)
    {
        AttackChart chart = CurrentChart;
        if (chart == null || chart.notes.Count == 0) return;

        while (true)
        {
            if (nextNoteIndex >= chart.notes.Count)
            {
                if (!chart.loop || chart.LoopLength <= 0) return;
                chartStartTime += chart.LoopLength;
                nextNoteIndex = 0;
            }

            CombatNote note = chart.notes[nextNoteIndex];
            if (now < chartStartTime + note.TelegraphTime) return;

            nextNoteIndex++;
            ActiveNote active = Judge.AddNote(note, chartStartTime + note.hitTime);
            SetTrigger(note.animTrigger);
            OnTelegraph?.Invoke(active);
        }
    }

    private void CheckChartFinished(double now)
    {
        AttackChart chart = CurrentChart;
        bool done = chart == null || (!chart.loop && nextNoteIndex >= chart.notes.Count);
        if (!done || Judge.ActiveNotes.Count > 0) return;

        OnChartFinished?.Invoke(chart);

        if (PhaseIndex + 1 < phases.Length) StartPhase(PhaseIndex + 1, now + resumeGrace);
        else State = EnemyCombatState.Idle;
    }

    /// <summary>Starts a chart so its earliest wind-up begins no sooner than <paramref name="startTime"/>.</summary>
    private void StartPhase(int index, double startTime)
    {
        PhaseIndex = index;
        nextNoteIndex = 0;

        double earliestTelegraph = 0;
        AttackChart chart = CurrentChart;
        if (chart != null)
        {
            foreach (CombatNote note in chart.notes) earliestTelegraph = Math.Min(earliestTelegraph, note.TelegraphTime);
        }
        chartStartTime = startTime - earliestTelegraph;

        OnPhaseChanged?.Invoke(index);
    }

    private void HandleNoteJudged(JudgeResult result)
    {
        if (logJudgments)
        {
            CombatNote note = result.note.Note;
            string action = result.action?.ToString() ?? "no input";
            Debug.Log($"[Combat] {note.type} {note.direction} vs {action}: {result.judgment} ({result.offset * 1000:+0;-0;0} ms)", this);
        }

        if (State == EnemyCombatState.Attacking && result.Success && result.response.effect == ResponseEffect.Staggered)
        {
            EnterStagger();
        }
    }

    private void EnterStagger()
    {
        double now = clock.Now;
        staggerStartTime = now;
        staggerEndTime = now + staggerDuration;
        State = EnemyCombatState.Staggered;

        Judge.Clear();
        ClearPlayerBuffer();
        SetTrigger(staggerTrigger);
        if (parryHitStop > 0f) clock.HitStop(parryHitStop);

        OnStaggered?.Invoke(staggerDuration);
    }

    private void Recover(double now)
    {
        State = EnemyCombatState.Attacking;
        SetTrigger(recoverTrigger);

        int phase = PhaseForHealth();
        if (phase != PhaseIndex) StartPhase(phase, now + resumeGrace);
        else ResumeChart(now);

        OnRecovered?.Invoke();
    }

    /// <summary>Shifts the chart by the time spent staggered, then pushes it back further if the next wind-up would start inside the grace period.</summary>
    private void ResumeChart(double now)
    {
        chartStartTime += now - staggerStartTime;

        AttackChart chart = CurrentChart;
        if (chart == null || chart.notes.Count == 0) return;

        double nextTelegraph;
        if (nextNoteIndex < chart.notes.Count) nextTelegraph = chartStartTime + chart.notes[nextNoteIndex].TelegraphTime;
        else if (chart.loop) nextTelegraph = chartStartTime + chart.LoopLength + chart.notes[0].TelegraphTime;
        else return;

        double earliest = now + resumeGrace;
        if (nextTelegraph < earliest) chartStartTime += earliest - nextTelegraph;
    }

    private int PhaseForHealth()
    {
        if (phases == null || phases.Length == 0) return 0;

        float lost = 1f - (float)Health / maxHealth;
        int phase = Mathf.Clamp(Mathf.FloorToInt(lost * phases.Length), 0, phases.Length - 1);
        return Mathf.Max(PhaseIndex, phase);
    }

    private void Defeat()
    {
        State = EnemyCombatState.Defeated;
        Judge.Clear();
        ClearPlayerBuffer();
        SetTrigger(defeatTrigger);
        OnDefeated?.Invoke();
    }

    private void ClearPlayerBuffer()
    {
        if (playerInput != null) playerInput.ClearBuffer();
    }

    private void SetTrigger(string trigger)
    {
        if (animator != null && !string.IsNullOrEmpty(trigger)) animator.SetTrigger(trigger);
    }
}

using UnityEngine;

/// <summary>
/// The single time source for combat. Everything that schedules or judges notes reads <see cref="Now"/>,
/// so pausing, slow-mo and hit-stop only need to touch this clock.
/// </summary>
[DefaultExecutionOrder(-100)]
public class CombatClock : MonoBehaviour
{
    [Tooltip("Multiplier applied on top of Time.deltaTime. 1 = normal, 0.5 = slow-mo.")]
    [Min(0f)] public float timeScale = 1f;

    [SerializeField] private bool startPaused;

    private double now;
    private float hitStopRemaining;

    public double Now => now;
    public bool Paused { get; private set; }
    public bool InHitStop => hitStopRemaining > 0f;

    private void Awake()
    {
        Paused = startPaused;
    }

    private void Update()
    {
        if (hitStopRemaining > 0f)
        {
            hitStopRemaining -= Time.unscaledDeltaTime;
            return;
        }

        if (Paused) return;

        now += Time.deltaTime * timeScale;
    }

    public void Pause() => Paused = true;

    public void Resume() => Paused = false;

    public void ResetClock(double time = 0)
    {
        now = time;
        hitStopRemaining = 0f;
    }

    /// <summary>Freezes combat time for <paramref name="duration"/> real seconds.</summary>
    public void HitStop(float duration)
    {
        hitStopRemaining = Mathf.Max(hitStopRemaining, duration);
    }
}

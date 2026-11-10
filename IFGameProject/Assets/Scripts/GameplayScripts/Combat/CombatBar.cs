using TMPro;
using UnityEngine;

/// <summary>
/// A horizontal UI bar driven by anchors, so the fill needs no sprite. An optional trail lingers
/// behind the fill when the value drops, showing how much was just lost.
/// </summary>
public class CombatBar : MonoBehaviour
{
    [Tooltip("Stretched from the left edge to the current value.")]
    [SerializeField] private RectTransform fill;
    [Tooltip("Optional. Sits behind the fill and catches up after a delay.")]
    [SerializeField] private RectTransform trail;
    [Tooltip("Optional. Shows \"current / max\".")]
    [SerializeField] private TMP_Text valueText;

    [Tooltip("Real seconds the trail holds before catching up.")]
    [Min(0f)] [SerializeField] private float trailDelay = 0.4f;
    [Tooltip("Fraction of the bar the trail drains per real second.")]
    [Min(0.01f)] [SerializeField] private float trailSpeed = 1.5f;

    private float fraction = -1f;
    private float trailFraction;
    private float trailHoldTimer;
    private int shownCurrent = -1;
    private int shownMax = -1;

    public void SetValue(float current, float max)
    {
        float next = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (fraction < 0f || next >= trailFraction) trailFraction = next;
        else if (next < fraction) trailHoldTimer = trailDelay;

        fraction = next;
        SetWidth(fill, fraction);
        UpdateText(current, max);
    }

    private void Update()
    {
        if (trail == null || fraction < 0f) return;

        if (trailFraction > fraction)
        {
            if (trailHoldTimer > 0f) trailHoldTimer -= Time.unscaledDeltaTime;
            else trailFraction = Mathf.Max(fraction, trailFraction - trailSpeed * Time.unscaledDeltaTime);
        }

        SetWidth(trail, trailFraction);
    }

    private void UpdateText(float current, float max)
    {
        if (valueText == null) return;

        int c = Mathf.CeilToInt(current);
        int m = Mathf.CeilToInt(max);
        if (c == shownCurrent && m == shownMax) return;

        shownCurrent = c;
        shownMax = m;
        valueText.text = $"{c} / {m}";
    }

    private static void SetWidth(RectTransform rect, float value)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(value, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

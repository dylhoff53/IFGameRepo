using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Placeholder telegraph for testing without animations: tints the enemy's renderer and shows the incoming
/// attack's type and direction while it winds up, and shows the stagger window after a parry.
/// </summary>
public class CombatTelegraphDebug : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static readonly CombatAction[] Answers =
    {
        CombatAction.DodgeLeft, CombatAction.DodgeRight, CombatAction.Block, CombatAction.Parry
    };

    [Header("References")]
    [SerializeField] private EnemyCombatController enemy;
    [SerializeField] private CombatClock clock;
    [Tooltip("Renderers to tint. Defaults to every renderer under the enemy.")]
    [SerializeField] private Renderer[] renderers;
    [Tooltip("Optional screen or world text for the attack label.")]
    [SerializeField] private TMP_Text label;

    [Header("Display")]
    [Tooltip("List the buttons that will work against the incoming attack.")]
    [SerializeField] private bool showValidAnswers = true;
    [Tooltip("Real seconds the tint flashes white at the moment of impact.")]
    [Min(0f)] [SerializeField] private float impactFlash = 0.08f;

    [Header("Colors")]
    [SerializeField] private Color slashColor = new Color(1f, 0.6f, 0.1f);
    [SerializeField] private Color overheadColor = new Color(1f, 0.9f, 0.1f);
    [SerializeField] private Color thrustColor = new Color(0.2f, 0.8f, 1f);
    [SerializeField] private Color heavyColor = new Color(1f, 0.3f, 0.1f);
    [SerializeField] private Color unblockableColor = new Color(0.9f, 0f, 0f);
    [SerializeField] private Color feintColor = new Color(0.7f, 0.3f, 1f);
    [SerializeField] private Color staggeredColor = new Color(0.3f, 1f, 0.3f);

    private readonly List<Color> baseColors = new List<Color>();
    private MaterialPropertyBlock block;
    private ActiveNote lastNote;
    private float flashTimer;

    private void Awake()
    {
        if (enemy == null) enemy = GetComponentInParent<EnemyCombatController>();
        if (enemy == null) enemy = FindFirstObjectByType<EnemyCombatController>();
        if (clock == null) clock = FindFirstObjectByType<CombatClock>();

        if (renderers == null || renderers.Length == 0)
        {
            renderers = enemy != null ? enemy.GetComponentsInChildren<Renderer>() : GetComponentsInChildren<Renderer>();
        }

        block = new MaterialPropertyBlock();
        foreach (Renderer r in renderers) baseColors.Add(ReadBaseColor(r));

        if (label != null) label.text = string.Empty;
    }

    private void OnDisable()
    {
        for (int i = 0; i < renderers.Length; i++) Tint(i, baseColors[i]);
    }

    private void Update()
    {
        if (enemy == null || clock == null || enemy.Judge == null) return;

        if (enemy.IsVulnerable)
        {
            lastNote = null;
            ApplyTint(staggeredColor, 0.8f);
            SetLabel($"STAGGERED  {enemy.StaggerTimeRemaining:0.0}s\nATTACK!", staggeredColor);
            return;
        }

        ActiveNote note = FirstPending();
        if (note != lastNote)
        {
            if (lastNote != null && note == null) flashTimer = impactFlash;
            lastNote = note;
        }

        if (flashTimer > 0f)
        {
            flashTimer -= Time.unscaledDeltaTime;
            ApplyTint(Color.white, 1f);
        }
        else if (note != null)
        {
            Color color = ColorFor(note.Note.type);
            ApplyTint(color, WindUpProgress(note));
            SetLabel(Describe(note.Note), color);
        }
        else
        {
            ApplyTint(Color.white, 0f);
            SetLabel(string.Empty, Color.white);
        }
    }

    private ActiveNote FirstPending()
    {
        IReadOnlyList<ActiveNote> active = enemy.Judge.ActiveNotes;
        return active.Count > 0 ? active[0] : null;
    }

    /// <summary>0 at the start of the wind-up, 1 at impact.</summary>
    private float WindUpProgress(ActiveNote note)
    {
        double lead = note.Note.telegraphLead;
        if (lead <= 0) return 1f;
        return Mathf.Clamp01((float)(1 - (note.HitTime - clock.Now) / lead));
    }

    private string Describe(CombatNote note)
    {
        string attack = $"{note.type.ToString().ToUpperInvariant()} from {note.direction.ToString().ToUpperInvariant()}";

        if (!showValidAnswers) return attack;

        var answers = new StringBuilder();
        foreach (CombatAction action in Answers)
        {
            if (!enemy.Judge.Rules.Evaluate(action, note.type, note.direction).valid) continue;
            if (answers.Length > 0) answers.Append(" / ");
            answers.Append(action);
        }

        string hint = answers.Length > 0 ? answers.ToString() : "don't react";
        return $"{attack}\n<size=60%>{hint}</size>";
    }

    private Color ColorFor(NoteType type)
    {
        switch (type)
        {
            case NoteType.Slash: return slashColor;
            case NoteType.Overhead: return overheadColor;
            case NoteType.Thrust: return thrustColor;
            case NoteType.Heavy: return heavyColor;
            case NoteType.Unblockable: return unblockableColor;
            case NoteType.Feint: return feintColor;
            default: return Color.white;
        }
    }

    private void SetLabel(string text, Color color)
    {
        if (label == null) return;
        label.text = text;
        label.color = color;
    }

    private void ApplyTint(Color color, float amount)
    {
        for (int i = 0; i < renderers.Length; i++) Tint(i, Color.Lerp(baseColors[i], color, amount));
    }

    private void Tint(int index, Color color)
    {
        Renderer r = renderers[index];
        if (r == null) return;

        r.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        r.SetPropertyBlock(block);
    }

    private static Color ReadBaseColor(Renderer r)
    {
        Material material = r != null ? r.sharedMaterial : null;
        if (material == null) return Color.white;
        if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
        if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
        return Color.white;
    }
}

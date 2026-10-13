using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAttackChart", menuName = "Combat/Attack Chart")]
public class AttackChart : ScriptableObject
{
    public List<CombatNote> notes = new List<CombatNote>();

    [Tooltip("Restart the chart after the last note.")]
    public bool loop = true;

    [Tooltip("Seconds between the last note's hit time and the start of the next loop.")]
    [Min(0f)] public float loopGap = 1f;

    public double Duration => notes.Count == 0 ? 0 : notes[notes.Count - 1].hitTime;

    public double LoopLength => Duration + loopGap;

    private void OnValidate()
    {
        if (notes == null)
        {
            notes = new List<CombatNote>();
            return;
        }

        notes = notes.Where(n => n != null).OrderBy(n => n.hitTime).ToList();
    }
}

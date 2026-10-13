[System.Serializable]
public class CombatNote
{
    public double hitTime;          // seconds from chart start
    public float telegraphLead = 0.6f;
    public NoteType type;
    public AttackDirection direction;
    public int damage = 10;
    public string animTrigger;      // enemy wind-up animation

    public double TelegraphTime => hitTime - telegraphLead;
}

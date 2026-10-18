using UnityEngine;

// A wall you can shoot apart. Used for the 3 map walls and for the
// Bulwark's carried shield (which the Bulwark starts shooting from
// once you destroy it).
public class DestructibleWall : MonoBehaviour
{
    Health health;
    bool shield;

    public bool Dead { get; private set; }

    public static DestructibleWall Create(Vector2 pos, Vector2 size, float hp, int order, bool isShield = false)
    {
        var g = new GameObject(isShield ? "Shield" : "Wall");
        g.transform.position = pos;

        var w = g.AddComponent<DestructibleWall>();
        w.shield = isShield;

        g.AddComponent<BoxCollider2D>().size = size;

        Art.Part(g.transform, "Shadow", SpriteFactory.Square, new Vector2(0.1f, -0.12f), size,
                 new Color(0, 0, 0, 0.45f), order - 2);
        var body = Art.Part(g.transform, "Body", Art.WallTile(), Vector2.zero, size, Color.white, order);
        // tint slightly so map walls and shields read differently
        body.color = isShield ? new Color(0.85f, 1f, 0.95f) : Color.white;

        w.health = g.AddComponent<Health>();
        w.health.Init(hp);
        w.health.OnDeath += w.Die;
        // its own little bar, deliberately separate from any unit health bar:
        // thin, sits above the wall, cool-tinted for shields vs amber for map walls
        w.health.showBar = true;
        w.health.barOffsetY = size.y * 0.5f + 0.22f;
        w.health.barWidth = Mathf.Clamp(size.x * 0.7f, 0.65f, 2.2f);
        w.health.barColor = isShield ? new Color(0.55f, 0.95f, 0.85f) : new Color(1f, 0.75f, 0.2f);

        return w;
    }

    void Die(Health h)
    {
        if (Dead) return;
        Dead = true;

        Vector2 p = transform.position;
        Vector2 s = transform.localScale;
        Fx.Burst(p, new Color(0.45f, 0.48f, 0.56f), 26, 8f);
        Fx.Burst(p, new Color(0.82f, 0.66f, 0.10f), 10, 6f);
        GameManager.Shake(shield ? 0.15f : 0.35f);
        Sfx.PlayAt(p, shield ? "shield_break" : "wall_smash", 0.7f);

        Destroy(gameObject);
    }
}

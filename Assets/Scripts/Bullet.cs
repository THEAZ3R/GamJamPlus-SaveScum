using System.Collections.Generic;
using UnityEngine;

public enum BulletKind { Normal, Pierce, Bounce, Shell }

public class Bullet : MonoBehaviour
{
    Vector2 dir, target, shellFrom;
    float speed, damage, life = 3f, arcT, flight, arcH, lastReflect, trailT;
    bool fromPlayer;
    Color color;
    BulletKind kind;

    // ---- player mods (configured right after Spawn by PlayerUpgrades)
    public int bounces;
    public bool corrosive;
    public float corrosiveDps;
    public int chainCount;                          // MAZIODYNE targets
    public float chainDmg;

    public bool FromPlayer { get { return fromPlayer; } }
    public Vector2 Dir { get { return dir; } }      // for the dodge scans

    Transform shadow;
    readonly HashSet<Health> alreadyHit = new HashSet<Health>();

    public static Bullet Spawn(Vector2 pos, Vector2 dir, float speed, float damage, bool fromPlayer,
                               Color color, float size = -1f, BulletKind kind = BulletKind.Normal,
                               Vector2 target = default(Vector2))
    {
        if (size < 0f) size = fromPlayer ? 0.20f : 0.24f;
        if (kind == BulletKind.Shell) size = 0.34f;

        var g = new GameObject(kind == BulletKind.Shell ? "Shell" : (fromPlayer ? "PlayerBullet" : "EnemyBullet"));
        g.transform.position = pos;
        g.transform.localScale = Vector3.one * size;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = color;
        sr.sortingOrder = kind == BulletKind.Shell ? 60 : 30;

        var rb = g.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        var col = g.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var b = g.AddComponent<Bullet>();
        b.dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        b.speed = speed;
        b.damage = damage;
        b.fromPlayer = fromPlayer;
        b.color = color;
        b.kind = kind;
        b.shellFrom = pos;

        if (kind == BulletKind.Bounce) b.bounces = 4;

        if (kind == BulletKind.Shell)
        {
            b.target = target;
            b.flight = Mathf.Clamp(Vector2.Distance(pos, target) / Mathf.Max(speed, 1f), 0.55f, 1.6f);
            b.arcH = 1.1f + b.flight * 0.9f;

            var sh = new GameObject("Shadow");
            sh.transform.SetParent(g.transform, false);
            sh.transform.localScale = new Vector3(1.1f, 0.6f, 1f);
            var ssr = sh.AddComponent<SpriteRenderer>();
            ssr.sprite = SpriteFactory.Circle;
            ssr.color = new Color(0, 0, 0, 0.4f);
            ssr.sortingOrder = -3;
            b.shadow = sh.transform;
        }

        if (!fromPlayer)                             // their guns announce themselves
            Sfx.PlayAt(pos, kind == BulletKind.Shell ? "trap_throw" : "enemy_shot", 0.4f);

        return b;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { if (kind == BulletKind.Shell) Explode(); else Destroy(gameObject); return; }

        if (kind == BulletKind.Shell) { TickShell(dt); return; }

        transform.position += (Vector3)(dir * speed * dt);

        // SOLVENT WAKE: the round drips acid along its whole flight
        if (corrosive)
        {
            trailT -= dt;
            if (trailT <= 0f)
            {
                trailT = 0.12f;
                CorrosivePool.Spawn(transform.position, corrosiveDps);
            }
        }
    }

    // ------------------------------------------------------------ SHELL
    void TickShell(float dt)
    {
        arcT += dt / flight;
        float t = Mathf.Clamp01(arcT);

        Vector2 pos = Vector2.Lerp(shellFrom, target, t);
        transform.position = new Vector3(pos.x, pos.y + Mathf.Sin(t * Mathf.PI) * arcH, 0f);

        if (shadow != null)
        {
            shadow.position = new Vector3(pos.x, pos.y, 0f);
            float k = 1f - Mathf.Sin(t * Mathf.PI) * 0.55f;
            shadow.localScale = new Vector3(1.1f * k, 0.6f * k, 1f);
        }

        if (t >= 1f) Explode();
    }

    void Explode()
    {
        Vector2 p = transform.position;
        Fx.Burst(p, new Color(1f, 0.55f, 0.1f), 24, 9f);
        Fx.Burst(p, new Color(1f, 0.9f, 0.4f), 12, 5f);
        GameManager.Shake(0.35f);

        float radius = 1.6f;
        var hits = Physics2D.OverlapCircleAll(p, radius);
        foreach (var h in hits)
        {
            if (h.isTrigger) continue;
            var hp = h.GetComponent<Health>();
            if (hp == null) continue;
            bool isPlayer = h.CompareTag("Player");
            if (fromPlayer == isPlayer) continue;
            Vector2 away = ((Vector2)h.transform.position - p);
            hp.TakeDamage(damage, away.normalized);
        }
        Destroy(gameObject);
    }

    // ------------------------------------------------------------ HITS
    void OnTriggerEnter2D(Collider2D other)
    {
        // CHRONO ANCHOR: your own time spot, collected by a bullet
        if (fromPlayer)
        {
            var anchor = other.GetComponent<DashAnchor>();
            if (anchor != null)
            {
                anchor.Teleport();
                Destroy(gameObject);
                return;
            }
        }

        // traps: any bullet disarms one
        var trap = other.GetComponent<Trap>();
        if (trap != null)
        {
            trap.Disarm();
            if (kind == BulletKind.Normal) { Fx.Burst(transform.position, color, 3, 3f); Destroy(gameObject); }
            return;
        }

        if (other.isTrigger) return;

        // DOGMEAT: rounds pass straight through the pack - a dog only stops a
        // bullet when it deliberately swats it out of the air
        if (other.GetComponent<CyberDog>() != null) return;

        var h = other.GetComponent<Health>();
        if (h != null)
        {
            bool targetIsPlayer = other.CompareTag("Player");
            if (fromPlayer == targetIsPlayer) return;              // friendly: pass through
            if (h.IsInvulnerable) return;
            if (kind == BulletKind.Pierce && alreadyHit.Contains(h)) return;

            h.TakeDamage(damage, dir);

            if (chainCount > 0) Chain(h);                          // ARC CASCADE
            if (corrosive) CorrosivePool.Spawn(transform.position, corrosiveDps);

            if (kind == BulletKind.Pierce) { alreadyHit.Add(h); return; }

            Fx.Burst(transform.position, color, 4, 4f);
            Destroy(gameObject);
            return;
        }

        // ---- no Health = wall / obstacle
        switch (kind)
        {
            case BulletKind.Pierce:
                break;                                             // sails through walls

            case BulletKind.Bounce:
                if (Time.time - lastReflect < 0.02f) return;
                BounceOff(other);
                break;

            default:
                Fx.Burst(transform.position, color, 4, 4f);
                Destroy(gameObject);
                break;
        }
    }

    void BounceOff(Collider2D other)
    {
        var box = other.bounds;
        Vector2 p = transform.position;
        float nx = (p.x - box.center.x) / Mathf.Max(box.size.x * 0.5f, 0.01f);
        float ny = (p.y - box.center.y) / Mathf.Max(box.size.y * 0.5f, 0.01f);

        if (Mathf.Abs(nx) > Mathf.Abs(ny)) dir.x = -dir.x;
        else dir.y = -dir.y;

        // nudge outside so the same wall does not retrigger instantly
        transform.position += (Vector3)(dir * 0.08f);
        lastReflect = Time.time;

        bounces--;
        // DEAD EYE: the round keeps ricochating - the wall answers with a glint
        if (fromPlayer)
        {
            Color glint = Upgrade.Accent(UpgradeKind.Ricochet);
            Fx.Ring(transform.position, glint, 0.15f, 0.9f, 0.18f, 39);
            Fx.Sparks(transform.position, glint, 6, 9f);
        }
        Fx.Burst(transform.position, color, 3, 3f);
        if (bounces <= 0) { Fx.Burst(transform.position, color, 5, 4f); Destroy(gameObject); }
    }

    // ------------------------------------------------------------ ARC CASCADE
    // the hit leaps to the closest enemies, each link jumping from the last
    void Chain(Health from)
    {
        Vector2 pos = transform.position;

        var live = new List<Enemy>();
        for (int i = 0; i < Enemy.All.Count; i++)
        {
            var e = Enemy.All[i];
            if (e == null || e.Health == null || e.Health == from) continue;
            live.Add(e);
        }
        live.Sort((a, b) => ((Vector2)a.transform.position - pos).sqrMagnitude
                                  .CompareTo(((Vector2)b.transform.position - pos).sqrMagnitude));

        Vector2 linkFrom = pos;
        int n = Mathf.Min(chainCount, live.Count);
        for (int i = 0; i < n; i++)
        {
            Vector2 to = live[i].transform.position;
            if ((to - linkFrom).sqrMagnitude > 64f) break;          // 8u reach
            ZapLine(linkFrom, to);
            Sfx.PlayAt(linkFrom, "elec_crackle", 0.5f, Random.Range(0.9f, 1.15f));  // MAZIODYNE
            live[i].Health.TakeDamage(chainDmg, (to - linkFrom).normalized);
            linkFrom = to;
        }
    }

    void ZapLine(Vector2 a, Vector2 b)
    {
        float dist = Vector2.Distance(a, b);
        int steps = Mathf.Max(2, Mathf.RoundToInt(dist / 0.5f));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps) + Random.insideUnitCircle * 0.13f;
            Fx.Flicker(p, Art.Zap(Random.Range(0, 3)), new Color(0.78f, 0.95f, 1f),
                       0.5f, 0.16f, Random.Range(0f, 360f));
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

public enum TrapKind { Slow, Boom, Stun }

// Thrown by the Trapper, and each kind announces itself with its own vfx so
// you can read the floor at a glance:
//   Slow  - a green goo puddle that pulses and drips; coats the victim
//   Boom  - blinks hard like a spider, then blows up in a real explosion
//   Stun  - a tesla node crackling with electric arcs; zaps the victim
public class Trap : MonoBehaviour
{
    const float Radius = 0.52f;        // matches the painted trigger ring
    const float Life = 25f;
    const float BaseScale = 0.95f;

    public static readonly List<Trap> All = new List<Trap>();   // for the herald's purge

    TrapKind kind;
    float life = Life;
    float flickerT, dripT, zapT;
    SpriteRenderer sr, glow;
    Color baseColor;
    SpriteRenderer[] zap;              // stun only: the crackling arcs

    public static Trap Spawn(Vector2 pos, TrapKind kind)
    {
        var g = new GameObject("Trap_" + kind);
        g.transform.position = pos;
        g.transform.localScale = Vector3.one * BaseScale;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.Trap(kind);
        sr.sortingOrder = 8;

        var col = g.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = Radius;

        var t = g.AddComponent<Trap>();
        t.kind = kind;
        t.sr = sr;
        t.baseColor = Art.TrapColor(kind);        // NOT sr.color (that is white tint)

        // soft underglow so the trap reads from across the arena
        var gg = new GameObject("TrapGlow");
        gg.transform.SetParent(g.transform, false);
        gg.transform.localScale = Vector3.one * 1.7f;
        var gsr = gg.AddComponent<SpriteRenderer>();
        gsr.sprite = SpriteFactory.Circle;
        gsr.color = new Color(t.baseColor.r, t.baseColor.g, t.baseColor.b, 0.22f);
        gsr.sortingOrder = 7;
        t.glow = gsr;

        if (kind == TrapKind.Stun) t.MakeZaps(g.transform);

        Fx.Ring(pos, t.baseColor, 0.3f, 1.7f, 0.32f, 7);
        Fx.Burst(pos, t.baseColor, 8, 4f);
        Sfx.PlayAt(pos, "trap_throw", 0.5f);
        All.Add(t);
        return t;
    }

    void OnDestroy() { All.Remove(this); }

    public static void ClearAll()
    {
        foreach (var t in All.ToArray())
            if (t != null) Object.Destroy(t.gameObject);
    }

    // three bolts of lightning around the node, flickered in Update
    void MakeZaps(Transform parent)
    {
        zap = new SpriteRenderer[3];
        for (int i = 0; i < 3; i++)
        {
            var z = new GameObject("Zap");
            z.transform.SetParent(parent, false);
            float a = i * 120f + 30f;
            z.transform.localPosition = Quaternion.Euler(0, 0, a) * new Vector3(0.66f, 0f, 0f);
            z.transform.localRotation = Quaternion.Euler(0, 0, a);
            z.transform.localScale = Vector3.one * 0.9f;
            var r = z.AddComponent<SpriteRenderer>();
            r.sprite = Art.Zap(Random.Range(0, 3));
            r.color = new Color(0.75f, 0.96f, 1f);
            r.sortingOrder = 9;
            zap[i] = r;
        }
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Fx.Burst(transform.position, baseColor, 5, 3f); Destroy(gameObject); return; }

        float dt = Time.deltaTime, t = Time.time;
        flickerT -= dt;

        switch (kind)
        {
            case TrapKind.Stun:                  // crackling electric node
                if (flickerT <= 0f)
                {
                    flickerT = 0.06f;
                    for (int i = 0; i < zap.Length; i++)
                    {
                        var r = zap[i];
                        r.enabled = Random.value < 0.55f;
                        if (r.enabled && Random.value < 0.35f)
                        {
                            r.sprite = Art.Zap(Random.Range(0, 3));
                            r.transform.localRotation = Quaternion.Euler(0, 0, i * 120f + 30f + Random.Range(-40f, 40f));
                        }
                        r.color = new Color(0.7f, 0.95f, 1f, Random.Range(0.75f, 1f));
                    }
                }
                glow.color = new Color(0.3f, 0.85f, 1f, 0.18f + 0.2f * Mathf.Abs(Mathf.Sin(t * 9f)));
                zapT -= dt;                       // the node hums and spits while it waits
                if (zapT <= 0f)
                {
                    zapT = Random.Range(1.2f, 2.2f);
                    Sfx.PlayAt(transform.position, Random.value < 0.35f ? "elec_crackle" : "elec_hum",
                               0.3f, Random.Range(0.95f, 1.1f));
                }
                break;

            case TrapKind.Boom:                   // blinks like a spider, hard
                sr.color = Mathf.Sin(t * 45f) > 0f ? Color.white
                                                   : Color.Lerp(baseColor, Color.black, 0.6f);
                glow.color = new Color(1f, 0.5f, 0.1f, Mathf.Sin(t * 45f) > 0f ? 0.42f : 0.14f);
                dripT -= dt;                      // the fuse ticks toward the bang
                if (dripT <= 0f) { dripT = 0.45f; Sfx.PlayAt(transform.position, "trap_boom_arm", 0.3f, 1.3f); }
                break;

            default:                              // Slow: goo breathes and drips
                transform.localScale = Vector3.one * (BaseScale * (1f + Mathf.Sin(t * 3.2f) * 0.05f));
                glow.color = new Color(0.35f, 1f, 0.45f, 0.16f + 0.14f * (0.5f + 0.5f * Mathf.Sin(t * 3.2f)));
                dripT -= dt;
                if (dripT <= 0f)
                {
                    dripT = Random.Range(0.55f, 1.1f);
                    Fx.Burst(transform.position + (Vector3)(Random.insideUnitCircle * 0.3f),
                             new Color(0.45f, 1f, 0.5f), 2, 1.6f);
                }
                break;
        }

        // blink before expiring so it does not surprise anyone
        if (life < 3f && kind != TrapKind.Boom)
            sr.color = new Color(1f, 1f, 1f, Mathf.Sin(Time.time * 22f) > 0f ? 0.35f : 1f);
        transform.Rotate(0, 0, 18f * dt);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        var p = PlayerController.Instance;
        if (p == null) return;
        Trigger(p);
    }

    public void Disarm()
    {
        // a bullet snuffs it out in a fizz of sparks
        Fx.Sparks(transform.position, Color.Lerp(baseColor, Color.white, 0.7f), 10, 9f);
        Fx.Burst(transform.position, baseColor, 6, 4f);
        Sfx.PlayAt(transform.position, "zap", 0.4f, 1.2f);
        Destroy(gameObject);
    }

    void Trigger(PlayerController p)
    {
        Vector2 pos = transform.position;
        switch (kind)
        {
            case TrapKind.Slow:                   // green + sticky
                p.ApplySlow(3f);
                Fx.Ring(pos, C(0.35f, 1f, 0.45f), 0.3f, 2.8f, 0.42f);
                Fx.Burst(pos, C(0.35f, 1f, 0.45f), 16, 5f);
                Fx.Burst(pos, C(0.72f, 1f, 0.55f), 8, 3f);
                Sfx.PlayAt(pos, "trap_slow", 0.7f);
                break;

            case TrapKind.Boom:                   // real explosion
                Fx.Explosion(pos, C(1f, 0.55f, 0.1f), C(1f, 0.9f, 0.4f), 1.3f);
                Gore.Stain(pos, C(0.05f, 0.045f, 0.04f), 1.6f);      // scorch mark
                GameManager.Shake(0.65f);
                Sfx.PlayAt(pos, "trap_boom", 0.9f);
                if (Vector2.Distance(p.transform.position, pos) < 1.5f)
                    p.Health.TakeDamage(22f, ((Vector2)p.transform.position - pos).normalized);
                break;

            case TrapKind.Stun:                   // electric vfx on trap and victim
                p.ApplyStun(1.4f);
                Fx.Ring(pos, C(0.35f, 0.9f, 1f), 0.3f, 2.6f, 0.36f);
                Fx.Sparks(pos, C(0.8f, 0.97f, 1f), 18, 15f);
                Fx.Burst(pos, C(0.3f, 0.85f, 1f), 10, 5f);
                Sfx.PlayAt(pos, "trap_stun", 0.75f);
                break;
        }
        Destroy(gameObject);
    }

    static Color C(float r, float g, float b) { return new Color(r, g, b); }
}

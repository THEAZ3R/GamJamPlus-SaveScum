using System.Collections.Generic;
using UnityEngine;

// THE THIRD REALITY'S TRAP: the traps are gone - now the sky owns patches of
// the ruin. Every few seconds a cloud settles over one area and rains acid
// into it: the patch soaks through, burns whoever stands in it, and keeps
// bubbling until the cloud moves on to another part of the map.
public class AcidRain : MonoBehaviour
{
    static AcidRain instance;
    float dropT = 2.2f;

    public static int Intensity;                     // set by each leap in map three

    public static void Reset() { Intensity = 0; }

    public static AcidRain Ensure()
    {
        if (instance != null) return instance;
        var g = new GameObject("AcidRain");
        instance = g.AddComponent<AcidRain>();
        return instance;
    }

    void Update()
    {
        if (GameManager.IsOver || !GameManager.Crumble || TimeLeap.Map4) return;

        dropT -= Time.deltaTime;
        if (dropT > 0f) return;

        dropT = Mathf.Max(2.8f, 6.8f - 0.5f * Intensity) * Random.Range(0.8f, 1.3f);

        // the cloud picks one patch of the ruin and owns it for a while
        Vector2 half = GameManager.ArenaHalf - Vector2.one * 2.6f;
        for (int tries = 0; tries < 12; tries++)
        {
            Vector2 at = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
            if (Pit.Inside(at)) continue;
            AcidField.Summon(at, Random.Range(2.3f, 3.1f) + 0.18f * Intensity);
            break;
        }
    }

    void OnDestroy() { if (instance == this) instance = null; }
}

// one area of acid weather: the rain falls inside it, the ground inside it
// burns, and everything the cloud touched stays dangerous until it dries
public class AcidField : MonoBehaviour
{
    static readonly List<AcidField> All = new List<AcidField>();

    float life, radius, tickT, sfxT, dropT;
    SpriteRenderer sr, core;

    public static void Summon(Vector2 at, float radius)
    {
        // one storm per patch of sky: nudge an old field aside if it is close
        foreach (var f in All.ToArray())
            if (f != null && Vector2.Distance(f.transform.position, at) < radius + f.radius * 0.6f)
                Destroy(f.gameObject);

        var g = new GameObject("AcidField");
        var root = GameManager.Root;
        if (root != null) g.transform.SetParent(root, false);
        g.transform.position = at;

        var f2 = g.AddComponent<AcidField>();
        f2.radius = radius;
        f2.life = 8.5f + 0.5f * AcidRain.Intensity;

        var cs = g.AddComponent<SpriteRenderer>();    // the soaked circle itself
        cs.sprite = SpriteFactory.Circle;
        cs.color = new Color(0.32f, 0.72f, 0.16f, 0.24f);
        cs.sortingOrder = -14;
        cs.transform.localScale = Vector3.one * radius * 2f;
        f2.sr = cs;

        var cg = new GameObject("Core");             // darker heart of the patch
        cg.transform.SetParent(g.transform, false);
        var csr = cg.AddComponent<SpriteRenderer>();
        csr.sprite = SpriteFactory.Circle;
        csr.color = new Color(0.2f, 0.5f, 0.1f, 0.22f);
        csr.sortingOrder = -13;
        cg.transform.localScale = new Vector3(1.2f, 0.9f, 1f);
        f2.core = csr;

        Fx.Ring(at, new Color(0.55f, 1f, 0.3f, 0.7f), 0.4f, radius * 2.1f, 0.4f, 24);
        Sfx.PlayAt(at, "acid_splash", 0.75f);
        All.Add(f2);
    }

    void OnDestroy() { All.Remove(this); }

    void Update()
    {
        if (GameManager.IsOver) return;
        float dt = Time.deltaTime;

        life -= dt;
        float fade = Mathf.Clamp01(life / 1.6f);
        if (life <= 0f) { Destroy(gameObject); return; }

        Vector2 pos = transform.position;
        float t = Time.time;

        // the patch breathes: bright where the rain hits, dark at the rim
        if (sr != null)
            sr.color = new Color(0.32f, 0.78f, 0.17f, (0.2f + 0.09f * Mathf.Sin(t * 3.6f + pos.x)) * fade);
        if (core != null)
        {
            core.color = new Color(0.18f, 0.5f, 0.09f, (0.2f + 0.12f * Mathf.Sin(t * 5.2f)) * fade);
            core.transform.localScale = new Vector3(1.15f + 0.15f * Mathf.Sin(t * 2.2f),
                                                   0.85f + 0.12f * Mathf.Sin(t * 2.9f), 1f);
        }

        // the rain keeps falling inside the area
        dropT -= dt;
        if (dropT <= 0f)
        {
            dropT = Random.Range(0.14f, 0.32f);
            Vector2 at = pos + (Vector2)(Random.insideUnitCircle * radius * 0.85f);
            AcidDrop.Summon(at, Random.value < 0.15f, false);   // the field owns this ground
        }

        // and the ground inside it eats whoever stands there
        tickT -= dt;
        if (tickT <= 0f)
        {
            tickT = 0.3f;
            var p = PlayerController.Instance;
            if (p != null && !p.Health.IsDead &&
                Vector2.Distance(p.transform.position, pos) <= radius)
            {
                p.Health.TakeDamage(3f, ((Vector2)p.transform.position - pos).normalized);
                p.ApplyAcid(0.8f);
            }
            var hits = Physics2D.OverlapCircleAll(pos, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null || hits[i].isTrigger) continue;
                if (hits[i].CompareTag("Player")) continue;
                var hp = hits[i].GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (Vector2.Distance(hits[i].transform.position, pos) > radius) continue;
                hp.Burn(9f);
                if (Random.value < 0.25f)
                    Fx.Burst(hits[i].transform.position, new Color(0.55f, 1f, 0.3f), 2, 2.5f);
            }
        }

        sfxT -= dt;
        if (sfxT <= 0f)
        {
            sfxT = Random.Range(0.45f, 0.85f);
            var p = PlayerController.Instance;
            bool near = p != null && Vector2.Distance(p.transform.position, pos) < radius + 3f;
            if (near || Random.value < 0.5f)
                Sfx.PlayAt(pos, "acid_bubble", near ? 0.5f : 0.3f, Random.Range(0.85f, 1.15f));
        }
    }
}

// one drop on its way down: the shadow on the floor grows, the streak
// shrinks, and where it lands a puddle starts cooking
public class AcidDrop : MonoBehaviour
{
    Vector2 at;
    float t;
    bool big, pool = true;
    Transform shade;
    SpriteRenderer sr;

    public static void Summon(Vector2 at, bool big) { Summon(at, big, true); }

    // pool false: the drop is only weather for an acid field - the patch
    // owns the ground, the splash is all the drop leaves behind
    public static void Summon(Vector2 at, bool big, bool pool)
    {
        var g = new GameObject("AcidDrop");
        g.transform.position = at;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(0.55f, 1f, 0.3f);
        sr.sortingOrder = 40;                          // over everything: it is falling
        g.transform.localScale = big ? Vector3.one * 0.5f : Vector3.one * 0.34f;

        var sg = new GameObject("Shade");
        var ssr = sg.AddComponent<SpriteRenderer>();
        ssr.sprite = SpriteFactory.Circle;
        ssr.color = new Color(0.25f, 0.5f, 0.1f, 0.4f);
        ssr.sortingOrder = -13;
        sg.transform.localScale = big ? new Vector3(0.8f, 0.44f, 1f) : new Vector3(0.55f, 0.3f, 1f);
        sg.transform.position = at;

        var d = g.AddComponent<AcidDrop>();
        d.at = at;
        d.big = big;
        d.pool = pool;
        d.sr = sr;
        d.shade = sg.transform;
    }

    void Update()
    {
        t += Time.deltaTime / 0.55f;
        float k = Mathf.Clamp01(t);
        float height = (1f - k) * 4.2f;                // falling from the grey sky
        transform.position = new Vector3(at.x, at.y + height, 0f);
        if (shade != null) shade.position = new Vector3(at.x, at.y, 0f);

        // a streak on the way down, a bead at the end
        float e = big ? 0.5f : 0.34f;
        transform.localScale = new Vector3(e, e * (1f + (1f - k) * 2.4f), 1f);
        if (sr != null)
            sr.color = Color.Lerp(new Color(0.35f, 0.8f, 0.18f), new Color(0.65f, 1f, 0.4f), k);

        if (t >= 1f) Land();
    }

    void Land()
    {
        if (pool)
            FluidPool.Spawn(at, FluidKind.Acid, big ? Random.Range(2.1f, 2.7f) : Random.Range(0.9f, 1.7f));
        Fx.Burst(at, new Color(0.5f, 0.95f, 0.25f), big ? 14 : 7, 5.5f);
        Fx.Ring(at, new Color(0.55f, 1f, 0.3f, 0.75f), 0.15f, big ? 3f : 1.8f, 0.28f, 30);
        Sfx.PlayAt(at, "acid_splash", big ? 0.7f : 0.5f, Random.Range(0.92f, 1.12f));
        if (shade != null) Destroy(shade.gameObject);
        Destroy(gameObject);
    }
}

// ============================================================ CRUMBLE DEBRIS
// bits of two maps that never finished falling: they hang over the ruin,
// turning slowly, waiting to be part of a third thing that never comes
public class CrumbleDebris : MonoBehaviour
{
    SpriteRenderer sr;
    Vector2 basePos;
    float seed, spin, bobSpeed;

    public static void Spawn(Vector2 at)
    {
        var g = new GameObject("CrumbleShard");
        var root = GameManager.Root;
        if (root != null) g.transform.SetParent(root, false);
        g.transform.position = at;
        g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Random.value < 0.5f ? Art.Gib(Random.Range(0, 4)) : Art.VoidTile();
        sr.color = Random.value < 0.4f
            ? new Color(0.55f, 0.45f, 0.9f, 0.8f)       // a piece of the lab
            : new Color(0.55f, 0.55f, 0.6f, 0.8f);      // a piece of the arena
        sr.sortingOrder = -9;
        float s = Random.Range(0.4f, 0.9f);
        g.transform.localScale = new Vector3(s, s, 1f);

        var d = g.AddComponent<CrumbleDebris>();
        d.sr = sr;
        d.basePos = at;
        d.seed = Random.Range(0f, 6.28f);
        d.spin = Random.Range(-14f, 14f);
        d.bobSpeed = Random.Range(0.6f, 1.2f);
    }

    void Update()
    {
        transform.Rotate(0, 0, spin * Time.deltaTime);
        float y = Mathf.Sin(Time.time * bobSpeed + seed) * 0.14f;
        transform.position = new Vector3(basePos.x, basePos.y + y, 0f);
        if (sr != null)
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b,
                                 0.62f + 0.2f * Mathf.Sin(Time.time * 1.7f + seed));
    }
}

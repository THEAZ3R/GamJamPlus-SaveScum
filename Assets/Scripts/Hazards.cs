using System.Collections.Generic;
using UnityEngine;

// THE LAB ERA: fluid pools and hazard machines. Oil slows and remembers fire;
// blood conducts; fire and sparks walk from pool to pool across the floor.
public enum FluidKind { Oil, Blood, Fire, Elec, Acid, Slime }

public class FluidPool : MonoBehaviour
{
    public static readonly List<FluidPool> All = new List<FluidPool>();

    FluidKind kind;
    float life, tickT, spreadT, size, sfxT;
    SpriteRenderer sr, flame;

    public FluidKind Kind { get { return kind; } }

    public static FluidPool Spawn(Vector2 pos, FluidKind kind, float size)
    {
        var g = new GameObject("Fluid_" + kind);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        g.transform.localScale = Vector3.one * size;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.BloodSplat(Random.Range(0, 4));
        sr.color = ColorOf(kind);
        sr.sortingOrder = -16;                      // on the floor: over tiles, under units

        var f = g.AddComponent<FluidPool>();
        f.kind = kind;
        f.size = size;
        f.sr = sr;
        f.life = kind == FluidKind.Fire ? 5f : kind == FluidKind.Elec ? 7f
               : kind == FluidKind.Acid ? 11f
               : kind == FluidKind.Slime ? 14f : 20f;    // the rain dries up too
        f.spreadT = Random.Range(0.1f, 0.35f);

        // real flames over the burning pool, flickering on their own clock
        if (kind == FluidKind.Fire)
        {
            var fg = new GameObject("Flame");
            fg.transform.SetParent(g.transform, false);
            var fsr = fg.AddComponent<SpriteRenderer>();
            fsr.sprite = Art.Flame();
            fsr.sortingOrder = -10;                  // over the floor paint, under units
            f.flame = fsr;
        }

        All.Add(f);
        return f;
    }

    static Color ColorOf(FluidKind k)
    {
        switch (k)
        {
            case FluidKind.Oil:   return new Color(0.05f, 0.05f, 0.06f);
            case FluidKind.Blood: return new Color(0.55f, 0.07f, 0.06f);
            case FluidKind.Fire:  return new Color(1f, 0.42f, 0.10f);
            case FluidKind.Acid:  return new Color(0.45f, 0.95f, 0.22f);
            case FluidKind.Slime: return new Color(0.46f, 0.10f, 0.20f);
            default:              return new Color(0.35f, 0.85f, 1f);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        life -= dt;
        if (GameManager.IsOver) return;

        if (life <= 0f) { Destroy(gameObject); return; }

        float t = Time.time;
        var p = PlayerController.Instance;
        Vector2 pos = transform.position;
        bool onPlayer = p != null && Vector2.Distance(p.transform.position, pos) <= size * 0.55f;

        switch (kind)
        {
            case FluidKind.Oil:                        // slick: you drag through it
                sr.color = Color.Lerp(new Color(0.05f, 0.05f, 0.06f), new Color(0.2f, 0.16f, 0.1f),
                                      0.3f + 0.2f * Mathf.Sin(t * 2.2f));
                if (onPlayer) p.ApplySlow(0.5f);
                break;

            case FluidKind.Blood:                      // quiet red slick, waiting for a current
                sr.color = Color.Lerp(new Color(0.55f, 0.07f, 0.06f), new Color(0.75f, 0.1f, 0.08f),
                                      0.5f + 0.5f * Mathf.Sin(t * 1.6f));
                break;

            case FluidKind.Fire:                       // burns, and keeps burning the ground
                sr.color = Color.Lerp(new Color(1f, 0.42f, 0.1f), new Color(1f, 0.85f, 0.3f),
                                      Mathf.PingPong(t * 4f, 1f));
                if (flame != null)                     // the tongues lick and settle
                {
                    float fl = 0.8f + 0.35f * Mathf.PingPong(t * 5f + pos.x, 1f);
                    flame.transform.localScale = new Vector3(fl, fl * 1.3f, 1f);
                    flame.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 7f + pos.y) * 8f);
                    flame.color = Color.Lerp(new Color(1f, 0.5f, 0.12f), new Color(1f, 0.95f, 0.55f),
                                             Mathf.PingPong(t * 6f, 1f));
                }
                sfxT -= dt;                            // the fire keeps talking
                if (sfxT <= 0f)
                {
                    sfxT = Random.Range(0.7f, 1.5f);
                    if (Random.value < 0.55f) Sfx.PlayAt(pos, "fire_cackle", 0.35f);
                }
                tickT -= dt;
                if (tickT <= 0f)
                {
                    tickT = 0.3f;
                    if (onPlayer)
                        p.Health.TakeDamage(4f, ((Vector2)p.transform.position - pos).normalized);
                    Fx.Burst(pos + (Vector2)(Random.insideUnitCircle * size * 0.3f),
                             new Color(1f, 0.6f, 0.15f), 2, 2.5f);
                }
                break;

            case FluidKind.Acid:                      // the rain: it burns and it bubbles
                sr.color = Color.Lerp(new Color(0.42f, 0.95f, 0.2f), new Color(0.72f, 1f, 0.35f),
                                      0.5f + 0.5f * Mathf.Sin(t * 3.4f + pos.x));
                sfxT -= dt;
                if (sfxT <= 0f)
                {
                    sfxT = Random.Range(0.5f, 0.95f);  // it never stops talking
                    if (onPlayer || Random.value < 0.4f)
                        Sfx.PlayAt(pos, "acid_bubble", onPlayer ? 0.5f : 0.3f, Random.Range(0.9f, 1.2f));
                }
                tickT -= dt;
                if (tickT <= 0f)
                {
                    tickT = 0.35f;
                    if (onPlayer)
                        p.Health.TakeDamage(5f, ((Vector2)p.transform.position - pos).normalized);
                    // it eats machines too: anything non-player standing in it corrodes
                    var hits = Physics2D.OverlapCircleAll(pos, size * 0.55f);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        if (hits[i] == null || hits[i].isTrigger) continue;
                        if (hits[i].CompareTag("Player")) continue;
                        var hp = hits[i].GetComponent<Health>();
                        if (hp == null || hp.IsDead) continue;
                        hp.Burn(9f);
                        if (Random.value < 0.3f)
                            Fx.Burst(hits[i].transform.position, new Color(0.5f, 1f, 0.3f), 2, 2.5f);
                    }
                    Fx.Burst(pos + (Vector2)(Random.insideUnitCircle * size * 0.3f),
                             new Color(0.6f, 1f, 0.35f), 2, 2.5f);
                }
                break;

            case FluidKind.Slime:                     // what the horde becomes
                sr.color = Color.Lerp(new Color(0.36f, 0.07f, 0.15f), new Color(0.58f, 0.16f, 0.26f),
                                      0.5f + 0.5f * Mathf.Sin(t * 2.6f + pos.x));
                sfxT -= dt;
                if (sfxT <= 0f)
                {
                    sfxT = Random.Range(0.7f, 1.3f);
                    if (onPlayer || Random.value < 0.4f)
                        Sfx.PlayAt(pos, "acid_bubble", onPlayer ? 0.45f : 0.28f, Random.Range(0.55f, 0.75f));
                }
                tickT -= dt;
                if (tickT <= 0f)
                {
                    tickT = 0.4f;
                    if (onPlayer)
                        p.Health.TakeDamage(4f, ((Vector2)p.transform.position - pos).normalized);
                    var hits = Physics2D.OverlapCircleAll(pos, size * 0.55f);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        if (hits[i] == null || hits[i].isTrigger) continue;
                        if (hits[i].CompareTag("Player")) continue;
                        var hp = hits[i].GetComponent<Health>();
                        if (hp == null || hp.IsDead) continue;
                        hp.Burn(7f);
                    }
                    Fx.Burst(pos + (Vector2)(Random.insideUnitCircle * size * 0.3f),
                             new Color(0.6f, 0.18f, 0.3f), 2, 2.2f);
                }
                break;

            default:                                   // Elec: your legs forget themselves
                sr.color = Color.Lerp(new Color(0.35f, 0.85f, 1f), new Color(0.9f, 0.98f, 1f),
                                      Mathf.PingPong(t * 6f, 1f));
                sfxT -= dt;                            // the current hums and spits
                if (sfxT <= 0f)
                {
                    sfxT = Random.Range(0.8f, 1.7f);
                    if (Random.value < 0.6f)
                        Sfx.PlayAt(pos, Random.value < 0.25f ? "elec_crackle" : "elec_hum", 0.32f,
                                   Random.Range(0.95f, 1.1f));
                }
                tickT -= dt;
                if (tickT <= 0f)
                {
                    tickT = 0.4f;
                    if (onPlayer)
                    {
                        p.ApplyElec(1f);              // movement stutters for a second
                        p.Health.TakeDamage(2f, ((Vector2)p.transform.position - pos).normalized);
                    }
                    Fx.Sparks(pos + (Vector2)(Random.insideUnitCircle * size * 0.3f),
                              new Color(0.7f, 0.95f, 1f), 2, 5f);
                }
                break;
        }

        // the floor remembers: fire walks onto oil, current walks onto blood
        spreadT -= dt;
        if (spreadT <= 0f)
        {
            spreadT = 0.35f;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var o = All[i];
                if (o == null || o == this) continue;
                float reach = (size + o.size) * 0.55f + 0.35f;
                if (Vector2.Distance(o.transform.position, pos) > reach) continue;

                if (kind == FluidKind.Fire && o.kind == FluidKind.Oil)
                    Ignite(o);
                else if (kind == FluidKind.Elec && o.kind == FluidKind.Blood)
                    Conduct(o);
            }
        }

        // fire and current fade out visibly - and after a while the floor
        // drinks up the blood and the oil too
        if (kind == FluidKind.Fire || kind == FluidKind.Elec)
            sr.color *= new Color(1f, 1f, 1f, Mathf.Clamp01(life / (kind == FluidKind.Fire ? 1.2f : 1.6f)));
        else
        {
            float a = Mathf.Clamp01(life / 2.5f);
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, a);
        }
    }

    static void Ignite(FluidPool oil)
    {
        Vector2 pos = oil.transform.position;
        float size = oil.size;
        Destroy(oil.gameObject);
        FluidPool.Spawn(pos, FluidKind.Fire, size * 1.15f);
        // the slick goes up in one hot red breath
        Fx.Flash(pos, new Color(1f, 0.35f, 0.1f), size * 2.4f, 0.2f);
        Fx.Ring(pos, new Color(1f, 0.45f, 0.15f, 0.85f), 0.3f, size * 3.2f, 0.4f, 30);
        Fx.Burst(pos, new Color(1f, 0.6f, 0.15f), 14, 6.5f);
        Fx.Burst(pos, new Color(1f, 0.3f, 0.08f), 10, 4f);
        GameManager.Shake(0.18f);
        Sfx.PlayAt(pos, "ignite", 0.75f);
    }

    static void Conduct(FluidPool blood)
    {
        Vector2 pos = blood.transform.position;
        float size = blood.size;
        Destroy(blood.gameObject);
        FluidPool.Spawn(pos, FluidKind.Elec, size * 1.15f);
        // whitish-blue: the current racing through the blood
        Fx.Flash(pos, new Color(0.88f, 0.96f, 1f), size * 2.2f, 0.18f);
        Fx.Ring(pos, new Color(0.8f, 0.95f, 1f, 0.85f), 0.3f, size * 3f, 0.35f, 30);
        Fx.Sparks(pos, new Color(0.8f, 0.97f, 1f), 14, 9f);
        Sfx.PlayAt(pos, "elec_crackle", 0.6f, Random.Range(0.95f, 1.1f));
    }

    void OnDestroy() { All.Remove(this); }

    // is the player standing in something that squelches? (acid too - it is
    // wet before it is dangerous)
    public static bool WetAt(Vector2 pos)
    {
        for (int i = 0; i < All.Count; i++)
        {
            var f = All[i];
            if (f == null) continue;
            if (f.kind != FluidKind.Oil && f.kind != FluidKind.Blood &&
                f.kind != FluidKind.Acid && f.kind != FluidKind.Slime) continue;
            if (Vector2.Distance(pos, f.transform.position) <= f.size * 0.55f) return true;
        }
        return false;
    }

    public static void Clear()
    {
        foreach (var f in All.ToArray())
            if (f != null) Object.Destroy(f.gameObject);
    }
}

// ============================================================ MACHINES
// the lab's own plumbing, and it is alive: a furnace and a tesla coil that
// climb out of the floor, spew their element around the arena for a while,
// then sink back under and come up somewhere else entirely. They wake on
// the second leap of the new map - and the herald purges them.
public class HazardMachine : MonoBehaviour
{
    public static readonly List<HazardMachine> All = new List<HazardMachine>();

    bool fire, rising, sinking;
    float t, life, riseT, sinkT, sfxT;
    SpriteRenderer body, core;

    const float Scale = 1.9f;

    public static HazardMachine Spawn(Vector2 pos, bool fire)
    {
        var g = new GameObject(fire ? "FireMachine" : "ElecMachine");
        g.transform.position = pos;
        g.transform.localScale = Vector3.one * 0.05f;

        var body = g.AddComponent<SpriteRenderer>();
        body.sprite = fire ? Art.MachineFire() : Art.MachineElec();
        body.color = Color.white;
        body.sortingOrder = -2;

        var cg = new GameObject("Core");
        cg.transform.SetParent(g.transform, false);
        cg.transform.localPosition = new Vector3(0f, fire ? 0.18f : 0.42f, 0f);   // mouth / orb
        cg.transform.localScale = Vector3.one * (fire ? 0.5f : 0.45f);
        var core = cg.AddComponent<SpriteRenderer>();
        core.sprite = SpriteFactory.Circle;
        core.color = fire ? new Color(1f, 0.45f, 0.1f) : new Color(0.35f, 0.85f, 1f);
        core.sortingOrder = -1;

        var m = g.AddComponent<HazardMachine>();
        m.fire = fire;
        m.body = body;
        m.core = core;
        m.t = Random.Range(1.5f, 2.5f);
        m.rising = true;
        m.riseT = 0.8f;
        m.life = Random.Range(35f, 50f);
        All.Add(m);

        Sfx.PlayAt(pos, "machine_rise", 0.6f);
        Fx.Ring(pos, core.color, 0.3f, 2.4f, 0.4f, 6);
        Fx.Burst(pos, new Color(0.4f, 0.35f, 0.5f), 8, 4f);      // the floor spits it out
        return m;
    }

    void Update()
    {
        if (GameManager.IsOver) return;
        float dt = Time.deltaTime;
        Vector2 pos = transform.position;

        // coming up out of the tilework
        if (rising)
        {
            riseT -= dt;
            float f = 1f - Mathf.Clamp01(riseT / 0.8f);
            transform.localScale = Vector3.one * (Scale * Mathf.SmoothStep(0.05f, 1f, f));
            if (Random.value < 0.14f)
                Fx.Burst(pos + (Vector2)(Random.insideUnitCircle * 0.6f),
                         new Color(0.4f, 0.35f, 0.5f), 2, 2.5f);
            if (riseT <= 0f) { rising = false; transform.localScale = Vector3.one * Scale; }
            return;                                   // it does not spew on the way up
        }

        // going back under
        if (sinking)
        {
            sinkT -= dt;
            float f = Mathf.Clamp01(sinkT / 0.8f);
            transform.localScale = Vector3.one * (Scale * Mathf.SmoothStep(0.05f, 1f, f));
            if (Random.value < 0.12f)
                Fx.Burst(pos + (Vector2)(Random.insideUnitCircle * 0.5f),
                         new Color(0.4f, 0.35f, 0.5f), 2, 2f);
            if (sinkT <= 0f) Respawn();               // somewhere else entirely
            return;
        }

        // idle voice: the furnace crackles, the coil hums
        sfxT -= dt;
        if (sfxT <= 0f)
        {
            sfxT = Random.Range(1.2f, 2.4f);
            if (fire && Random.value < 0.6f) Sfx.PlayAt(pos, "fire_cackle", 0.3f);
            else if (!fire && Random.value < 0.7f) Sfx.PlayAt(pos, "elec_hum", 0.32f, Random.Range(0.95f, 1.1f));
        }

        float w = Mathf.Sin(Time.time * (fire ? 7f : 11f));
        core.color = fire
            ? new Color(1f, 0.45f + 0.25f * w, 0.1f)
            : new Color(0.35f + 0.2f * w, 0.85f, 1f);

        life -= dt;
        if (life <= 0f)
        {
            sinking = true;
            sinkT = 0.8f;
            Sfx.PlayAt(pos, "machine_sink", 0.6f);
            return;
        }

        t -= dt;
        if (t <= 0f)
        {
            t = fire ? Random.Range(2.2f, 3.0f) : Random.Range(2.6f, 3.4f);
            Spew();
        }
    }

    // back under the floor, then out again somewhere else
    void Respawn()
    {
        Vector2 pos = transform.position;
        var p = PlayerController.Instance;
        for (int i = 0; i < 30; i++)
        {
            Vector2 c = new Vector2(Random.Range(-GameManager.ArenaHalf.x + 2f, GameManager.ArenaHalf.x - 2f),
                                    Random.Range(-GameManager.ArenaHalf.y + 2f, GameManager.ArenaHalf.y - 2f));
            if (Pit.Inside(c)) continue;
            if (p != null && Vector2.Distance(c, p.transform.position) < 4f) continue;
            pos = c;
            break;
        }
        transform.position = pos;
        transform.localScale = Vector3.one * 0.05f;
        rising = true;
        sinking = false;
        riseT = 0.8f;
        life = Random.Range(35f, 50f);
        t = Random.Range(1.5f, 2.5f);
        Sfx.PlayAt(pos, "machine_rise", 0.55f);
        Fx.Ring(pos, fire ? new Color(1f, 0.5f, 0.15f) : new Color(0.4f, 0.9f, 1f), 0.3f, 2.2f, 0.4f, 6);
    }

    void Spew()
    {
        Vector2 pos = transform.position;
        Color c = fire ? new Color(1f, 0.55f, 0.12f) : new Color(0.45f, 0.9f, 1f);

        // biased toward the player: these things hunt with their spills
        var p = PlayerController.Instance;
        Vector2 toPlayer = p != null ? ((Vector2)p.transform.position - pos).normalized : Vector2.up;
        float baseA = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;

        for (int i = 0; i < 3; i++)
        {
            float a = (baseA + Random.Range(-70f, 70f)) * Mathf.Deg2Rad;
            Vector2 at = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.4f, 4.2f);
            at.x = Mathf.Clamp(at.x, -GameManager.ArenaHalf.x + 1f, GameManager.ArenaHalf.x - 1f);
            at.y = Mathf.Clamp(at.y, -GameManager.ArenaHalf.y + 1f, GameManager.ArenaHalf.y - 1f);
            FluidPool.Spawn(at, fire ? FluidKind.Fire : FluidKind.Elec, Random.Range(0.7f, 1.15f));
        }

        Fx.Burst(pos + toPlayer * 0.5f, c, 14, 7f);
        if (fire) Fx.Sparks(pos + toPlayer * 0.5f, new Color(1f, 0.8f, 0.4f), 10, 9f);
        else Fx.Sparks(pos + toPlayer * 0.5f, new Color(0.8f, 0.97f, 1f), 14, 12f);
        GameManager.Shake(0.12f);
        Sfx.PlayAt(pos, "machine_spew", 0.6f, fire ? 1f : 1.2f);
    }

    void OnDestroy() { All.Remove(this); }

    // the herald's purge: every machine that is not his goes up at once
    public static void ClearAll()
    {
        foreach (var m in All.ToArray())
        {
            if (m == null) continue;
            Vector2 pos = m.transform.position;
            Fx.Explosion(pos, new Color(0.7f, 0.4f, 1f), Color.white, 1.1f);
            Fx.Ring(pos, new Color(0.75f, 0.4f, 1f), 0.4f, 4f, 0.45f, 30);
            Sfx.PlayAt(pos, "trap_boom", 0.55f);
            GameManager.Shake(0.3f);
            Object.Destroy(m.gameObject);
        }
    }
}

// ============================================================ CONSOLE
// retro terminals the lab never turned off: phosphor glyphs, a blinking
// cursor, the odd signal hiccup
public class ConsoleScreen : MonoBehaviour
{
    SpriteRenderer sr, cursor;
    float flickT, cursorT;
    bool dim;

    public static ConsoleScreen Spawn(Vector2 pos)
    {
        var g = new GameObject("Console");
        var root = GameManager.Root;                  // dies with the map it hangs on
        if (root != null) g.transform.SetParent(root, false);
        g.transform.position = pos;
        g.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.ComputerScreen();
        sr.color = Color.white;
        sr.sortingOrder = -5;

        var cg = new GameObject("Cursor");
        cg.transform.SetParent(g.transform, false);
        cg.transform.localPosition = new Vector3(-0.11f, -0.21f, 0f);
        cg.transform.localScale = new Vector3(0.10f, 0.05f, 1f);
        var csr = cg.AddComponent<SpriteRenderer>();
        csr.sprite = SpriteFactory.Square;
        csr.color = new Color(0.4f, 1f, 0.55f, 0.9f);
        csr.sortingOrder = -4;

        var c = g.AddComponent<ConsoleScreen>();
        c.sr = sr;
        c.cursor = csr;
        c.flickT = Random.Range(0.5f, 1.5f);
        return c;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        flickT -= dt;
        if (flickT <= 0f)
        {
            flickT = Random.Range(0.4f, 1.6f);
            dim = Random.value < 0.25f;               // a signal hiccup now and then
        }
        sr.color = new Color(1f, 1f, 1f,
                             dim ? 0.55f + 0.15f * Mathf.Sin(Time.time * 30f)
                                 : 0.92f + 0.08f * Mathf.Sin(Time.time * 11f));

        cursorT -= dt;
        if (cursorT <= 0f) { cursorT = 0.5f; cursor.enabled = !cursor.enabled; }
    }
}

// ============================================================ VENTS
// floor grates the herald shares a nervous system with: when the flame
// sweeps, the whole floor answers through them.
public class Vent : MonoBehaviour
{
    public static readonly List<Vent> All = new List<Vent>();

    SpriteRenderer sr;

    public static Vent Spawn(Vector2 pos)
    {
        var g = new GameObject("Vent");
        var root = GameManager.Root;
        if (root != null) g.transform.SetParent(root, false);
        g.transform.position = pos;
        g.transform.localScale = new Vector3(1.6f, 0.8f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.Vent();
        sr.sortingOrder = -15;                       // with the other floor paint
        var v = g.AddComponent<Vent>();
        v.sr = sr;
        All.Add(v);
        return v;
    }

    void Update()
    {
        // the grates breathe with the room's heat
        sr.color = new Color(1f, 1f, 1f, 0.8f + 0.2f * Mathf.Sin(Time.time * 2f + transform.position.x));
    }

    // the herald's flamethrower: every vent in the room jets fire at once
    public static void Burst()
    {
        foreach (var v in All)
        {
            if (v == null) continue;
            Vector2 pos = v.transform.position;
            Fx.Burst(pos + Vector2.up * 0.6f, new Color(1f, 0.55f, 0.12f), 12, 7f);
            Fx.Sparks(pos + Vector2.up * 0.8f, new Color(1f, 0.8f, 0.4f), 8, 10f);
            FluidPool.Spawn(pos + Vector2.up * 0.9f, FluidKind.Fire, Random.Range(0.6f, 0.9f));
        }
    }

    void OnDestroy() { All.Remove(this); }

    public static void ClearAll()
    {
        foreach (var v in All.ToArray())
            if (v != null) Object.Destroy(v.gameObject);
    }
}

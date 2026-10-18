using System.Collections.Generic;
using UnityEngine;

// THE MACHINE HERALD: boss two. A hextech hexagon spinning slowly over the
// floor, and inside it a distinct floating rider whose bright magenta eye is
// exactly what you shoot. It throws oil in wide volleys, sweeps flame across
// almost the whole room, teleports behind you for a perfect-dash stab, plants
// a squishy shield and fills the room with laser bots around you. At half
// the eye goes white and the sky itself starts shooting.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Health))]
public class Herald : MonoBehaviour
{
    public static Herald Active;
    public static bool BossAlive { get { return Active != null; } }

    enum St { Float, OilWind, FlameWind, Flame, TeleWarn, TeleHide, TeleStab,
              BotShield, BotStagger, ZeusCast, DashWarn, DashIn, LaserSweep, DashOut, Recover }

    static readonly Color NeonPurple = new Color(0.7f, 0.35f, 1f);
    static readonly Color NeonCyan = new Color(0.35f, 0.95f, 1f);
    static readonly Color Magenta = new Color(1f, 0.3f, 0.8f);

    St st = St.Float;
    int atk = -1, phase = 1;
    float stT, attackCd, dmgScale, speed, contactT, flameTick, boltT, trailT, sweepTick, coreFlash, hitT;
    float humT, dashFireT;
    bool stabHit, shieldUp, sweeping;
    Vector2 moveDir, dashDir, dashTarget, telePos, sweepFrom;

    Rigidbody2D rb;
    CircleCollider2D bodyCol;
    Health health;
    Transform visual, hexPart, corePart;
    SpriteRenderer eyeSr;
    DestructibleWall shield;
    readonly List<LaserBot> bots = new List<LaserBot>();
    SpriteRenderer[] coreFlashers;
    SpriteRenderer flameCone;
    GUIStyle nameStyle;

    public static Herald Spawn(float difficulty)
    {
        var p = PlayerController.Instance;

        // THE PURGE: every trap, every stray machine and everything that
        // still walks for the old system dies in one rolling spectacle
        foreach (var t in Trap.All.ToArray())
            if (t != null)
            {
                Fx.Explosion(t.transform.position, NeonPurple, Color.white, 0.7f);
                Destroy(t.gameObject);
            }
        HazardMachine.ClearAll();
        foreach (var e in Enemy.All.ToArray())
        {
            if (e == null || e.Health == null) continue;
            Vector2 pos = e.transform.position;
            Fx.Explosion(pos, NeonPurple, Color.white, 0.9f);
            Fx.Ring(pos, Magenta, 0.3f, 3.2f, 0.4f, 30);
            e.Health.TakeDamage(99999f, (pos - (p != null ? (Vector2)p.transform.position : Vector2.zero)).normalized);
        }
        GameManager.Shake(1f);

        // the herald itself descends on the ring
        Vector2 pos2 = p != null ? (Vector2)p.transform.position + Vector2.right * 7f : Vector2.right * 7f;
        for (int i = 0; i < 30; i++)
        {
            Vector2 c = p != null ? (Vector2)p.transform.position : Vector2.zero;
            pos2 = c + Random.insideUnitCircle.normalized * Random.Range(7f, 10f);
            pos2.x = Mathf.Clamp(pos2.x, -GameManager.ArenaHalf.x + 2f, GameManager.ArenaHalf.x - 2f);
            pos2.y = Mathf.Clamp(pos2.y, -GameManager.ArenaHalf.y + 2f, GameManager.ArenaHalf.y - 2f);
            if (Pit.Inside(pos2)) continue;
            if (!Nav.IsFree(pos2, true)) continue;
            break;
        }

        var g = new GameObject("Herald");
        g.transform.position = pos2;
        var h = g.AddComponent<Herald>();
        h.Setup(difficulty);
        return h;
    }

    void Setup(float difficulty)
    {
        Active = this;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        dmgScale = Mathf.Sqrt(difficulty);
        speed = 4.3f;

        bodyCol = GetComponent<CircleCollider2D>();
        bodyCol.radius = 0.4f;

        health = GetComponent<Health>();
        health.OnDeath += Die;
        health.Init(700f * GameManager.HpScale(difficulty));   // heavier: reality bent around the eye
        health.showBar = false;                       // the named bar rides the HUD
        health.barColor = NeonPurple;

        visual = BuildVisual();

        attackCd = 2.2f;
        Fx.Ring(transform.position, NeonPurple, 0.5f, 8f, 0.7f, 30);
        Fx.Ring(transform.position, NeonCyan, 0.5f, 8f, 0.9f, 30);
        Fx.Burst(transform.position, Color.white, 30, 11f);
        GameManager.Shake(0.8f);
        if (TimeLeap.Instance != null)
            TimeLeap.Instance.Announce("THE MACHINE HERALD DESCENDS", 2.4f);
        Sfx.PlayAt(transform.position, "horn", 1f);
    }

    // the herald made manifest: a hextech hexagon spinning slowly on itself,
    // and inside it a distinct floating entity whose only concern is the
    // bright magenta eye at dead center - that eye is what you shoot
    Transform BuildVisual()
    {
        var root = new GameObject("HeraldVisual").transform;
        root.SetParent(transform, false);

        var hx = new GameObject("Hex").transform;
        hx.SetParent(root, false);
        hx.localScale = Vector3.one * 2.2f;
        var hsr = hx.gameObject.AddComponent<SpriteRenderer>();
        hsr.sprite = Art.HeraldHex();
        hsr.sortingOrder = 20;
        hexPart = hx;

        var core = new GameObject("Core").transform;
        core.SetParent(root, false);
        core.localScale = Vector3.one * 0.62f;
        var csr = core.gameObject.AddComponent<SpriteRenderer>();
        csr.sprite = Art.Herald();                    // the old thin blade, now the rider
        csr.sortingOrder = 21;
        corePart = core;

        var eye = new GameObject("Eye").transform;
        eye.SetParent(root, false);
        eye.localScale = Vector3.one * 0.55f;
        var esr = eye.gameObject.AddComponent<SpriteRenderer>();
        esr.sprite = Art.HeraldEye();
        esr.color = Magenta;
        esr.sortingOrder = 22;
        eyeSr = esr;

        return root;
    }

    // ------------------------------------------------------------ ai
    void Update()
    {
        if (GameManager.IsOver) { rb.SetVel(Vector2.zero); return; }
        var p = PlayerController.Instance;
        if (p == null) return;

        float dt = Time.deltaTime;
        Vector2 pos = transform.position;
        Vector2 to = (Vector2)p.transform.position - pos;
        float dist = to.magnitude;
        Vector2 dir = to / Mathf.Max(dist, 0.001f);

        // the flip waits for a clean moment so it always reads
        if (phase == 1 && health.current <= health.max * 0.5f &&
            (st == St.Float || st == St.Recover)) ToPhase2();

        // the ward can always be burned off - it just rewrites itself
        if (shieldUp && (shield == null || shield.Dead))
        {
            shieldUp = false;
            if (phase == 2) shieldRegenT = 8f;
        }
        if (phase == 2 && !shieldUp)
        {
            shieldRegenT -= dt;
            if (shieldRegenT <= 0f) BuildPhaseShield();
        }

        if (contactT > 0f) contactT -= dt;
        if (coreFlash > 0f)
        {
            coreFlash -= dt;
            if (coreFlashers != null)
            {
                Color c = Color.Lerp(Color.white, Color.clear, Mathf.PingPong(coreFlash * 6f, 1f));
                foreach (var sr in coreFlashers) if (sr != null) sr.color = Color.white;
            }
        }

        switch (st)
        {
            case St.Float:                             // drift to its fighting range
                moveDir = dist > 6.2f ? Nav.Steer(pos, p.transform.position, false)
                         : dist < 4.2f ? -dir
                         : new Vector2(-dir.y, dir.x) * 0.5f;
                attackCd -= dt;
                if (attackCd <= 0f) NextAttack(dir);
                break;

            case St.OilWind:                           // the arm comes up, the globs shine
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 180f);
                PulseVisual(0.10f);
                stT -= dt;
                if (stT <= 0f)
                {
                    // the whole patch goes black: seven big globs across a wide band
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    Sfx.PlayAt(pos, "oil_throw", 0.8f);
                    for (int i = -3; i <= 3; i++)
                    {
                        Vector2 target = ClampFloor((Vector2)p.transform.position +
                                                    perp * (i * 3.2f) + dir * Random.Range(-2f, 2f));
                        OilGlob.Lob(pos + dir * 0.8f + Vector2.up * 0.4f + perp * (i * 0.28f), target);
                    }
                    EndAttack(1.9f);
                }
                break;

            case St.FlameWind:                         // the cone telegraphs on the floor
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 140f);
                stT -= dt;
                if (stT <= 0f)
                {
                    st = St.Flame;
                    stT = 1.1f;
                    flameTick = 0f;
                    ClearWarn();
                    MakeFlameCone(dir);                // the whole cone catches, not just the edge
                    MakeSweepLine(7f, 0.34f, new Color(1f, 0.5f, 0.15f, 0.5f));
                    Vent.Burst();                      // the floor answers through the grates
                    Sfx.PlayAt(transform.position, "flame", 0.7f);
                }
                break;

            case St.Flame:                             // a burning fan sweeps the room
                moveDir = Vector2.zero;
                {
                    float prog = 1f - Mathf.Clamp01(stT / 1.1f);
                    float baseA = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    float a = Mathf.Lerp(baseA - 90f, baseA + 90f, prog);
                    Vector2 fd = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                    RotateSweepLine(a, pos + fd * 3.5f);

                    // the cone itself is on fire: it flickers, breathes, and
                    // throws flame bursts along the whole ray - edge to edge
                    if (flameCone != null)
                    {
                        flameCone.color = new Color(1f, 0.45f, 0.1f,
                            0.5f + 0.18f * Mathf.Sin(Time.time * 13f) + 0.1f * Mathf.Sin(Time.time * 29f));
                        flameCone.transform.localScale =
                            new Vector3(7.8f * (0.94f + 0.08f * Mathf.Sin(Time.time * 17f)), 6.2f, 1f);
                    }

                    Fx.Burst(pos + fd * 3.2f, new Color(1f, 0.55f, 0.12f), 3, 3f);
                    flameTick -= dt;
                    if (flameTick <= 0f)
                    {
                        flameTick = 0.14f;
                        Fx.Burst(pos + fd * Random.Range(1f, 3.6f), new Color(1f, 0.65f, 0.2f), 2, 2.5f);
                        FluidPool.Spawn(pos + fd * Random.Range(0.8f, 3.9f), FluidKind.Fire, Random.Range(0.65f, 1f));

                        // caught in the fan: burn damage and a short burn on the suit
                        Vector2 fp = pos + fd * 3.2f;
                        float ang = Vector2.Angle(fd, ((Vector2)p.transform.position - pos).normalized);
                        if (Vector2.Distance(p.transform.position, pos) < 4.2f && ang < 46f)
                            p.Health.TakeDamage(5f * dmgScale, ((Vector2)p.transform.position - pos).normalized);
                    }
                }
                stT -= dt;
                if (stT <= 0f) { ClearSweepLine(); EndAttack(1.6f); }
                break;

            case St.TeleWarn:                          // the mark: right behind you
                moveDir = Vector2.zero;
                stT -= dt;
                if (stT <= 0f)
                {
                    st = St.TeleHide;
                    stT = 0.22f;
                    Sfx.PlayAt(pos, "tele", 0.7f);
                    Fx.Burst(pos, NeonPurple, 16, 8f);
                    Fx.Sparks(pos, NeonCyan, 10, 12f);
                    if (visual != null) visual.gameObject.SetActive(false);
                    bodyCol.enabled = false;
                }
                break;

            case St.TeleHide:
                moveDir = Vector2.zero;
                stT -= dt;
                if (stT <= 0f)
                {
                    transform.position = telePos;
                    rb.position = telePos;
                    if (visual != null) visual.gameObject.SetActive(true);
                    bodyCol.enabled = true;
                    st = St.TeleStab;
                    stT = 0.32f;
                    stabHit = false;
                    Fx.Ring(telePos, Magenta, 0.2f, 2.2f, 0.3f, 30);
                    Fx.Burst(telePos, NeonPurple, 14, 7f);
                }
                break;

            case St.TeleStab:                          // the blade comes out on a beat
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 900f);
                hitT += dt;
                if (!stabHit && hitT >= 0.14f)
                {
                    stabHit = true;
                    Fx.Burst(pos + dir * 0.9f, Magenta, 12, 9f);
                    Fx.Sparks(pos + dir * 0.9f, Color.white, 8, 13f);
                    Sfx.PlayAt(pos + dir * 0.9f, "stab", 0.85f);
                    if (dist < 1.6f)                   // dash the i-frames or bleed
                        p.Health.TakeDamage(26f * dmgScale, dir);
                }
                stT -= dt;
                if (stT <= 0f) EndAttack(1.2f);
                break;

            case St.BotShield:                         // the shield breeds bots
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 90f);
                stT -= dt;
                if (shield == null || shield.Dead)
                {
                    // broken: the channel snaps and the bots spark out
                    shieldUp = false;
                    st = St.BotStagger;
                    stT = 0.9f;
                    Sfx.PlayAt(pos, "stagger", 0.7f);
                    Fx.Burst(pos, NeonCyan, 20, 9f);
                    Fx.Ring(pos, NeonCyan, 0.3f, 3.5f, 0.4f, 30);
                    foreach (var b in bots) if (b != null) b.Dismiss();
                    bots.Clear();
                    GameManager.Shake(0.3f);
                }
                else if (stT <= 0f)
                {
                    shieldUp = false;
                    if (shield != null) { Destroy(shield.gameObject); shield = null; }
                    foreach (var b in bots) if (b != null) b.Dismiss();
                    bots.Clear();
                    EndAttack(1.6f);
                }
                break;

            case St.BotStagger:
                moveDir = Vector2.zero;
                TintFlash(true);
                stT -= dt;
                if (stT <= 0f) { TintFlash(false); EndAttack(1.2f); }
                break;

            case St.ZeusCast:                          // the sky takes the shots for him
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 120f);
                boltT -= dt;
                if (boltT <= 0f)
                {
                    boltT = 0.65f;
                    ZeusBolt.Summon(p.transform.position);      // where you stand, not where you go
                    Sfx.PlayAt(p.transform.position, "elec_crackle", 0.45f, 1.2f);
                    GameManager.Shake(0.2f);
                }
                stT -= dt;
                if (stT <= 0f) EndAttack(1.6f);
                break;

            case St.DashWarn:                          // the neon lane paints itself
                moveDir = Vector2.zero;
                stT -= dt;
                if (stT <= 0f)
                {
                    st = St.DashIn;
                    stT = 0.4f;
                    trailT = 0f;
                    dashFireT = 0f;
                    Sfx.PlayAt(transform.position, "sweep", 0.8f);
                    ClearWarn();
                }
                break;

            case St.DashIn:                            // a magenta streak across the room
                moveDir = dashDir * 3.6f;
                trailT -= dt;
                if (trailT <= 0f)
                {
                    trailT = 0.03f;
                    Fx.Sparks(pos, NeonCyan, 3, 3f);
                    Fx.Burst(pos, new Color(1f, 0.3f, 0.8f, 0.55f), 2, 2f);
                }
                dashFireT -= dt;                       // the lane it crosses burns after it
                if (dashFireT <= 0f)
                {
                    dashFireT = 0.15f;
                    FluidPool.Spawn(pos + (Vector2)(Random.insideUnitCircle * 0.35f),
                                    FluidKind.Fire, Random.Range(0.5f, 0.75f));
                    Sfx.PlayAt(pos, "fire_cackle", 0.35f);
                }
                if (!stabHit && dist < 1.3f)
                {
                    stabHit = true;
                    if (contactT <= 0f)
                    {
                        contactT = 0.6f;
                        p.Health.TakeDamage(10f * dmgScale, dir);
                    }
                }
                stT -= dt;
                if (stT <= 0f)
                {
                    st = St.LaserSweep;
                    stT = 0.42f;
                    sweepTick = 0f;
                    float a0 = Mathf.Atan2(dashDir.y, dashDir.x) * Mathf.Rad2Deg;
                    sweepFrom = pos;
                    MakeSweepLine(7f, 0.22f, new Color(1f, 0.3f, 0.8f, 0.7f));
                    Sfx.PlayAt(pos, "beam", 0.85f);
                    GameManager.Shake(0.25f);
                }
                break;

            case St.LaserSweep:                        // the blade of light sweeps around it
                moveDir = Vector2.zero;
                {
                    float prog = 1f - Mathf.Clamp01(stT / 0.42f);
                    Vector2 hd = (sweepFrom - pos).normalized;
                    float baseA = Mathf.Atan2(hd.y, hd.x) * Mathf.Rad2Deg;
                    float a = Mathf.Lerp(baseA - 75f, baseA + 75f, prog);
                    Vector2 ld = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                    RotateSweepLine(a, pos + ld * 3.5f);

                    sweepTick -= dt;
                    if (sweepTick <= 0f)
                    {
                        sweepTick = 0.07f;
                        Vector2 rel = (Vector2)p.transform.position - pos;
                        float along = Vector2.Dot(rel, ld);
                        float side = Mathf.Abs(ld.x * rel.y - ld.y * rel.x);
                        if (along > 0f && along < 7f && side < 0.4f)
                            p.Health.TakeDamage(16f * dmgScale, ((Vector2)p.transform.position - pos).normalized);
                    }
                }
                stT -= dt;
                if (stT <= 0f)
                {
                    ClearSweepLine();
                    st = St.DashOut;
                    stT = 0.35f;
                    trailT = 0f;
                    dashDir = (pos - (Vector2)p.transform.position + new Vector2(0.5f, 0.3f)).normalized;
                }
                break;

            case St.DashOut:                           // and it is gone before you turn
                moveDir = dashDir * 3.2f;
                trailT -= dt;
                if (trailT <= 0f)
                {
                    trailT = 0.03f;
                    Fx.Sparks(pos, NeonCyan, 2, 3f);
                }
                stT -= dt;
                if (stT <= 0f) EndAttack(1.2f);
                break;

            case St.Recover:
                moveDir = new Vector2(-dir.y, dir.x) * 0.35f;
                stT -= dt;
                if (stT <= 0f) { st = St.Float; }
                break;
        }

        if (visual != null)
        {
            // it never lands: a hover bob, higher once the core ignites
            float lift = phase == 2 ? 0.45f : 0.25f;
            visual.localPosition = new Vector3(0f, lift + Mathf.Sin(Time.time * 2.6f) * 0.08f, 0f);
        }

        if (hexPart != null)
            hexPart.localRotation = Quaternion.Euler(0, 0, Time.time * (phase == 2 ? 46f : 24f));

        if (eyeSr != null)                      // the eye: what you shoot, and it knows it
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (phase == 2 ? 11f : 4f));
            eyeSr.color = phase == 2 ? Color.Lerp(Magenta, Color.white, pulse)
                                     : Color.Lerp(new Color(0.8f, 0.2f, 0.65f), Magenta, pulse);
            eyeSr.transform.localScale = Vector3.one * (0.55f * (1f + 0.14f * pulse));
        }

        // it is more machine than anything else: the current hums inside it
        humT -= dt;
        if (humT <= 0f)
        {
            humT = Random.Range(2f, 3.2f);
            Sfx.PlayAt(pos, "elec_hum", 0.4f, phase == 2 ? 1.15f : 1f);
        }

        if (shieldUp && shield != null)
        {
            shield.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            shield.transform.localPosition = dir * 1.05f;
        }
    }

    float shieldRegenT;

    void FixedUpdate()
    {
        if (GameManager.IsOver) { rb.SetVel(Vector2.zero); return; }
        Vector2 v = moveDir * speed * (phase == 2 ? 1.12f : 1f);
        Vector2 p0 = rb.position;
        if (Pit.Blocks(p0, p0 + v * Time.fixedDeltaTime)) v = Vector2.zero;
        rb.SetVel(v);
    }

    // ------------------------------------------------------------ flow
    void FacePlayer(Vector2 dir, float dt, float rate)
    {
        if (corePart == null) return;
        // only the rider turns - the hexagon keeps its own slow spin
        float want = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        corePart.localRotation = Quaternion.Euler(0, 0,
            Mathf.MoveTowardsAngle(corePart.localEulerAngles.z, want, rate * dt));
    }

    void PulseVisual(float amount)
    {
        if (visual != null) visual.localScale = Vector3.one * (1f + amount * Mathf.Sin(Time.time * 26f));
    }

    void TintFlash(bool on)
    {
        if (coreFlashers == null) return;
        foreach (var sr in coreFlashers)
            if (sr != null) sr.color = on && Mathf.Sin(Time.time * 40f) > 0f ? Color.white : Color.clear;
    }

    void EndAttack(float cd)
    {
        st = St.Float;
        attackCd = cd;
        moveDir = Vector2.zero;
        if (visual != null) visual.localScale = Vector3.one;
        ClearWarn();
        ClearSweepLine();
        ClearFlameCone();
    }

    void NextAttack(Vector2 dir)
    {
        int n = phase == 2 ? 6 : 4;                  // phase two adds sky and chase
        atk = (atk + 1) % n;
        hitT = 0f;
        stabHit = false;
        switch (atk)
        {
            case 0: st = St.OilWind; stT = 0.5f; break;

            case 1:
                st = St.FlameWind; stT = 0.55f;
                MakeWarnCone(dir);
                break;

            case 2:
                st = St.TeleWarn; stT = 0.55f;
                // the mark lands just behind you, relative to the herald
                Vector2 toPlayer = (dir.sqrMagnitude > 0.01f ? dir : Vector2.right);
                telePos = (Vector2)PlayerController.Instance.transform.position + toPlayer * 1.9f;
                telePos.x = Mathf.Clamp(telePos.x, -GameManager.ArenaHalf.x + 1f, GameManager.ArenaHalf.x - 1f);
                telePos.y = Mathf.Clamp(telePos.y, -GameManager.ArenaHalf.y + 1f, GameManager.ArenaHalf.y - 1f);
                MakeTeleMark(telePos);
                break;

            case 3:
                st = St.BotShield;
                stT = 7f;
                BuildChannelShield();
                // the chokehold: six bots around you, every beam painting at once
                Vector2 pp = PlayerController.Instance.transform.position;
                for (int i = 0; i < 6; i++)
                {
                    float ringA = i * 60f * Mathf.Deg2Rad;
                    Vector2 ring = pp + new Vector2(Mathf.Cos(ringA), Mathf.Sin(ringA)) * 2.7f;
                    ring.x = Mathf.Clamp(ring.x, -GameManager.ArenaHalf.x + 1f, GameManager.ArenaHalf.x - 1f);
                    ring.y = Mathf.Clamp(ring.y, -GameManager.ArenaHalf.y + 1f, GameManager.ArenaHalf.y - 1f);
                    bots.Add(LaserBot.Summon(this, ring));
                }
                if (TimeLeap.Instance != null) TimeLeap.Instance.Announce("SHOOT THE SHIELD - STOP THE BREEDING", 1.6f);
                break;

            case 4: st = St.ZeusCast; stT = 2.3f; boltT = 0.35f; break;

            default:
                st = St.DashWarn; stT = 0.45f;
                dashTarget = PlayerController.Instance.transform.position;
                dashDir = (dashTarget - (Vector2)transform.position).normalized;
                MakeDashLane(dashDir);
                break;
        }
    }

    // ------------------------------------------------------------ phase two
    void ToPhase2()
    {
        phase = 2;
        coreFlash = 1f;

        // the core goes white: every shard of it flashes, then settles brighter
        coreFlashers = visual != null ? visual.GetComponentsInChildren<SpriteRenderer>() : new SpriteRenderer[0];
        Fx.Flash(transform.position, Color.white, 4f, 0.25f);
        Fx.Ring(transform.position, Color.white, 0.5f, 7f, 0.5f, 30);
        Fx.Ring(transform.position, NeonCyan, 0.5f, 7f, 0.7f, 30);
        Fx.Burst(transform.position, NeonPurple, 26, 10f);
        GameManager.Shake(0.7f);
        GameManager.Instance.HitStop(0.12f);
        Sfx.PlayAt(transform.position, "horn", 0.95f, 1.3f);

        BuildPhaseShield();                           // the small ward: permanent from here
        Music.SetIntensity(3);                        // phase two: up an octave, into the bits
        if (TimeLeap.Instance != null)
            TimeLeap.Instance.Announce("THE HERALD'S CORE IGNITES", 2.2f);
    }

    void BuildChannelShield()                         // squishy on purpose: burn it down
    {
        if (shield != null) Destroy(shield.gameObject);
        shield = DestructibleWall.Create(Vector2.zero, new Vector2(0.22f, 2f), 90f, 22, true);
        shield.transform.SetParent(transform, false);
        shieldUp = true;
        Sfx.PlayAt(transform.position, "shield_up", 0.7f);
    }

    void BuildPhaseShield()                           // smaller, permanent, rewrites itself
    {
        if (shield != null) Destroy(shield.gameObject);
        shield = DestructibleWall.Create(Vector2.zero, new Vector2(0.2f, 1.6f), 130f, 22, true);
        shield.transform.SetParent(transform, false);
        shieldUp = true;
        shieldRegenT = 8f;
        Fx.Ring((Vector2)transform.position + dirToPlayer() * 1.05f, NeonCyan, 0.2f, 2f, 0.35f, 30);
        Sfx.PlayAt(transform.position, "shield_up", 0.7f);
    }

    Vector2 dirToPlayer()
    {
        var p = PlayerController.Instance;
        if (p == null) return Vector2.right;
        return ((Vector2)p.transform.position - (Vector2)transform.position).normalized;
    }

    // keeps lob targets on solid floor, inside the room
    Vector2 ClampFloor(Vector2 v)
    {
        v.x = Mathf.Clamp(v.x, -GameManager.ArenaHalf.x + 1.5f, GameManager.ArenaHalf.x - 1.5f);
        v.y = Mathf.Clamp(v.y, -GameManager.ArenaHalf.y + 1.5f, GameManager.ArenaHalf.y - 1.5f);
        for (int i = 0; i < 4 && Pit.Inside(v); i++)
            v.y = v.y > 0f ? v.y - 2f : v.y + 2f;
        return v;
    }

    // ------------------------------------------------------------ telegraphs
    Transform warnStrip, sweepLine;

    void MakeWarnCone(Vector2 dir)
    {
        var g = new GameObject("FlameWarn");
        g.transform.position = (Vector2)transform.position + dir * 3.2f;
        g.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        g.transform.localScale = new Vector3(7.8f, 6.2f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(1f, 0.4f, 0.1f, 0.26f);
        sr.sortingOrder = -5;
        warnStrip = g.transform;
    }

    // the swept area itself catches: fire fills the whole cone, edge to edge
    void MakeFlameCone(Vector2 dir)
    {
        var g = new GameObject("FlameCone");
        g.transform.position = (Vector2)transform.position + dir * 3.2f;
        g.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        g.transform.localScale = new Vector3(7.8f, 6.2f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(1f, 0.45f, 0.1f, 0.55f);
        sr.sortingOrder = 19;                       // over the floor, under the units
        flameCone = sr;
    }

    void ClearFlameCone() { if (flameCone != null) { Destroy(flameCone.gameObject); flameCone = null; } }

    void MakeTeleMark(Vector2 at)    {
        var g = new GameObject("TeleMark");
        g.transform.position = at;
        g.transform.localScale = Vector3.one * 2.4f;
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(1f, 0.3f, 0.8f, 0.35f);
        sr.sortingOrder = -4;
        warnStrip = g.transform;
    }

    void MakeDashLane(Vector2 dir)
    {
        var g = new GameObject("DashLane");
        g.transform.position = (Vector2)transform.position + dir * 4.5f;
        g.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        g.transform.localScale = new Vector3(9.5f, 1f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square;
        sr.color = new Color(1f, 0.3f, 0.8f, 0.32f);
        sr.sortingOrder = -5;
        warnStrip = g.transform;
    }

    void ClearWarn() { if (warnStrip != null) { Destroy(warnStrip.gameObject); warnStrip = null; } }

    void MakeSweepLine(float len, float width, Color c)
    {
        if (sweepLine != null) Destroy(sweepLine.gameObject);
        var g = new GameObject("SweepLaser");
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square;
        sr.color = c;
        sr.sortingOrder = 40;
        sweepLine = g.transform;
        RotateSweepLine(0f, (Vector2)transform.position + Vector2.right * (len * 0.5f));
        // width via scale on the short axis
        sweepLine.localScale = new Vector3(len, width, 1f);
    }

    void RotateSweepLine(float angleDeg, Vector2 head)
    {
        if (sweepLine == null) return;
        Vector2 tail = transform.position;
        Vector2 mid = (tail + head) * 0.5f;
        sweepLine.position = mid;
        sweepLine.rotation = Quaternion.Euler(0, 0, angleDeg);
        sweepLine.localScale = new Vector3(Vector2.Distance(tail, head), sweepLine.localScale.y, 1f);
    }

    void ClearSweepLine() { if (sweepLine != null) { Destroy(sweepLine.gameObject); sweepLine = null; } }

    // test tooling: the eye closes without the ceremony (bots see the dead
    // owner and dismiss themselves)
    public static void DevDismiss()
    {
        var h = Active;
        Active = null;
        if (h != null)
        {
            h.ClearFlameCone();
            h.ClearWarn();
            h.ClearSweepLine();
            Destroy(h.gameObject);
        }
    }

    // ------------------------------------------------------------ contact
    void OnCollisionStay2D(Collision2D c)
    {
        if (!c.collider.CompareTag("Player")) return;
        if (contactT > 0f) return;
        var p = PlayerController.Instance;
        if (p == null) return;
        Vector2 away = ((Vector2)c.collider.transform.position - (Vector2)transform.position).normalized;
        if (p.Health.TakeDamage(8f * dmgScale, away)) contactT = 0.7f;
    }

    public void ForgetBot(LaserBot b) { bots.Remove(b); }

    // ------------------------------------------------------------ hud
    // same bottom slot as the Smasher's bar, but neon - and white-hot in phase two
    void OnGUI()
    {
        if (GameManager.IsOver || health == null || health.IsDead) return;

        if (nameStyle == null)
            nameStyle = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(), fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };

        float w = Mathf.Min(820f, Screen.width * 0.66f);
        float x = (Screen.width - w) * 0.5f, y = Screen.height - 86f;

        GameManager.GuiBar(new Rect(x, y, w, 38f),
                           phase == 2 ? new Color(0.95f, 0.95f, 1f) : new Color(0.7f, 0.35f, 1f),
                           health.current / health.max);

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(x + 2f, y - 34f, w, 30f), "THE MACHINE HERALD", nameStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y - 36f, w, 30f), "THE MACHINE HERALD", nameStyle);
    }

    // ------------------------------------------------------------ death
    void Die(Health h)
    {
        Vector2 pos = transform.position;
        foreach (var b in bots) if (b != null) b.Dismiss();
        bots.Clear();
        if (shield != null) Destroy(shield.gameObject);
        if (visual != null) Destroy(visual.gameObject);

        Gore.Burst(pos, h.bloodColor, Art.MechFlesh, 14, 1.8f);
        Fx.Explosion(pos, NeonPurple, Color.white, 1.8f);
        Fx.Explosion(pos, NeonCyan, Color.white, 1.4f);
        Fx.Ring(pos, Magenta, 0.5f, 8f, 0.7f, 30);
        Fx.Sparks(pos, Color.white, 40, 20f);
        Gore.Stain(pos, new Color(0.08f, 0.05f, 0.12f), 2.2f);
        GameManager.Shake(1f);
        GameManager.Instance.HitStop(0.14f);
        GameManager.Kills++;
        Sfx.PlayAt(pos, "herald_die", 1f);

        PlayerUpgrades.GainXp(300f);
        if (TimeLeap.Instance != null)
        {
            TimeLeap.Instance.Announce("THE HERALD IS DOWN", 2.4f);
            TimeLeap.Instance.OnHeraldDown();         // the lab lets go: the ruin comes
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
    }
}

// ============================================================ LASER BOT
// the herald's spawn: a hovering triangle that paints you with a beam
// before it fires. Everything it does is dashable.
public class LaserBot : MonoBehaviour
{
    Herald owner;
    Vector2 offset;
    float fireT = 1.2f, aimT, beamT, bobT;
    bool aiming;
    Transform line;
    SpriteRenderer lineSr, bodySr;
    Health health;
    bool gone;

    public static LaserBot Summon(Herald owner, Vector2 pos)
    {
        var g = new GameObject("LaserBot");
        g.transform.position = pos;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.LaserBot();
        sr.color = Color.white;
        sr.sortingOrder = 20;

        var col = g.AddComponent<CircleCollider2D>();
        col.radius = 0.22f;

        var b = g.AddComponent<LaserBot>();
        b.owner = owner;
        b.bodySr = sr;
        b.offset = pos - (Vector2)owner.transform.position;

        b.health = g.AddComponent<Health>();
        b.health.Init(22f);
        b.health.showBar = false;
        b.health.OnDeath += b.Die;

        Fx.Burst(pos, new Color(0.35f, 0.95f, 1f), 10, 5f);
        return b;
    }

    void Update()
    {
        if (GameManager.IsOver || gone) return;
        float dt = Time.deltaTime;

        // it hangs off the herald, bobbing like a hunting drone
        if (owner == null) { Dismiss(); return; }
        bobT += dt;
        transform.position = (Vector2)owner.transform.position + offset +
                             Vector2.up * (Mathf.Sin(bobT * 3f) * 0.15f);

        var p = PlayerController.Instance;
        if (p == null) return;
        Vector2 pos = transform.position;

        if (aiming)
        {
            aimT -= dt;
            Vector2 dir = ((Vector2)p.transform.position - pos).normalized;
            if (line != null)
            {
                line.position = pos + dir * 3.5f;
                line.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                lineSr.color = new Color(1f, 0.3f, 0.8f, 0.3f + 0.3f * Mathf.Sin(Time.time * 30f));
            }
            if (aimT <= 0f)
            {
                aiming = false;
                beamT = 0.14f;
                Fire(pos, dir);
            }
        }
        else if (beamT > 0f)
        {
            beamT -= dt;
            if (line != null) lineSr.color = new Color(0.85f, 1f, 1f, 0.95f);
        }
        else
        {
            if (line != null) { Destroy(line.gameObject); line = null; }
            fireT -= dt;
            if (fireT <= 0f)
            {
                fireT = 2.2f;
                aiming = true;
                aimT = 0.7f;                          // the paint: 0.7s to leave the line

                var g = new GameObject("BotBeam");
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Square;
                sr.sortingOrder = 40;
                line = g.transform;
                lineSr = sr;
                line.localScale = new Vector3(7f, 0.1f, 1f);
            }
        }
    }

    void Fire(Vector2 pos, Vector2 dir)
    {
        Fx.Sparks(pos + dir * 1.2f, Color.white, 10, 12f);
        Fx.Burst(pos + dir * 1.2f, new Color(1f, 0.3f, 0.8f), 8, 6f);
        Sfx.PlayAt(pos, "beam", 0.5f);

        var p = PlayerController.Instance;
        if (p == null) return;
        Vector2 rel = (Vector2)p.transform.position - pos;
        float along = Vector2.Dot(rel, dir);
        float side = Mathf.Abs(dir.x * rel.y - dir.y * rel.x);
        if (along > 0f && along < 7f && side < 0.35f)
            p.Health.TakeDamage(10f * Mathf.Sqrt(EnemySpawner.Difficulty),
                                ((Vector2)p.transform.position - pos).normalized);
    }

    // the channel ends: the little machine winks out in a fizz
    public void Dismiss()
    {
        if (gone) return;
        gone = true;
        Fx.Burst(transform.position, new Color(0.35f, 0.95f, 1f), 10, 6f);
        Fx.Sparks(transform.position, Color.white, 8, 10f);
        Sfx.PlayAt(transform.position, "bot_die", 0.5f, 1.2f);
        if (line != null) Destroy(line.gameObject);
        Destroy(gameObject);
    }

    void Die(Health h)
    {
        if (gone) return;
        gone = true;
        Fx.Burst(transform.position, h.bloodColor, 12, 7f);
        Fx.Flash(transform.position, Color.white, 1.2f, 0.15f);
        Fx.Sparks(transform.position, Color.white, 10, 12f);
        Sfx.PlayAt(transform.position, "bot_die", 0.6f);
        if (line != null) Destroy(line.gameObject);
        PlayerUpgrades.GainXp(6f);
        if (owner != null) owner.ForgetBot(this);
        Destroy(gameObject);
    }
}

// ============================================================ OIL GLOB
// an arcing glob of machine oil: it lands, it spreads, it waits for fire
public class OilGlob : MonoBehaviour
{
    Vector2 from, to;
    float t, flight;
    Transform shadow;

    public static void Lob(Vector2 from, Vector2 to)
    {
        var g = new GameObject("OilGlob");
        g.transform.position = from;
        g.transform.localScale = Vector3.one * 0.5f;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(0.06f, 0.06f, 0.08f);
        sr.sortingOrder = 30;

        var sg = new GameObject("Shadow");
        sg.transform.SetParent(g.transform, false);
        sg.transform.localScale = new Vector3(1.2f, 0.6f, 1f);
        var ssr = sg.AddComponent<SpriteRenderer>();
        ssr.sprite = SpriteFactory.Circle;
        ssr.color = new Color(0f, 0f, 0f, 0.35f);
        ssr.sortingOrder = -3;

        var o = g.AddComponent<OilGlob>();
        o.from = from;
        o.to = to;
        o.shadow = sg.transform;
        o.flight = Mathf.Clamp(Vector2.Distance(from, to) / 9f, 0.5f, 1f);
    }

    void Update()
    {
        if (GameManager.IsOver) return;
        t += Time.deltaTime / flight;
        float k = Mathf.Clamp01(t);

        Vector2 pos = Vector2.Lerp(from, to, k);
        transform.position = new Vector3(pos.x, pos.y + Mathf.Sin(k * Mathf.PI) * 1.3f, 0f);
        if (shadow != null)
        {
            shadow.position = new Vector3(pos.x, pos.y, 0f);
            float s = 1f - Mathf.Sin(k * Mathf.PI) * 0.45f;
            shadow.localScale = new Vector3(1.2f * s, 0.6f * s, 1f);
        }

        if (k >= 1f)
        {
            FluidPool.Spawn(pos, FluidKind.Oil, 3.2f);   // a patch, not a puddle
            Fx.Burst(pos, new Color(0.15f, 0.15f, 0.2f), 10, 5f);
            Fx.Ring(pos, new Color(0.4f, 0.3f, 0.6f, 0.6f), 0.3f, 3.4f, 0.3f, 8);
            Sfx.PlayAt(pos, "glob", 0.6f);
            Destroy(gameObject);
        }
    }
}

// ============================================================ ZEUS BOLT
// phase two: a shadow crawls where you stand for half a second, then the
// sky comes down like a hammer
public class ZeusBolt : MonoBehaviour
{
    float t = 0.5f;
    bool struck;
    SpriteRenderer shadow, bolt;

    public static void Summon(Vector2 at)
    {
        var g = new GameObject("ZeusBolt");
        g.transform.position = at;

        var sg = new GameObject("Shadow");
        sg.transform.SetParent(g.transform, false);
        sg.transform.localScale = Vector3.one * 0.4f;
        var ssr = sg.AddComponent<SpriteRenderer>();
        ssr.sprite = SpriteFactory.Circle;
        ssr.color = new Color(0f, 0f, 0f, 0f);
        ssr.sortingOrder = -4;

        var bg = new GameObject("Bolt");
        bg.transform.SetParent(g.transform, false);
        bg.transform.localPosition = new Vector3(0f, 4.5f, 0f);   // hanging from the sky
        bg.transform.localScale = new Vector3(0.9f, 9f, 1f);
        var bsr = bg.AddComponent<SpriteRenderer>();
        bsr.sprite = Art.Bolt();
        bsr.color = new Color(0.85f, 0.97f, 1f, 0f);
        bsr.sortingOrder = 42;

        var z = g.AddComponent<ZeusBolt>();
        z.shadow = ssr;
        z.bolt = bsr;
    }

    void Update()
    {
        if (GameManager.IsOver) return;
        t -= Time.deltaTime;

        if (!struck)
        {
            // the shadow crawls for half a second - the only warning you get
            float k = 1f - Mathf.Clamp01(t / 0.5f);
            shadow.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.6f, k);
            shadow.color = new Color(0f, 0f, 0f, 0.25f + 0.35f * k);
            shadow.transform.Rotate(0f, 0f, 60f * Time.deltaTime);

            if (t <= 0f)
            {
                struck = true;
                t = 0.28f;
                shadow.gameObject.SetActive(false);
                bolt.color = new Color(0.85f, 0.97f, 1f, 0.95f);
                Fx.Sparks(transform.position, Color.white, 22, 16f);
                Fx.Burst(transform.position, new Color(0.7f, 0.9f, 1f), 16, 9f);
                Fx.Ring(transform.position, new Color(0.7f, 0.9f, 1f), 0.4f, 2.8f, 0.3f, 30);
                GameManager.Shake(0.4f);
                Sfx.PlayAt(transform.position, "zeus", 0.95f);
                Sfx.PlayAt(transform.position, "elec_crackle", 0.7f, 1.05f);

                var p = PlayerController.Instance;
                if (p != null && Vector2.Distance(p.transform.position, transform.position) < 1.35f)
                    p.Health.TakeDamage(26f * Mathf.Sqrt(EnemySpawner.Difficulty),
                                        ((Vector2)p.transform.position - (Vector2)transform.position).normalized);
            }
        }
        else
        {
            bolt.color = new Color(0.85f, 0.97f, 1f, Mathf.Clamp01(t / 0.28f) * 0.95f);
            if (t <= 0f) Destroy(gameObject);
        }
    }
}

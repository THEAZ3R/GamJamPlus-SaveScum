using System.Collections.Generic;
using UnityEngine;

// THE SMASHER: the first boss. It drops out of the sky onto the whole field -
// player and horde alike - then runs its old tricks in phase one (leap,
// charge, quake) with a shield that, once broken, is broken for good.
// At half health it sees red: a rage aura, a shield that grows back, and a
// scattergun added to the rotation. While it holds the ring the timeleap is
// locked, the clock freezes and no new enemies spawn.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Health))]
public class Smasher : MonoBehaviour
{
    public static Smasher Active;
    public static bool BossAlive { get { return Active != null; } }

    enum St { Enter, EnterSlam, Chase, Air, Pause, DashWarn, Dash, QuakeWind, Quake, ShotgunWind }

    St st = St.Enter;
    int atk = -1, phase = 1;
    float stT, attackCd, contactT, quakeR, quakeMax, quakeSpeed, quakeDmg, quakeKnock, quakeT, trailT;
    float speed = 2.6f, baseSpeed = 2.6f, dmgScale = 1f;
    float contactDamage = 18f, baseContact = 18f, shieldRegenT;
    bool quakeHit, shieldUp;
    float wheelT, engineT;
    Vector2 dashDir, shadowPos, moveDir, leapFrom;

    readonly HashSet<Enemy> enterHit = new HashSet<Enemy>();   // the entrance ring's receipts
    Transform[] aura;                                          // the phase two rage flames

    Rigidbody2D rb;
    CircleCollider2D bodyCol;
    Health health;
    Transform visual;
    SpriteRenderer shadow;
    DestructibleWall shield;
    Transform warnStrip;
    float lastFace, orbit;                           // stable heading, circling side
    GUIStyle nameStyle;

    public static Smasher Spawn(float difficulty)
    {
        var p = PlayerController.Instance;

        // ring the player with a spot on open ground
        Vector2 pos = p != null ? (Vector2)p.transform.position + Vector2.right * 6f : Vector2.right * 6f;
        for (int i = 0; i < 30; i++)
        {
            Vector2 c = p != null ? (Vector2)p.transform.position : Vector2.zero;
            pos = c + Random.insideUnitCircle.normalized * Random.Range(6.5f, 10f);
            pos.x = Mathf.Clamp(pos.x, -GameManager.ArenaHalf.x + 2f, GameManager.ArenaHalf.x - 2f);
            pos.y = Mathf.Clamp(pos.y, -GameManager.ArenaHalf.y + 2f, GameManager.ArenaHalf.y - 2f);
            if (Pit.Inside(pos)) continue;
            if (!Nav.IsFree(pos, true)) continue;
            break;
        }

        var g = new GameObject("Smasher");
        g.transform.position = pos;
        var s = g.AddComponent<Smasher>();
        s.Setup(difficulty);
        return s;
    }

    void Setup(float difficulty)
    {
        Active = this;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        dmgScale = Mathf.Sqrt(difficulty);

        bodyCol = GetComponent<CircleCollider2D>();
        bodyCol.radius = 0.72f;
        bodyCol.enabled = false;                      // nothing to touch while it falls

        health = GetComponent<Health>();
        health.OnDeath += Die;
        health.Init(950f * GameManager.HpScale(difficulty));   // it has to feel like a wall
        health.showBar = false;                       // the named bar rides the HUD
        health.invulnerable = true;

        visual = Humanoid.Build(transform, Art.Smasher(), 2.1f, 20, AnimStyle.Heavy);
        if (visual != null) visual.gameObject.SetActive(false);

        // the shadow: the warning painted on the floor before it lands
        var sg = new GameObject("Shadow");
        sg.transform.SetParent(transform, false);
        sg.transform.localScale = new Vector3(2.8f, 1.4f, 1f);
        shadow = sg.AddComponent<SpriteRenderer>();
        shadow.sprite = SpriteFactory.Circle;
        shadow.color = new Color(1f, 0.3f, 0.15f, 0.5f);
        shadow.sortingOrder = -3;

        baseSpeed = speed = 2.6f;
        orbit = Random.value < 0.5f ? -1f : 1f;      // it picks a side and commits
        baseContact = contactDamage = 18f;

        // the entrance: it hangs overhead for a beat, then the whole field pays
        st = St.Enter;
        stT = 1.5f;
        shadowPos = transform.position;
        if (TimeLeap.Instance != null) TimeLeap.Instance.Announce("!! SOMETHING IS FALLING FROM THE SKY !!", 1.6f);

        BuildShield();
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
        // riding the player turns "toward the player" into float noise - hold
        // the last heading instead of spinning the chassis like a top
        if (dist > 0.3f) lastFace = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float toAngle = lastFace;

        // half health: the shield starts growing back and the gun comes out
        if (phase == 1 && health.current <= health.max * 0.5f) ToPhase2();

        if (shieldUp && (shield == null || shield.Dead))
        {
            shieldUp = false;                        // the shield is scrap: it gets meaner
            RefreshSpeed();
            if (phase == 2) shieldRegenT = 16f;      // phase one: scrap stays scrap
            Fx.Burst(pos, new Color(1f, 0.6f, 0.2f), 18, 7f);
            GameManager.Shake(0.4f);
        }
        else if (!shieldUp && phase == 2)
        {
            shieldRegenT -= dt;
            if (shieldRegenT <= 0f) RegrowShield();
        }

        if (contactT > 0f) contactT -= dt;
        if (aura != null) TurnAura();

        // the wheels on that chassis never stop complaining
        wheelT -= dt;
        if (wheelT <= 0f && (moveDir.sqrMagnitude > 0.05f || st == St.Dash))
        {
            wheelT = 0.42f;
            Sfx.PlayAt(pos, "wheel", 0.8f, 0.8f);
        }

        // and underneath it all, the engine idles like a tank in a bad mood
        engineT -= dt;
        if (engineT <= 0f)
        {
            engineT = phase == 2 ? Random.Range(0.7f, 1f) : Random.Range(1.1f, 1.6f);
            Sfx.PlayAt(pos, "engine_chug", 0.5f, phase == 2 ? 1.12f : 0.92f);
        }

        switch (st)
        {
            case St.Enter:
                // high above the arena - the shadow under you swells and reddens
                moveDir = Vector2.zero;
                {
                    float f = 1f - Mathf.Clamp01(stT / 1.5f);
                    shadow.transform.localScale = new Vector3(2.8f, 1.4f, 1f) * (1f + 1.6f * f);
                    shadow.color = new Color(1f, 0.3f, 0.15f, 0.45f + 0.3f * f);
                }
                stT -= dt;
                if (stT <= 0f) EnterSlam();
                break;

            case St.EnterSlam:
                // the shock ring crawls across the entire field - dash it
                moveDir = Vector2.zero;
                quakeR += quakeSpeed * dt;
                EnterRingHits(p, pos);
                if (quakeR > quakeMax) ToChase(1.6f);
                break;

            case St.Chase:
                moveDir = Nav.Steer(pos, p.transform.position, true);
                // it does not ride you: at arm's length it circles for the next hit
                if (dist < 1.6f)
                    moveDir = (moveDir + Vector2.Perpendicular(dir) * orbit).normalized;
                attackCd -= dt;
                if (attackCd <= 0f) NextAttack(dir);
                break;

            case St.Air:
                // the body is gone - only the shadow hunts you overhead
                moveDir = Vector2.zero;
                shadowPos = Vector2.MoveTowards(shadowPos, p.transform.position, 5f * dt);
                rb.position = Vector2.MoveTowards(rb.position, shadowPos, 7f * dt);
                transform.position = rb.position;
                stT -= dt;
                if (stT <= 0f) { st = St.Pause; stT = 0.3f; }   // the beat before the drop
                break;

            case St.Pause:
                moveDir = Vector2.zero;
                {
                    float fl = Mathf.Sin(Time.time * 40f) > 0f ? 1f : 0.35f;
                    shadow.color = new Color(1f, 0.25f, 0.15f, 0.55f * fl);
                    shadow.transform.localScale = new Vector3(3.2f, 1.6f, 1f) * (1f + (0.3f - stT));
                }
                stT -= dt;
                if (stT <= 0f) Slam(p);
                break;

            case St.DashWarn:
                moveDir = Vector2.zero;
                if (visual != null)
                    visual.localScale = Vector3.one * (Mathf.Sin(Time.time * 30f) > 0f ? 1.06f : 1f);
                stT -= dt;
                if (stT <= 0f) { st = St.Dash; stT = 0.8f; ClearWarn(); Sfx.PlayAt(transform.position, "dash", 0.9f, 0.7f); }
                break;

            case St.Dash:
                moveDir = dashDir;
                stT -= dt;
                trailT -= dt;                         // a burning wake behind the charge
                if (trailT <= 0f)
                {
                    trailT = 0.04f;
                    Fx.Sparks(pos - dashDir * 0.5f, new Color(1f, 0.5f, 0.12f), 3, 3f);
                    Fx.Burst(pos - dashDir * 0.4f, new Color(1f, 0.35f, 0.1f, 0.6f), 2, 2f);
                }
                if (dist < 1.5f && contactT <= 0f)
                {
                    contactT = 0.6f;
                    p.Health.TakeDamage(22f * dmgScale, dir);
                    p.Shove(dashDir, 7f);
                    Fx.Flash(pos + dashDir * 0.8f, new Color(1f, 0.75f, 0.4f), 1.6f, 0.12f);
                    Sfx.PlayAt(pos, "slam", 0.55f, 1.35f);      // sixteen tonnes hitting something
                    GameManager.Shake(0.5f);
                }
                if (stT <= 0f || !Nav.IsFree(pos + dashDir * 0.7f, true)) ToChase(1.2f);
                break;

            case St.QuakeWind:
                moveDir = Vector2.zero;
                if (visual != null) visual.localScale = Vector3.one * (1f + 0.10f * Mathf.Sin(Time.time * 24f));
                stT -= dt;
                if (stT <= 0f) Quake(pos);
                break;

            case St.Quake:
                moveDir = Vector2.zero;
                quakeR += quakeSpeed * dt;
                quakeT -= dt;                         // the floor cracks as the ring passes
                if (quakeT <= 0f)
                {
                    quakeT = 0.08f;
                    Gore.Stain(pos + Random.insideUnitCircle * quakeR,
                               new Color(0.08f, 0.055f, 0.045f), Random.Range(0.6f, 1.2f));
                }
                if (!quakeHit && Vector2.Distance(p.transform.position, pos) <= quakeR)
                {
                    quakeHit = true;
                    Vector2 away = ((Vector2)p.transform.position - pos).normalized;
                    p.Health.TakeDamage(quakeDmg * dmgScale, away);
                    p.Shove(away, quakeKnock);
                    GameManager.Shake(0.55f);
                }
                if (quakeR > quakeMax) ToChase(2f);
                break;

            case St.ShotgunWind:
                // phase two: it plants, the muzzle glows, then the wall of lead
                moveDir = Vector2.zero;
                if (visual != null)
                    visual.localScale = Vector3.one * (1f + 0.14f * (1f - stT / 0.55f));
                stT -= dt;
                if (stT <= 0f) FireScatter(p);
                break;
        }

        if (visual != null)
        {
            float face = st == St.Dash ? Mathf.Atan2(dashDir.y, dashDir.x) * Mathf.Rad2Deg : toAngle;
            visual.rotation = Quaternion.Euler(0, 0, face);
            if (st != St.DashWarn && st != St.QuakeWind && st != St.ShotgunWind) visual.localScale = Vector3.one;
        }

        if (shieldUp && shield != null)              // the shield stays between you and it
        {
            shield.transform.localRotation = Quaternion.Euler(0, 0, toAngle);
            shield.transform.localPosition = dir * 1.3f;
        }
    }

    void FixedUpdate()
    {
        if (GameManager.IsOver) { rb.SetVel(Vector2.zero); return; }
        Vector2 v = st == St.Dash ? dashDir * 13f : moveDir * speed;
        Vector2 p0 = rb.position;
        if (Pit.Blocks(p0, p0 + v * Time.fixedDeltaTime)) v = Vector2.zero;   // it does not leap pits
        rb.SetVel(v);
    }

    void RefreshSpeed()
    {
        speed = baseSpeed * (phase == 2 ? 1.12f : 1f) * (shieldUp ? 1f : 1.2f);
        contactDamage = baseContact * (shieldUp ? 1f : 1.2f);
    }

    void NextAttack(Vector2 dir)
    {
        int n = phase == 2 ? 4 : 3;                  // phase two slips the scattergun in
        atk = (atk + 1) % n;
        switch (atk)
        {
            case 0: StartLeap(); break;
            case 1: StartDashWarn(dir); break;
            case 2: st = St.QuakeWind; stT = 0.45f;
                    Fx.Ring(transform.position, new Color(1f, 0.45f, 0.12f), 0.3f, 1.2f, 0.45f, 29);
                    break;
            default: StartShotgunWarn(dir); break;
        }
    }

    // ------------------------------------------------------------ phase two
    void ToPhase2()
    {
        phase = 2;
        RefreshSpeed();
        if (!shieldUp) shieldRegenT = 16f;           // the armour starts over, from nothing

        // the rage flames: the vampire's ki trick, in its colours
        aura = new Transform[8];
        for (int i = 0; i < aura.Length; i++)
        {
            var g = new GameObject("RageFlame");
            g.transform.SetParent(transform, false);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Art.KiFlame();
            sr.color = new Color(1f, 0.3f, 0.1f, 0.9f);
            sr.sortingOrder = 21;
            aura[i] = g.transform;
        }

        Fx.Ring(transform.position, new Color(1f, 0.25f, 0.1f), 0.5f, 7f, 0.6f, 30);
        Fx.Burst(transform.position, new Color(1f, 0.35f, 0.12f), 26, 10f);
        Fx.Sparks(transform.position, new Color(1f, 0.6f, 0.2f), 20, 12f);
        GameManager.Shake(0.85f);
        GameManager.Instance.HitStop(0.12f);
        Sfx.PlayAt(transform.position, "horn", 0.9f, 1.2f);
        if (TimeLeap.Instance != null) TimeLeap.Instance.Announce("THE SMASHER SEES RED", 2.2f);
    }

    void TurnAura()
    {
        float t = Time.time * 2.2f;
        for (int i = 0; i < aura.Length; i++)
        {
            float a = t + i * Mathf.PI * 2f / aura.Length;
            aura[i].localPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * 1.2f;
            aura[i].localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90f);
            aura[i].localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(Time.time * 18f + i));
        }
    }

    // ------------------------------------------------------------ entrance
    void EnterSlam()
    {
        Vector2 at = transform.position;

        st = St.EnterSlam;
        quakeR = 0.6f;
        quakeMax = 26f;                               // corner to corner of the arena
        quakeSpeed = 22f;                             // ~1.2s to cross: dashable
        quakeDmg = 30f;
        quakeKnock = 11f;
        quakeHit = false;
        enterHit.Clear();

        health.invulnerable = false;
        bodyCol.enabled = true;
        shadow.enabled = false;
        if (visual != null) { visual.gameObject.SetActive(true); visual.localScale = Vector3.one; }

        // the visual: it arrives like a bomb
        Fx.Ring(at, new Color(1f, 0.45f, 0.12f), 0.8f, 52f, 1.15f, 29);
        Fx.Ring(at, new Color(1f, 0.8f, 0.45f), 0.8f, 52f, 0.85f, 29);
        Fx.Burst(at, new Color(1f, 0.6f, 0.2f), 34, 12f);
        Fx.Flash(at, new Color(1f, 0.85f, 0.6f), 4f, 0.18f);
        Gore.Stain(at, new Color(0.1f, 0.07f, 0.05f), 2.4f);
        GameManager.Shake(1.15f);
        GameManager.Instance.HitStop(0.1f);
        Sfx.PlayAt(at, "slam", 1f, 0.6f);
        Sfx.PlayAt(at, "quake", 1f, 0.8f);
        if (TimeLeap.Instance != null) TimeLeap.Instance.Announce("THE SMASHER ENTERS THE RING", 2.4f);
    }

    // the entrance ring takes the player and the whole horde alike
    void EnterRingHits(PlayerController p, Vector2 pos)
    {
        if (!quakeHit && Vector2.Distance(p.transform.position, pos) <= quakeR)
        {
            quakeHit = true;
            Vector2 away = ((Vector2)p.transform.position - pos).normalized;
            p.Health.TakeDamage(quakeDmg * dmgScale, away);
            p.Shove(away, quakeKnock);
            GameManager.Shake(0.6f);
        }

        var horde = Enemy.All.ToArray();
        foreach (var e in horde)
        {
            if (e == null || e.Health == null || e.Health.IsDead) continue;
            if (enterHit.Contains(e)) continue;
            if (Vector2.Distance(e.transform.position, pos) > quakeR) continue;

            enterHit.Add(e);
            Vector2 away = ((Vector2)e.transform.position - pos).normalized;
            e.Health.TakeDamage(quakeDmg * dmgScale, away);
            Fx.Burst(e.transform.position, new Color(1f, 0.55f, 0.2f), 8, 6f);
        }
    }

    // ------------------------------------------------------------ shield
    void BuildShield()
    {
        shield = DestructibleWall.Create(Vector2.zero, new Vector2(0.36f, 2.8f), 380f, 22, true);
        shield.transform.SetParent(transform, false);
        shieldUp = true;
        shieldRegenT = 0f;
    }

    void RegrowShield()
    {
        if (shield != null) Destroy(shield.gameObject);
        BuildShield();
        RefreshSpeed();

        Vector2 at = (Vector2)transform.position + dirToPlayer() * 1.3f;
        Fx.Ring(at, new Color(0.6f, 0.95f, 1f), 0.3f, 2.6f, 0.4f, 30);
        Fx.Burst(at, new Color(0.8f, 1f, 1f), 14, 6f);
        Sfx.PlayAt(at, "shield_up", 0.8f);
        if (st == St.Air || st == St.Pause || st == St.Enter) SetShieldShown(false);
        if (TimeLeap.Instance != null) TimeLeap.Instance.Announce("IT RE-ARMOURS", 1.2f);
    }

    Vector2 dirToPlayer()
    {
        var p = PlayerController.Instance;
        if (p == null) return Vector2.right;
        return ((Vector2)p.transform.position - (Vector2)transform.position).normalized;
    }

    void SetShieldShown(bool on)
    {
        if (shield == null) return;
        shield.gameObject.SetActive(on);              // it hangs in the air with you
        var c = shield.GetComponent<Collider2D>();
        if (c != null) c.enabled = on;
    }

    // ------------------------------------------------------------ leap
    void StartLeap()
    {
        st = St.Air;
        stT = 1.3f;
        leapFrom = transform.position;
        shadowPos = transform.position;
        shadow.enabled = true;
        shadow.transform.localScale = new Vector3(2.8f, 1.4f, 1f);
        shadow.color = new Color(0f, 0f, 0f, 0.45f);

        health.invulnerable = true;                   // nothing to shoot while it hangs
        bodyCol.enabled = false;
        SetShieldShown(false);
        if (visual != null) visual.gameObject.SetActive(false);

        // the takeoff: a ring of dirt and a geyser of sparks
        Fx.Ring(transform.position, new Color(1f, 0.45f, 0.12f), 0.4f, 3.2f, 0.4f, 29);
        Fx.Burst(transform.position, new Color(1f, 0.6f, 0.2f), 14, 6f);
        Fx.Sparks(transform.position, new Color(1f, 0.75f, 0.35f), 16, 12f);
        Gore.Stain(transform.position, new Color(0.09f, 0.06f, 0.05f), 1.4f);
        if (TimeLeap.Instance != null) TimeLeap.Instance.Announce("MOVE! THE SHADOW IS HUNTING", 1.2f);
        Sfx.PlayAt(transform.position, "leap", 0.85f);
    }

    void Slam(PlayerController p)
    {
        Vector2 at = shadowPos;
        transform.position = at;
        rb.position = at;

        st = St.Chase;
        attackCd = Random.Range(2.2f, 3.2f);
        health.invulnerable = false;
        bodyCol.enabled = true;
        SetShieldShown(true);
        if (visual != null) { visual.gameObject.SetActive(true); visual.localScale = Vector3.one; }
        shadow.enabled = false;

        // the landing: twin rings, a fountain of dirt, scorched ground
        Fx.Ring(at, new Color(1f, 0.45f, 0.12f), 0.5f, 4.6f, 0.35f, 29);
        Fx.Ring(at, new Color(1f, 0.8f, 0.45f), 0.5f, 4.6f, 0.55f, 29);
        Fx.Burst(at, new Color(1f, 0.6f, 0.2f), 26, 9f);
        Fx.Sparks(at, new Color(1f, 0.85f, 0.5f), 22, 14f);
        Gore.Stain(at, new Color(0.09f, 0.06f, 0.05f), 2f);
        GameManager.Shake(1f);
        Sfx.PlayAt(at, "slam", 1f, 0.85f);

        // phase two: it comes down like a meteor - the whole path is scrap
        // and a ring of spikes erupts where it lands
        if (phase == 2)
        {
            for (float t = 0f; t <= 1f; t += 0.18f)
                GameManager.SmashWallsNear(Vector2.Lerp(leapFrom, at, t), 2.1f);
            SpikeRing(at);
        }

        Vector2 away = (Vector2)p.transform.position - at;
        if (away.magnitude < 2.1f)                   // dash out of the ring
            p.Health.TakeDamage(34f * dmgScale, away.normalized);
    }

    void SpikeRing(Vector2 at)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            Spike.Summon(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.2f);
        }
        Fx.Ring(at, new Color(1f, 0.35f, 0.1f), 0.5f, 5.2f, 0.5f, 29);
        GameManager.Shake(0.65f);
    }

    // ------------------------------------------------------------ dash
    void StartDashWarn(Vector2 dir)
    {
        st = St.DashWarn;
        stT = 0.5f;                                  // the warning lane
        dashDir = dir;

        var g = new GameObject("DashWarn");
        g.transform.SetParent(transform, false);
        g.transform.position = (Vector2)transform.position + dashDir * 4.5f;
        g.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dashDir.y, dashDir.x) * Mathf.Rad2Deg);
        g.transform.localScale = new Vector3(9.5f, 1.15f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square;
        sr.color = new Color(1f, 0.25f, 0.15f, 0.35f);
        sr.sortingOrder = -5;                        // painted on the floor
        warnStrip = g.transform;
    }

    void ClearWarn() { if (warnStrip != null) { Destroy(warnStrip.gameObject); warnStrip = null; } }

    void ToChase(float cd)
    {
        st = St.Chase;
        attackCd = cd;
        moveDir = Vector2.zero;
        if (visual != null) visual.localScale = Vector3.one;
        ClearWarn();
    }

    // ------------------------------------------------------------ quake
    void Quake(Vector2 pos)
    {
        st = St.Quake;
        quakeR = 0.5f;
        quakeMax = 5.4f;
        quakeSpeed = 10f;
        quakeDmg = 24f;
        quakeKnock = 9f;
        quakeT = 0f;
        quakeHit = false;
        Fx.Ring(pos, new Color(1f, 0.45f, 0.12f), 0.5f, 10.8f, 0.55f, 29);
        Fx.Ring(pos, new Color(1f, 0.75f, 0.4f), 0.5f, 10.8f, 0.8f, 29);
        GameManager.Shake(0.6f);
        Sfx.PlayAt(pos, "quake", 1f);
    }

    // ------------------------------------------------------------ scattergun
    void StartShotgunWarn(Vector2 dir)
    {
        st = St.ShotgunWind;
        stT = 0.55f;
        dashDir = dir;

        // the cone painted on the floor: where the wall of lead will go
        var g = new GameObject("ShotgunWarn");
        g.transform.SetParent(transform, false);
        g.transform.position = (Vector2)transform.position + dashDir * 3.4f;
        g.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        g.transform.localScale = new Vector3(7f, 3.4f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(1f, 0.35f, 0.1f, 0.28f);
        sr.sortingOrder = -5;
        warnStrip = g.transform;

        Fx.Ring((Vector2)transform.position + dashDir * 1.2f, new Color(1f, 0.4f, 0.12f), 0.2f, 1.1f, 0.5f, 30);
        if (TimeLeap.Instance != null) TimeLeap.Instance.Announce("SCATTERGUN!", 1f);
    }

    void FireScatter(PlayerController p)
    {
        Vector2 dir = dashDir;
        Vector2 muzzle = (Vector2)transform.position + dir * 1.5f;
        ClearWarn();

        const int n = 9;
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg
                      + Mathf.Lerp(-26f, 26f, i / (float)(n - 1)) + Random.Range(-3f, 3f);
            Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            Bullet.Spawn(muzzle + d * 0.3f, d, Random.Range(7.5f, 9f), 6.5f * dmgScale, false,
                         new Color(1f, 0.52f, 0.14f), 0.22f);
        }

        Fx.Flash(muzzle, new Color(1f, 0.8f, 0.45f), 1.8f, 0.12f);
        Fx.Burst(muzzle, new Color(1f, 0.55f, 0.15f), 18, 8f);
        Fx.Sparks(muzzle, Color.white, 12, 13f);
        GameManager.Shake(0.55f);
        Sfx.PlayAt(muzzle, "shotgun", 1f, 0.75f);

        if (visual != null) visual.localScale = Vector3.one;
        ToChase(1.5f);
    }

    // ------------------------------------------------------------ contact
    void OnCollisionStay2D(Collision2D c)
    {
        if (!c.collider.CompareTag("Player")) return;
        if (contactT > 0f || contactDamage <= 0f) return;
        var p = PlayerController.Instance;
        if (p == null) return;

        Vector2 away = ((Vector2)c.collider.transform.position - (Vector2)transform.position).normalized;
        if (p.Health.TakeDamage(contactDamage * dmgScale, away)) contactT = 0.7f;
    }

    // ------------------------------------------------------------ hud
    // the named bar rides the bottom of the screen - big, and angrier in phase two
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
                           phase == 2 ? new Color(1f, 0.25f, 0.1f) : new Color(1f, 0.55f, 0.15f),
                           health.current / health.max);

        // the name sits over the bar with a dark shadow so it reads on any fill
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(x + 2f, y - 34f, w, 30f), "THE SMASHER", nameStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y - 36f, w, 30f), "THE SMASHER", nameStyle);
    }

    // ------------------------------------------------------------ death
    // test tooling: the wall comes down without the ceremony
    public static void DevDismiss()
    {
        var s = Active;
        Active = null;
        if (s != null)
        {
            s.ClearWarn();
            Destroy(s.gameObject);                   // the shield hangs off it too
        }
    }

    void Die(Health h)
    {
        Vector2 pos = transform.position;
        if (aura != null) foreach (var a in aura) if (a != null) Destroy(a.gameObject);
        Gore.Burst(pos, h.bloodColor, Art.MechFlesh, 16, 1.8f);
        Fx.Flash(pos, Color.Lerp(h.bloodColor, Color.white, 0.75f), 3.4f, 0.22f);
        Fx.Explosion(pos, new Color(1f, 0.5f, 0.12f), new Color(1f, 0.9f, 0.4f), 1.5f);
        Fx.Ring(pos, new Color(1f, 0.5f, 0.1f), 0.5f, 6.5f, 0.55f);
        Gore.Stain(pos, new Color(0.05f, 0.045f, 0.04f), 2f);
        GameManager.Shake(0.8f);
        GameManager.Instance.HitStop(0.12f);
        GameManager.Kills++;

        PlayerUpgrades.GainXp(220f);                 // a boss pays boss money
        if (TimeLeap.Instance != null)
        {
            TimeLeap.Instance.Announce("THE SMASHER IS DOWN", 2.4f);
            TimeLeap.Instance.OnSmasherDown();       // the system glitches: the lab comes
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
    }
}

// a phase two landing spike: a red dot swells, then the tooth erupts
public class Spike : MonoBehaviour
{
    float t = 0.45f;
    bool fired;
    SpriteRenderer sr;

    public static void Summon(Vector2 pos)
    {
        var g = new GameObject("Spike");
        g.transform.position = pos;
        g.transform.localScale = Vector3.one * 0.01f;
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.Spike();
        sr.color = new Color(1f, 0.4f, 0.12f, 0.9f);
        sr.sortingOrder = 18;
        g.AddComponent<Spike>().sr = sr;

        // the ground marks every tooth before it comes through
        Fx.Ring(pos, new Color(1f, 0.3f, 0.1f), 0.2f, 1.6f, 0.5f, 29);
        Fx.Ring(pos, new Color(1f, 0.55f, 0.2f), 0.2f, 1.6f, 0.4f, 29);
    }

    void Update()
    {
        if (GameManager.IsOver) return;
        t -= Time.deltaTime;

        if (!fired)
        {
            // telegraph: a red dot that swells and stutters
            float f = 1f - Mathf.Clamp01(t / 0.45f);
            transform.localScale = Vector3.one * (0.55f * f);
            sr.color = new Color(1f, 0.25f, 0.08f, 0.7f + 0.3f * Mathf.Sin(Time.time * 40f));
            if (t <= 0f)
            {
                fired = true;
                t = 0.6f;
                transform.localScale = Vector3.one * 2f;
                Sfx.PlayAt(transform.position, "stab", 0.8f, 0.7f);
                Fx.Burst(transform.position, new Color(1f, 0.5f, 0.15f), 18, 8f);
                Fx.Sparks(transform.position, new Color(1f, 0.8f, 0.45f), 12, 11f);
                GameManager.Shake(0.2f);

                var p = PlayerController.Instance;
                if (p != null && Vector2.Distance(p.transform.position, transform.position) < 0.95f)
                {
                    Vector2 away = ((Vector2)p.transform.position - (Vector2)transform.position).normalized;
                    p.Health.TakeDamage(20f * Mathf.Sqrt(EnemySpawner.Difficulty), away);
                    p.Shove(away, 6f);
                }
            }
        }
        else
        {
            transform.localScale = Vector3.one * Mathf.Lerp(2f, 0f, 1f - t / 0.6f);
            if (t <= 0f) Destroy(gameObject);
        }
    }
}

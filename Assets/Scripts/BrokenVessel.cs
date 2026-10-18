using System.Collections.Generic;
using UnityEngine;

// THE BROKEN VESSEL: boss three. What is left of two dead realities stapled
// together - scrap plates, corpses and broken machinery dragging itself over
// the ruin. Every attack opens with a roar, every step leaves a scrape.
// It vomits a cone of acid, rips cover out of the floor and throws it, and
// sweeps a thick rust beam that eats whatever it touches. At half health it
// stops moving for good and roots itself into a turret of acid, bullets
// and beams. While it holds the ring the timeleap is locked and the clock holds.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Health))]
public class BrokenVessel : MonoBehaviour
{
    public static BrokenVessel Active;
    public static bool BossAlive { get { return Active != null; } }

    enum St { Enter, Chase, VomitWind, Vomit, RipWind, Avalanche, BeamWind, Beam,
              TurretAcid, TurretBullets, Recover }

    static readonly Color Rust = new Color(0.78f, 0.48f, 0.16f);
    static readonly Color Sludge = new Color(0.42f, 0.85f, 0.16f);
    static readonly Color AcidGreen = new Color(0.55f, 1f, 0.25f);

    St st = St.Enter;
    int atk = -1, phase = 1, chunksLeft, globsLeft, burstsLeft;
    float stT, attackCd, contactT, dmgScale, speed;
    float tickT, dragT, breathT, chunkT, globT, burstT, smashT, zapT, sfxT;
    float beamDur, beamHalf, beamWidth, beamDmg, beamBase;
    Vector2 moveDir;

    Rigidbody2D rb;
    CircleCollider2D bodyCol;
    Health health;
    Transform visual, facing, armPart, mechPart, legPart, gearPart;
    SpriteRenderer tankSr, mawSr;
    Transform warnCone, sweepLine;
    GUIStyle nameStyle;

    public static BrokenVessel Spawn(float difficulty)
    {
        var p = PlayerController.Instance;

        // ring the player with a spot on whatever floor is left
        Vector2 pos = p != null ? (Vector2)p.transform.position + Vector2.right * 7f : Vector2.right * 7f;
        for (int i = 0; i < 30; i++)
        {
            Vector2 c = p != null ? (Vector2)p.transform.position : Vector2.zero;
            pos = c + Random.insideUnitCircle.normalized * Random.Range(6.5f, 10f);
            pos.x = Mathf.Clamp(pos.x, -GameManager.ArenaHalf.x + 2.5f, GameManager.ArenaHalf.x - 2.5f);
            pos.y = Mathf.Clamp(pos.y, -GameManager.ArenaHalf.y + 2.5f, GameManager.ArenaHalf.y - 2.5f);
            if (Pit.Inside(pos)) continue;
            if (!Nav.IsFree(pos, true)) continue;
            break;
        }

        var g = new GameObject("BrokenVessel");
        g.transform.position = pos;
        var v = g.AddComponent<BrokenVessel>();
        v.Setup(difficulty, pos);
        return v;
    }

    void Setup(float difficulty, Vector2 pos)
    {
        Active = this;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        dmgScale = Mathf.Sqrt(difficulty);
        speed = 1.95f;                                // it drags; it does not run

        bodyCol = GetComponent<CircleCollider2D>();
        bodyCol.radius = 0.9f;

        health = GetComponent<Health>();
        health.OnDeath += Die;
        health.Init(700f * GameManager.HpScale(difficulty));   // a wall of dead things
        health.showBar = false;                       // the named bar rides the HUD
        health.barColor = Rust;
        health.bloodColor = new Color(0.45f, 0.82f, 0.18f);   // green ichor
        health.invulnerable = true;                   // it is still being born

        visual = BuildVisual();

        st = St.Enter;
        stT = 1.0f;
        attackCd = 1.7f;
        if (TimeLeap.Instance != null)
            TimeLeap.Instance.Announce("REALITY IS CRUMBLING", 2.6f);
        Sfx.PlayAt(pos, "roar", 1f, 0.85f);
        GameManager.Shake(0.9f);
        if (GameManager.Instance != null) GameManager.Instance.CollapseEvent(pos);
    }

    // ------------------------------------------------------------ visual
    // a lopsided heap: machinery bolted high on one shoulder, a slab of an
    // arm on the other, a tank of the stuff it vomits, one glowing maw, and
    // a leg that never learned to walk again
    Transform BuildVisual()
    {
        var root = new GameObject("VesselVisual").transform;
        root.SetParent(transform, false);
        root.localScale = Vector3.one * 0.2f;         // it is still assembling

        Art.Part(root, "Shadow", SpriteFactory.Circle, new Vector2(0.15f, -0.2f),
                  new Vector3(3.6f, 1.7f, 1f), new Color(0, 0, 0, 0.45f), -3);

        facing = new GameObject("Facing").transform;
        facing.SetParent(root, false);

        var torso = Art.Part(facing, "Torso", Art.VesselBody(), Vector2.zero,
                             Vector3.one * 3.0f, Color.white, 20);

        mechPart = Art.Part(facing, "Mech", Art.VesselMech(), new Vector2(-1.35f, 1.05f),
                            Vector3.one * 1.6f, Color.white, 19, 14f).transform;
        armPart = Art.Part(facing, "HumanArm", Art.VesselHumanArm(), new Vector2(-1.8f, -0.35f),
                           Vector3.one * 1.7f, Color.white, 21, 8f).transform;
        legPart = Art.Part(facing, "DragLeg", Art.VesselLeg(), new Vector2(1.3f, -1.3f),
                           Vector3.one * 1.7f, Color.white, 21, -34f).transform;
        var tank = Art.Part(facing, "Tank", Art.VesselTank(), new Vector2(1.55f, -0.15f),
                            Vector3.one * 1.25f, Color.white, 21, -8f);
        tankSr = tank;
        var maw = Art.Part(facing, "Maw", Art.VesselMaw(), new Vector2(0.35f, 0.2f),
                           Vector3.one * 1.15f, AcidGreen, 22);
        mawSr = maw;

        // the human parts sit on top of the pile where the eye lands first:
        // a skull bolted to the crown, a gear still turning in the shoulder
        Art.Part(facing, "Skull", Art.VesselSkull(), new Vector2(0.05f, 1.72f),
                 Vector3.one * 1.15f, Color.white, 23, -6f);
        gearPart = Art.Part(facing, "Gear", Art.VesselGear(), new Vector2(1.62f, 1.15f),
                            Vector3.one * 1.5f, Color.white, 21).transform;

        return root;
    }

    // ------------------------------------------------------------ ai
    void Update()
    {
        if (GameManager.IsOver) { rb.SetVel(Vector2.zero); return; }
        var p = PlayerController.Instance;
        if (p == null || health == null) return;

        float dt = Time.deltaTime;
        Vector2 pos = transform.position;
        Vector2 to = (Vector2)p.transform.position - pos;
        float dist = to.magnitude;
        Vector2 dir = to / Mathf.Max(dist, 0.001f);

        if (contactT > 0f) contactT -= dt;

        // the flip waits for a clean moment so the turret roots itself readable
        if (phase == 1 && health.current <= health.max * 0.5f &&
            (st == St.Chase || st == St.Recover)) ToPhase2();

        switch (st)
        {
            case St.Enter:                              // bolted together from nothing
                stT -= dt;
                if (visual != null)
                {
                    float f = 1f - Mathf.Clamp01(stT / 1.0f);
                    visual.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.05f, f * f);
                    if (Random.value < 0.3f)
                        Fx.Burst(pos + (Vector2)(Random.insideUnitCircle * 1.4f),
                                 new Color(0.5f, 0.45f, 0.4f), 3, 4f);
                }
                if (stT <= 0f)
                {
                    if (visual != null) visual.localScale = Vector3.one;
                    health.invulnerable = false;
                    Sfx.PlayAt(pos, "roar", 0.95f, 0.9f);
                    GameManager.Shake(0.5f);
                    Fx.Ring(pos, Sludge, 0.4f, 6f, 0.55f, 30);
                    st = St.Chase;
                    attackCd = 1.4f;
                }
                break;

            case St.Chase:
                if (phase == 1)
                {
                    moveDir = dist > 3.4f ? Nav.Steer(pos, p.transform.position, true)
                             : new Vector2(-dir.y, dir.x) * 0.4f;
                    dragT -= dt;                        // the body never stops scraping
                    if (dragT <= 0f)
                    {
                        dragT = Random.Range(0.9f, 1.4f);
                        if (moveDir.sqrMagnitude > 0.05f) Sfx.PlayAt(pos, "drag", 0.45f);
                    }
                }
                else moveDir = Vector2.zero;            // phase two: it never moves again
                attackCd -= dt;
                if (attackCd <= 0f) NextAttack(dir);
                break;

            case St.VomitWind:                          // it heaves, the floor goes green
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 150f);
                PulseVisual(0.08f);
                stT -= dt;
                if (stT <= 0f)
                {
                    st = St.Vomit;
                    stT = 1.25f;
                    tickT = 0f;
                    sfxT = 0f;
                    ClearWarn();
                    MakeFillCone(dir);
                    Sfx.PlayAt(pos, "sludge", 0.9f);
                }
                break;

            case St.Vomit:                              // a massive cone of green sludge
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 60f);
                {
                    if (warnCone != null)               // the fill boils
                    {
                        warnCone.localScale = new Vector3(7.8f * (0.96f + 0.06f * Mathf.Sin(Time.time * 15f)), 6.4f, 1f);
                        var sr = warnCone.GetComponent<SpriteRenderer>();
                        if (sr != null)
                            sr.color = new Color(0.35f, 0.75f, 0.12f,
                                0.5f + 0.15f * Mathf.Sin(Time.time * 21f));
                    }

                    // the spray: sludge bursts and pools cooking the floor
                    Vector2 fd = new Vector2(Mathf.Cos(Mathf.Atan2(dir.y, dir.x)),
                                             Mathf.Sin(Mathf.Atan2(dir.y, dir.x)));
                    Fx.Burst(pos + fd * Random.Range(1.4f, 4.4f), Sludge, 3, 5f);
                    sfxT -= dt;
                    if (sfxT <= 0f)
                    {
                        sfxT = 0.34f;
                        Vector2 at = pos + fd * Random.Range(2f, 5f);
                        FluidPool.Spawn(ClampFloor(at), FluidKind.Acid, Random.Range(0.9f, 1.5f));
                        Sfx.PlayAt(pos, "sludge", 0.4f, Random.Range(1f, 1.2f));
                    }

                    // caught in the fan: burn damage, and the suit corrodes
                    tickT -= dt;
                    if (tickT <= 0f)
                    {
                        tickT = 0.3f;
                        bool inCone = dist < 6.6f && Vector2.Angle(dir, to) < 37f;
                        if (inCone)
                        {
                            p.ApplyAcid(4f);           // -50% speed and damage for 4s
                            p.Health.TakeDamage(13f * dmgScale, to.normalized);
                        }
                    }
                }
                stT -= dt;
                if (stT <= 0f) { ClearFillCone(); EndAttack(1.9f); }
                break;

            case St.RipWind:                            // it reaches into the wall line
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 90f);
                PulseVisual(0.10f);
                stT -= dt;
                if (stT <= 0f)
                {
                    // the rip: real cover in reach comes out of the floor
                    GameManager.SmashWallsNear(pos, 5f);
                    Rubble.ClearNear(pos, 3.4f);
                    GameManager.Shake(0.5f);
                    Sfx.PlayAt(pos, "wall_smash", 0.85f);
                    st = St.Avalanche;
                    stT = 1.0f;
                    chunkT = 0.12f;
                    chunksLeft = 4;                     // and it throws what it tore out
                }
                break;

            case St.Avalanche:                          // scrap, arcing toward you
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 70f);
                chunkT -= dt;
                if (chunksLeft > 0 && chunkT <= 0f)
                {
                    chunksLeft--;
                    chunkT = 0.17f;
                    Vector2 target = ClampFloor((Vector2)p.transform.position +
                                                Random.insideUnitCircle * 1.1f);
                    ScrapChunk.Throw(pos + dir * 0.9f + Vector2.up * 0.5f, target,
                                     15f * dmgScale);
                    Sfx.PlayAt(pos, "shatter", 0.5f, 0.8f);
                }
                stT -= dt;
                if (stT <= 0f) EndAttack(1.7f);
                break;

            case St.BeamWind:                           // the maw lights up, rust to the core
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 110f);
                stT -= dt;
                if (stT <= 0f)
                {
                    st = St.Beam;
                    stT = beamDur;
                    smashT = 0f;
                    zapT = 0f;
                    sfxT = 0f;
                    ClearWarn();
                    MakeSweepLine(15f, beamWidth, new Color(0.85f, 0.45f, 0.12f, 0.85f));
                    Sfx.PlayAt(pos, "rust_beam", 0.8f);
                    GameManager.Shake(0.3f);
                }
                break;

            case St.Beam:                               // thick, slow, and it eats cover
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 40f);
                {
                    float prog = 1f - Mathf.Clamp01(stT / beamDur);
                    float a = beamBase + Mathf.Lerp(-beamHalf, beamHalf, prog);
                    Vector2 ld = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                    RotateSweepLine(a, pos + ld * 7.5f);
                    GameManager.Shake(0.07f);

                    zapT -= dt;                         // and it burns what it touches
                    if (zapT <= 0f)
                    {
                        zapT = 0.28f;
                        Vector2 rel = (Vector2)p.transform.position - pos;
                        float along = Vector2.Dot(rel, ld);
                        float side = Mathf.Abs(ld.x * rel.y - ld.y * rel.x);
                        if (along > 0.9f && along < 15f && side < beamWidth * 0.5f)
                            p.Health.TakeDamage(beamDmg * dmgScale, to.normalized);
                        Fx.Burst(pos + ld * Random.Range(2f, 8f), Rust, 2, 3.5f);
                    }

                    smashT -= dt;                       // every cover it crosses is scrap
                    if (smashT <= 0f)
                    {
                        smashT = 0.18f;
                        for (int k = 1; k <= 4; k++)
                        {
                            Vector2 at = pos + ld * (k * 2.7f);
                            if (Mathf.Abs(at.x) > GameManager.ArenaHalf.x + 1f) continue;
                            if (Mathf.Abs(at.y) > GameManager.ArenaHalf.y + 1f) continue;
                            GameManager.SmashWallsNear(at, 1.05f);
                            Rubble.ClearNear(at, 1.05f);
                        }
                    }

                    sfxT -= dt;
                    if (sfxT <= 0f)
                    {
                        sfxT = 0.45f;
                        Sfx.PlayAt(pos, "rust_beam", 0.55f, Random.Range(0.96f, 1.06f));
                    }
                }
                stT -= dt;
                if (stT <= 0f) { ClearSweepLine(); EndAttack(2.0f); }
                break;

            // ------------------------------------------------- phase two
            case St.TurretAcid:                         // globs over the ruin
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 50f);
                globT -= dt;
                if (globT <= 0f && globsLeft > 0)
                {
                    globsLeft--;
                    globT = 0.34f;
                    Vector2 at = ClampFloor((Vector2)p.transform.position + Random.insideUnitCircle * 1.4f);
                    AcidGlob.Lob(pos + Vector2.up * 0.6f, at, 9f * dmgScale);
                    Sfx.PlayAt(pos, "sludge", 0.45f, Random.Range(0.95f, 1.15f));
                }
                stT -= dt;
                if (stT <= 0f) EndAttack(1.1f);
                break;

            case St.TurretBullets:                      // it spams the whole room
                moveDir = Vector2.zero;
                FacePlayer(dir, dt, 60f);
                burstT -= dt;
                if (burstT <= 0f && burstsLeft > 0)
                {
                    burstsLeft--;
                    burstT = 0.3f;
                    float baseA = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    for (int i = -2; i <= 2; i++)
                    {
                        float a = (baseA + i * 8f + Random.Range(-3f, 3f)) * Mathf.Deg2Rad;
                        Bullet.Spawn(pos + dir * 1.1f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)),
                                     9.5f, 8f * dmgScale, false, new Color(0.85f, 1f, 0.4f));
                    }
                    if (burstsLeft == 1)                // the last burst rings outward
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * 45f * Mathf.Deg2Rad;
                            Bullet.Spawn(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)),
                                         7.5f, 7f * dmgScale, false, new Color(1f, 0.75f, 0.3f));
                        }
                }
                stT -= dt;
                if (stT <= 0f) EndAttack(1.0f);
                break;

            case St.Recover:
                moveDir = phase == 2 ? Vector2.zero : new Vector2(-dir.y, dir.x) * 0.3f;
                stT -= dt;
                if (stT <= 0f) st = St.Chase;
                break;
        }

        Animate(dt, moveDir.sqrMagnitude > 0.05f);
    }

    void FixedUpdate()
    {
        if (GameManager.IsOver) { rb.SetVel(Vector2.zero); return; }
        Vector2 v = moveDir * speed;
        Vector2 p0 = rb.position;
        if (Pit.Blocks(p0, p0 + v * Time.fixedDeltaTime)) v = Vector2.zero;
        rb.SetVel(v);
    }

    // ------------------------------------------------------------ flow
    void FacePlayer(Vector2 dir, float dt, float rate)
    {
        if (facing == null || rate <= 0f) return;
        float want = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        facing.localRotation = Quaternion.Euler(0, 0,
            Mathf.MoveTowardsAngle(facing.localEulerAngles.z, want, rate * dt));
    }

    void PulseVisual(float amount)
    {
        if (visual != null) visual.localScale = Vector3.one * (1f + amount * Mathf.Sin(Time.time * 24f));
    }

    void EndAttack(float cd)
    {
        st = phase == 2 ? St.Chase : St.Recover;
        attackCd = phase == 2 ? Random.Range(1.0f, 1.5f) : cd;
        moveDir = Vector2.zero;
        if (visual != null) visual.localScale = Vector3.one;
        ClearWarn();
        ClearSweepLine();
    }

    // every attack opens the same way: a roar that shakes the room
    void NextAttack(Vector2 dir)
    {
        Vector2 pos = transform.position;
        Sfx.PlayAt(pos, "roar", 0.95f, Random.Range(0.92f, 1.1f));
        GameManager.Shake(0.22f);

        if (phase == 1)
        {
            atk = (atk + 1) % 3;
            switch (atk)
            {
                case 0:
                    st = St.VomitWind; stT = 0.75f;
                    MakeWarnCone(dir);
                    break;

                case 1:
                    st = St.RipWind; stT = 0.85f;
                    break;

                default:
                    st = St.BeamWind; stT = 0.6f;
                    beamDur = 3.0f; beamHalf = 95f; beamWidth = 1.3f; beamDmg = 11f;
                    beamBase = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    MakeWarnLine(dir);
                    break;
            }
        }
        else
        {
            atk = (atk + 1) % 3;
            switch (atk)
            {
                case 0:                                  // acid, everywhere
                    st = St.TurretAcid; stT = 1.7f;
                    globT = 0.25f; globsLeft = 4;
                    FluidPool.Spawn(ClampFloor(pos + dir * 2.6f), FluidKind.Acid, 1.5f);
                    FluidPool.Spawn(ClampFloor(pos - dir * 2.2f), FluidKind.Acid, 1.3f);
                    break;

                case 1:                                  // bullets, then a ring
                    st = St.TurretBullets; stT = 1.5f;
                    burstT = 0.2f; burstsLeft = 3;
                    Sfx.PlayAt(pos, "turret_up", 0.5f, 1.3f);
                    break;

                default:                                 // and the beam again, wider
                    st = St.BeamWind; stT = 0.5f;
                    beamDur = 2.4f; beamHalf = 140f; beamWidth = 1.55f; beamDmg = 10f;
                    beamBase = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    MakeWarnLine(dir);
                    break;
            }
        }
    }

    // ------------------------------------------------------------ phase two
    void ToPhase2()
    {
        phase = 2;
        speed = 0f;
        moveDir = Vector2.zero;
        atk = -1;

        Fx.Flash(transform.position, AcidGreen, 4.4f, 0.3f);
        Fx.Ring(transform.position, AcidGreen, 0.5f, 8f, 0.6f, 30);
        Fx.Ring(transform.position, new Color(0.8f, 0.6f, 0.3f), 0.5f, 7f, 0.8f, 30);
        Fx.Burst(transform.position, Sludge, 30, 11f);
        Gore.Stain(transform.position, new Color(0.25f, 0.45f, 0.1f), 2.4f);
        GameManager.Shake(0.9f);
        GameManager.Instance.HitStop(0.13f);
        Sfx.PlayAt(transform.position, "turret_up", 1f);
        Sfx.PlayAt(transform.position, "roar", 0.95f, 0.75f);

        // roots tear into whatever floor is under it
        Rubble.Spawn((Vector2)transform.position + new Vector2(1.8f, 0.4f));
        Rubble.Spawn((Vector2)transform.position + new Vector2(-1.6f, -0.6f));

        if (TimeLeap.Instance != null)
            TimeLeap.Instance.Announce("THE VESSEL ROOTS ITSELF", 2.2f);
    }

    // ------------------------------------------------------------ animation
    void Animate(float dt, bool moving)
    {
        if (visual == null) return;
        float t = Time.time;

        visual.localPosition = new Vector3(0f, Mathf.Sin(t * (phase == 2 ? 2.6f : 4.2f)) * 0.06f, 0f);

        if (armPart != null)
            armPart.localRotation = Quaternion.Euler(0, 0,
                10f + (moving ? Mathf.Sin(t * 6f) * 11f : Mathf.Sin(t * 1.4f) * 3f));
        if (legPart != null)
            legPart.localRotation = Quaternion.Euler(0, 0,
                -34f + (moving ? Mathf.Sin(t * 6f + 1.7f) * 9f : 0f));
        if (mechPart != null && st != St.Chase && st != St.Recover)
            mechPart.localRotation = Quaternion.Euler(0, 0, 14f + Mathf.Sin(t * 42f) * 2.5f);
        if (gearPart != null)                          // something still turns inside
            gearPart.Rotate(0, 0, (st == St.Chase ? -110f : -34f) * dt);

        // the tank of what it is about to throw up
        if (tankSr != null)
        {
            float w = 0.5f + 0.5f * Mathf.Sin(t * (phase == 2 ? 9f : 3.4f));
            tankSr.color = Color.Lerp(new Color(0.25f, 0.55f, 0.1f), AcidGreen, w);
        }
        // the maw: where the acid comes from, and it knows you are watching
        if (mawSr != null)
        {
            float w = 0.5f + 0.5f * Mathf.Sin(t * (phase == 2 ? 12f : 4.5f));
            mawSr.color = Color.Lerp(Sludge, Color.Lerp(AcidGreen, Color.white, w), phase == 2 ? 1f : 0.6f);
            mawSr.transform.localScale = Vector3.one * (1.15f * (1f + (phase == 2 ? 0.22f : 0.1f) * w));
        }

        // the voice of something that can no longer close its mouth
        breathT -= dt;
        if (breathT <= 0f)
        {
            breathT = Random.Range(2.8f, 4.6f);
            if (!GameManager.IsOver)
            {
                Sfx.PlayAt(transform.position, "roar", 0.6f, Random.Range(0.95f, 1.15f));
                Sfx.PlayAt(transform.position, "drag", 0.35f, Random.Range(0.9f, 1.05f));
            }
        }
    }

    // ------------------------------------------------------------ telegraphs
    void MakeWarnCone(Vector2 dir)
    {
        var g = new GameObject("VomitWarn");
        g.transform.position = (Vector2)transform.position + dir * 3.4f;
        g.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        g.transform.localScale = new Vector3(7.6f, 6.2f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(0.5f, 0.9f, 0.2f, 0.26f);
        sr.sortingOrder = -5;
        warnCone = g.transform;
    }

    // the cone itself: sludge filling the whole fan, edge to edge
    void MakeFillCone(Vector2 dir)
    {
        var g = new GameObject("VomitFill");
        g.transform.position = (Vector2)transform.position + dir * 3.4f;
        g.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        g.transform.localScale = new Vector3(7.6f, 6.2f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(0.35f, 0.75f, 0.12f, 0.5f);
        sr.sortingOrder = 19;                          // over the floor, under units
        warnCone = g.transform;
    }

    void MakeWarnLine(Vector2 dir)
    {
        var g = new GameObject("BeamWarn");
        g.transform.position = (Vector2)transform.position + dir * 5f;
        g.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        g.transform.localScale = new Vector3(11f, beamWidth * 0.9f, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square;
        sr.color = new Color(0.85f, 0.45f, 0.12f, 0.3f);
        sr.sortingOrder = -5;
        warnCone = g.transform;
    }

    void ClearWarn() { if (warnCone != null) { Destroy(warnCone.gameObject); warnCone = null; } }
    void ClearFillCone() { ClearWarn(); }

    void MakeSweepLine(float len, float width, Color c)
    {
        if (sweepLine != null) Destroy(sweepLine.gameObject);
        var g = new GameObject("RustBeam");
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square;
        sr.color = c;
        sr.sortingOrder = 40;
        sweepLine = g.transform;
        RotateSweepLine(0f, (Vector2)transform.position + Vector2.right * len);
        sweepLine.localScale = new Vector3(len, width, 1f);
    }

    void RotateSweepLine(float angleDeg, Vector2 head)
    {
        if (sweepLine == null) return;
        Vector2 tail = transform.position;
        sweepLine.position = (tail + head) * 0.5f;
        sweepLine.rotation = Quaternion.Euler(0, 0, angleDeg);
        sweepLine.localScale = new Vector3(Vector2.Distance(tail, head), sweepLine.localScale.y, 1f);
    }

    void ClearSweepLine() { if (sweepLine != null) { Destroy(sweepLine.gameObject); sweepLine = null; } }

    // test tooling: the body lets go without the ceremony
    public static void DevDismiss()
    {
        var v = Active;
        Active = null;
        if (v != null)
        {
            v.ClearWarn();
            v.ClearSweepLine();
            Destroy(v.gameObject);
        }
    }

    // keeps vomit pools and glob targets on solid floor, inside the room
    Vector2 ClampFloor(Vector2 v)
    {
        v.x = Mathf.Clamp(v.x, -GameManager.ArenaHalf.x + 1.5f, GameManager.ArenaHalf.x - 1.5f);
        v.y = Mathf.Clamp(v.y, -GameManager.ArenaHalf.y + 1.5f, GameManager.ArenaHalf.y - 1.5f);
        for (int i = 0; i < 4 && Pit.Inside(v); i++)
            v.y = v.y > 0f ? v.y - 2f : v.y + 2f;
        return v;
    }

    // ------------------------------------------------------------ contact
    void OnCollisionStay2D(Collision2D c)
    {
        if (!c.collider.CompareTag("Player")) return;
        if (contactT > 0f) return;
        var p = PlayerController.Instance;
        if (p == null) return;
        Vector2 away = ((Vector2)c.collider.transform.position - (Vector2)transform.position).normalized;
        if (p.Health.TakeDamage(16f * dmgScale, away)) contactT = 0.7f;
    }

    // ------------------------------------------------------------ hud
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
                           phase == 2 ? new Color(0.55f, 1f, 0.2f) : new Color(0.78f, 0.5f, 0.2f),
                           health.current / health.max);

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(x + 2f, y - 34f, w, 30f), "THE BROKEN VESSEL", nameStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y - 36f, w, 30f), "THE BROKEN VESSEL", nameStyle);
    }

    // ------------------------------------------------------------ death
    void Die(Health h)
    {
        Vector2 pos = transform.position;
        if (visual != null) Destroy(visual.gameObject);
        ClearWarn();
        ClearSweepLine();

        Gore.Burst(pos, h.bloodColor, Art.MechFlesh, 20, 2f);
        Gore.Burst(pos, h.bloodColor, new Color(0.55f, 0.5f, 0.42f), 6, 1.6f);
        Fx.Explosion(pos, Sludge, new Color(0.8f, 0.95f, 0.4f), 1.9f);
        Fx.Explosion(pos, new Color(0.6f, 0.4f, 0.2f), Color.white, 1.3f);
        Fx.Ring(pos, Sludge, 0.5f, 8f, 0.7f, 30);
        Fx.Sparks(pos, Color.white, 44, 20f);
        Gore.Stain(pos, new Color(0.16f, 0.3f, 0.07f), 2.6f);
        GameManager.Shake(1.1f);
        GameManager.Instance.HitStop(0.15f);
        GameManager.Kills++;
        Sfx.PlayAt(pos, "roar", 1f, 0.6f);
        Sfx.PlayAt(pos, "herald_die", 0.7f, 0.8f);

        Rubble.ClearAll();                             // the ruin stops growing
        PlayerUpgrades.GainXp(340f);
        if (TimeLeap.Instance != null)
        {
            TimeLeap.Instance.Announce("THE VESSEL IS EMPTY", 2.4f);
            TimeLeap.Instance.OnVesselDown();          // the flesh takes the room
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
    }
}

// ============================================================ SCRAP CHUNK
// a piece of the wall, thrown: it arcs over the room, shatters where it
// lands, and leaves a pile of rubble that blocks movement until you shoot it
public class ScrapChunk : MonoBehaviour
{
    Vector2 from, to;
    float t, flight, arcH, damage;
    Transform shade;

    public static void Throw(Vector2 from, Vector2 to, float damage)
    {
        var g = new GameObject("ScrapChunk");
        g.transform.position = from;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.WallTile();
        sr.color = new Color(0.55f, 0.5f, 0.45f);
        sr.sortingOrder = 24;
        g.transform.localScale = new Vector3(0.55f, 0.45f, 1f);

        var sg = new GameObject("Shadow");
        sg.transform.SetParent(g.transform, false);
        var ssr = sg.AddComponent<SpriteRenderer>();
        ssr.sprite = SpriteFactory.Circle;
        ssr.color = new Color(0, 0, 0, 0.4f);
        ssr.sortingOrder = -3;
        sg.transform.localScale = new Vector3(0.6f, 0.34f, 1f);

        var c = g.AddComponent<ScrapChunk>();
        c.from = from;
        c.to = to;
        c.damage = damage;
        c.shade = sg.transform;
        c.flight = Mathf.Clamp(Vector2.Distance(from, to) / 8f, 0.5f, 1.3f);
        c.arcH = 1.5f + c.flight;
        return;
    }

    void Update()
    {
        t += Time.deltaTime / flight;
        float k = Mathf.Clamp01(t);
        Vector2 pos = Vector2.Lerp(from, to, k);
        transform.position = new Vector3(pos.x, pos.y + Mathf.Sin(k * Mathf.PI) * arcH, 0f);
        transform.Rotate(0, 0, 340f * Time.deltaTime);
        if (shade != null)                              // the floor keeps the shadow
        {
            shade.position = new Vector3(pos.x, pos.y, 0f);
            float shrink = 1f - 0.4f * Mathf.Sin(k * Mathf.PI);
            shade.localScale = new Vector3(0.6f * shrink, 0.34f * shrink, 1f);
        }

        if (t >= 1f) Shatter();
    }

    void Shatter()
    {
        Vector2 p = to;
        Fx.Burst(p, new Color(0.6f, 0.55f, 0.5f), 20, 9f);
        Fx.Burst(p, new Color(0.35f, 0.33f, 0.3f), 12, 6f);
        Fx.Sparks(p, new Color(0.9f, 0.8f, 0.6f), 14, 13f);
        GameManager.Shake(0.32f);
        Sfx.PlayAt(p, "shatter", 0.85f);

        var pl = PlayerController.Instance;
        if (pl != null && Vector2.Distance(pl.transform.position, p) < 1.1f)
            pl.Health.TakeDamage(damage, ((Vector2)pl.transform.position - p).normalized);

        Rubble.Spawn(p);
        Destroy(gameObject);
    }
}

// ============================================================ ACID GLOB
// the turret's lugged sludge: arcs over the room, lands, and pools
public class AcidGlob : MonoBehaviour
{
    Vector2 from, to;
    float t, flight, arcH, damage;
    Transform shade;

    public static void Lob(Vector2 from, Vector2 to, float damage)
    {
        var g = new GameObject("AcidGlob");
        g.transform.position = from;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(0.5f, 0.95f, 0.22f);
        sr.sortingOrder = 24;
        g.transform.localScale = Vector3.one * 0.4f;

        var sg = new GameObject("Shadow");
        sg.transform.SetParent(g.transform, false);
        var ssr = sg.AddComponent<SpriteRenderer>();
        ssr.sprite = SpriteFactory.Circle;
        ssr.color = new Color(0, 0, 0, 0.4f);
        ssr.sortingOrder = -3;

        var c = g.AddComponent<AcidGlob>();
        c.from = from;
        c.to = to;
        c.damage = damage;
        c.shade = sg.transform;
        c.flight = Mathf.Clamp(Vector2.Distance(from, to) / 7f, 0.5f, 1.15f);
        c.arcH = 1f + c.flight;
    }

    void Update()
    {
        t += Time.deltaTime / flight;
        float k = Mathf.Clamp01(t);
        Vector2 pos = Vector2.Lerp(from, to, k);
        transform.position = new Vector3(pos.x, pos.y + Mathf.Sin(k * Mathf.PI) * arcH, 0f);
        if (shade != null) shade.position = new Vector3(pos.x, pos.y, 0f);
        srPulse();

        if (t >= 1f) Land();
    }

    void srPulse()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.Lerp(new Color(0.35f, 0.75f, 0.14f), new Color(0.65f, 1f, 0.35f),
                                              Mathf.PingPong(Time.time * 6f, 1f));
    }

    void Land()
    {
        Vector2 p = to;
        FluidPool.Spawn(p, FluidKind.Acid, Random.Range(1.5f, 2.1f));
        Fx.Burst(p, new Color(0.5f, 0.95f, 0.25f), 14, 6.5f);
        Fx.Ring(p, new Color(0.55f, 1f, 0.3f, 0.8f), 0.2f, 2.4f, 0.3f, 30);
        Sfx.PlayAt(p, "acid_splash", 0.75f);

        var pl = PlayerController.Instance;
        if (pl != null && Vector2.Distance(pl.transform.position, p) < 1.15f)
            pl.Health.TakeDamage(damage, ((Vector2)pl.transform.position - p).normalized);

        Destroy(gameObject);
    }
}

// ============================================================ RUBBLE
// what a shattered chunk leaves behind: a low pile that blocks your movement
// (and the horde's pathing) until you shoot it apart or the void takes it
public class Rubble : MonoBehaviour
{
    public static readonly List<Rubble> All = new List<Rubble>();

    Health health;
    Vector2 size;
    float life = 22f, sinkT = -1f;
    bool removed;

    public static Rubble Spawn(Vector2 pos)
    {
        pos.x = Mathf.Clamp(pos.x, -GameManager.ArenaHalf.x + 0.8f, GameManager.ArenaHalf.x - 0.8f);
        pos.y = Mathf.Clamp(pos.y, -GameManager.ArenaHalf.y + 0.8f, GameManager.ArenaHalf.y - 0.8f);
        if (Pit.Inside(pos)) return null;

        var g = new GameObject("RubblePile");
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-24f, 24f));

        Vector2 size = new Vector2(Random.Range(0.95f, 1.45f), Random.Range(0.7f, 1.05f));

        var sh = new GameObject("Shadow");
        sh.transform.SetParent(g.transform, false);
        sh.transform.localScale = new Vector3(1.15f, 0.9f, 1f);
        var ssr = sh.AddComponent<SpriteRenderer>();
        ssr.sprite = SpriteFactory.Square;
        ssr.color = new Color(0, 0, 0, 0.45f);
        ssr.sortingOrder = -4;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.WallTile();
        sr.color = new Color(0.5f, 0.46f, 0.42f);
        sr.sortingOrder = -3;
        g.transform.localScale = size;

        g.AddComponent<BoxCollider2D>().size = Vector2.one;

        var r = g.AddComponent<Rubble>();
        r.size = size;
        r.health = g.AddComponent<Health>();
        r.health.Init(45f);
        r.health.showBar = false;
        r.health.OnDeath += r.Die;
        All.Add(r);

        Nav.AddObstacle(pos, size);
        Nav.AddSight(pos, size);
        Nav.Bake(GameManager.ArenaHalf);
        return r;
    }

    void Update()
    {
        if (sinkT >= 0f)
        {
            sinkT -= Time.deltaTime;
            transform.localScale = Vector3.LerpUnclamped(size, Vector2.zero, 1f - Mathf.Clamp01(sinkT / 0.4f));
            if (sinkT <= 0f) Remove(false);
            return;
        }
        life -= Time.deltaTime;
        if (life <= 0f) { sinkT = 0.4f; }              // the ruin sweeps up after a while
    }

    void Die(Health h)
    {
        Fx.Burst(transform.position, new Color(0.6f, 0.55f, 0.5f), 14, 7f);
        Fx.Sparks(transform.position, new Color(0.9f, 0.85f, 0.7f), 8, 11f);
        Sfx.PlayAt(transform.position, "shatter", 0.5f, Random.Range(0.9f, 1.15f));
        Remove(true);
    }

    void Remove(bool fromDeath)
    {
        if (removed) return;
        removed = true;
        All.Remove(this);
        Nav.RemoveObstacle(transform.position, size);
        Nav.RemoveSight(transform.position, size);
        Nav.Bake(GameManager.ArenaHalf);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (!removed) { removed = true; All.Remove(this); }
    }

    // the void swallow: piles caught in the collapse just go
    public static void ClearNear(Vector2 pos, float radius)
    {
        foreach (var r in All.ToArray())
            if (r != null && Vector2.Distance(r.transform.position, pos) <= radius)
                r.Remove(false);
    }

    public static void ClearAll()
    {
        foreach (var r in All.ToArray())
            if (r != null) r.Remove(false);
        All.Clear();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// SHEPARD: the last one. Your own model, bigger and red, wearing your suit
// and carrying your rifle. He reads your movement and answers with three-
// round bursts at where you are going, flashes blue and slashes anything
// that stays in knifing range, slides away and takes a sniper shot.
//
// At seventy percent he time-leaps like you do - full health, a smaller
// room - and adds the stasis shot and the ghost protocol. At fifty the
// tape itself stops once: he pauses the world, rewinds you into the
// position of the attack, and gives you one slim window to dash out.
// At thirty he rewinds the run bare: your upgrades are gone and the
// mirror of you walks in for the last duel.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Health))]
public class Shepard : MonoBehaviour
{
    public static Shepard Active;
    public static bool BossAlive { get { return Active != null; } }

    // the vhs dodge: the tape stops, the hud reads it
    public static bool VhsActive;
    public static float VhsCountdown;

    enum St { Enter, Chase, BurstWind, Burst, BladeFlash, Blade, Slide, SniperWind, Sniper,
              StasisWind, StasisMag, Ghost, Rewind, Recover }

    static readonly Color Blue = new Color(0.35f, 0.85f, 1f);
    static readonly Color Purple = new Color(0.78f, 0.55f, 1f);
    static readonly Color BloodRed = new Color(0.95f, 0.2f, 0.2f);

    St st = St.Enter;
    int phase = 1, atk = -1, burstShots, magLeft;
    float stT, attackCd, dashT, dashCd, bladeCd, magT, contactT, dmgScale, myDiff;
    float speed = 4.4f;
    Vector2 moveDir, dashDir;
    bool leap1, leap2, vhsDone;
    Camera cam;
    bool ghostUsed, ghostBack;
    float ghostSec = -1f, ghostTotal = 6.0f;
    Vector2 ghostFrom, ghostReturn;
    Coroutine stasisTick;

    Rigidbody2D rb;
    CircleCollider2D bodyCol;
    Health health;
    Transform visual;
    HumanoidAnim anim;
    SpriteRenderer bodySr, warnLine;
    Color bodyOrig;
    Transform glow;
    GUIStyle nameStyle;
    Texture2D pauseCircle;

    public static Shepard Spawn(float difficulty)
    {
        var p = PlayerController.Instance;
        Vector2 pos = p != null ? (Vector2)p.transform.position + Vector2.right * 7f : Vector2.right * 7f;
        for (int i = 0; i < 30; i++)
        {
            Vector2 c = p != null ? (Vector2)p.transform.position : Vector2.zero;
            pos = c + Random.insideUnitCircle.normalized * Random.Range(6f, 9f);
            pos.x = Mathf.Clamp(pos.x, -GameManager.ArenaHalf.x + 2.5f, GameManager.ArenaHalf.x - 2.5f);
            pos.y = Mathf.Clamp(pos.y, -GameManager.ArenaHalf.y + 2.5f, GameManager.ArenaHalf.y - 2.5f);
            if (Pit.Inside(pos)) continue;
            if (!Nav.IsFree(pos, true)) continue;
            break;
        }

        var g = new GameObject("Shepard");
        g.transform.position = pos;
        var s = g.AddComponent<Shepard>();
        s.Setup(difficulty, pos);
        return s;
    }

    void Setup(float difficulty, Vector2 pos)
    {
        Active = this;
        myDiff = difficulty;
        cam = Camera.main;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        dmgScale = Mathf.Sqrt(difficulty);

        bodyCol = GetComponent<CircleCollider2D>();
        bodyCol.radius = 0.34f;

        health = GetComponent<Health>();
        health.OnDeath += Die;
        health.Init(1000f * GameManager.HpScale(difficulty));
        health.showBar = false;                       // the named bar rides the HUD
        health.barColor = BloodRed;
        health.bloodColor = BloodRed;
        health.invulnerable = true;                   // still arriving

        visual = Humanoid.Build(transform, Art.Shepard(), 1.55f, 25);
        anim = visual.GetComponent<HumanoidAnim>();
        bodySr = visual.GetComponentInChildren<SpriteRenderer>();
        if (bodySr != null) bodyOrig = bodySr.color;

        // a red halo so he reads as the player wearing the wrong color
        var hg = new GameObject("ShepardGlow");
        hg.transform.SetParent(transform, false);
        hg.transform.localScale = Vector3.one * 1.7f;
        var hsr = hg.AddComponent<SpriteRenderer>();
        hsr.sprite = SpriteFactory.Circle;
        hsr.color = new Color(0.9f, 0.15f, 0.15f, 0.16f);
        hsr.sortingOrder = 18;
        glow = hg.transform;

        st = St.Enter;
        stT = 1.0f;
        attackCd = 1.4f;

        if (TimeLeap.Instance != null)
            TimeLeap.Instance.Announce("SHEPARD", 2.6f);
        Sfx.PlayAt(pos, "horn", 0.9f, 0.72f);
        Sfx.PlayAt(pos, "rewind", 0.8f);
        GameManager.Shake(0.8f);
        Fx.Ring(pos, BloodRed, 0.4f, 6f, 0.6f, 30);
        Fx.Burst(pos, BloodRed, 24, 8f);

        Cutscenes.OnShepardSpawn();                    // the world stops for the words
        if (Cutscenes.SeenShepard) stT = 0f;           // he was already standing there
    }

    // ------------------------------------------------------------ loop
    void Update()
    {
        if (GameManager.IsOver) { if (rb != null) rb.SetVel(Vector2.zero); return; }
        var p = PlayerController.Instance;
        if (p == null || health == null) return;

        float dt = Time.deltaTime;
        Vector2 pos = transform.position;
        Vector2 to = (Vector2)p.transform.position - pos;
        float dist = to.magnitude;
        Vector2 dir = to / Mathf.Max(dist, 0.001f);

        // the model looks where the gun looks
        if (anim != null)
        {
            anim.SetFacing(moveDir.sqrMagnitude > 0.05f ? moveDir : dir);
            anim.SetAim(dir);
        }

        if (glow != null)
        {
            float pulse = phase == 2 ? 0.22f : 0.14f;
            glow.GetComponent<SpriteRenderer>().color =
                new Color(0.9f, 0.15f, phase == 2 ? 0.35f : 0.15f,
                          pulse + 0.08f * Mathf.Sin(Time.time * (phase == 2 ? 6f : 3f)));
        }

        if (dashT > 0f) dashT -= dt;
        if (bladeCd > 0f) bladeCd -= dt;

        switch (st)
        {
            case St.Enter:
                moveDir = Vector2.zero;
                if (visual != null)
                {
                    float f = 1f - Mathf.Clamp01(stT / 1.0f);
                    visual.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.55f, f * f);
                }
                if ((stT -= dt) <= 0f)
                {
                    if (visual != null) visual.localScale = Vector3.one * 1.55f;
                    health.invulnerable = false;
                    st = St.Chase;
                }
                break;

            case St.Chase:
                Chase(dt, dir, dist);
                break;

            case St.BurstWind:
                moveDir = Vector2.zero;
                Tint(Mathf.Sin(Time.time * 44f) > 0f);
                if ((stT -= dt) <= 0f) { st = St.Burst; burstShots = 3; stT = 0f; }
                break;

            case St.Burst:
                stT -= dt;
                if (stT <= 0f && burstShots > 0)
                {
                    burstShots--;
                    stT = 0.13f;
                    PredictedShot(pos, dir, dist);
                }
                if (burstShots <= 0 && stT <= 0f) EndAttack(0.55f);
                break;

            case St.BladeFlash:
                moveDir = Vector2.zero;
                if (visual != null) visual.localScale = Vector3.one * (1.55f * (1f + 0.08f * Mathf.Sin(Time.time * 50f)));
                if ((stT -= dt) <= 0f)
                {
                    if (visual != null) visual.localScale = Vector3.one * 1.55f;
                    st = St.Blade;
                    stT = 0.12f;
                }
                break;

            case St.Blade:
                moveDir = Vector2.zero;
                stT -= dt;
                if (stT <= 0f)
                {
                    // the slash: everything inside the blade's reach eats steel
                    Vector2 slashTo = (Vector2)p.transform.position - pos;
                    if (slashTo.magnitude < 3.0f && !p.Health.IsDead)
                    {
                        Vector2 away = slashTo.normalized;
                        p.Health.TakeDamage(30f * dmgScale, away * 1.5f);
                        p.Shove(away, 9f);
                        GameManager.Shake(0.5f);
                    }
                    Fx.Ring(pos + (Vector2)(dir * 1.2f), Blue, 0.3f, 3.6f, 0.22f, 42);
                    Fx.Sparks(pos + (Vector2)(dir * 1.2f), Blue, 18, 14f);
                    Sfx.PlayAt(pos, "blade_slash", 1f);
                    EndAttack(0.45f);
                }
                break;

            case St.Slide:
                stT -= dt;
                if (stT <= 0f) { st = St.SniperWind; stT = 0.45f; MakeWarnLine(pos, dir); }
                break;

            case St.SniperWind:
                moveDir = dashT > 0f ? dashDir : Vector2.zero;
                AimWarnLine(pos, dir);
                if ((stT -= dt) <= 0f)
                {
                    ClearWarnLine();
                    Bullet.Spawn(pos + dir * 0.7f, dir, 36f, 22f * dmgScale, false,
                                 new Color(1f, 0.6f, 0.2f));
                    Fx.Sparks(pos + dir * 0.7f, new Color(1f, 0.75f, 0.3f), 14, 18f);
                    Sfx.PlayAt(pos, "shepard_sniper", 1f);
                    GameManager.Shake(0.25f);
                    EndAttack(0.55f);
                }
                break;

            case St.StasisWind:
                moveDir = Vector2.zero;
                Tint(Mathf.Sin(Time.time * 40f) > 0f);
                if ((stT -= dt) <= 0f)
                {
                    Tint(false);
                    StasisBullet.Summon(pos + dir * 0.6f, dir, 5f * dmgScale);
                    Sfx.PlayAt(pos, "stasis_fire", 0.9f);
                    EndAttack(0.45f);
                }
                break;

            case St.StasisMag:
                moveDir = Vector2.zero;
                magT -= dt;
                if (magT <= 0f && magLeft > 0)
                {
                    magLeft--;
                    magT = 0.07f;
                    Vector2 mdir = ((Vector2)p.transform.position - pos).normalized;
                    Bullet.Spawn(pos + mdir * 0.6f, mdir, 22f, 7f * dmgScale, false,
                                 new Color(1f, 0.5f, 0.35f));
                    Sfx.PlayAt(pos, "shepard_rifle", 0.75f, 1.1f);
                }
                if (magLeft <= 0) EndAttack(0.4f);
                break;

            case St.Ghost:
                GhostUpdate(dt, pos, dir, dist);
                break;

            case St.Rewind:
                moveDir = Vector2.zero;
                break;

            case St.Recover:
                moveDir = Vector2.zero;
                if ((stT -= dt) <= 0f) { st = St.Chase; attackCd = phase == 2 ? 0.55f : 0.85f; }
                break;
        }
    }

    // ------------------------------------------------------------ chase
    // he keeps dashing at you - and the moment you stay close he flashes
    // blue; the moment you are far he cycles his guns
    void Chase(float dt, Vector2 dir, float dist)
    {
        // at fifty the tape arms itself once: the next clean moment he stops it
        if (!vhsDone && health.current <= health.max * 0.5f)
        {
            ArmVhs();
            return;
        }

        // dash toward you whenever the gap allows it: he always closes
        dashCd -= dt;
        if (dashCd <= 0f && dashT <= 0f && dist > 2.2f)
        {
            dashCd = phase == 2 ? 0.7f : 0.95f;
            dashT = 0.22f;
            dashDir = dir;
            Sfx.PlayAt(transform.position, "dash", 0.5f);
        }

        if (dashT > 0f) { moveDir = dashDir; return; }

        // orbit at range: close enough to matter, far enough to be shot
        if (dist > 6.5f) moveDir = dir;
        else if (dist < 3.4f) moveDir = -dir * 0.5f + Vector2.Perpendicular(dir) * 0.5f;
        else moveDir = Vector2.Perpendicular(dir) * (atk % 2 == 0 ? 1f : -1f) * 0.55f;

        // the omni-blade answers proximity instantly
        if (dist < 2.6f && bladeCd <= 0f)
        {
            bladeCd = 2.6f;
            st = St.BladeFlash;
            stT = 0.38f;
            Fx.Flash(transform.position, Blue, 2.4f, 0.34f);
            Sfx.PlayAt(transform.position, "blade_flash", 0.95f);
            Sfx.PlayAt(transform.position, "tick", 0.85f, 1.5f);   // the clock strikes, then the cut
            return;
        }

        attackCd -= dt;
        if (attackCd > 0f) return;

        atk++;
        if (phase == 2 && (!ghostUsed || atk % 5 == 4))
        {
            // the ghost protocol: his first move in phase two, then one slot
            // every few cycles - he leaves the frame entirely and the three
            // dead bosses take the room one at a time
            ghostUsed = true;
            st = St.Ghost;
            stT = ghostTotal;
            GhostBegin();
            return;
        }

        bool stasisSlot = phase == 2 && (atk % 3 == 2);
        if (stasisSlot)
        {
            st = St.StasisWind;
            stT = 0.45f;
            Sfx.PlayAt(transform.position, "stasis_fire", 0.35f, 0.7f);   // the charge before the orb
            return;
        }

        if (atk % 2 == 0)
        {
            st = St.BurstWind;                        // he raises the rifle
            stT = 0.4f;
            Sfx.PlayAt(transform.position, "shepard_rifle", 0.3f, 0.6f);
        }
        else
        {
            st = St.Slide;                            // he slides away and snipes
            stT = 0.32f;
            dashT = 0.32f;
            dashDir = -dir + Vector2.Perpendicular(dir) * (Random.value < 0.5f ? 1f : -1f);
            dashDir = dashDir.normalized;
            Sfx.PlayAt(transform.position, "dash", 0.6f);
        }
    }

    // the sniper's tell and the burst: the muzzle is just ahead of the body
    void EndAttack(float cd)
    {
        st = St.Recover;
        stT = cd;
        attackCd = cd + (phase == 2 ? 0.25f : 0.5f);
    }

    // the three-round burst: he fires where you are going, not where you are
    void PredictedShot(Vector2 pos, Vector2 dir, float dist)
    {
        var p = PlayerController.Instance;
        if (p == null) return;

        float travel = dist / 24f;                    // their bullet speed
        Vector2 lead = p.Velocity * Mathf.Min(travel, 0.5f);
        Vector2 target = (Vector2)p.transform.position + lead;
        Vector2 shot = (target - pos).normalized;

        Bullet.Spawn(pos + shot * 0.7f, shot, 24f, 9f * dmgScale, false, new Color(1f, 0.4f, 0.3f));
        Fx.Sparks(pos + shot * 0.7f, new Color(1f, 0.6f, 0.4f), 6, 9f);
        Sfx.PlayAt(pos, "shepard_rifle", 0.85f);
    }

    // ------------------------------------------------------------ stasis
    // the orb landed: the color leaves, the world clunks, and he empties
    // the magazine into the statue you have become
    public void OnStasisHit()
    {
        if (health == null || health.IsDead) return;
        // out of time: the orb passes through whatever he has become
        if (st == St.Ghost || st == St.Rewind || st == St.Enter) return;

        ClearWarnLine();                              // the sniper tell is over
        if (visual != null) visual.localScale = Vector3.one * 1.55f;
        st = St.StasisMag;
        stT = 1.2f;
        magLeft = 10;
        magT = 0.1f;
        attackCd = 99f;

        // the clock keeps counting while you cannot move
        if (stasisTick != null) StopCoroutine(stasisTick);
        stasisTick = StartCoroutine(StasisTicks());
    }

    IEnumerator StasisTicks()
    {
        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForSeconds(0.4f);
            Sfx.PlayAt(transform.position, "tick", 0.7f, 0.8f + i * 0.13f);
        }
        stasisTick = null;
    }

    // ------------------------------------------------------------ ghost protocol
    // you cannot touch him while it runs: he floats out of the frame above
    // the screen and the three bosses that came before him take the room one
    // at a time - the smasher's slam, the herald's sweep, the vessel's beam -
    // while he keeps firing down at you from outside the picture. when the
    // dead are done, he drifts back in and becomes solid again
    void GhostBegin()
    {
        Vector2 pos = transform.position;
        ghostFrom = pos;
        ghostBack = false;
        ghostSec = -1f;
        ghostFireT = 0.5f;
        ghostSlam = ghostLaser = ghostBeam = false;

        health.invulnerable = true;                   // out of time: no gun reaches him
        if (bodyCol != null) bodyCol.enabled = false;
        ghostReturn = PickReturn();

        Fx.Burst(pos, Blue, 20, 7f);
        Fx.Ring(pos, Blue, 0.3f, 5f, 0.5f, 30);
        Sfx.PlayAt(pos, "ghost", 0.9f);
        Sfx.PlayAt(pos, "shepard_leap", 0.85f, 1.25f);   // he steps out of the frame
        if (glow != null) glow.localScale = Vector3.one * 2.4f;
    }

    // where he comes back: inside the ring, on your side of it
    Vector2 PickReturn()
    {
        var p = PlayerController.Instance;
        Vector2 half = GameManager.ArenaHalf - Vector2.one * 2f;
        for (int i = 0; i < 14; i++)
        {
            Vector2 at = p != null
                ? (Vector2)p.transform.position + Random.insideUnitCircle.normalized * Random.Range(4f, 7f)
                : Random.insideUnitCircle * 4f;
            at.x = Mathf.Clamp(at.x, -half.x, half.x);
            at.y = Mathf.Clamp(at.y, -half.y, half.y);
            if (Pit.Inside(at)) continue;
            if (!Nav.IsFree(at, true)) continue;
            return at;
        }
        return Vector2.zero;
    }

    // above the top edge of the frame, wherever the camera actually ends
    float OffScreenY()
    {
        float aspect = Mathf.Max(0.4f, cam != null ? cam.aspect : 1.7f);
        float size = Mathf.Max(GameManager.ArenaHalf.y + 1.2f, (GameManager.ArenaHalf.x + 1.2f) / aspect);
        return size + 3f;
    }

    void GhostUpdate(float dt, Vector2 pos, Vector2 dir, float dist)
    {
        var p = PlayerController.Instance;
        float used = ghostTotal - stT;                // time since the protocol began
        const float outT = 0.6f, backT = 5.2f;        // he leaves in a breath, comes back when they finish

        moveDir = Vector2.zero;

        Vector2 off = new Vector2(Mathf.Sin(Time.time * 0.9f) * GameManager.ArenaHalf.x * 0.55f,
                                  OffScreenY());
        if (used < outT)
        {
            transform.position = Vector3.Lerp(ghostFrom, off, used / outT);
        }
        else if (used < backT)
        {
            transform.position = new Vector3(off.x, off.y, 0f);

            // he still shoots from out there: a volley every half second.
            // the rounds come over the wall at the top edge of the ring -
            // spawning them above the boundary would only pop them in it
            ghostFireT -= dt;
            if (ghostFireT <= 0f && p != null)
            {
                ghostFireT = 0.5f;
                Vector2 muzzle = new Vector2(off.x, GameManager.ArenaHalf.y - 0.7f);
                Vector2 toP = (Vector2)p.transform.position - muzzle;
                if (toP.magnitude > 1.8f)
                    PredictedShot(muzzle, toP.normalized, toP.magnitude);
            }

            // the clock ticks once a second while he is gone
            int sec = Mathf.FloorToInt(used);
            if (sec != ghostSec)
            {
                ghostSec = sec;
                Sfx.PlayAt(transform.position, "tick", 0.55f, 0.75f + sec * 0.1f);
            }
        }
        else
        {
            // the attacks are over: he drifts back down into the ring
            float f = Mathf.Clamp01((used - backT) / (ghostTotal - backT));
            transform.position = Vector3.Lerp(off, ghostReturn, f * f * (3f - 2f * f));
            if (!ghostBack && f > 0.02f)
            {
                ghostBack = true;
                Sfx.PlayAt(ghostReturn, "shepard_leap", 0.75f, 0.8f);
                Fx.Burst(ghostReturn, Blue, 14, 6f);
            }
        }

        // the dead take the room, one at a time
        if (!ghostSlam && used > 0.7f)
        {
            ghostSlam = true;
            GhostSlam.Summon(p != null ? (Vector2)p.transform.position : Vector2.zero, 28f * dmgScale);
        }
        if (!ghostLaser && used > 2.2f)
        {
            ghostLaser = true;
            // the herald comes back wearing its own attacks, not the beam
            // the vessel pours: fire from the wall, or the sky called down
            if (Random.value < 0.5f)
                GhostFlame.Summon(9f * dmgScale);
            else
                GhostZeus.Summon();
        }
        if (!ghostBeam && used > 3.5f)
        {
            ghostBeam = true;
            GhostBeam.Summon(12f * dmgScale);
        }

        if ((stT -= dt) <= 0f)
        {
            transform.position = new Vector3(ghostReturn.x, ghostReturn.y, 0f);   // he is here again
            health.invulnerable = false;
            if (bodyCol != null) bodyCol.enabled = true;
            if (glow != null) glow.localScale = Vector3.one * 1.7f;
            Fx.Ring(ghostReturn, Blue, 0.3f, 3f, 0.35f, 30);
            Sfx.PlayAt(ghostReturn, "shepard_leap", 0.6f, 0.95f);
            EndAttack(0.5f);
        }
    }
    float ghostFireT;
    bool ghostSlam, ghostLaser, ghostBeam;

    // ------------------------------------------------------------ phase leaps
    void FixedUpdate()
    {
        if (GameManager.IsOver) { rb.SetVel(Vector2.zero); return; }
        Vector2 v = moveDir * (dashT > 0f ? 10.5f : speed);
        Vector2 p0 = rb.position;
        if (Pit.Blocks(p0, p0 + v * Time.fixedDeltaTime)) v = Vector2.zero;
        rb.SetVel(v);

        // the two leaps fire the moment the bar crosses their line
        if (health != null && !health.IsDead && st != St.Rewind && st != St.Enter)
        {
            if (!leap1 && health.current <= health.max * 0.7f)
            {
                leap1 = true;
                phase = 2;
                Music.SetIntensity(3);             // the tape's phase two: heavy below
                StartCoroutine(RewindLeap());
            }
            else if (leap1 && !leap2 && phase == 2 && health.current <= health.max * 0.3f)
            {
                leap2 = true;
                StartCoroutine(RewindBare());
            }
        }
    }

    // seventy percent: he takes his own timeleap - full health, and the
    // room closes in around you both
    IEnumerator RewindLeap()
    {
        st = St.Rewind;
        ClearWarnLine();
        health.invulnerable = true;
        rb.SetVel(Vector2.zero);
        moveDir = Vector2.zero;

        Vector2 pos = transform.position;
        Sfx.PlayAt(pos, "shepard_leap", 1f, 1f);      // the tape takes him back
        for (int i = 0; i < 3; i++)
        {
            Fx.Ring(pos, Purple, 0.3f, 5.5f, 0.3f, 30);
            Sfx.PlayAt(pos, "rewind", 0.7f, 0.8f + i * 0.15f);
            Sfx.PlayAt(pos, "tick", 0.65f, 1f + i * 0.2f);   // the clock under the rewind
            yield return new WaitForSeconds(0.22f);
        }
        GameManager.Instance.HitStop(0.12f);
        yield return new WaitForSeconds(0.5f);

        health.Restore(health.max);
        if (TimeLeap.Instance != null)
            TimeLeap.Instance.Announce("SHEPARD TIME-LEAPS - THE ROOM CLOSES IN", 2.6f);
        GameManager.Instance.ShrinkArena(2.4f);
        Fx.Ring(pos, Purple, 0.5f, 9f, 0.6f, 30);
        GameManager.Shake(0.9f);

        health.invulnerable = false;
        st = St.Chase;
        attackCd = 1.1f;
    }

    // thirty percent: he rewinds the run bare - the hud loses its grip, the
    // upgrades come out, and the mirror of you walks in for the last duel
    IEnumerator RewindBare()
    {
        st = St.Rewind;
        ClearWarnLine();
        health.invulnerable = true;
        rb.SetVel(Vector2.zero);
        moveDir = Vector2.zero;

        Vector2 pos = transform.position;
        Sfx.PlayAt(pos, "shepard_leap", 1f, 0.72f);   // the tape eating the run
        for (int i = 0; i < 3; i++)
        {
            Fx.Ring(pos, BloodRed, 0.3f, 6.5f, 0.3f, 30);
            Sfx.PlayAt(pos, "rewind", 0.9f, 0.6f + i * 0.12f);
            Sfx.PlayAt(pos, "tick", 0.75f, 0.7f + i * 0.18f);   // the clock coming apart
            yield return new WaitForSeconds(0.25f);
        }
        yield return new WaitForSeconds(0.4f);

        // the tape eats your work: every module the table ever sold you
        if (TimeLeap.Instance != null)
            TimeLeap.Instance.Announce("SHEPARD REWINDS - YOUR MODS ARE GONE", 2.8f);
        var up = PlayerUpgrades.Instance;
        if (up != null) up.ResetAll();
        var gm = GameManager.Instance;
        if (gm != null) gm.HudGlitch(3.5f);
        Sfx.Play("glitch");
        Sfx.PlayAt(pos, "tick", 0.9f, 0.55f);
        GameManager.Shake(0.7f);

        yield return new WaitForSeconds(0.9f);

        // the augments are gone: he says so before the mirror walks in
        yield return Cutscenes.BareWait();

        // he does not die here: he steps out of the tape and leaves the
        // mirror behind
        Fx.Burst(pos, BloodRed, 30, 9f);
        Fx.Ring(pos, BloodRed, 0.4f, 7f, 0.6f, 30);
        Sfx.PlayAt(pos, "rewind", 1f, 0.55f);
        Sfx.PlayAt(pos, "shepard_leap", 1f, 0.6f);

        Active = null;
        float diff = myDiff;
        Destroy(gameObject);                          // the coroutine dies with him
        Sfx.PlayAt(pos, "siren", 1f, 0.7f);           // the same alarm, one last time
        MirrorFighter.Spawn(diff);                    // the mirror walks in
    }

    // test tooling: he steps out of the tape without the ceremony - and the
    // tape stops with him, or the world stays frozen
    public static void DevDismiss()
    {
        if (VhsActive)
        {
            VhsActive = false;
            VhsCountdown = 0f;
            Time.timeScale = 1f;
        }
        var s = Active;
        Active = null;
        if (s != null)
        {
            s.ClearWarnStrike();
            s.ClearWarnLine();
            Destroy(s.gameObject);
        }
    }

    // ------------------------------------------------------------ vhs dodge
    // once, at fifty: the moment you dodge, the tape stops. a big pause
    // button takes the screen, the picture rewinds with static, a timer
    // counts - and when the picture comes back you ARE the attack position
    public void ArmVhs()
    {
        if (vhsDone || phase < 2) return;
        vhsDone = true;
        StartCoroutine(VhsRoutine());
    }

    IEnumerator VhsRoutine()
    {
        st = St.Rewind;
        ClearWarnLine();
        health.invulnerable = true;
        rb.SetVel(Vector2.zero);
        moveDir = Vector2.zero;

        VhsActive = true;
        VhsCountdown = 1f;
        Time.timeScale = 0f;
        Sfx.Play("vhs_stop");

        for (int i = 1; i <= 3; i++)
        {
            VhsCountdown = i;
            Sfx.PlayAt(Vector2.zero, "vhs_beep", 0.9f, 0.95f + i * 0.12f);
            Sfx.PlayAt(Vector2.zero, "tick", 0.8f, 0.9f + i * 0.15f);   // the clock is the
            yield return new WaitForSecondsRealtime(0.5f);               // only thing moving
            Sfx.PlayAt(Vector2.zero, "tick", 0.55f, 0.95f + i * 0.15f);
            yield return new WaitForSecondsRealtime(0.5f);
        }

        Sfx.Play("vhs_stop");
        Time.timeScale = 1f;
        VhsActive = false;
        VhsCountdown = 0f;

        // the picture comes back and you are standing where the attack lands
        var p = PlayerController.Instance;
        if (p != null && !p.Health.IsDead)
        {
            Vector2 here = p.transform.position;
            WarnStrike(here);
            yield return new WaitForSeconds(0.35f);   // the window: dash or eat it

            ClearWarnStrike();
            Fx.Ring(here, BloodRed, 0.4f, 5f, 0.25f, 42);
            Fx.Burst(here, BloodRed, 26, 9f);
            Sfx.PlayAt(here, "blade_slash", 1f, 0.8f);
            GameManager.Shake(0.6f);
            if (p != null && !p.Health.IsDead && Vector2.Distance(p.transform.position, here) < 2.4f)
                p.Health.TakeDamage(36f * dmgScale, ((Vector2)p.transform.position - here).normalized);
        }

        health.invulnerable = false;
        st = St.Chase;
        attackCd = 0.8f;
    }

    Transform strikeRing, strikeMark, strikeMark2;

    void WarnStrike(Vector2 at)
    {
        var g = new GameObject("VhsStrike");
        g.transform.position = at;
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(1f, 0.25f, 0.2f, 0.3f);
        sr.sortingOrder = 28;
        g.transform.localScale = Vector3.one * 4.8f;
        strikeRing = g.transform;

        var m = new GameObject("VhsMark");
        m.transform.position = at;
        var msr = m.AddComponent<SpriteRenderer>();
        msr.sprite = SpriteFactory.Square;
        msr.color = new Color(1f, 0.3f, 0.25f, 0.85f);
        msr.sortingOrder = 28;
        m.transform.localScale = new Vector3(0.18f, 4.8f, 1f);
        strikeMark = m.transform;

        var m2 = new GameObject("VhsMark");
        m2.transform.position = at;
        var msr2 = m2.AddComponent<SpriteRenderer>();
        msr2.sprite = SpriteFactory.Square;
        msr2.color = new Color(1f, 0.3f, 0.25f, 0.85f);
        msr2.sortingOrder = 28;
        m2.transform.localScale = new Vector3(4.8f, 0.18f, 1f);
        strikeMark2 = m2.transform;
        Sfx.PlayAt(at, "vhs_beep", 0.9f, 1.3f);
    }

    void ClearWarnStrike()
    {
        if (strikeRing != null) Destroy(strikeRing.gameObject);
        if (strikeMark != null) Destroy(strikeMark.gameObject);
        if (strikeMark2 != null) Destroy(strikeMark2.gameObject);
    }

    // the sniper's tell: one bright line from the muzzle to wherever you stand
    void MakeWarnLine(Vector2 pos, Vector2 dir)
    {
        var g = new GameObject("SniperWarn");
        g.transform.SetParent(transform, false);
        warnLine = g.AddComponent<SpriteRenderer>();
        warnLine.sprite = SpriteFactory.Square;
        warnLine.color = new Color(1f, 0.55f, 0.2f, 0.35f);
        warnLine.sortingOrder = 24;
        AimWarnLine(pos, dir);
    }

    void AimWarnLine(Vector2 pos, Vector2 dir)
    {
        if (warnLine == null) return;
        warnLine.transform.position = pos + dir * 14f;
        warnLine.transform.localScale = new Vector3(28f, 0.07f, 1f);
        warnLine.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    void ClearWarnLine()
    {
        if (warnLine != null) Destroy(warnLine.gameObject);
    }

    void Tint(bool on)
    {
        if (bodySr != null) bodySr.color = on ? Color.white : bodyOrig;
    }

    // ------------------------------------------------------------ contact
    void OnCollisionStay2D(Collision2D col)
    {
        if (GameManager.IsOver || health == null || health.IsDead) return;
        var p = PlayerController.Instance;
        if (p == null || p.Health == null || p.Health.IsDead) return;
        if (!col.gameObject.CompareTag("Player")) return;

        contactT -= Time.deltaTime;
        if (contactT > 0f) return;
        contactT = 0.5f;

        Vector2 away = ((Vector2)p.transform.position - (Vector2)transform.position).normalized;
        p.Health.TakeDamage((dashT > 0f ? 16f : 8f) * dmgScale, away);
        if (dashT > 0f) p.Shove(away, 6f);
    }

    // ------------------------------------------------------------ death edge
    // he is not supposed to die here - the leaps fire first - but if the
    // math ever lets the bar reach zero, the run still ends well
    void Die(Health h)
    {
        Vector2 pos = transform.position;
        if (visual != null) Destroy(visual.gameObject);
        ClearWarnLine();

        Gore.Burst(pos, h.bloodColor, new Color(0.85f, 0.55f, 0.45f), 16, 2f);
        Fx.Explosion(pos, BloodRed, Color.white, 1.8f);
        Fx.Ring(pos, BloodRed, 0.5f, 8f, 0.7f, 30);
        Fx.Sparks(pos, Color.white, 40, 20f);
        GameManager.Shake(1f);
        GameManager.Kills++;
        Sfx.PlayAt(pos, "herald_die", 1f);

        Active = null;
        GameManager.Win("YOU WIN");
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    // ------------------------------------------------------------ hud
    // the bottom bar, red as your own - and while the tape is stopped the
    // screen wears the pause button and the static of a rewind
    void OnGUI()
    {
        if (VhsActive) { DrawVhs(); return; }
        if (GameManager.IsOver || health == null || health.IsDead) return;

        if (nameStyle == null)
            nameStyle = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(), fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };

        float w = Mathf.Min(820f, Screen.width * 0.66f);
        float x = (Screen.width - w) * 0.5f, y = Screen.height - 86f;

        GameManager.GuiBar(new Rect(x, y, w, 38f),
                           phase == 2 ? Purple : BloodRed,
                           health.current / health.max);

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(x + 2f, y - 34f, w, 30f), "SHEPARD", nameStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y - 36f, w, 30f), "SHEPARD", nameStyle);
    }

    // the paused tape: a big pause button, rolling static, and the counter
    void DrawVhs()
    {
        if (pauseCircle == null) pauseCircle = MakeCircleTexture();

        float t = Time.unscaledTime;

        // the rest of the picture: rolling bands of static and color
        for (int i = 0; i < 26; i++)
        {
            float y = Mathf.Floor(Random.value * Screen.height / 3f) * 3f;
            float h = Random.Range(2f, 22f);
            float g = Random.Range(0f, 1f);
            GUI.color = new Color(g, g, g, Random.Range(0.06f, 0.3f));
            GUI.DrawTexture(new Rect(0, y, Screen.width, h), Texture2D.whiteTexture);
        }
        for (int i = 0; i < 8; i++)                    // the rgb fringe of a worn tape
        {
            float y = Mathf.Floor(Random.value * Screen.height / 4f) * 4f;
            GUI.color = Random.value < 0.5f
                ? new Color(1f, 0.2f, 0.2f, 0.12f) : new Color(0.2f, 0.4f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(Mathf.Sin(t * 40f + i) * 6f, y, Screen.width, Random.Range(2f, 14f)),
                            Texture2D.whiteTexture);
        }
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // the big pause button, dead center
        float s = Mathf.Min(Screen.width, Screen.height) * 0.34f;
        var r = new Rect((Screen.width - s) * 0.5f, (Screen.height - s) * 0.5f, s, s);
        GUI.color = new Color(0.05f, 0.05f, 0.06f, 0.92f);
        GUI.DrawTexture(r, pauseCircle);
        GUI.color = new Color(0.85f, 0.85f, 0.9f, 0.95f);
        GUI.DrawTexture(new Rect(r.x + s * 0.32f, r.y + s * 0.22f, s * 0.12f, s * 0.56f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x + s * 0.56f, r.y + s * 0.22f, s * 0.12f, s * 0.56f), Texture2D.whiteTexture);

        if (style == null)
            style = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };

        // the counter ticks, the tape rewinds, and the picture comes back
        // with you standing in the blast
        GUI.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Sin(t * 18f));
        style.fontSize = 40;
        GUI.Label(new Rect(0, r.yMax + 8f, Screen.width, 60f), "<< REWINDING   " + (int)VhsCountdown, style);
        GUI.color = Color.white;
    }

    static Texture2D MakeCircleTexture()
    {
        int n = 96;
        var tex = new Texture2D(n, n, TextureFormat.ARGB32, false);
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                tex.SetPixel(x, y, d <= c ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        tex.Apply();
        return tex;
    }

    GUIStyle style;
}

// ============================================================ STASIS BULLET
// a slow purple-white orb that turns toward you the way patience does.
// it does not hit hard - it holds you still while he empties the gun
public class StasisBullet : MonoBehaviour
{
    Vector2 dir;
    float speed = 4.2f, turn = 75f, dmg, life = 6f;
    SpriteRenderer sr;

    public static void Summon(Vector2 from, Vector2 dir, float dmg)
    {
        var g = new GameObject("StasisOrb");
        g.transform.position = from;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(0.85f, 0.7f, 1f, 0.95f);
        sr.sortingOrder = 40;
        g.transform.localScale = Vector3.one * 0.42f;

        var halo = new GameObject("Halo");
        halo.transform.SetParent(g.transform, false);
        halo.transform.localScale = Vector3.one * 2.6f;
        var hsr = halo.AddComponent<SpriteRenderer>();
        hsr.sprite = SpriteFactory.Circle;
        hsr.color = new Color(0.7f, 0.45f, 1f, 0.25f);
        hsr.sortingOrder = 39;

        var s = g.AddComponent<StasisBullet>();
        s.dir = dir.normalized;
        s.dmg = dmg;
        s.sr = sr;
    }

    void Update()
    {
        var p = PlayerController.Instance;
        if (p == null || GameManager.IsOver) { Destroy(gameObject); return; }

        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        // slight tracking: it leans toward you the whole way
        Vector2 to = (Vector2)p.transform.position - (Vector2)transform.position;
        float a = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float ta = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        a = Mathf.MoveTowardsAngle(a, ta, turn * Time.deltaTime);
        dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));

        transform.position += (Vector3)(dir * speed * Time.deltaTime);
        if (sr != null)
            sr.color = Color.Lerp(new Color(0.7f, 0.45f, 1f), new Color(1f, 1f, 1f),
                                  0.5f + 0.5f * Mathf.Sin(Time.time * 14f));

        if (Vector2.Distance(transform.position, p.transform.position) < 0.55f)
        {
            // the clunk: the color leaves, your legs forget the idea of moving
            p.ApplyStun(1.5f);
            p.Flush(new Color(0.7f, 0.6f, 0.9f), 0.4f);
            GameManager.DesatT = 1.6f;
            Sfx.PlayAt(transform.position, "clunk", 1f);
            Fx.Ring(transform.position, new Color(0.8f, 0.6f, 1f), 0.3f, 4f, 0.35f, 30);
            if (Shepard.Active != null) Shepard.Active.OnStasisHit();
            Destroy(gameObject);
        }
    }
}

// ============================================================ GHOST PROTOCOL
// wireframe echoes of the three bosses that came before: the smasher's
// slam, the herald's laser, the vessel's beam - cyan ghosts doing their
// one signature attack, once each, in the room where you are standing

public class GhostSlam : MonoBehaviour   // the smasher, still slamming
{
    float t, dmg;
    Vector2 at;
    SpriteRenderer warn, bodySr;
    Transform body;
    bool landed;

    public static void Summon(Vector2 at, float dmg)
    {
        var g = new GameObject("GhostSmasher");
        g.transform.position = at;

        var b = new GameObject("Body");               // he rises over the mark, then comes down on it
        b.transform.SetParent(g.transform, false);
        var bodySr = b.AddComponent<SpriteRenderer>();
        bodySr.sprite = Art.Smasher();
        bodySr.color = new Color(0.45f, 0.9f, 1f, 0.45f);
        bodySr.sortingOrder = 30;
        b.transform.localScale = Vector3.one * 3.4f;

        var w = new GameObject("Warn");               // the danger zone, drawn on the ground
        w.transform.SetParent(g.transform, false);
        var wr = w.AddComponent<SpriteRenderer>();
        wr.sprite = SpriteFactory.Circle;
        wr.color = new Color(0.45f, 0.9f, 1f, 0.28f);
        wr.sortingOrder = 28;
        w.transform.localScale = Vector3.one * 2.4f;

        var s = g.AddComponent<GhostSlam>();
        s.at = at;
        s.dmg = dmg;
        s.warn = wr;
        s.body = b.transform;
        s.bodySr = bodySr;
    }

    void Update()
    {
        t += Time.deltaTime;

        // the mark fills up as he rears back: it is exactly his hit radius
        if (warn != null && t < 0.6f)
        {
            float f = Mathf.Clamp01(t / 0.45f);
            float k = Mathf.Lerp(2.4f, 5.4f, f);
            warn.transform.localScale = new Vector3(k, k, 1f);
            warn.color = new Color(0.45f, 0.9f, 1f, 0.2f + 0.2f * Mathf.Sin(t * 30f));
        }

        if (body != null)
        {
            if (t < 0.45f)                            // the rear-up
            {
                float f = t / 0.45f;
                body.position = new Vector3(at.x, at.y + Mathf.Lerp(1f, 3.6f, f * f), 0f);
                body.localScale = Vector3.one * Mathf.Lerp(3.4f, 3.9f, f);
            }
            else if (t < 0.6f)                        // the drop: straight down, hard
            {
                float f = (t - 0.45f) / 0.15f;
                body.position = new Vector3(at.x, Mathf.Lerp(at.y + 3.6f, at.y + 0.3f, f * f), 0f);
                body.localScale = new Vector3(3.9f * (1f + 0.28f * f), 3.9f * (1f - 0.22f * f), 1f);
            }
            else                                       // the recoil: back up, fading out
            {
                float f = Mathf.Clamp01((t - 0.6f) / 0.7f);
                body.position = new Vector3(at.x, at.y + 0.3f + f * 1.4f, 0f);
                body.localScale = Vector3.one * 3.4f;
                if (bodySr != null)
                    bodySr.color = new Color(0.45f, 0.9f, 1f, 0.45f * (1f - f));
            }
        }

        if (t >= 0.6f && !landed)
        {
            landed = true;
            if (warn != null) warn.color = new Color(0.45f, 0.9f, 1f, 0f);   // the mark is spent
            var p = PlayerController.Instance;
            if (p != null && !p.Health.IsDead && Vector2.Distance(p.transform.position, at) < 2.7f)
                p.Health.TakeDamage(dmg, ((Vector2)p.transform.position - at).normalized * 1.4f);
            Fx.Ring(at, new Color(0.45f, 0.9f, 1f), 0.3f, 6f, 0.3f, 30);
            Fx.Burst(at, new Color(0.45f, 0.9f, 1f), 22, 9f);
            Sfx.PlayAt(at, "wall_smash", 0.9f, 0.8f);
            GameManager.Shake(0.5f);
        }
        if (t >= 1.3f) Destroy(gameObject);
    }
}

public class GhostFlame : MonoBehaviour   // the herald, back as fire
{
    float t, dmg, tickT, aim, warnT;
    bool lit;
    SpriteRenderer cone, ghostSr;
    Transform ghost;

    public static void Summon(float dmg)
    {
        var p = PlayerController.Instance;
        bool left = Random.value < 0.5f;
        Vector2 at = new Vector2(left ? -GameManager.ArenaHalf.x + 2.4f : GameManager.ArenaHalf.x - 2.4f,
                                  Random.Range(-2.6f, 2.6f));
        Vector2 toP = p != null ? (Vector2)p.transform.position - at : Vector2.right;

        var g = new GameObject("GhostHeraldFire");
        g.transform.position = at;

        g.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(toP.y, toP.x) * Mathf.Rad2Deg);

        // the herald himself, big and lit - a ghost has to read from clear
        // across the ring or it is not in the game at all
        var h = new GameObject("Herald");
        h.transform.SetParent(g.transform, false);
        var gr = h.AddComponent<SpriteRenderer>();
        gr.sprite = Art.Herald();
        gr.color = new Color(1f, 0.88f, 0.62f, 0.85f);
        gr.sortingOrder = 45;                          // over the fight, never behind it
        h.transform.localScale = Vector3.one * 3.2f;
        h.transform.localPosition = new Vector3(0f, 0.55f, 0f);

        // the glow: an apparition reads as one before your eye even finds the
        // sprite, on a dark floor, at any tint
        var halo = new GameObject("Halo");
        halo.transform.SetParent(g.transform, false);
        var hr = halo.AddComponent<SpriteRenderer>();
        hr.sprite = SpriteFactory.Circle;
        hr.color = new Color(1f, 0.55f, 0.15f, 0.20f);
        hr.sortingOrder = 44;
        halo.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        halo.transform.localScale = new Vector3(5.2f, 3.8f, 1f);

        var c = new GameObject("Cone");
        c.transform.SetParent(g.transform, false);
        var cs = c.AddComponent<SpriteRenderer>();
        cs.sprite = SpriteFactory.Circle;
        cs.color = new Color(1f, 0.45f, 0.1f, 0.16f);
        cs.sortingOrder = 19;                          // over the floor, under the units

        var s = g.AddComponent<GhostFlame>();
        s.ghost = g.transform;
        s.ghostSr = gr;
        s.cone = cs;
        s.dmg = dmg;
        s.aim = Mathf.Atan2(toP.y, toP.x) * Mathf.Rad2Deg;
        s.warnT = 0.65f;                               // the only warning: the cone filling in
        Fx.Ring(at, new Color(1f, 0.6f, 0.2f), 0.3f, 5f, 0.35f, 30);   // he announces himself
        Sfx.PlayAt(at, "fire_cackle", 0.65f, 0.85f);
    }

    void Update()
    {
        t += Time.deltaTime;

        var p = PlayerController.Instance;
        Vector2 origin = ghost != null ? (Vector2)ghost.position : Vector2.zero;

        if (ghostSr != null)
            ghostSr.color = new Color(1f, 0.88f, 0.62f, 0.72f + 0.18f * Mathf.Sin(Time.time * 6f));

        if (t < warnT)                                 // the cone fills in, faint
        {
            float k = t / warnT;
            if (cone != null)
            {
                // world space: the root is turned toward you, so local maths
                // would swing the warning out at double the angle - straight
                // into the wall behind him, where nobody could see it
                Vector2 fw = new Vector2(Mathf.Cos(aim * Mathf.Deg2Rad), Mathf.Sin(aim * Mathf.Deg2Rad));
                cone.transform.position = (Vector3)(origin + fw * (7.5f * k));
                cone.transform.rotation = Quaternion.Euler(0, 0, aim);
                cone.transform.localScale = new Vector3(15f * k, 9f * k, 1f);
                cone.color = new Color(1f, 0.45f, 0.1f, 0.14f + 0.1f * Mathf.Sin(t * 26f));
            }
            return;
        }

        if (!lit)                                     // the whole cone catches
        {
            lit = true;
            Sfx.PlayAt(origin, "flame", 0.8f);
            Fx.Burst(origin, new Color(1f, 0.55f, 0.12f), 14, 8f);
            Vent.Burst();
            tickT = 0f;
        }

        // a burning fan sweeps the room, exactly as the living herald's did
        float prog = Mathf.Clamp01((t - warnT) / 1.1f);
        float a = Mathf.Lerp(aim - 60f, aim + 60f, prog);
        Vector2 fd = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));

        if (cone != null)
        {
            cone.transform.position = (Vector3)(origin + fd * 7.5f);
            cone.transform.rotation = Quaternion.Euler(0, 0, a);
            cone.transform.localScale = new Vector3(15f * (0.94f + 0.08f * Mathf.Sin(Time.time * 17f)), 9f, 1f);
            cone.color = new Color(1f, 0.45f, 0.1f,
                0.5f + 0.18f * Mathf.Sin(Time.time * 13f) + 0.1f * Mathf.Sin(Time.time * 29f));
        }

        Fx.Burst(origin + fd * 4f, new Color(1f, 0.55f, 0.12f), 3, 3f);
        tickT -= Time.deltaTime;
        if (tickT <= 0f)
        {
            tickT = 0.14f;
            Fx.Burst(origin + fd * Random.Range(2f, 7f), new Color(1f, 0.65f, 0.2f), 2, 2.5f);
            FluidPool.Spawn(origin + fd * Random.Range(1.5f, 7.5f), FluidKind.Fire, Random.Range(0.65f, 1f));
            Sfx.PlayAt(origin, "fire_cackle", 0.3f, 1.05f);

            // caught in the fan: the same burn, from across the room
            if (p != null && !p.Health.IsDead)
            {
                Vector2 to = (Vector2)p.transform.position - origin;
                if (to.magnitude < 16f && Vector2.Angle(fd, to.normalized) < 40f)
                    p.Health.TakeDamage(dmg, to.normalized);
            }
        }

        if (t >= warnT + 1.15f) Destroy(gameObject);
    }
}

public class GhostZeus : MonoBehaviour    // the herald, calling the sky down
{
    float t;
    SpriteRenderer ghostSr;
    int cast;

    public static void Summon()
    {
        Vector2 at = new Vector2(0f, GameManager.ArenaHalf.y * 0.5f);   // on the floor, in the frame
        var g = new GameObject("GhostHeraldZeus");
        g.transform.position = at;

        // the herald himself, big and lit over his own glow - summoned into
        // the open where you cannot possibly miss him
        var h = new GameObject("Herald");
        h.transform.SetParent(g.transform, false);
        var gr = h.AddComponent<SpriteRenderer>();
        gr.sprite = Art.Herald();
        gr.color = new Color(0.8f, 0.95f, 1f, 0.85f);
        gr.sortingOrder = 45;                          // over the fight, never behind it
        h.transform.localScale = Vector3.one * 3.4f;
        h.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        var halo = new GameObject("Halo");
        halo.transform.SetParent(g.transform, false);
        var hr = halo.AddComponent<SpriteRenderer>();
        hr.sprite = SpriteFactory.Circle;
        hr.color = new Color(0.55f, 0.85f, 1f, 0.20f);
        hr.sortingOrder = 44;
        halo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        halo.transform.localScale = new Vector3(5.4f, 4f, 1f);

        var s = g.AddComponent<GhostZeus>();
        s.ghostSr = gr;

        Fx.Ring(at, new Color(0.6f, 0.9f, 1f), 0.3f, 5f, 0.35f, 30);   // he announces himself
        Sfx.PlayAt(at, "elec_crackle", 0.65f, 0.9f);
    }

    void Update()
    {
        t += Time.deltaTime;

        if (ghostSr != null)
            ghostSr.color = new Color(0.8f, 0.95f, 1f, 0.72f + 0.18f * Mathf.Sin(Time.time * 9f));

        // the sky takes the shots for him - where you stand, not where you go
        var p = PlayerController.Instance;
        if (cast < 3 && t > 0.35f + cast * 0.55f)
        {
            Vector2 at = p != null && !p.Health.IsDead
                ? (Vector2)p.transform.position + Random.insideUnitCircle * 1.3f
                : Vector2.zero;
            ZeusBolt.Summon(at);
            cast++;
            Sfx.PlayAt(at, "zeus", 0.5f, 1.15f + cast * 0.08f);
        }

        if (t >= 1.9f) Destroy(gameObject);
    }
}

public class GhostBeam : MonoBehaviour    // the vessel, still pouring rust
{
    float t, dmg, tickT;
    SpriteRenderer beam, ghostSr;
    float angle;

    public static void Summon(float dmg)
    {
        var g = new GameObject("GhostVessel");
        g.transform.position = Vector2.zero;

        var gr = g.AddComponent<SpriteRenderer>();
        gr.sprite = Art.VesselBody();
        gr.color = new Color(0.9f, 0.6f, 0.3f, 0.42f);
        gr.sortingOrder = 30;
        g.transform.localScale = Vector3.one * 3.2f;

        var l = new GameObject("Beam");
        l.transform.SetParent(g.transform, false);
        var lr = l.AddComponent<SpriteRenderer>();
        lr.sprite = SpriteFactory.Square;
        lr.color = new Color(0.9f, 0.62f, 0.25f, 0.7f);
        lr.sortingOrder = 29;

        var s = g.AddComponent<GhostBeam>();
        s.beam = lr;
        s.ghostSr = gr;
        s.dmg = dmg;
        s.angle = -60f;
        s.tickT = 0.3f;                     // the pour starts a beat after the pose
        Sfx.PlayAt(Vector2.zero, "rust_beam", 0.55f, 0.9f);
    }

    void Update()
    {
        t += Time.deltaTime;
        float span = Mathf.Min(1f, t / 1.4f);
        float a = Mathf.Lerp(-60f, 240f, span);
        angle = a;

        // the vessel pours along its own beam: it leans into the arc and
        // shudders the whole way, the way something heavy does when it is
        // letting something worse out
        transform.position = new Vector3(Mathf.Sin(Time.time * 44f) * 0.05f,
                                         Mathf.Sin(Time.time * 2.1f) * 0.28f, 0f);

        Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
        transform.rotation = Quaternion.Euler(0, 0, -dir.x * 45f);   // tips toward the arc

        if (ghostSr != null)
            ghostSr.color = new Color(0.9f, 0.6f, 0.3f, 0.36f + 0.1f * Mathf.Sin(Time.time * 5f));

        if (beam != null)
        {
            float grow = Mathf.Clamp01(t / 0.3f);   // it opens up, then sweeps
            beam.transform.position = (Vector2)transform.position + dir * 14f * grow;
            beam.transform.localScale = new Vector3(28f * grow, 0.5f, 1f);
            beam.transform.rotation = Quaternion.Euler(0, 0, a);
        }

        tickT -= Time.deltaTime;
        if (tickT <= 0f)
        {
            tickT = 0.25f;
            var p = PlayerController.Instance;
            if (p == null || p.Health.IsDead) return;
            Vector2 to = (Vector2)p.transform.position - (Vector2)transform.position;
            float along = Vector2.Dot(to, dir);
            if (along > 0f && along < 30f)
            {
                float perp = Mathf.Abs(to.x * dir.y - to.y * dir.x);
                if (perp < 0.6f)
                    p.Health.TakeDamage(dmg, dir);
            }
        }

        if (t >= 1.6f) Destroy(gameObject);
    }
}

// ============================================================ THE MIRROR
// at thirty percent the run is stripped to the bone: this is you, with
// your gun, your dash and your damage - and only one of you walks out
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Health))]
public class MirrorFighter : MonoBehaviour
{
    public static MirrorFighter Active;
    public static bool BossAlive { get { return Active != null; } }

    Rigidbody2D rb;
    Health health;
    Transform visual;
    HumanoidAnim anim;
    SpriteRenderer bodySr;
    Vector2 moveDir, dashDir;
    float dashT, dashCd, burstT, burstShots, contactT, shootCd = 1.15f, dmg = 10f;

    // test tooling: the mirror steps back through the glass
    public static void DevDismiss()
    {
        var m = Active;
        Active = null;
        if (m != null) Destroy(m.gameObject);
    }

    public static MirrorFighter Spawn(float difficulty)
    {
        var p = PlayerController.Instance;
        Vector2 pos = p != null ? (Vector2)p.transform.position + Random.insideUnitCircle.normalized * 6f : Vector2.right * 5f;
        pos.x = Mathf.Clamp(pos.x, -GameManager.ArenaHalf.x + 2f, GameManager.ArenaHalf.x - 2f);
        pos.y = Mathf.Clamp(pos.y, -GameManager.ArenaHalf.y + 2f, GameManager.ArenaHalf.y - 2f);
        if (Pit.Inside(pos)) pos = Vector2.right * 4f;

        var g = new GameObject("Mirror");
        g.transform.position = pos;
        var m = g.AddComponent<MirrorFighter>();
        m.Setup(difficulty, pos);
        return m;
    }

    void Setup(float difficulty, Vector2 pos)
    {
        Active = this;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        GetComponent<CircleCollider2D>().radius = 0.3f;

        // your gun, your damage: the mirror never learned any mods
        var p = PlayerController.Instance;
        if (p != null) dmg = p.BaseDamage;

        health = GetComponent<Health>();
        health.OnDeath += Die;
        health.Init(240f * GameManager.HpScale(difficulty));
        health.showBar = true;
        health.barWidth = 1.1f;
        health.barOffsetY = 1.1f;
        health.barColor = new Color(0.95f, 0.35f, 0.3f);
        health.bloodColor = new Color(0.85f, 0.3f, 0.25f);

        visual = Humanoid.Build(transform, Art.Player(), 1.15f, 25);
        anim = visual.GetComponent<HumanoidAnim>();
        bodySr = visual.GetComponentInChildren<SpriteRenderer>();
        if (bodySr != null)
            bodySr.color = Color.Lerp(bodySr.color, new Color(0.95f, 0.3f, 0.25f), 0.3f);   // you, but red

        if (TimeLeap.Instance != null)
            TimeLeap.Instance.Announce("1V1 - THE MIRROR", 2.6f);
        Sfx.PlayAt(pos, "rewind", 0.8f, 1.2f);
        Fx.Ring(pos, new Color(0.95f, 0.35f, 0.3f), 0.4f, 5f, 0.5f, 30);
        Fx.Burst(pos, new Color(0.95f, 0.35f, 0.3f), 18, 7f);
    }

    void Update()
    {
        if (GameManager.IsOver) { rb.SetVel(Vector2.zero); return; }
        var p = PlayerController.Instance;
        if (p == null || health == null || health.IsDead) return;

        // halfway down, the mirror stops holding back: full heavy
        if (health.current <= health.max * 0.5f) Music.SetIntensity(3);

        float dt = Time.deltaTime;
        Vector2 pos = transform.position;
        Vector2 to = (Vector2)p.transform.position - pos;
        float dist = to.magnitude;
        Vector2 dir = to / Mathf.Max(dist, 0.001f);

        if (anim != null)
        {
            anim.SetFacing(moveDir.sqrMagnitude > 0.05f ? moveDir : dir);
            anim.SetAim(dir);
        }

        if (dashT > 0f) dashT -= dt;

        // it plays like you: strafe at range, dash out of the knife range
        if (dashT > 0f) moveDir = dashDir;
        else if (dist > 6f) moveDir = dir;
        else if (dist < 3.2f)
        {
            dashCd -= dt;
            if (dashCd <= 0f)
            {
                dashCd = 1.3f;
                dashT = 0.18f;
                dashDir = (-dir + Vector2.Perpendicular(dir) * (Random.value < 0.5f ? 1f : -1f)).normalized;
                Sfx.PlayAt(pos, "dash", 0.45f);
            }
            else moveDir = Vector2.Perpendicular(dir) * 0.7f;
        }
        else moveDir = Vector2.Perpendicular(dir) * 0.65f;

        // and it shoots your gun: three-round bursts at your own damage
        if (burstShots > 0f)
        {
            burstT -= dt;
            if (burstT <= 0f)
            {
                burstShots--;
                burstT = 0.12f;
                Bullet.Spawn(pos + dir * 0.5f, dir, 16f, dmg, false, new Color(1f, 0.6f, 0.55f));
                Sfx.PlayAt(pos, "shepard_rifle", 0.6f, 1.15f);
            }
        }
        else
        {
            shootCd -= dt;
            if (shootCd <= 0f)
            {
                shootCd = 1.15f;
                burstShots = 3;
                burstT = 0f;
            }
        }
    }

    void FixedUpdate()
    {
        if (GameManager.IsOver) { rb.SetVel(Vector2.zero); return; }
        Vector2 v = moveDir * (dashT > 0f ? 20f : 6f);
        Vector2 p0 = rb.position;
        if (Pit.Blocks(p0, p0 + v * Time.fixedDeltaTime)) v = Vector2.zero;
        rb.SetVel(v);
    }

    void OnCollisionStay2D(Collision2D col)
    {
        if (GameManager.IsOver || health == null || health.IsDead) return;
        var p = PlayerController.Instance;
        if (p == null || p.Health == null || p.Health.IsDead) return;
        if (!col.gameObject.CompareTag("Player")) return;

        contactT -= Time.deltaTime;
        if (contactT > 0f) return;
        contactT = 0.5f;
        Vector2 away = ((Vector2)p.transform.position - (Vector2)transform.position).normalized;
        p.Health.TakeDamage(6f, away);
    }

    void Die(Health h)
    {
        Vector2 pos = transform.position;
        if (visual != null) Destroy(visual.gameObject);

        Gore.Burst(pos, h.bloodColor, new Color(0.85f, 0.55f, 0.45f), 16, 2f);
        Fx.Explosion(pos, new Color(0.95f, 0.3f, 0.3f), Color.white, 1.7f);
        Fx.Ring(pos, new Color(0.95f, 0.35f, 0.3f), 0.5f, 8f, 0.7f, 30);
        Fx.Sparks(pos, Color.white, 36, 18f);
        GameManager.Shake(0.9f);
        GameManager.Kills++;
        Sfx.PlayAt(pos, "herald_die", 1f, 1.15f);

        Active = null;
        GameManager.Win("YOU WIN");
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
    }
}

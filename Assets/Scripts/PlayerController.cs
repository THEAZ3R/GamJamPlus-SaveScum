using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Health))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;

    [Header("Shoot")]
    public float fireRate = 0.15f;
    public float bulletDamage = 10f;
    public float bulletSpeed = 16f;

    [Header("Dash")]
    public float dashSpeed = 22f;
    public float dashDuration = 0.17f;      // reach = 3.7u, enough to clear a hole
    public float dashCooldown = 0.75f;

    public const int GrabBondsNeeded = 5;

    public static PlayerController Instance;
    public Health Health { get; private set; }
    public Vector2 Velocity { get { return rb != null ? rb.linearVelocity : Vector2.zero; } }   // what shepard leads by
    public float BaseDamage { get { return bulletDamage; } }   // the mirror shoots exactly this
    public float DashReady { get { return 1f - Mathf.Clamp01(dashCdTimer / dashCooldown); } }

    public bool IsGrabbed { get; private set; }
    public int GrabBonds { get; private set; }
    public Enemy GrabberEnemy { get; private set; }
    public bool IsStunned { get { return stunTimer > 0f; } }
    public static bool GodMode;                        // G: testing without the bruises

    public bool IsSlowed { get { return slowTimer > 0f; } }
    public bool IsCorroded { get { return acidTimer > 0f; } }   // vomit: slower, weaker

    // cyber-mods (null-safe: the table component can be missing in tests)
    static PlayerUpgrades Upg { get { return PlayerUpgrades.Instance; } }
    float UpgSpeed { get { var u = Upg; return u != null ? u.SpeedMult : 1f; } }

    Rigidbody2D rb;
    Transform visual;
    HumanoidAnim anim;
    SpriteRenderer bodySr;
    LineRenderer aimLine;
    Camera cam;

    public Vector2 CutsceneVel { get; set; }           // the script drives the body

    Vector2 moveInput, aimDir = Vector2.right, dashDir, lastTrailPos, lastStepPos;
    public static int Tick;                          // F6: did Update reach the aim code this frame
    public static int TickFrame;                     // ...and on which frame was that
    public Vector2 AimDir { get { return aimDir; } } // ...and where it is pointing
    float stepDist, boostSfxT;
    float fireTimer, dashTimer, dashCdTimer, stunTimer, slowTimer, elecTimer, acidTimer, iFrameBlink, trailDist;
    bool dashing, trailInit;
    float bodyMul = 1f;                           // body size from vitagraft / celerity
    float bodyTarget = 1f;                        // ...eased toward here, so growth is felt
    float tpGuard;                                // chrono bubble: nothing can touch you
    float flushT;                                 // astarion: red flush that fades out
    Color flushColor = Color.white;
    Vector2 shove;                                // quake / knock shoves that decay
    float leechAuraT;                             // astarion: ki burn while drinking
    SpriteRenderer[] kiFlames;                    // the red super-saiyan spikes
    CircleCollider2D bodyCol;
    GameObject guardFx;                           // the purple bubble ring
    SpriteRenderer guardSr;

    const float TrailGap = 0.55f;                 // acid wake: one puddle per X travelled
    const float BaseRadius = 0.32f;               // hitbox, scaled by VITAGRAFT / CELERITY

    // status vfx: crackling arcs while stunned, sticky goo while slowed
    GameObject stunFx, slowFx;
    SpriteRenderer[] stunZaps;
    float zapT, gooT;

    void Awake()
    {
        Instance = this;
        gameObject.tag = "Player";
        cam = Camera.main;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        bodyCol = GetComponent<CircleCollider2D>();
        bodyCol.radius = BaseRadius;

        Health = GetComponent<Health>();
        Health.Init(100f);
        Health.hitInvulnTime = 0.7f;          // i-frames after every hit
        Health.bloodColor = new Color(0.85f, 0.15f, 0.15f);
        Health.OnDeath += OnDeath;

        // XP / levels / cyber-mods live on the player
        if (GetComponent<PlayerUpgrades>() == null) gameObject.AddComponent<PlayerUpgrades>();

        visual = Humanoid.Build(transform, Art.Player(), 1.15f, 25);
        anim = visual.GetComponent<HumanoidAnim>();
        bodySr = visual.GetComponentInChildren<SpriteRenderer>();

        var lg = new GameObject("AimLine");
        lg.transform.SetParent(transform, false);
        aimLine = lg.AddComponent<LineRenderer>();
        aimLine.positionCount = 2;
        aimLine.startWidth = aimLine.endWidth = 0.045f;
        aimLine.material = bodySr.sharedMaterial;
        aimLine.startColor = new Color(1f, 0.55f, 0.10f, 0.60f);   // neon orange: reads over blood
        aimLine.endColor = new Color(1f, 0.30f, 0.45f, 0.05f);     // ...fading toward pink
        aimLine.sortingOrder = 5;
        aimLine.useWorldSpace = true;
    }

    // ------------------------------------------------------------ cutscene hooks
    // a cutscene walks you to your mark and turns your face
    public void CutscenePlace(Vector2 p)
    {
        rb.position = p;
        transform.position = p;
        rb.SetVel(Vector2.zero);
        lastStepPos = p;
        lastTrailPos = p;
    }

    public void CutsceneFace(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        aimDir = dir.normalized;
        if (anim != null)
        {
            anim.SetFacing(aimDir);
            anim.SetAim(aimDir);
        }
    }

    void Update()
    {
        if (Cutscenes.Running || GameMenu.Open)        // the cutscene / the menu has the body
        {
            moveInput = Vector2.zero;
            if (aimLine != null) aimLine.enabled = false;
            return;
        }
        if (GameManager.IsOver && !GameManager.EndPrompt)   // the T-prompt still lets you walk
        {
            aimLine.enabled = false;
            return;
        }
        if (LevelUpUI.IsOpen)                          // the table has the world
        {
            moveInput = Vector2.zero;
            return;
        }
        if (GameManager.IntroT > 0f)                   // five seconds of reading first
        {
            moveInput = Vector2.zero;
            if (aimLine != null) aimLine.enabled = false;
            return;
        }

        float dt = Time.deltaTime;

        if (stunTimer > 0f) stunTimer -= dt;
        if (slowTimer > 0f) slowTimer -= dt;
        if (acidTimer > 0f) acidTimer -= dt;           // the corrosion dries off

        // ---- input
        moveInput = Bindings.Move();
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

        // ELECTRIFIED: for a second your legs forget themselves - the stick
        // cuts out at random and the floor answers with sparks
        if (elecTimer > 0f)
        {
            elecTimer -= dt;
            if (Random.value < 0.4f)
            {
                moveInput = Vector2.zero;
                if (Random.value < 0.25f)
                    Fx.Sparks(transform.position, new Color(0.7f, 0.95f, 1f), 2, 3f);
            }
        }

        // footsteps: soft ticks on steel, wet squelches over oil and blood
        Vector2 here = transform.position;
        stepDist += Vector2.Distance(here, lastStepPos);
        lastStepPos = here;
        if (!dashing && !IsStunned && moveInput.sqrMagnitude > 0.1f && stepDist > 0.55f)
        {
            stepDist = 0f;
            if (FluidPool.WetAt(here))
                Sfx.PlayAt(here, "step_wet", 0.5f, Random.Range(0.9f, 1.12f));
            else
                Sfx.PlayAt(here, "step", 0.32f, Random.Range(0.9f, 1.12f));
        }

        Tick++;
        TickFrame = Time.frameCount;
        if (cam == null) cam = Camera.main;          // never aim through a dead camera reference
        Aim.Tick(Screen.width, Screen.height);
        Vector2 mouse = Aim.World(cam);
        Vector2 toMouse = mouse - (Vector2)transform.position;
        if (toMouse.sqrMagnitude > 0.01f) aimDir = toMouse.normalized;
        if (!dashing && anim != null)
        {
            anim.SetFacing(aimDir);                // stand and look, never spin
            anim.SetAim(aimDir);                   // the gun is its own orbiting sprite
        }

        // ---- dash (works while tied up: that is how you break free)
        dashCdTimer -= dt;
        bool dashPressed = Bindings.Down(Bindings.Action.Dash);
        if (!dashing && !IsStunned && dashCdTimer <= 0f && dashPressed)
            StartDash();

        if (dashing)
        {
            dashTimer -= dt;
            if (dashTimer <= 0f) EndDash();
        }

        // ---- shoot
        fireTimer -= dt;
        if (!dashing && !IsStunned && !IsGrabbed && !GameManager.EndPrompt &&
            Input.GetMouseButton(0) && fireTimer <= 0f)
            Shoot();

        UpdateAimLine();
        UpdateIFrameBlink(dt);
        UpdateStatusFx(dt);
        UpdateBodyScale(dt);
        UpdateGuard(dt);
        UpdateFlush(dt);
        UpdateLeechAura(dt);
        EmitGassyTrail();
    }

    // ------------------------------------------------------------ body size
    // VITAGRAFT / CELERITY: the suit (and the hitbox inside it) grows or
    // shrinks. The new size eases in over a beat instead of snapping, with
    // rings drawn at the old and the new size, so +5% reads as growth.
    public void SetBodyScale(float mul, Color tint)
    {
        if (Mathf.Abs(mul - bodyTarget) < 0.001f) return;

        Fx.Ring(transform.position, tint, 1.44f * bodyMul, 1.44f * mul, 0.5f, 29);
        Fx.Burst(transform.position, tint, 12, 5f);
        if (anim != null) anim.Pop(0.22f);
        bodyTarget = mul;
    }

    void UpdateBodyScale(float dt)
    {
        if (Mathf.Abs(bodyMul - bodyTarget) < 0.0005f) return;

        bodyMul = Mathf.Lerp(bodyMul, bodyTarget, 1f - Mathf.Exp(-8f * dt));
        if (Mathf.Abs(bodyMul - bodyTarget) < 0.0005f) bodyMul = bodyTarget;
        if (anim != null) anim.sizeMul = bodyMul;
        if (bodyCol != null) bodyCol.radius = BaseRadius * bodyMul;
    }

    // ------------------------------------------------------------ chrono bubble
    // level 2+ of the portal gun: a few seconds after the blink nothing
    // damages you, stuns you, grabs you, or stands in your way
    void UpdateGuard(float dt)
    {
        if (tpGuard <= 0f) return;

        tpGuard -= dt;
        Health.invulnerable = true;               // nothing lands while it holds
        SetGhost(true);                           // enemy bodies stop blocking you

        if (guardFx != null)
        {
            float t = Mathf.Clamp01(tpGuard / 3f);
            float w = Mathf.Sin(Time.time * 6f);
            guardFx.transform.localScale = Vector3.one * ((1.7f + w * 0.16f) * bodyMul);
            guardSr.color = new Color(0.80f, 0.46f, 1f, (0.35f + 0.25f * w) * (0.45f + 0.55f * t));
        }

        if (tpGuard <= 0f)
        {
            tpGuard = 0f;
            SetGhost(false);
            if (!dashing) Health.invulnerable = false;
            if (guardFx != null) guardFx.SetActive(false);
        }
    }

    void SetGhost(bool on)
    {
        if (bodyCol == null) return;
        for (int i = 0; i < Enemy.All.Count; i++)
        {
            var e = Enemy.All[i];
            if (e == null || e.Body == null) continue;
            Physics2D.IgnoreCollision(bodyCol, e.Body, on);
        }
    }

    void EnsureGuardFx()
    {
        if (guardFx == null)
        {
            guardFx = new GameObject("ChronoBubble");
            guardFx.transform.SetParent(transform, false);
            guardSr = guardFx.AddComponent<SpriteRenderer>();
            guardSr.sprite = Art.AuraRing();
            guardSr.sortingOrder = 28;            // over the suit
        }
        guardFx.SetActive(true);
    }

    // ------------------------------------------------------------ astarion flush
    // the suit turns red for a beat whenever the thirst drinks
    public void Flush(Color c, float time)
    {
        flushColor = c;
        flushT = Mathf.Max(flushT, time);
    }

    void UpdateFlush(float dt)
    {
        if (flushT <= 0f || bodySr == null) return;
        flushT -= dt;
        float a = Mathf.Clamp01(flushT * 3.5f);   // fades over ~0.28s
        bodySr.color = Color.Lerp(Color.white, flushColor, a * 0.75f);
        if (flushT <= 0f) bodySr.color = Color.white;
    }

    // ------------------------------------------------------------ astarion ki
    // the super-saiyan burn: red flame spikes climb off the suit for as long
    // as the thirst keeps drinking
    public void LeechAura()
    {
        leechAuraT = 1.0f;
        EnsureLeechAura();
        for (int i = 0; i < kiFlames.Length; i++)
            if (kiFlames[i] != null) kiFlames[i].gameObject.SetActive(true);
    }

    void EnsureLeechAura()
    {
        if (kiFlames != null) return;
        kiFlames = new SpriteRenderer[8];
        for (int i = 0; i < kiFlames.Length; i++)
        {
            var g = new GameObject("KiFlame");
            g.transform.SetParent(transform, false);
            var r = g.AddComponent<SpriteRenderer>();
            r.sprite = Art.KiFlame();
            r.color = new Color(0.95f, 0.22f, 0.30f);
            r.sortingOrder = 27;                    // over the suit, under the gun
            kiFlames[i] = r;
        }
    }

    void UpdateLeechAura(float dt)
    {
        if (kiFlames == null || leechAuraT <= 0f) return;

        leechAuraT -= dt;
        float ring = 0.40f * bodyMul;
        for (int i = 0; i < kiFlames.Length; i++)
        {
            var f = kiFlames[i];
            if (f == null) continue;

            // every spike rises and dies on its own beat
            float ph = Mathf.Repeat(Time.time * 1.7f + i * 0.31f, 1f);
            float ang = i * (360f / kiFlames.Length) * Mathf.Deg2Rad;
            Vector2 basePos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ring;
            f.transform.localPosition = (Vector3)(basePos + new Vector2(0f, ph * 0.65f));
            float wob = 1f + 0.22f * Mathf.Sin(Time.time * 31f + i * 1.7f);
            f.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 0.35f, ph) * wob;
            f.color = new Color(0.95f, 0.22f, 0.30f, (1f - ph) * 0.95f);
        }

        if (leechAuraT <= 0f)
            for (int i = 0; i < kiFlames.Length; i++)
                if (kiFlames[i] != null) kiFlames[i].gameObject.SetActive(false);
    }

    // THE INCREDIBLE GASSY: you are the acid now - every step you take drops
    // a puddle of the wake you drag behind you, and it lingers for a moment
    void EmitGassyTrail()
    {
        Vector2 p = transform.position;
        if (!trailInit) { trailInit = true; lastTrailPos = p; return; }

        var up = Upg;
        float dps = up != null ? up.SolventDps : 0f;
        if (dps > 0f && !IsGrabbed && !GameManager.IsOver)
        {
            trailDist += Vector2.Distance(lastTrailPos, p);
            while (trailDist >= TrailGap)
            {
                trailDist -= TrailGap;
                CorrosivePool.Wake(p, dps);
            }
        }
        else trailDist = 0f;

        lastTrailPos = p;
    }

    void FixedUpdate()
    {
        if (GameMenu.Open || Cutscenes.Running) { rb.SetVel(CutsceneVel); return; }
        if (GameManager.IsOver && !GameManager.EndPrompt) { rb.SetVel(Vector2.zero); return; }
        if (LevelUpUI.IsOpen) { rb.SetVel(Vector2.zero); return; }
        if (GameManager.EndPrompt)                              // you may walk to the edge
        {
            rb.SetVel(moveInput * moveSpeed * UpgSpeed);
            return;
        }

        Vector2 v;
        if (dashing) v = dashDir * dashSpeed;                 // dash always moves you, even when tied up
        else if (IsStunned || IsGrabbed) v = Vector2.zero;
        else
        {
            Vector2 bdir;
            if (Booster.PushAt(rb.position, out bdir))
            {
                v = bdir * Booster.BoostSpeed;
                boostSfxT -= Time.fixedDeltaTime;
                if (boostSfxT <= 0f) { boostSfxT = 0.35f; Sfx.PlayAt(rb.position, "boost", 0.55f); }
            }
            else v = moveInput * moveSpeed * UpgSpeed * (IsSlowed ? 0.42f : 1f) *
                     (IsCorroded ? 0.5f : 1f) + shove;
        }

        // holes block walking, never a dash
        if (!dashing)
        {
            Vector2 p = rb.position;
            Vector2 next = p + v * Time.fixedDeltaTime;
            if (Pit.Blocks(p, next))
            {
                Vector2 nx = new Vector2(next.x, p.y), ny = new Vector2(p.x, next.y);
                if (!Pit.Blocks(p, nx)) v = new Vector2(v.x, 0f);
                else if (!Pit.Blocks(p, ny)) v = new Vector2(0f, v.y);
                else v = Vector2.zero;
            }
        }

        rb.SetVel(v);
        shove = Vector2.MoveTowards(shove, Vector2.zero, 30f * Time.fixedDeltaTime);   // quakes shove, then fade
    }

    // the SMASHER's quake (and anything else that throws its weight around)
    public void Shove(Vector2 dir, float force) { shove += dir.normalized * force; }

    void LateUpdate()
    {
        Vector2 a = GameManager.ArenaHalf - Vector2.one * 0.45f;
        Vector2 p = rb.position;
        Vector2 c = new Vector2(Mathf.Clamp(p.x, -a.x, a.x), Mathf.Clamp(p.y, -a.y, a.y));
        if (c != p) rb.position = c;

        // never left standing inside a hole (the clamp returns the fixed point;
        // comparing it against the live position is what actually writes it back)
        if (!dashing)
        {
            Vector2 q = Pit.ClampOutOf(rb.position);
            if (q != rb.position) rb.position = q;
        }
    }

    // ------------------------------------------------------------ dash
    void StartDash()
    {
        dashing = true;
        dashTimer = dashDuration;
        dashCdTimer = dashCooldown;
        dashDir = moveInput.sqrMagnitude > 0.1f ? moveInput.normalized : aimDir;
        Health.invulnerable = true;                 // every dash grants i-frames
        Sfx.PlayAt(transform.position, "dash", 0.6f);

        var up = Upg;
        if (up != null) up.OnDash(transform.position);   // CHRONO ANCHOR leaves a spot

        if (IsGrabbed)
        {
            GrabBonds--;
            Fx.Burst(transform.position, new Color(1f, 0.9f, 0.4f), 12, 6f);
            if (GrabBonds <= 0) ReleaseGrab();
            return;
        }

        Fx.Burst(transform.position, new Color(0.3f, 0.9f, 1f), 6, 3f);
    }

    void EndDash()
    {
        dashing = false;
        Health.invulnerable = GameManager.EndPrompt;   // the last walk stays untouchable
    }

    // ------------------------------------------------------------ status
    public void ApplySlow(float duration)
    {
        slowTimer = Mathf.Max(slowTimer, duration);
        EnsureSlowFx();
    }

    // ELECTRIFIED: standing in a current short-circuits the controls
    public void ApplyElec(float duration)
    {
        elecTimer = Mathf.Max(elecTimer, duration);
    }

    // CORRODED: the vessel's vomit - for four seconds the suit eats itself:
    // half speed, half damage
    public void ApplyAcid(float duration)
    {
        acidTimer = Mathf.Max(acidTimer, duration);
    }

    public void ApplyStun(float duration)
    {
        if (tpGuard > 0f) return;                 // the chrono bubble shrugs it off
        stunTimer = Mathf.Max(stunTimer, duration);
        EnsureStunFx();
        Fx.Burst(transform.position, new Color(0.3f, 0.85f, 1f), 10, 4f);
        Fx.Sparks(transform.position, new Color(0.8f, 0.97f, 1f), 10, 11f);
    }

    // four lightning bolts wrapped around the suit while the stun lasts
    void EnsureStunFx()
    {
        if (stunFx == null)
        {
            stunFx = new GameObject("StunFX");
            stunFx.transform.SetParent(transform, false);
            stunZaps = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var z = new GameObject("Zap");
                z.transform.SetParent(stunFx.transform, false);
                float a = i * 90f + 45f;
                z.transform.localPosition = Quaternion.Euler(0, 0, a) * new Vector3(0.42f, 0f, 0f);
                z.transform.localRotation = Quaternion.Euler(0, 0, a);
                z.transform.localScale = Vector3.one * 0.75f;
                var r = z.AddComponent<SpriteRenderer>();
                r.sprite = Art.Zap(i % 3);
                r.color = new Color(0.75f, 0.96f, 1f);
                r.sortingOrder = 30;                // over the suit
                stunZaps[i] = r;
            }
        }
        stunFx.SetActive(true);
    }

    // a translucent coat of green goo over the suit
    void EnsureSlowFx()
    {
        if (slowFx == null)
        {
            slowFx = new GameObject("GooFX");
            slowFx.transform.SetParent(transform, false);
            slowFx.transform.localScale = Vector3.one * 1.2f;
            var r = slowFx.AddComponent<SpriteRenderer>();
            r.sprite = SpriteFactory.Circle;
            r.color = new Color(0.35f, 1f, 0.45f, 0.34f);
            r.sortingOrder = 26;                    // over the suit, tinted green
        }
        slowFx.SetActive(true);
    }

    // flicker/pulse the status vfx; switch them off when the effect ends
    void UpdateStatusFx(float dt)
    {
        if (stunFx != null)
        {
            if (stunTimer > 0f)
            {
                zapT -= dt;
                if (zapT <= 0f)
                {
                    zapT = 0.06f;
                    for (int i = 0; i < stunZaps.Length; i++)
                    {
                        var r = stunZaps[i];
                        r.enabled = Random.value < 0.6f;
                        if (r.enabled && Random.value < 0.4f)
                        {
                            r.sprite = Art.Zap(Random.Range(0, 3));
                            r.transform.localRotation = Quaternion.Euler(0, 0, i * 90f + 45f + Random.Range(-45f, 45f));
                        }
                        r.color = new Color(0.7f, 0.95f, 1f, Random.Range(0.75f, 1f));
                    }
                }
            }
            else if (stunFx.activeSelf) stunFx.SetActive(false);
        }

        if (slowFx != null)
        {
            if (slowTimer > 0f || acidTimer > 0f)      // goo ring: slow AND corroded
            {
                float w = Mathf.Sin(Time.time * 4f);
                slowFx.transform.localScale = Vector3.one * (1.2f * (1f + w * 0.08f));
                slowFx.GetComponent<SpriteRenderer>().color = new Color(0.35f, 1f, 0.45f, 0.30f + 0.10f * w);
                gooT -= dt;
                if (gooT <= 0f)
                {
                    gooT = 0.75f;                   // sticky drips
                    Fx.Burst(transform.position + (Vector3)(Random.insideUnitCircle * 0.35f),
                             new Color(0.45f, 1f, 0.5f), 3, 2.2f);
                }
            }
            else if (slowFx.activeSelf) slowFx.SetActive(false);
        }
    }

    public void OnGrabbed(Enemy grabber)
    {
        if (IsGrabbed || grabber == null) return;
        if (tpGuard > 0f) return;                 // nothing grabs you inside the bubble
        IsGrabbed = true;
        GrabberEnemy = grabber;
        GrabBonds = GrabBondsNeeded;
        GameManager.Shake(0.4f);
        Sfx.PlayAt(transform.position, "grab", 0.7f);
        Fx.Burst(transform.position, new Color(1f, 0.4f, 0.3f), 14, 6f);
    }

    public void ReleaseGrab()
    {
        if (!IsGrabbed) return;
        var g = GrabberEnemy;
        IsGrabbed = false;
        GrabberEnemy = null;
        GrabBonds = 0;
        if (g != null) g.OnReleased();
        Fx.Burst(transform.position, new Color(1f, 0.9f, 0.4f), 18, 7f);
        GameManager.Shake(0.3f);
    }

    // ------------------------------------------------------------ combat
    void Shoot()
    {
        fireTimer = fireRate;

        var up = Upg;
        BulletKind kind = up != null ? up.BulletKind : BulletKind.Normal;
        float dmg = bulletDamage * (up != null ? up.DamageMult * up.PierceBonus * up.ArcBonus : 1f) *
                    (IsCorroded ? 0.5f : 1f);          // the corrosion weakens every shot
        int shots = up != null ? up.Projectiles : 1;        // TRIDENT ARRAY

        // the installed modules paint the rounds: colour and weight change
        // with what you have plugged in
        Color bcol = new Color(1f, 0.9f, 0.35f);
        float bsize = 0.20f;
        if (up != null) up.BulletLook(kind, out bcol, out bsize);

        Vector2 muzzle = (Vector2)transform.position + aimDir * (1.05f * bodyMul);
        for (int i = 0; i < shots; i++)
        {
            float off = (i - (shots - 1) * 0.5f) * 7f;      // even fan around the aim
            Vector2 d = Quaternion.Euler(0, 0, off) * aimDir;
            var b = Bullet.Spawn(muzzle + d * 0.15f, d, bulletSpeed, dmg, true,
                                 bcol, bsize, kind);
            if (kind == BulletKind.Pierce)
            {
                // the slug reads as a lance: long in its flight direction
                b.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                b.transform.localScale = new Vector3(bsize * 2.2f, bsize * 0.8f, 1f);
            }
            if (up != null)
            {
                if (kind == BulletKind.Bounce) b.bounces = up.Bounces;
                b.chainCount = up.Chains;
                b.chainDmg = dmg * 0.6f;
            }
        }

        Humanoid.Kick(visual, 0.55f);                 // rifle recoil
        Sfx.PlayAt(muzzle, kind == BulletKind.Pierce ? "laser_shot" : "pew", 0.55f);
    }

    // CHRONO ANCHOR: a bullet collected the spot you dashed from
    public void TeleportTo(Vector2 pos)
    {
        if (IsGrabbed) ReleaseGrab();
        dashing = false;
        Health.invulnerable = false;
        Sfx.PlayAt(pos, "chrono", 0.5f);
        rb.position = pos;
        transform.position = pos;
        rb.SetVel(Vector2.zero);
        Health.GrantGrace(0.45f);
        Fx.Burst(pos, new Color(0.8f, 0.46f, 1f), 12, 6f);

        // level 2+: the chrono bubble - blink out of whatever had you locked,
        // and for a few seconds nothing can touch, stun, grab or block you
        var up = PlayerUpgrades.Instance;
        float guard = up != null ? Upgrade.ChronoGuard(up.LevelOf(UpgradeKind.Chrono)) : 0f;
        if (guard > 0f)
        {
            stunTimer = 0f;
            tpGuard = guard;
            Health.invulnerable = true;
            EnsureGuardFx();
            Fx.Ring(pos, new Color(0.80f, 0.46f, 1f), 0.5f, 3.2f, 0.5f, 29);
            Fx.Sparks(pos, Color.white, 14, 12f);
        }
    }

    // TIMELEAP: dragged back three seconds - the spot, the wounds, the world
    public void RewindTo(Vector2 pos, float hp)
    {
        if (IsGrabbed) ReleaseGrab();
        dashing = false;
        Health.invulnerable = false;
        pos = Pit.ClampOutOf(pos);
        rb.position = pos;
        transform.position = pos;
        rb.SetVel(Vector2.zero);
        Health.Restore(hp);
        Health.GrantGrace(0.6f);
        stunTimer = 0f;
        slowTimer = 0f;
        elecTimer = 0f;
        acidTimer = 0f;
        shove = Vector2.zero;
        if (tpGuard > 0f) SetGhost(false);             // UpdateGuard bails at 0, so enemy collisions would stay ignored forever
        tpGuard = 0f;
        if (stunFx != null) stunFx.SetActive(false);
        if (slowFx != null) slowFx.SetActive(false);
        if (guardFx != null) guardFx.SetActive(false);
    }

    // THE AIM LINE: one straight run from the muzzle to the exact world spot
    // under the crosshair - the same spot the round flies at. Nothing may cut
    // it short: the old raycast stopped the line at whatever it hit first, so
    // with the horde pressed against you it stubbed out at arm's length while
    // the crosshair sat across the room (a clean 1V1 had nothing to hit, which
    // is why it felt fine there). The line can no longer disagree with where
    // you are pointing, because it ends ON the crosshair.
    void UpdateAimLine()
    {
        aimLine.enabled = !IsStunned && !GameManager.EndPrompt;   // no sight line when you cannot shoot
        Vector2 origin = (Vector2)transform.position;
        Vector2 aimPoint = (Vector2)Aim.World(cam);
        float start = 1.05f * bodyMul;

        if ((aimPoint - origin).magnitude > start + 0.25f)
        {
            aimLine.SetPosition(0, origin + aimDir * start);
            aimLine.SetPosition(1, aimPoint);           // ends on the crosshair itself
        }
        else
        {
            // the crosshair is sitting on top of you: a short stub along the
            // aim, never an inverted line
            aimLine.SetPosition(0, origin + aimDir * start);
            aimLine.SetPosition(1, origin + aimDir * (start + 8f));
        }
    }

    void UpdateIFrameBlink(float dt)
    {
        if (bodySr == null) return;

        if (Health.Grace > 0f || IsStunned)
        {
            iFrameBlink += dt * 26f;
            SetShown(Mathf.Sin(iFrameBlink) > -0.2f);
        }
        else if (!bodySr.enabled)
        {
            SetShown(true);
        }
    }

    // the body and its orbiting weapon blink together
    void SetShown(bool on)
    {
        bodySr.enabled = on;
        if (anim != null && anim.gunSr != null) anim.gunSr.enabled = on;
    }

    void OnDeath(Health h)
    {
        // same as an enemy: the suit comes apart, chunks scatter, stains stay
        Vector2 pos = transform.position;
        Gore.Burst(pos, new Color(0.85f, 0.12f, 0.10f), Art.PlayerFlesh, 10, 1.6f);
        Fx.Explosion(pos, new Color(0.85f, 0.15f, 0.15f), new Color(0.45f, 0.65f, 1f), 1.35f);
        Fx.Burst(pos, new Color(0.85f, 0.15f, 0.15f), 30, 10f);
        GameManager.Shake(0.7f);
        if (stunFx != null) stunFx.SetActive(false);
        if (slowFx != null) slowFx.SetActive(false);
        visual.gameObject.SetActive(false);
        aimLine.enabled = false;
        ReleaseGrab();
        GameManager.Instance.GameOver();
    }
}
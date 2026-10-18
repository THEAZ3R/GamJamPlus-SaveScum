using System.Collections.Generic;
using UnityEngine;

public enum EnemyKind { Stabber, Spider, Shooter, Bulwark, Trapper, Piercer, Bouncer, Artillery, Dasher, Grabber, Butcher }

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Health))]
public class Enemy : MonoBehaviour
{
    public static int Alive, BigAlive;
    public static readonly List<Enemy> Bulwarks = new List<Enemy>();
    public static readonly List<Enemy> All = new List<Enemy>();    // live enemies (mods, laser)

    // fluids: humans bleed red, everything mechanical - and the eldritch one -
    // leaks black oil
    static readonly Color RedBlood = new Color(0.85f, 0.12f, 0.10f);
    static readonly Color BlackOil = new Color(0.05f, 0.05f, 0.06f);
    static readonly Color FleshBlood = new Color(0.55f, 0.12f, 0.16f);   // the fourth reality

    EnemyKind Kind { get; set; }
    public int XpValue { get; private set; }
    public EnemyKind KindOf { get { return Kind; } }
    public Health Health { get { return health; } }
    public Collider2D Body { get { return bodyCol; } }    // for chrono pass-through
    public bool BleedsRed { get { return health != null && health.bloodColor == RedBlood; } }

    // TIMELEAP: how sharp the horde has become - every leap they start
    // stepping out of the line of fire a little more reliably
    public static float Skill;

    enum Stab { Chase, Windup, Lunge, Recover }

    float speed, contactDamage, shootInterval, bulletDamage, preferredDist, explosionDamage, trapInterval;
    float attackTimer, shootTimer, fuse = -1f, fuseTickT, faceAngle, hullAngle, strafeSign, dmgScale;
    float stabTimer, dashCd, dashTimer, deployT, burstT, burstShots, staggerT, grabPulse, hopTimer = 0.7f;
    float toxinDps, toxinT, toxinTick;              // hemo-toxin burn
    float dodgeT, dodgeScanT;                       // timeleap: sidestep incoming fire
    float dodgeCdT;                                 // ...but not forever: the step needs a beat
    Vector2 dodgeDir;
    Vector2 hopVel;                 // the bouncer's leap, decays in FixedUpdate
    Vector2 faceDir = Vector2.right, knock;         // facing (no spin), shove impulses
    bool gone, shieldUp, upright;   // upright = pokemon-style 3-way sprite set
    Stab stab = Stab.Chase;
    TrapKind nextTrap = TrapKind.Slow;

    Rigidbody2D rb;
    CircleCollider2D bodyCol;
    Health health;
    Transform visual, bodyT;
    SpriteRenderer bodySr, toxinSr;
    Color bodyOrig, flesh = Color.gray;
    HumanoidAnim anim;
    DestructibleWall shield;
    Vector2 moveDir, dashDir, grabOffset;

    public static Enemy Spawn(EnemyKind kind, Vector2 pos, float difficulty)
    {
        var go = new GameObject(kind.ToString());
        go.transform.position = pos;
        var e = go.AddComponent<Enemy>();
        e.Setup(kind, difficulty);
        return e;
    }

    // ------------------------------------------------------------ SETUP
    void Setup(EnemyKind kind, float difficulty)
    {
        Kind = kind;

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        health = GetComponent<Health>();
        health.OnDeath += Die;

        float hp = 20f, radius = 0.32f, vScale = 1.15f;
        Sprite sprite = null;                    // single sprite: mechs, spider, horror
        Art.UnitArt art3 = new Art.UnitArt();    // 3-way set: the humanoids
        contactDamage = 6f;
        AnimStyle style = AnimStyle.Standard;

        switch (kind)
        {
            case EnemyKind.Stabber:             // small, hunched, leans into the stab
                art3 = Art.Stabber(); hp = 30f; speed = 3.3f; contactDamage = 12f; XpValue = 12;
                radius = 0.28f; vScale = 0.95f; style = AnimStyle.Hunch;
                flesh = Art.StabberFlesh;
                health.bloodColor = RedBlood;
                break;

            case EnemyKind.Dasher:              // lean and coiled
                art3 = Art.Dasher(); hp = 34f; speed = 2.9f; contactDamage = 14f; XpValue = 18;
                radius = 0.30f; vScale = 1.0f; style = AnimStyle.Vibrate; dashCd = 1f;
                flesh = Art.DasherFlesh;
                health.bloodColor = RedBlood;
                break;

            case EnemyKind.Spider:              // tiny, fast, explodes
                sprite = Art.Spider(); hp = 12f; speed = 5.6f; contactDamage = 0f;
                explosionDamage = 45f; radius = 0.26f; vScale = 0.8f; XpValue = 20;
                style = AnimStyle.Scuttle;
                flesh = Art.SpiderFlesh;
                health.bloodColor = BlackOil;    // machine blood
                break;

            case EnemyKind.Shooter:             // baseline trooper
                art3 = Art.Shooter(); hp = 28f; speed = 2.4f; contactDamage = 5f;
                shootInterval = 1.5f; bulletDamage = 8f; preferredDist = 7f; XpValue = 16;
                radius = 0.32f; vScale = 1.05f; style = AnimStyle.Standard;
                flesh = Art.ShooterFlesh;
                health.bloodColor = RedBlood;
                break;

            case EnemyKind.Bulwark:             // huge, slow, carries the shield
                sprite = Art.Bulwark(); hp = 120f; speed = 1.5f; contactDamage = 15f;
                radius = 0.5f; vScale = 1.35f; XpValue = 45; style = AnimStyle.Heavy;
                preferredDist = 5.5f; shieldUp = true;
                bulletDamage = 9f;              // it shoots back once the shield is gone
                flesh = Art.MechFlesh;
                health.bloodColor = BlackOil;    // the tank leaks oil
                break;

            case EnemyKind.Trapper:             // unhurried, sways while it aims
                art3 = Art.Trapper(); hp = 34f; speed = 2.5f; contactDamage = 5f;
                trapInterval = 3.2f; preferredDist = 6.5f; XpValue = 20;
                radius = 0.32f; vScale = 1.05f; style = AnimStyle.Sway;
                flesh = Art.TrapperFlesh;
                health.bloodColor = RedBlood;
                break;

            case EnemyKind.Piercer:             // heavy rigid frame, big cannon
                art3 = Art.Piercer(); hp = 30f; speed = 2.2f; contactDamage = 5f;
                shootInterval = 2.0f; bulletDamage = 12f; preferredDist = 8f; XpValue = 22;
                radius = 0.42f; vScale = 1.25f; style = AnimStyle.Stiff;
                flesh = Art.PiercerFlesh;
                health.bloodColor = BlackOil;    // robot
                break;

            case EnemyKind.Bouncer:             // squat, springy, wide stance
                art3 = Art.Bouncer(); hp = 32f; speed = 2.5f; contactDamage = 5f;
                shootInterval = 1.7f; bulletDamage = 10f; preferredDist = 7f; XpValue = 22;
                radius = 0.42f; vScale = 1.3f; style = AnimStyle.Bounce;
                flesh = Art.BouncerFlesh;
                health.bloodColor = RedBlood;
                break;

            case EnemyKind.Artillery:           // biggest turret on legs
                sprite = TimeLeap.Map4 ? Art.FleshArtillery() : Art.Artillery();
                hp = 90f; speed = 1.6f; contactDamage = 12f;
                radius = 0.5f; vScale = 1.35f; shootInterval = 4.6f; bulletDamage = 18f;
                preferredDist = 11f; XpValue = 50; style = AnimStyle.Heavy;
                flesh = Art.MechFlesh;
                health.bloodColor = TimeLeap.Map4 ? FleshBlood : BlackOil;
                break;

            case EnemyKind.Butcher:             // the flesh's tank: it closes
                                                // the gap and coughs a shotgun
                sprite = Art.Butcher(); hp = 150f; speed = 2.15f; contactDamage = 18f;
                radius = 0.55f; vScale = 1.45f; XpValue = 55; style = AnimStyle.Heavy;
                shootInterval = 2.7f; bulletDamage = 5f; preferredDist = 4.2f;
                flesh = new Color(0.6f, 0.2f, 0.2f);
                health.bloodColor = FleshBlood;
                break;

            default: // Grabber                 // floats, no legs
                sprite = TimeLeap.Map4 ? Art.FleshGrabber() : Art.GrabberBot();
                hp = 210f; speed = 3.0f; contactDamage = 0f;
                radius = 0.4f; vScale = 1.2f; XpValue = 60; style = AnimStyle.Hover;
                flesh = Art.GrabberFlesh;
                health.bloodColor = TimeLeap.Map4 ? FleshBlood : BlackOil;
                break;
        }

        bool big = kind == EnemyKind.Bulwark || kind == EnemyKind.Artillery ||
                   kind == EnemyKind.Grabber || kind == EnemyKind.Butcher;
        if (big) BigAlive++;

        dmgScale = Mathf.Sqrt(difficulty);
        hp *= difficulty;
        contactDamage *= dmgScale;
        bulletDamage *= dmgScale;
        explosionDamage *= dmgScale;
        speed *= 1f + (difficulty - 1f) * 0.2f;

        bodyCol = GetComponent<CircleCollider2D>();
        bodyCol.radius = radius;

        health.Init(hp);
        health.showBar = true;
        health.barWidth = Mathf.Max(0.7f, radius * 2.6f);
        health.barOffsetY = radius + 0.44f;
        health.barColor = big ? new Color(1f, 0.6f, 0.15f) : new Color(0.95f, 0.25f, 0.2f);

        // humanoids stand upright and swap views (pokemon-style); mechs and
        // monsters keep the one spinning top-down sprite
        upright = art3.down != null;
        visual = upright ? Humanoid.Build(transform, art3, vScale, 20, style)
                         : Humanoid.Build(transform, sprite, vScale, 20, style);
        bodyT = visual.GetChild(0);
        bodySr = visual.GetComponentInChildren<SpriteRenderer>();
        if (bodySr != null) bodyOrig = bodySr.color;
        anim = visual.GetComponent<HumanoidAnim>();

        // the lab re-skins its pack in the herald's livery (the flesh era
        // paints over everything, so it takes the last word)
        if (TimeLeap.Map2 && !TimeLeap.Map4)
        {
            if (bodySr != null)
            {
                bodyOrig = Color.Lerp(bodyOrig, new Color(0.62f, 0.42f, 1f), 0.35f);
                bodySr.color = bodyOrig;
            }
            health.barColor = new Color(0.8f, 0.45f, 1f);
        }

        // the flesh era takes the last layer of paint too: everything that
        // walks here wears meat and looks back at you with too many eyes
        if (TimeLeap.Map4)
        {
            if (bodySr != null)
            {
                bodyOrig = Color.Lerp(bodyOrig, new Color(0.9f, 0.38f, 0.34f), 0.32f);
                bodySr.color = bodyOrig;
            }
            health.barColor = new Color(0.85f, 0.28f, 0.3f);
        }

        if (kind == EnemyKind.Bulwark) BuildShield();

        shootTimer = Random.Range(0.5f, Mathf.Max(0.6f, shootInterval));
        strafeSign = Random.value < 0.5f ? -1f : 1f;
        Alive++;
        All.Add(this);
        if (kind == EnemyKind.Bulwark) Bulwarks.Add(this);
        Sfx.PlayAt(transform.position, "spawn", 0.3f, kind == EnemyKind.Spider ? 1.2f : 1f);
    }

    void BuildShield()
    {
        shield = DestructibleWall.Create(Vector2.zero, new Vector2(0.28f, 2.1f), 140f, 22, true);
        shield.transform.SetParent(transform, false);
    }

    void OnDestroy()
    {
        Bulwarks.Remove(this);
        All.Remove(this);
    }

    float wheelT, crawlT;                           // heavy chassis and little legs

    // ------------------------------------------------------------ AI
    void Update()
    {
        if (gone) return;
        var p = PlayerController.Instance;
        if (p == null || GameManager.IsOver) { moveDir = Vector2.zero; return; }

        float dt = Time.deltaTime;

        // HEMO-TOXIN: the wound keeps boiling - a red pulse over the body and
        // a quiet burn that ticks regardless of the AI below
        if (toxinT > 0f)
        {
            toxinT -= dt;
            if (toxinSr != null)
                toxinSr.color = new Color(0.95f, 0.22f, 0.28f, 0.16f + 0.12f * Mathf.Sin(Time.time * 16f));

            toxinTick -= dt;
            if (toxinTick <= 0f)
            {
                toxinTick = 0.25f;
                health.Burn(toxinDps * 0.25f);
                if (gone) return;
                Fx.Burst(transform.position + (Vector3)(Random.insideUnitCircle * 0.3f),
                         new Color(0.92f, 0.25f, 0.3f), 2, 2f);
            }
        }
        else if (toxinSr != null && toxinSr.enabled) toxinSr.enabled = false;

        Vector2 pos = transform.position;
        Vector2 target = p.transform.position;
        Vector2 to = target - pos;
        float dist = to.magnitude;
        Vector2 dir = to / Mathf.Max(dist, 0.001f);
        float toAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        bool los = Nav.SightClear(pos, target);
        bool bigMover = Kind == EnemyKind.Bulwark || Kind == EnemyKind.Artillery ||
                        Kind == EnemyKind.Grabber || Kind == EnemyKind.Butcher;
        Vector2 steer = Nav.Steer(pos, target, bigMover);

        attackTimer -= dt;

        // the chassis rolls, the crawler skitters
        if (moveDir.sqrMagnitude > 0.05f)
        {
            if (Kind == EnemyKind.Bulwark || Kind == EnemyKind.Artillery || Kind == EnemyKind.Butcher)
            {
                wheelT -= dt;
                if (wheelT <= 0f) { wheelT = 0.42f; Sfx.PlayAt(pos, "wheel", 0.55f); }
            }
            else if (Kind == EnemyKind.Spider)
            {
                crawlT -= dt;
                if (crawlT <= 0f) { crawlT = Random.Range(0.5f, 0.85f); Sfx.PlayAt(pos, "crawl", 0.45f, Random.Range(0.9f, 1.15f)); }
            }
        }

        // TIMELEAP: later leaps learn to read your muzzle - scan for rounds
        // aimed this way and throw themselves off the line (spiders are too
        // single-minded to read anything: they just come)
        if (Skill > 0f && Kind != EnemyKind.Spider)
        {
            dodgeScanT -= dt;
            if (dodgeScanT <= 0f) { dodgeScanT = 0.12f; ScanDodge(); }
            if (dodgeT > 0f) dodgeT -= dt;
            if (dodgeCdT > 0f) dodgeCdT -= dt;       // the sidestep has a cool-down
        }

        // the bouncer hops around the arena instead of walking
        if (Kind == EnemyKind.Bouncer && hopVel.sqrMagnitude < 0.04f)
        {
            hopTimer -= dt;
            if (hopTimer <= 0f)
            {
                hopTimer = Random.Range(1.5f, 2.5f);
                Vector2 hop = Vector2.Perpendicular(dir) * (Random.value < 0.5f ? 1f : -1f);
                if (dist > preferredDist + 1.5f) hop += dir;           // hop closer
                else if (dist < preferredDist - 1.5f) hop -= dir;      // hop away
                hopVel = (hop.sqrMagnitude > 0.01f ? hop.normalized : dir) * Random.Range(5.5f, 7.5f);
                if (anim != null) anim.Hop(0.26f);
            }
        }

        if (staggerT > 0f)
        {
            staggerT -= dt;
            moveDir = Vector2.zero;
            return;
        }

        switch (Kind)
        {
            case EnemyKind.Stabber:
                MeleeUpdate(dt, dir, dist, toAngle, steer, los, 2.4f, 26f, 0.30f, 0.22f, 0.55f);
                return;

            case EnemyKind.Dasher:
                DasherUpdate(dt, dir, dist, toAngle, steer, los);
                return;

            case EnemyKind.Spider:
                SpiderUpdate(dt, dir, dist, toAngle);
                return;

            case EnemyKind.Shooter:
                ShooterUpdate(dt, pos, dir, dist, toAngle, steer, los, BulletKind.Normal);
                return;

            case EnemyKind.Piercer:
                ShooterUpdate(dt, pos, dir, dist, toAngle, steer, false, BulletKind.Pierce);
                return;

            case EnemyKind.Bouncer:
                ShooterUpdate(dt, pos, dir, dist, toAngle, steer, false, BulletKind.Bounce);
                return;

            case EnemyKind.Bulwark:
                BulwarkUpdate(dt, pos, dir, dist, toAngle, steer, los);
                break;

            case EnemyKind.Trapper:
                TrapperUpdate(dt, pos, dir, dist, toAngle, steer, target);
                return;

            case EnemyKind.Artillery:
                ArtilleryUpdate(dt, pos, dir, dist, toAngle, steer, target, los);
                return;

            case EnemyKind.Grabber:
                GrabberUpdate(dt, pos, dir, dist, toAngle, steer, p);
                return;

            case EnemyKind.Butcher:
                ButcherUpdate(dt, pos, dir, dist, toAngle, steer, los);
                return;
        }

        if (visual != null)
            Face(faceAngle);
    }

    // ------------------------------------------------------------ facing
    // Humanoids never spin: they swap between the front / back / side views
    // with the side one flipped. Mechs and monsters still rotate the sprite.
    void Face(float deg)
    {
        if (visual == null || anim == null) return;

        if (!upright)
        {
            visual.rotation = Quaternion.Euler(0, 0, deg);
            return;
        }

        float r = deg * Mathf.Deg2Rad;
        faceDir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        anim.SetFacing(faceDir);
        anim.SetAim(faceDir);                      // the orbiting weapon tracks the aim
    }

    // FUS RO DAH: a shove that sends the target sliding without lifting it
    public void Push(Vector2 dir, float force)
    {
        knock += dir.normalized * force;
    }

    // ------------------------------------------------------------ timeleap
    // every leap sharpens what is already on the field
    public void ApplyLeapBuff()
    {
        if (gone) return;
        speed *= 1.15f;
        dmgScale *= 1.15f;
        contactDamage *= 1.15f;
        bulletDamage *= 1.15f;
        explosionDamage *= 1.15f;
    }

    // back to where you stood three seconds ago - and to who you were then
    public void RewindTo(Vector2 pos, float hp)
    {
        if (gone) return;
        transform.position = pos;
        if (rb != null) { rb.position = pos; rb.SetVel(Vector2.zero); }
        knock = hopVel = Vector2.zero;
        dodgeT = 0f;
        fuse = -1f;                                   // an un-diffused spider forgets
        if (health != null) health.Restore(hp);
    }

    // pulled out of the timeline: no gore, no xp, no kill
    public void Despawn() { Remove(); }

    // is a player round actually aimed at this body?
    void ScanDodge()
    {
        // one step, then a breath: a stream of fire still finds its home
        if (dodgeT > 0f || dodgeCdT > 0f) return;

        // the pre-boss trio reads the wind a little slower than the lab pack
        float chance = Skill * (Kind == EnemyKind.Shooter || Kind == EnemyKind.Trapper ||
                                Kind == EnemyKind.Stabber ? 0.55f : 1f);

        var hits = Physics2D.OverlapCircleAll(transform.position, 2.1f);
        foreach (var h in hits)
        {
            var b = h.GetComponent<Bullet>();
            if (b == null || !b.FromPlayer) continue;
            Vector2 toMe = ((Vector2)transform.position - (Vector2)b.transform.position).normalized;
            if (Vector2.Dot(b.Dir, toMe) < 0.88f) continue;   // not aimed our way
            if (Random.value > chance) continue;              // not sharp enough yet
            dodgeDir = Vector2.Perpendicular(toMe).normalized * (Random.value < 0.5f ? 1f : -1f);
            dodgeT = 0.38f;
            dodgeCdT = 1.5f;                                  // and now it has to mean it
            return;
        }
    }

    // ------------------------------------------------------------ melee
    void MeleeUpdate(float dt, Vector2 dir, float dist, float toAngle, Vector2 steer, bool los,
                     float reach, float dmg, float windupT, float lungeT, float recoverT)
    {
        // only the lunge itself hurts
        contactDamage = (stab == Stab.Lunge) ? dmg * dmgScale : 0f;

        switch (stab)
        {
            case Stab.Chase:
                moveDir = steer;
                faceAngle = Turn(moveDir, toAngle, 320f, dt);
                if (dist < reach && los && attackTimer <= 0f)
                {
                    stab = Stab.Windup;
                    stabTimer = windupT;
                }
                break;

            case Stab.Windup:
                moveDir = -dir * 0.3f;
                faceAngle = Turn(moveDir, toAngle, 200f, dt);
                Thrust(-0.18f);
                Tint(Mathf.Sin(Time.time * 40f) > 0f);
                if ((stabTimer -= dt) <= 0f) { stab = Stab.Lunge; stabTimer = lungeT; dashDir = dir; Kick(1.2f); }
                break;

            case Stab.Lunge:
                moveDir = dashDir * 1.5f;
                faceAngle = Turn(moveDir, toAngle, 700f, dt);
                Thrust(0.45f);
                Tint(true);
                if ((stabTimer -= dt) <= 0f) { stab = Stab.Recover; stabTimer = recoverT; }
                break;

            case Stab.Recover:
                moveDir = -dir * 0.25f;
                faceAngle = Turn(moveDir, toAngle, 200f, dt);
                Thrust(0f);
                Tint(false);
                if ((stabTimer -= dt) <= 0f) { stab = Stab.Chase; attackTimer = 0.4f; }
                break;
        }
    }

    void DasherUpdate(float dt, Vector2 dir, float dist, float toAngle, Vector2 steer, bool los)
    {
        if (dashTimer > 0f)
        {
            dashTimer -= dt;
            moveDir = dashDir * 1.6f;
            contactDamage = 14f * dmgScale;
            faceAngle = Turn(moveDir, toAngle, 700f, dt);
            Thrust(0.3f);
            return;
        }

        contactDamage = 0f;
        dashCd -= dt;
        moveDir = steer;
        faceAngle = Turn(moveDir, toAngle, 300f, dt);
        Thrust(0f);

        if (dashCd <= 0f && dist > 1.7f && dist < 6.5f && los)
        {
            dashTimer = 0.30f;
            dashCd = 1.15f;
            dashDir = dir;
            Kick(1f);
            Fx.Burst(transform.position, new Color(1f, 0.5f, 0.1f), 6, 4f);
        }
    }

    void SpiderUpdate(float dt, Vector2 dir, float dist, float toAngle)
    {
        if (fuse >= 0f)
        {
            moveDir = Vector2.zero;
            fuse -= dt;
            Tint(Mathf.Sin(Time.time * 45f) > 0f);

            // ticking: faster and louder the closer it gets to going off
            fuseTickT -= dt;
            if (fuseTickT <= 0f)
            {
                fuseTickT = Mathf.Clamp(fuse * 0.28f, 0.07f, 0.16f);
                Sfx.PlayAt(transform.position, "fuse_tick", 0.4f + (1f - fuse / 0.8f) * 0.3f,
                           Random.Range(1f, 1.2f));
            }

            if (fuse <= 0f) Explode();
            return;
        }

        moveDir = Nav.Steer(transform.position, PlayerController.Instance.transform.position, false);
        faceAngle = Turn(moveDir, toAngle, 540f, dt);
        if (dist < 1.6f) StartFuse();
    }

    // ------------------------------------------------------------ ranged
    void ShooterUpdate(float dt, Vector2 pos, Vector2 dir, float dist, float toAngle,
                       Vector2 steer, bool needLos, BulletKind bkind)
    {
        faceAngle = toAngle;

        Vector2? cover = CoverPoint(pos);
        if (cover.HasValue)
        {
            Vector2 cp = cover.Value - pos;
            moveDir = cp.magnitude > 0.6f ? cp.normalized : Vector2.zero;
        }
        // it keeps its distance: only closes when far, backs off the moment
        // you step inside its preferred ring (the bouncer used to skip this
        // and rush you down - now it holds the ring and banks its shots)
        else if (dist > preferredDist + 2.5f) moveDir = steer;
        else if (dist < preferredDist) moveDir = -dir;
        else moveDir = new Vector2(-dir.y, dir.x) * strafeSign * 0.6f;

        shootTimer -= dt;
        bool blocked = needLos && !Nav.SightClear(pos, PlayerController.Instance.transform.position);
        if (shootTimer > 0f || blocked || dist > 14f) return;

        shootTimer = shootInterval;
        strafeSign = Random.value < 0.5f ? -1f : 1f;

        Vector2 muzzle = pos + dir * 0.95f;
        switch (bkind)
        {
            case BulletKind.Pierce:
                Bullet.Spawn(muzzle, dir, 6.5f, bulletDamage, false, new Color(0.35f, 0.95f, 1f), 0.26f, bkind);
                break;
            case BulletKind.Bounce:
                Bullet.Spawn(muzzle, dir, 6.5f, bulletDamage, false, new Color(1f, 0.75f, 0.2f), 0.24f, bkind);
                break;
            default:
                Bullet.Spawn(muzzle, dir, 7.5f, bulletDamage, false, new Color(0.95f, 0.4f, 0.55f));
                break;
        }

        Kick(0.9f);                                  // recoil
    }

    // shooters tuck in behind a live Bulwark shield
    Vector2? CoverPoint(Vector2 pos)
    {
        if (Kind != EnemyKind.Shooter) return null;
        var p = PlayerController.Instance;
        if (p == null) return null;

        for (int i = 0; i < Bulwarks.Count; i++)
        {
            var b = Bulwarks[i];
            if (b == null || b.gone || !b.shieldUp) continue;
            Vector2 bp = b.transform.position;
            if (Vector2.Distance(bp, p.transform.position) > 13f) continue;
            if (Vector2.Distance(bp, pos) > 9f) continue;
            return bp + (bp - (Vector2)p.transform.position).normalized * 1.5f;
        }
        return null;
    }

    void BulwarkUpdate(float dt, Vector2 pos, Vector2 dir, float dist, float toAngle,
                       Vector2 steer, bool los)
    {
        faceAngle = Mathf.MoveTowardsAngle(faceAngle, toAngle, 90f * dt);

        if (shieldUp && (shield == null || shield.Dead)) shieldUp = false;

        if (shieldUp)
        {
            moveDir = dist > preferredDist ? steer : Vector2.zero;
            if (shield != null)
            {
                shield.transform.localRotation = Quaternion.Euler(0, 0, toAngle);
                shield.transform.localPosition = dir * 0.95f;
            }
        }
        else
        {
            moveDir = dist > preferredDist + 2f ? steer
                    : (dist < preferredDist - 1.5f ? -dir * 0.6f : Vector2.zero);

            shootTimer -= dt;
            if (shootTimer <= 0f && los && dist < 13f)
            {
                shootTimer = 1.5f;
                Bullet.Spawn(pos + dir * 1.0f, dir, 8f, bulletDamage, false, new Color(1f, 0.45f, 0.3f));
            }
        }
    }

    void TrapperUpdate(float dt, Vector2 pos, Vector2 dir, float dist, float toAngle,
                       Vector2 steer, Vector2 target)
    {
        faceAngle = Mathf.MoveTowardsAngle(faceAngle, toAngle, 140f * dt);

        // the trapper wants a wall between you and it: hunt a spot your eye
        // cannot reach, and hold still once the wall does its job
        bool los = Nav.SightClear(pos, target);
        Vector2? hide = los ? HidePoint(pos, target) : null;

        if (dist < 3.5f) moveDir = -dir;
        else if (hide.HasValue) moveDir = (hide.Value - pos).normalized * 0.85f;
        else if (!los) moveDir = dist > preferredDist + 2f ? steer : Vector2.zero;
        else if (dist > preferredDist + 1.5f) moveDir = steer;
        else moveDir = new Vector2(-dir.y, dir.x) * strafeSign * 0.45f;

        shootTimer -= dt;
        if (shootTimer <= 0f && dist < 12f)
        {
            shootTimer = trapInterval;
            strafeSign = Random.value < 0.5f ? -1f : 1f;
            ThrowTrap(target);
            nextTrap = (TrapKind)(((int)nextTrap + 1) % 3);
        }
    }

    // eight probes around itself: the first spot the player's eye cannot see
    Vector2? HidePoint(Vector2 pos, Vector2 target)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            Vector2 c = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.4f;
            if (!Nav.IsFree(c, false)) continue;
            if (Nav.SightClear(c, target)) continue;          // still in the open
            return c;
        }
        return null;
    }

    void ThrowTrap(Vector2 target)
    {
        // land on a ring around the player, never right under their feet
        for (int i = 0; i < 6; i++)
        {
            Vector2 t = target + Random.insideUnitCircle.normalized * Random.Range(1.1f, 2.2f);
            if (Pit.Inside(t) || !Nav.IsFree(t, false)) continue;
            Trap.Spawn(t, nextTrap);
            return;
        }
    }

    void ArtilleryUpdate(float dt, Vector2 pos, Vector2 dir, float dist, float toAngle,
                         Vector2 steer, Vector2 target, bool los)
    {
        if (deployT > 0f)
        {
            deployT -= dt;
            moveDir = Vector2.zero;
            turretTurn(dt, toAngle, 120f);
            Tint(Mathf.Sin(Time.time * 30f) > 0f);
            if (deployT <= 0f) { burstShots = 3f; burstT = 0f; }
        }
        else if (burstShots > 0f)
        {
            moveDir = Vector2.zero;
            turretTurn(dt, toAngle, 120f);
            burstT -= dt;
            if (burstT <= 0f)
            {
                Vector2 fd = new Vector2(Mathf.Cos(faceAngle * Mathf.Deg2Rad), Mathf.Sin(faceAngle * Mathf.Deg2Rad));
                Vector2 muzzle = pos + fd * 1.4f;
                Vector2 land = target + Random.insideUnitCircle * 1.2f;
                Bullet.Spawn(muzzle, (land - muzzle).normalized, 9f, bulletDamage, false,
                             new Color(1f, 0.55f, 0.15f), -1f, BulletKind.Shell, land);
                Fx.Burst(muzzle, new Color(1f, 0.8f, 0.3f), 8, 6f);
                GameManager.Shake(0.1f);
                Kick(1.2f);                                  // tube recoil
                burstShots -= 1f;
                burstT = 0.5f;
            }
        }
        else
        {
            Tint(false);
            if (dist > preferredDist + 1.5f) moveDir = steer;
            else if (dist < preferredDist - 2.5f) moveDir = -dir * 0.6f;
            else moveDir = Vector2.zero;

            turretTurn(dt, toAngle, 90f);

            shootTimer -= dt;
            if (shootTimer <= 0f && dist < 15f && los)
            {
                shootTimer = shootInterval;
                deployT = 1.1f;
            }
        }

        // hull drifts toward movement, turret tracks the player (faceAngle)
        if (moveDir.sqrMagnitude > 0.01f)
            hullAngle = Mathf.MoveTowardsAngle(hullAngle, toAngleOf(moveDir), 90f * dt);
        if (visual != null) Face(hullAngle);
    }

    void turretTurn(float dt, float toAngle, float rate)
    {
        faceAngle = Mathf.MoveTowardsAngle(faceAngle, toAngle, rate * dt);
    }

    // ------------------------------------------------------------ butcher
    // a wall of meat that only knows one direction: closer. when the gap
    // closes it rears back and coughs six pellets of tooth and bone
    void ButcherUpdate(float dt, Vector2 pos, Vector2 dir, float dist, float toAngle,
                       Vector2 steer, bool los)
    {
        if (attackTimer > 0f)                         // the reared-back windup
                                                // (the global tick counts it down)
        {
            moveDir = Vector2.zero;
            Tint(Mathf.Sin(Time.time * 40f) > 0f);
            if (attackTimer <= 0f)
            {
                float baseA = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                for (int i = -2; i <= 3; i++)          // six teeth, wide mouth
                {
                    float a = (baseA + i * 6.5f + Random.Range(-3f, 3f)) * Mathf.Deg2Rad;
                    Bullet.Spawn(pos + dir * 0.9f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)),
                                 13f, bulletDamage, false, new Color(0.95f, 0.55f, 0.5f));
                }
                Fx.Burst(pos + dir * 0.9f, new Color(0.85f, 0.35f, 0.3f), 10, 7f);
                Sfx.PlayAt(pos, "butcher_gun", 0.8f);
                Kick(1.6f);
                GameManager.Shake(0.18f);
            }
            return;
        }

        Tint(false);
        moveDir = steer;
        Thrust(dist < 6.5f ? 0.2f : 0f);              // it smells you, it hurries
        contactDamage = 18f * dmgScale;
        faceAngle = Mathf.MoveTowardsAngle(faceAngle, toAngle, 140f * dt);

        shootTimer -= dt;
        if (shootTimer <= 0f && los && dist < 5.4f)
        {
            shootTimer = shootInterval * Random.Range(0.85f, 1.2f);
            attackTimer = 0.38f;                      // it winds up, then it coughs
            Sfx.PlayAt(pos, "grab", 0.45f, 0.7f);     // a wet breath before the blast
        }
    }

    // ------------------------------------------------------------ grabber
    void GrabberUpdate(float dt, Vector2 pos, Vector2 dir, float dist, float toAngle,
                       Vector2 steer, PlayerController p)
    {
        if (p.IsGrabbed && p.GrabberEnemy == this)
        {
            Vector2 want = ((Vector2)p.transform.position + grabOffset) - pos;
            moveDir = want.magnitude > 0.2f ? want.normalized : Vector2.zero;
            faceAngle = Turn(moveDir, toAngle, 400f, dt);

            grabPulse += dt;
            float s = 1f + Mathf.Sin(grabPulse * 14f) * 0.06f;
            if (visual != null) visual.localScale = Vector3.one * s;
            return;
        }

        if (visual != null && visual.localScale != Vector3.one) visual.localScale = Vector3.one;
        moveDir = steer;
        faceAngle = Turn(moveDir, toAngle, 260f, dt);
    }

    // ------------------------------------------------------------ physics
    void FixedUpdate()
    {
        if (gone) { rb.SetVel(Vector2.zero); return; }

        // sidestep whatever is flying at us (timeleap skill) instead of walking
        Vector2 v = (dodgeT > 0f ? dodgeDir * 1.4f : moveDir) * speed + hopVel + knock;
        knock = Vector2.MoveTowards(knock, Vector2.zero, 34f * Time.fixedDeltaTime);

        Vector2 p = rb.position;
        Vector2 next = p + v * Time.fixedDeltaTime;
        if (Pit.Blocks(p, next))
        {
            Vector2 nx = new Vector2(next.x, p.y), ny = new Vector2(p.x, next.y);
            if (!Pit.Blocks(p, nx)) v = new Vector2(v.x, 0f);
            else if (!Pit.Blocks(p, ny)) v = new Vector2(0f, v.y);
            else v = Vector2.zero;
        }

        rb.SetVel(v);
        hopVel = Vector2.MoveTowards(hopVel, Vector2.zero, 16f * Time.fixedDeltaTime);
    }

    void OnCollisionStay2D(Collision2D c)
    {
        if (!c.collider.CompareTag("Player")) return;
        if (attackTimer > 0f) return;
        var p = PlayerController.Instance;
        if (p == null) return;

        if (Kind == EnemyKind.Grabber)
        {
            if (staggerT > 0f || p.IsGrabbed) return;
            p.OnGrabbed(this);
            grabOffset = ((Vector2)transform.position - (Vector2)p.transform.position).normalized * 0.85f;
            attackTimer = 1f;
            return;
        }

        if (contactDamage <= 0f) return;
        var h = c.collider.GetComponent<Health>();
        Vector2 away = ((Vector2)c.collider.transform.position - (Vector2)transform.position).normalized;
        if (h != null && h.TakeDamage(contactDamage, away)) attackTimer = 0.7f;
    }

    public void OnReleased()
    {
        staggerT = 1.7f;
        moveDir = Vector2.zero;
        StopAllCoroutines();
        StartCoroutine(StaggerFlash());
    }

    // HEMO-TOXIN: red-blooded flesh keeps burning after the hit. The pulse is
    // a soft red coat over the body, so it reads even in a crowd.
    public bool Burning { get { return toxinT > 0f; } }   // hemotoxin tick running

    public void ApplyToxin(float dps, float duration)
    {
        toxinDps = Mathf.Max(toxinDps, dps);
        toxinT = Mathf.Max(toxinT, duration);

        if (toxinSr == null)
        {
            var g = new GameObject("Toxin");
            g.transform.SetParent(transform, false);
            g.transform.localScale = Vector3.one * 0.95f;
            toxinSr = g.AddComponent<SpriteRenderer>();
            toxinSr.sprite = SpriteFactory.Circle;
            toxinSr.color = new Color(0.95f, 0.22f, 0.28f, 0.2f);
            toxinSr.sortingOrder = 21;                  // over the body, under the bars
        }
        toxinSr.enabled = true;
    }

    System.Collections.IEnumerator StaggerFlash()
    {
        for (float t = 0f; t < 1.6f; t += 0.1f)
        {
            Tint(Mathf.Sin(t * 40f) > 0f);
            yield return null;
        }
        Tint(false);
    }

    // ------------------------------------------------------------ spider
    void StartFuse()
    {
        fuse = 0.8f;
        fuseTickT = 0f;
        var w = new GameObject("Warning");
        w.transform.SetParent(transform, false);
        w.transform.localScale = Vector3.one * 2.4f;
        var sr = w.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(1f, 0.2f, 0.1f, 0.28f);
        sr.sortingOrder = -2;
    }

    void Explode()
    {
        if (gone) return;
        var p = PlayerController.Instance;
        if (p != null && Vector2.Distance(p.transform.position, transform.position) < 2.3f)
        {
            Vector2 away = ((Vector2)p.transform.position - (Vector2)transform.position).normalized;
            p.Health.TakeDamage(explosionDamage, away);
        }

        Sfx.PlayAt(transform.position, "boom_small", 0.85f, 1.05f);
        Gore.Burst(transform.position, health.bloodColor, flesh, 9, 1.4f);
        Fx.Explosion(transform.position, new Color(1f, 0.55f, 0.1f), new Color(1f, 0.9f, 0.4f), 1.3f);
        Gore.Stain(transform.position, new Color(0.05f, 0.045f, 0.04f), 1.4f);   // scorch
        GameManager.Shake(0.6f);

        GameManager.Kills++;                            // it still went off: it counts
        var up = PlayerUpgrades.Instance;
        if (up != null) up.OnEnemyKilled(transform.position, this);
        Remove();
    }

    // ------------------------------------------------------------ death
    void Die(Health h)
    {
        if (gone) return;
        bool big = Kind == EnemyKind.Bulwark || Kind == EnemyKind.Artillery || Kind == EnemyKind.Grabber;
        Vector2 pos = transform.position;

        // the lab floor drinks: red blood pools conduct, machine oil burns
        if (TimeLeap.Map2)
        {
            if (h.bloodColor == RedBlood && Random.value < 0.75f)
                FluidPool.Spawn(pos, FluidKind.Blood, Random.Range(0.55f, 0.95f));
            else if (h.bloodColor == BlackOil && Random.value < 0.55f)
                FluidPool.Spawn(pos, FluidKind.Oil, Random.Range(0.5f, 0.9f));
        }

        Sfx.PlayAt(pos, big ? "die_big" : "die_small", big ? 0.9f : 0.55f,
                   Kind == EnemyKind.Spider ? 1.2f : 1f);

        // the corpse comes apart: chunks scatter + trail blood, stains stay
        Gore.Burst(pos, h.bloodColor, flesh, big ? 11 : 7, big ? 1.7f : 1f);
        Fx.Burst(pos, h.bloodColor, big ? 24 : 10, big ? 11f : 8f);
        // pixel explosion: white-hot flash, then sparks of "metal" where it broke
        Fx.Flash(pos, Color.Lerp(h.bloodColor, Color.white, 0.75f), big ? 3f : 1.4f, 0.2f);
        Fx.Sparks(pos, Color.Lerp(h.bloodColor, Color.white, 0.55f), big ? 30 : 14, big ? 18f : 12f);
        if (big)
        {
            Fx.Ring(pos, new Color(1f, 0.5f, 0.1f), 0.5f, 5.5f, 0.5f);
            Fx.Burst(pos, new Color(1f, 0.5f, 0.1f), 25, 9f);
        }

        GameManager.Kills++;
        var up = PlayerUpgrades.Instance;
        if (up != null) up.OnEnemyKilled(pos, this);   // xp + corpse effects
        GameManager.Instance.HitStop(big ? 0.09f : 0.04f);
        if (big) GameManager.Shake(0.5f);
        Remove();
    }

    void Remove()
    {
        if (gone) return;
        gone = true;
        Alive--;
        All.Remove(this);
        if (Kind == EnemyKind.Bulwark || Kind == EnemyKind.Artillery || Kind == EnemyKind.Grabber) BigAlive--;
        if (shield != null) Destroy(shield.gameObject);

        var p = PlayerController.Instance;
        if (p != null && p.GrabberEnemy == this) p.ReleaseGrab();

        Destroy(gameObject);
    }

    // ------------------------------------------------------------ helpers
    void Kick(float power)
    {
        if (anim != null) anim.Kick(power);
    }

    static float toAngleOf(Vector2 v)
    {
        return v.sqrMagnitude > 0.0001f ? Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg : 0f;
    }

    float Turn(Vector2 mv, float fallback, float rate, float dt)
    {
        float want = mv.sqrMagnitude > 0.01f ? toAngleOf(mv) : fallback;
        return Mathf.MoveTowardsAngle(faceAngle, want, rate * dt);
    }

    void Thrust(float amount)
    {
        if (bodyT != null) bodyT.localPosition = (Vector3)(faceDir * amount);
    }

    void Tint(bool on)
    {
        if (bodySr != null) bodySr.color = on ? Color.white : bodyOrig;
    }
}

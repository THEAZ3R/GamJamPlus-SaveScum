using System.Collections.Generic;
using UnityEngine;

// XP, level-ups and every cyber-mod the run has picked up. One component on
// the player: the HUD reads it, and the rest of the game pokes it through the
// small hooks (OnEnemyDamaged / OnEnemyKilled / OnDash / Absorb).
public class PlayerUpgrades : MonoBehaviour
{
    public static PlayerUpgrades Instance;

    public int Level { get { return level; } }
    public float Xp { get { return xp; } }
    public float XpNeed { get { return need; } }
    public int Pending { get { return pending; } }

    readonly Dictionary<UpgradeKind, int> levels = new Dictionary<UpgradeKind, int>();
    float xp, need = 200f;
    int level = 1, pending;

    float shield, shieldMax, shieldBack;       // halo ward
    float orbT, corpseGuardT, regenFxT, healSfxT;    // orbital laser, corpse-chain brake, regen pulse
    float leechFxT, leechRingT;                // astarion: blood mist / pulse throttles
    readonly List<CyberDog> dogs = new List<CyberDog>();

    Transform aura;
    SpriteRenderer auraSr;
    Transform regenAura;                       // push push push: the green haze
    SpriteRenderer regenAuraSr;

    public int LevelOf(UpgradeKind k) { int v; return levels.TryGetValue(k, out v) ? v : 0; }

    // ------------------------------------------------------------ stats
    public float DamageMult { get { return 1f + 0.2f * LevelOf(UpgradeKind.Kinetic); } }
    public float SpeedMult { get { return 1f + 0.12f * LevelOf(UpgradeKind.Synapse); } }
    public float PierceBonus { get { int l = LevelOf(UpgradeKind.PhaseSlug); return l > 0 ? Upgrade.PierceMult(l) : 1f; } }
    public float ArcBonus { get { return Upgrade.ArcMult(LevelOf(UpgradeKind.Arc)); } }
    public float HoundBite { get { return Upgrade.HoundDmg(LevelOf(UpgradeKind.Hound)); } }
    public float SolventDps { get { int l = LevelOf(UpgradeKind.Solvent); return l > 0 ? Upgrade.SolventDps(l) : 0f; } }
    public int Projectiles { get { return Upgrade.TridentShots(LevelOf(UpgradeKind.Trident)); } }
    public int Bounces { get { return Upgrade.RicoBounces(LevelOf(UpgradeKind.Ricochet)); } }
    public int Chains { get { int l = LevelOf(UpgradeKind.Arc); return l > 0 ? Upgrade.ArcChains(l) : 0; } }

    // two mods each come as a pair: pick one and the other is welded shut
    public bool IsLocked(UpgradeKind k)
    {
        if (k == UpgradeKind.Ricochet && LevelOf(UpgradeKind.PhaseSlug) > 0) return true;
        if (k == UpgradeKind.PhaseSlug && LevelOf(UpgradeKind.Ricochet) > 0) return true;
        if (k == UpgradeKind.Vitagraft && LevelOf(UpgradeKind.Synapse) > 0) return true;
        if (k == UpgradeKind.Synapse && LevelOf(UpgradeKind.Vitagraft) > 0) return true;
        return false;
    }

    public BulletKind BulletKind
    {
        get
        {
            if (LevelOf(UpgradeKind.PhaseSlug) > 0) return BulletKind.Pierce;
            if (LevelOf(UpgradeKind.Ricochet) > 0) return BulletKind.Bounce;
            return BulletKind.Normal;
        }
    }

    void Awake()
    {
        Instance = this;
        need = XpFor(1);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------ xp
    // every level costs more than the one before it (200, 268, 344, 428...)
    public static float XpFor(int level)
    {
        float l = Mathf.Max(level, 1) - 1f;
        return 200f + 60f * l + 8f * l * l;
    }

    public static void GainXp(float amount)
    {
        var u = Instance;
        if (u == null || GameManager.IsOver || amount <= 0f) return;

        // TIMELEAP: the opening kills are rich, every leap afterwards pays less
        u.xp += amount * TimeLeap.XpMul;
        while (u.xp >= u.need)
        {
            u.xp -= u.need;
            u.level++;
            u.pending++;
            u.need = XpFor(u.level);
        }

        if (u.pending > 0 && !LevelUpUI.IsOpen && !GameManager.IsOver)
            LevelUpUI.Open(u);
    }

    // SHEPARD'S LAST TRICK: at thirty percent he rewinds the run bare -
    // every module the table ever sold you comes out, and the mirror of you
    // walks in with nothing but the gun you started with
    public void ResetAll()
    {
        levels.Clear();
        pending = 0;
        xp = 0f;
        level = 1;
        need = XpFor(level);

        shield = shieldMax = shieldBack = 0f;
        if (aura != null) aura.gameObject.SetActive(false);
        if (regenAura != null) regenAura.gameObject.SetActive(false);

        orbT = 0f;
        corpseGuardT = 0f;
        Gore.FadingBlood = false;

        for (int i = 0; i < dogs.Count; i++)
            if (dogs[i] != null) Destroy(dogs[i].gameObject);
        dogs.Clear();

        ApplySize(Color.white);                      // the body forgets the meat

        Fx.Burst(transform.position, new Color(0.85f, 0.2f, 0.25f), 30, 9f);
        Fx.Ring(transform.position, new Color(0.85f, 0.2f, 0.25f), 0.5f, 4.5f, 0.5f, 30);
    }

    // called by the level-up table once a card is clicked
    public void PickUpgrade(UpgradeKind k)
    {
        pending = Mathf.Max(0, pending - 1);

        // a maxed module picked off a thin table still spends the pick: it
        // tops the suit off instead of stacking past its limit
        if (Upgrade.IsMaxed(k, LevelOf(k)))
        {
            var ph = GetComponent<Health>();
            if (ph != null && !ph.IsDead && ph.current < ph.max)
                ph.current = Mathf.Min(ph.max, ph.current + ph.max * 0.15f);
            return;
        }

        int lvl = LevelOf(k) + 1;
        levels[k] = lvl;
        Apply(k, lvl);
    }

    void Apply(UpgradeKind k, int lvl)
    {
        switch (k)
        {
            case UpgradeKind.Vitagraft:      // no more full heal: meat, and you fill out the suit
                Health hp = GetComponent<Health>();
                if (hp != null) { hp.max += 25f; hp.current = Mathf.Min(hp.max, hp.current + 25f); }
                ApplySize(Upgrade.Accent(k));
                Fx.Burst(transform.position, Upgrade.Accent(k), 18, 6f);
                break;

            case UpgradeKind.Synapse:        // faster, and the hitbox draws in with it
                ApplySize(Upgrade.Accent(k));
                Fx.Burst(transform.position, Upgrade.Accent(k), 12, 5f);
                break;

            case UpgradeKind.Push:           // the green haze of somebody who keeps knitting
                EnsureRegenAura();
                Fx.Burst(transform.position, Upgrade.Accent(k), 12, 5f);
                break;

            case UpgradeKind.Halo:
                shieldMax = Upgrade.HaloCap(lvl);
                shield = shieldMax;
                shieldBack = 0f;
                EnsureAura();
                Fx.Ring(transform.position, new Color(0.45f, 0.95f, 1f), 0.4f, 2.6f, 0.42f, 28);
                Fx.Sparks(transform.position, Color.white, 10, 8f);
                break;

            case UpgradeKind.Hemotoxin:
                Gore.FadingBlood = true;            // blood stops living on the floor
                Fx.Burst(transform.position, Upgrade.Accent(k), 12, 5f);
                break;

            case UpgradeKind.Hound:
                if (lvl <= 5) SpawnDog();
                Fx.Burst(transform.position, Upgrade.Accent(k), 12, 5f);
                break;

            case UpgradeKind.Orbital:
                orbT = Mathf.Min(orbT, Upgrade.OrbCooldown(lvl));
                Fx.Burst(transform.position, Upgrade.Accent(k), 12, 5f);
                break;

            default:
                Fx.Burst(transform.position, Upgrade.Accent(k), 12, 5f);
                break;
        }
    }

    // ------------------------------------------------------------ halo ward
    public bool Absorb(ref float amount)
    {
        if (shieldMax <= 0f || shield <= 0f || amount <= 0f) return false;

        float eat = Mathf.Min(shield, amount);
        shield -= eat;
        amount -= eat;

        Fx.Ring(transform.position, new Color(0.45f, 0.95f, 1f), 0.5f, 1.7f, 0.22f, 28);
        Fx.Burst(transform.position, new Color(0.6f, 0.96f, 1f), 6, 4f);

        if (shield <= 0f)
        {
            shield = 0f;
            shieldBack = Upgrade.HaloRecharge(LevelOf(UpgradeKind.Halo));
            Fx.Burst(transform.position, new Color(0.45f, 0.95f, 1f), 24, 8f);
            Fx.Sparks(transform.position, Color.white, 16, 12f);
            GameManager.Shake(0.35f);
            if (aura != null) aura.gameObject.SetActive(false);
        }
        return true;
    }

    void EnsureAura()
    {
        if (aura != null) { aura.gameObject.SetActive(true); return; }

        var g = new GameObject("HaloWard");
        g.transform.SetParent(transform, false);
        g.transform.localScale = Vector3.one * 1.55f;
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.AuraRing();
        sr.sortingOrder = 27;                    // over the suit
        aura = g.transform;
        auraSr = sr;
    }

    // PUSH PUSH PUSH: a slow green haze that swells as the trickle deepens
    void EnsureRegenAura()
    {
        if (regenAura != null) { regenAura.gameObject.SetActive(true); return; }

        var g = new GameObject("RegenAura");
        g.transform.SetParent(transform, false);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.AuraRing();
        sr.sortingOrder = 26;                    // under the halo, over the suit
        sr.color = new Color(0.30f, 0.92f, 0.55f, 0.5f);
        regenAura = g.transform;
        regenAuraSr = sr;
    }

    // VITAGRAFT fills the suit out, CELERITY draws it in - the body (and the
    // hitbox inside it) scales with it, capped at double / half size. The new
    // size eases in so you feel the growth instead of it snapping.
    void ApplySize(Color tint)
    {
        float mul = Mathf.Clamp(Mathf.Pow(1.05f, LevelOf(UpgradeKind.Vitagraft)) *
                                Mathf.Pow(0.95f, LevelOf(UpgradeKind.Synapse)), 0.5f, 2.0f);
        var p = PlayerController.Instance;
        if (p != null) p.SetBodyScale(mul, tint);
    }

    // ------------------------------------------------------------ healing
    // PUSH PUSH PUSH (passive trickle) and ASTARION (lifesteal) both land here
    void Heal(float amount, bool blood = false)
    {
        if (amount <= 0f || GameManager.IsOver) return;
        var p = PlayerController.Instance;
        if (p == null) return;
        var hp = p.Health;
        if (hp == null || hp.IsDead) return;

        bool gained = hp.current < hp.max;
        if (gained) hp.current = Mathf.Min(hp.max, hp.current + amount);

        // the wound closing has a voice - throttled so the trickle is not a strobe
        healSfxT -= Time.deltaTime;
        if (gained && healSfxT <= 0f)
        {
            healSfxT = 0.4f;
            Sfx.PlayAt(p.transform.position, "heal", 0.45f, blood ? 1.15f : 1f);
        }

        // ASTARION: the thirst shows even over a full bar - the suit flushes
        // red, mist rises, and a slow pulse rolls off you while you drink
        if (blood)
        {
            p.Flush(new Color(0.95f, 0.30f, 0.35f), 0.28f);
            p.LeechAura();                            // the red super-saiyan burn

            leechFxT -= Time.deltaTime;
            if (leechFxT <= 0f)
            {
                leechFxT = 0.12f;
                Vector2 bp = (Vector2)transform.position + Random.insideUnitCircle * 0.42f;
                Fx.Burst(bp, new Color(0.95f, 0.30f, 0.35f), 4, 3.2f);
            }

            leechRingT -= Time.deltaTime;
            if (leechRingT <= 0f)
            {
                leechRingT = 0.45f;
                Fx.Ring(transform.position, new Color(0.95f, 0.30f, 0.35f), 0.3f, 1.9f, 0.32f, 28);
            }
        }
    }

    // ------------------------------------------------------------ hooks
    // what the installed modules do to the look of a round - colour first,
    // then the weight GUTS puts behind it
    public void BulletLook(BulletKind kind, out Color color, out float size)
    {
        color = new Color(1f, 0.9f, 0.35f);           // the plain yellow pill
        size = 0.20f;

        switch (kind)
        {
            case BulletKind.Bounce:                   // DEAD EYE: gold slugs
                color = Upgrade.Accent(UpgradeKind.Ricochet);
                break;
            case BulletKind.Pierce:                   // FUS RO DAH: cyan lances
                color = Upgrade.Accent(UpgradeKind.PhaseSlug);
                size = 0.22f;
                break;
            default:                                  // paint by what is installed
                if (LevelOf(UpgradeKind.Hemotoxin) > 0) color = Upgrade.Accent(UpgradeKind.Hemotoxin);
                else if (LevelOf(UpgradeKind.Solvent) > 0) color = Upgrade.Accent(UpgradeKind.Solvent);
                else if (LevelOf(UpgradeKind.Arc) > 0) color = Upgrade.Accent(UpgradeKind.Arc);
                break;
        }

        size *= 1f + 0.05f * LevelOf(UpgradeKind.Kinetic);   // GUTS rounds are heavier
        if (size > 0.34f) size = 0.34f;
    }

    // a bullet (or anything else) bit into an enemy
    public void OnEnemyDamaged(Health h, float amount)
    {
        if (h == null || amount <= 0f) return;

        var e = h.GetComponent<Enemy>();
        var boss = e == null ? h.GetComponent<Smasher>() : null;
        if (e == null && boss == null) return;        // walls and props feed nothing

        // ASTARION: drink a cut of every wound you open
        int leech = LevelOf(UpgradeKind.Astarion);
        if (leech > 0) Heal(amount * Upgrade.Leech(leech), true);

        if (boss != null) return;                     // the Smasher: no execute, no shove

        // FUS RO DAH: the slug lands like a shout - it shoves the target back
        int phase = LevelOf(UpgradeKind.PhaseSlug);
        if (phase > 0)
        {
            Vector2 away = (Vector2)h.transform.position - (Vector2)transform.position;
            if (away.sqrMagnitude > 0.0001f)
            {
                e.Push(away.normalized, 5.5f + 1.5f * phase);
                Fx.Ring(h.transform.position, Upgrade.Accent(UpgradeKind.PhaseSlug), 0.3f, 1.5f, 0.22f, 30);
            }
        }

        // 225: anything dropped under the execute line dies right now
        int exe = LevelOf(UpgradeKind.Dragon);
        if (exe > 0 && !h.IsDead && h.current <= h.max * Upgrade.ExecuteAt(exe))
        {
            Fx.Executed(h.transform.position, Upgrade.Accent(UpgradeKind.Dragon));
            Sfx.PlayAt(h.transform.position, "execute", 0.75f);
            GameManager.Shake(0.2f);
            h.Kill();
            return;
        }

        // BLOODTINGE: the first bite of the burn is marked in red
        int lvl = LevelOf(UpgradeKind.Hemotoxin);
        if (lvl <= 0 || !e.BleedsRed) return;       // only red-blooded flesh burns
        if (!e.Burning)
        {
            Fx.Ring(h.transform.position, new Color(0.9f, 0.2f, 0.25f), 0.25f, 1.3f, 0.25f, 30);
            Fx.Burst(h.transform.position, new Color(0.9f, 0.2f, 0.25f), 6, 4f);
        }
        e.ApplyToxin(Upgrade.HemoDps(lvl), 3.2f);
    }

    public void OnEnemyKilled(Vector2 pos, Enemy e)
    {
        if (e == null) return;
        GainXp(e.XpValue);

        // corpse charge: the body goes off. Guarded so the blast cannot chain
        // into an infinite chain reaction of exploding corpses.
        int corpse = LevelOf(UpgradeKind.Corpse);
        if (corpse > 0 && corpseGuardT <= 0f)
        {
            corpseGuardT = 0.05f;
            float r = Upgrade.CorpseRadius(corpse);
            Fx.Explosion(pos, new Color(1f, 0.55f, 0.12f), new Color(1f, 0.9f, 0.4f), 0.65f + 0.08f * corpse);
            Fx.Ring(pos, Upgrade.Accent(UpgradeKind.Corpse), 0.4f, r * 2f, 0.35f);
            Damage(pos, r, Upgrade.CorpseDmg(corpse), false);
        }

        // hemotoxin: the blood splash burns the red-blooded
        int hemo = LevelOf(UpgradeKind.Hemotoxin);
        if (hemo > 0 && e.BleedsRed)
        {
            Damage(pos, 2.2f, Upgrade.HemoSplash(hemo), true);
            Fx.Burst(pos, new Color(0.9f, 0.2f, 0.25f), 14, 6f);
        }
    }

    // damage everything in a radius except the player; redOnly = only things
    // that bleed red
    static void Damage(Vector2 pos, float radius, float amount, bool redOnly)
    {
        var hits = Physics2D.OverlapCircleAll(pos, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].isTrigger || hits[i].CompareTag("Player")) continue;
            var hp = hits[i].GetComponent<Health>();
            if (hp == null) continue;
            if (redOnly)
            {
                var e = hits[i].GetComponent<Enemy>();
                if (e == null || !e.BleedsRed) continue;
            }
            hp.TakeDamage(amount, ((Vector2)hits[i].transform.position - pos).normalized);
        }
    }

    // a dash with a chrono anchor installed
    public void OnDash(Vector2 pos)
    {
        int lvl = LevelOf(UpgradeKind.Chrono);
        if (lvl <= 0) return;
        DashAnchor.Create(pos, Upgrade.ChronoLife(lvl), Upgrade.ChronoBurst(lvl), Upgrade.ChronoBurstRadius(lvl));
    }

    // ------------------------------------------------------------ dogs
    void SpawnDog()
    {
        Vector2 p = transform.position;
        for (int i = 0; i < 14; i++)
        {
            Vector2 s = p + Random.insideUnitCircle.normalized * Random.Range(1.2f, 2.6f);
            if (Pit.Inside(s) || !Nav.IsFree(s, false)) continue;
            var d = CyberDog.Create(s);
            if (d != null) { dogs.Add(d); return; }
        }
    }

    // ------------------------------------------------------------ tick
    void Update()
    {
        float dt = Time.deltaTime;

        if (corpseGuardT > 0f) corpseGuardT -= dt;

        // PUSH PUSH PUSH: a slow, steady trickle of hp back (1 -> 5 per sec)
        int regen = LevelOf(UpgradeKind.Push);
        if (regen > 0 && !GameManager.IsOver)
        {
            Heal(Upgrade.RegenRate(regen) * dt);

            regenFxT -= dt;
            if (regenFxT <= 0f)
            {
                regenFxT = 1f;
                var me = PlayerController.Instance;
                if (me != null && me.Health != null && me.Health.current < me.Health.max)
                    Fx.Burst(transform.position, Upgrade.Accent(UpgradeKind.Push), 3, 2.4f);
            }

            // the haze itself: swells and settles like breathing, wider per level
            if (regenAura != null)
            {
                float t = Mathf.PingPong(Time.time * 0.8f, 1f);
                regenAura.localScale = Vector3.one * (1.15f + 0.14f * regen + t * 0.22f);
                regenAura.Rotate(0, 0, 40f * dt);
                regenAuraSr.color = new Color(0.30f, 0.92f, 0.55f, 0.30f + 0.22f * (1f - t));
            }
        }

        // halo ward: spins while it holds, recharges after it shatters
        if (shieldMax > 0f && aura != null)
        {
            if (shield > 0f)
            {
                aura.Rotate(0, 0, -130f * dt);
                auraSr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 1f, shield / shieldMax));
            }
            else
            {
                shieldBack -= dt;
                if (shieldBack <= 0f)
                {
                    shield = shieldMax;
                    aura.gameObject.SetActive(true);
                    Fx.Ring(transform.position, new Color(0.45f, 0.95f, 1f), 0.4f, 2.6f, 0.45f, 28);
                    Fx.Burst(transform.position, new Color(0.6f, 0.96f, 1f), 12, 6f);
                }
            }
        }

        // orbital specter
        int orb = LevelOf(UpgradeKind.Orbital);
        if (orb > 0 && !GameManager.IsOver)
        {
            orbT -= dt;
            if (orbT <= 0f)
            {
                var live = new List<Enemy>();
                for (int i = 0; i < Enemy.All.Count; i++)
                    if (Enemy.All[i] != null) live.Add(Enemy.All[i]);

                if (live.Count == 0) orbT = 1f;              // nothing to shoot: look again soon
                else
                {
                    Enemy e = live[Random.Range(0, live.Count)];
                    OrbitalBeam.Lock(e.transform.position, e.transform, Upgrade.OrbDamage(orb));
                    orbT = Upgrade.OrbCooldown(orb);
                }
            }
        }

        for (int i = dogs.Count - 1; i >= 0; i--)
            if (dogs[i] == null) dogs.RemoveAt(i);
    }
}

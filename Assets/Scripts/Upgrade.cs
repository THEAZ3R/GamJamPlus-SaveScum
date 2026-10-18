using UnityEngine;

public enum UpgradeKind
{
    Vitagraft, Kinetic, Synapse, Halo, Trident, Corpse, Hemotoxin,
    Ricochet, PhaseSlug, Chrono, Solvent, Arc, Hound, Orbital,
    Push, Dragon, Astarion
}

// The catalogue of cyber-mods. Every entry has a name, the two colours its
// icon is drawn in, a level cap, and a description that rewrites itself once
// the mod converts or tops out (the dog pack, the chain, the laser...).
public static class Upgrade
{
    public static readonly int Count = 17;

    public static string Name(UpgradeKind k)
    {
        switch (k)
        {
            case UpgradeKind.Kinetic:   return "SANDEVISTAN OVERCLOCK";
            case UpgradeKind.Synapse:   return "CELERITY";
            case UpgradeKind.Halo:      return "QUEN MATRIX";
            case UpgradeKind.Trident:   return "GUTS";
            case UpgradeKind.Corpse:    return "BLOODY MESS";
            case UpgradeKind.Hemotoxin: return "BLOODTINGE.EXE";
            case UpgradeKind.Ricochet:  return "DEAD EYE";
            case UpgradeKind.PhaseSlug: return "FUS RO DAH";
            case UpgradeKind.Chrono:    return "PORTAL GUN";
            case UpgradeKind.Solvent:   return "THE INCREDIBLE GASSY";
            case UpgradeKind.Arc:       return "MAZIODYNE";
            case UpgradeKind.Hound:     return "DOGMEAT";
            case UpgradeKind.Orbital:   return "HELIOS' JUDGMENT";
            case UpgradeKind.Push:      return "PUSH PUSH PUSH";
            case UpgradeKind.Dragon:    return "225";
            case UpgradeKind.Astarion:  return "ASTARION";
            default:                    return "BLOOD PUMP";
        }
    }

    public static Color Accent(UpgradeKind k)
    {
        switch (k)
        {
            case UpgradeKind.Kinetic:   return C(1f, 0.74f, 0.16f);
            case UpgradeKind.Synapse:   return C(0.30f, 0.95f, 1f);
            case UpgradeKind.Halo:      return C(0.55f, 0.92f, 1f);
            case UpgradeKind.Trident:   return C(1f, 0.55f, 0.12f);
            case UpgradeKind.Corpse:    return C(1f, 0.42f, 0.10f);
            case UpgradeKind.Hemotoxin: return C(0.90f, 0.13f, 0.18f);
            case UpgradeKind.Ricochet:  return C(1f, 0.86f, 0.20f);
            case UpgradeKind.PhaseSlug: return C(0.42f, 0.90f, 1f);
            case UpgradeKind.Chrono:    return C(0.80f, 0.46f, 1f);
            case UpgradeKind.Solvent:   return C(0.50f, 1f, 0.25f);
            case UpgradeKind.Arc:       return C(0.60f, 0.86f, 1f);
            case UpgradeKind.Hound:     return C(0.35f, 0.90f, 1f);
            case UpgradeKind.Orbital:   return C(1f, 0.42f, 0.62f);
            case UpgradeKind.Push:      return C(0.30f, 0.92f, 0.55f);
            case UpgradeKind.Dragon:    return C(0.70f, 0.40f, 1f);
            case UpgradeKind.Astarion:  return C(0.88f, 0.84f, 0.98f);
            default:                    return C(0.95f, 0.24f, 0.24f);
        }
    }

    public static Color Second(UpgradeKind k)
    {
        if (k == UpgradeKind.Hemotoxin) return C(0.45f, 1f, 0.40f);   // green toxin in the drop
        if (k == UpgradeKind.Hound)     return C(0.75f, 1f, 1f);      // visor glow
        if (k == UpgradeKind.Solvent)   return C(0.72f, 1f, 0.35f);
        if (k == UpgradeKind.Astarion)  return C(0.95f, 0.30f, 0.35f);   // blood in the mouth
        return Color.Lerp(Accent(k), Color.white, 0.35f);
    }

    // 99 = effectively endless (the three commons keep the pool alive forever)
    public static int MaxLevel(UpgradeKind k)
    {
        switch (k)
        {
            case UpgradeKind.Vitagraft:
            case UpgradeKind.Kinetic:
            case UpgradeKind.Synapse:   return 99;
            case UpgradeKind.Halo:
            case UpgradeKind.Trident:
            case UpgradeKind.Corpse:
            case UpgradeKind.Hemotoxin:
            case UpgradeKind.Solvent:
            case UpgradeKind.Push:      // 1 hp/s per level, tops out at 5
            case UpgradeKind.Astarion:  return 5;   // 5% lifesteal per level, up to 25%
            case UpgradeKind.Dragon:    return 6;   // 5% execute per level, up to 30%
            case UpgradeKind.Ricochet:
            case UpgradeKind.PhaseSlug:
            case UpgradeKind.Chrono:    return 3;
            case UpgradeKind.Arc:       return 8;
            default:                    return 10;   // Hound, Orbital
        }
    }

    public static bool IsMaxed(UpgradeKind k, int lvl) { return lvl >= MaxLevel(k); }

    // ------------------------------------------------------------ numbers
    // `lvl` always means "levels you already own", so the helpers describe
    // what the run currently has.
    public static float HaloCap(int lvl)          { return 40f * Mathf.Max(lvl, 0); }
    public static float HaloRecharge(int lvl)     { return Mathf.Max(4f, 6.5f - 0.5f * lvl); }

    public static int   TridentShots(int lvl)     { return Mathf.Min(5, Mathf.Max(lvl, 0) + 1); }

    public static float CorpseDmg(int lvl)        { return 22f + 14f * lvl; }
    public static float CorpseRadius(int lvl)     { return 1.7f + 0.35f * lvl; }

    public static float HemoDps(int lvl)          { return 10f + 6f * lvl; }
    public static float HemoSplash(int lvl)       { return 14f + 10f * lvl; }

    public static int   RicoBounces(int lvl)      { return 3 + Mathf.Max(lvl, 1); }
    public static float PierceMult(int lvl)       { return 1f + 0.25f * lvl; }

    public static float ChronoLife(int lvl)       { return 6f + 3f * lvl; }
    // the blink landing wrecks everything around it (level 2 and up)
    public static float ChronoBurst(int lvl)      { return lvl >= 2 ? 70f + 60f * (lvl - 1) : 0f; }
    public static float ChronoBurstRadius(int lvl){ return lvl >= 2 ? 2.2f + 0.3f * (lvl - 2) : 0f; }
    // ... and buys you a few seconds nothing can touch, stun or grab (level 2+)
    public static float ChronoGuard(int lvl)      { return lvl >= 2 ? 2f + 0.5f * (lvl - 1) : 0f; }

    // how often this mod shows up in the level-up roll (1 = common)
    public static float Rarity(UpgradeKind k)
    {
        if (k == UpgradeKind.Chrono) return 0.4f;   // the portal gun is a rare find
        return 1f;
    }

    public static float SolventDps(int lvl)       { return 5f + 4f * lvl; }   // the wake bites softer now

    public static int   ArcChains(int lvl)        { return Mathf.Min(lvl + 2, 6); }
    public static float ArcMult(int lvl)          { return lvl > 4 ? 1f + 0.15f * (lvl - 4) : 1f; }

    public static int   HoundDogs(int lvl)        { return Mathf.Min(lvl, 5); }
    public static float HoundDmg(int lvl)         { return 10f + 6f * Mathf.Max(0, lvl - 5); }

    public static float OrbCooldown(int lvl)      { return Mathf.Max(7.5f, 14f - 1.5f * lvl); }
    public static float OrbDamage(int lvl)        { return 60f + 40f * Mathf.Max(0, lvl - 5); }

    // PUSH PUSH PUSH: slow passive regeneration, 1 hp/s per level (max 5)
    public static float RegenRate(int lvl)        { return Mathf.Clamp(lvl, 0, 5); }
    // 225: enemies at or under this fraction of max hp are executed (5% per level, cap 30%)
    public static float ExecuteAt(int lvl)        { return Mathf.Min(0.30f, 0.05f * Mathf.Max(lvl, 0)); }
    // ASTARION: share of the damage you deal that comes back as hp (5% per level, cap 25%)
    public static float Leech(int lvl)            { return Mathf.Min(0.25f, 0.05f * Mathf.Max(lvl, 0)); }

    // ------------------------------------------------------------ flavour
    // what picking this mod RIGHT NOW would do (lvl = levels you own now)
    public static string Desc(UpgradeKind k, int lvl)
    {
        switch (k)
        {
            case UpgradeKind.Vitagraft:
                return "+25 max HP (+25 now) and +5% body size (200% cap). (Locks out celerity)";

            case UpgradeKind.Kinetic:
                return "+20% bullet damage.";

            case UpgradeKind.Synapse:
                return "+12% move speed and -5% body size (50% floor, smaller hitbox). (Locks out blood pump)";

            case UpgradeKind.Halo:
                return lvl <= 0
                    ? "Spin up a hex shield that eats " + (int)HaloCap(1) + " dmg before it shatters."
                    : "Shield holds " + (int)(HaloCap(lvl + 1) - HaloCap(lvl)) +
                      " more dmg (next: " + (int)HaloCap(lvl + 1) + ") and recharges faster.";

            case UpgradeKind.Trident:
                return TridentShots(lvl + 1) >= 5
                    ? "5 projectiles in a fan - the array is complete."
                    : TridentShots(lvl + 1) + " projectiles in a fan (up to 5).";

            case UpgradeKind.Corpse:
                return lvl >= MaxLevel(k)
                    ? "MAX: kills erupt for " + (int)CorpseDmg(lvl) + " dmg in " + CorpseRadius(lvl).ToString("0.0") + "m."
                    : "Kills detonate for " + (int)CorpseDmg(lvl + 1) + " dmg in " +
                      CorpseRadius(lvl + 1).ToString("0.0") + "m around them.";

            case UpgradeKind.Hemotoxin:
                return lvl >= MaxLevel(k)
                    ? "MAX: red blood boils for " + (int)HemoDps(lvl) + " dps + a " + (int)HemoSplash(lvl) + " dmg splash."
                    : "Red blood burns red-blooded enemies: " + (int)HemoDps(lvl + 1) +
                      " dps on hit, " + (int)HemoSplash(lvl + 1) + " dmg splash on death.";

            case UpgradeKind.Ricochet:
                return "Rounds bounce off walls " + RicoBounces(lvl + 1) + " times. (Locks out phase-slugs)";

            case UpgradeKind.PhaseSlug:
                return "Bullets pierce targets and walls, +" + (int)(PierceMult(lvl + 1) * 100f - 100f) +
                      "% dmg. (Locks out ricochet)";

            case UpgradeKind.Chrono:
                if (lvl <= 0) return "Dashes leave a portal for " + (int)ChronoLife(1) + "s - shoot it to blink back.";
                if (lvl >= MaxLevel(k)) return "MAX: the blink landing erupts for " + (int)ChronoBurst(lvl) +
                                               " dmg and shields you for " + ChronoGuard(lvl).ToString("0.0") + "s.";
                return "The blink landing erupts for " + (int)ChronoBurst(lvl + 1) + " dmg and shields you for " +
                       ChronoGuard(lvl + 1).ToString("0.0") + "s.";

            case UpgradeKind.Solvent:
                return "You drag an acid wake behind you: it burns for " + (int)SolventDps(lvl + 1) + " dps.";

            case UpgradeKind.Arc:
                if (lvl >= 4 && lvl < MaxLevel(k)) return "MAX chains: further picks grant +15% damage.";
                if (lvl >= MaxLevel(k)) return "MAX: chains " + ArcChains(lvl) + " enemies, +" +
                                              (int)(ArcMult(lvl) * 100f - 100f) + "% damage.";
                return "Hits jump to " + ArcChains(lvl + 1) + " nearby enemies.";

            case UpgradeKind.Hound:
                return lvl < 5
                    ? "+1 cyberdog companion (pack of " + (lvl + 1) + "/5) - they lunge and bite."
                    : "+6 bite damage to every cyberdog. (Pack full)";

            case UpgradeKind.Push:
                if (lvl <= 0) return "Push push push: passively recover " + (int)RegenRate(1) + " HP per second.";
                if (lvl >= MaxLevel(k)) return "MAX: passively recover " + (int)RegenRate(lvl) + " HP per second.";
                return "+1 HP per second of passive regen (now " + (int)RegenRate(lvl + 1) + " HP/s).";

            case UpgradeKind.Dragon:
                if (lvl <= 0) return "Enemies at " + (int)(ExecuteAt(1) * 100f) +
                                     "% HP or below are executed on hit.";
                if (lvl >= MaxLevel(k)) return "MAX: anything at " + (int)(ExecuteAt(lvl) * 100f) +
                                               "% HP or below dies on hit.";
                return "Execute threshold +5% (now " + (int)(ExecuteAt(lvl + 1) * 100f) +
                       "% HP or below).";

            case UpgradeKind.Astarion:
                if (lvl <= 0) return "Thirst: heal for " + (int)(Leech(1) * 100f) + "% of the damage you deal.";
                if (lvl >= MaxLevel(k)) return "MAX: heal for " + (int)(Leech(lvl) * 100f) + "% of the damage you deal.";
                return "+5% lifesteal (now " + (int)(Leech(lvl + 1) * 100f) + "% of damage dealt).";

            default: // Orbital
                return lvl < 4
                    ? "Orbital laser fires every " + OrbCooldown(lvl + 1).ToString("0.0") + "s - pick again to charge it faster."
                    : "MAX rate: +40% laser damage (now " + (int)OrbDamage(lvl + 1) + ").";
        }
    }

    static Color C(float r, float g, float b) { return new Color(r, g, b); }
}

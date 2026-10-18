using System.Collections.Generic;
using UnityEngine;

// Every sprite in the game, drawn as text grids. Characters map to palette
// colors per sprite, so the same drawing is reused with different palettes
// (player blue/white, enemies gunmetal + green, etc).
public static class Art
{
    public static Color C(float r, float g, float b) { return new Color(r, g, b); }

    static Dictionary<char, Color> Pal(params object[] kv)
    {
        var d = new Dictionary<char, Color>();
        for (int i = 0; i + 1 < kv.Length; i += 2) d[(char)kv[i]] = (Color)kv[i + 1];
        return d;
    }

    // per-unit colours: what the unit is made of (also tints its gore and the
    // body parts it explodes into). One colour per character archetype:
    // astronaut / soldier / ninja / engineer / robot / white armour / samurai.
    public static readonly Color PlayerFlesh = C(0.20f, 0.34f, 0.62f);
    public static readonly Color ShooterFlesh = C(0.28f, 0.34f, 0.20f);
    public static readonly Color StabberFlesh = C(0.09f, 0.09f, 0.11f);
    public static readonly Color PiercerFlesh = C(0.07f, 0.07f, 0.09f);
    public static readonly Color BouncerFlesh = C(0.84f, 0.86f, 0.90f);
    public static readonly Color TrapperFlesh = C(0.85f, 0.58f, 0.10f);
    public static readonly Color DasherFlesh  = C(0.72f, 0.12f, 0.12f);
    public static readonly Color SpiderFlesh  = C(0.22f, 0.25f, 0.20f);
    public static readonly Color MechFlesh    = C(0.26f, 0.29f, 0.34f);
    public static readonly Color GrabberFlesh = C(0.13f, 0.10f, 0.17f);

    // ============================================================ PALETTES
    static readonly Dictionary<char, Color> PlayerPal = Pal(
        'k', C(0.05f, 0.06f, 0.10f), 'H', C(0.86f, 0.90f, 0.95f),
        'm', PlayerFlesh, 'd', C(0.13f, 0.22f, 0.40f),
        'a', C(0.25f, 0.90f, 1.00f), 'v', C(0.15f, 0.75f, 0.95f),
        'g', C(0.28f, 0.30f, 0.36f), 'w', C(0.95f, 0.97f, 1.00f));

    static Dictionary<char, Color> EnemyPal(Color accent, Color body)
    {
        return Pal(
            'k', C(0.03f, 0.04f, 0.05f), 'H', Color.Lerp(body, Color.white, 0.18f),
            'm', body, 'd', Color.Lerp(body, Color.black, 0.45f),
            'a', accent, 'v', accent,
            'g', C(0.22f, 0.24f, 0.28f), 'w', C(0.88f, 0.94f, 0.90f),
            'r', C(1f, 0.25f, 0.20f));
    }

    static readonly Dictionary<char, Color> MechPal = Pal(
        'k', C(0.03f, 0.04f, 0.05f), 'd', C(0.14f, 0.17f, 0.21f),
        'g', C(0.26f, 0.29f, 0.34f), 'a', C(0.35f, 1f, 0.45f),
        'w', C(0.85f, 0.95f, 0.88f));

    // ============================================================ HUMANOIDS
    // Every unit is assembled from separate head / torso / arm / weapon / leg
    // stamps on a 20x20 canvas (facing right), so you can always read where the
    // head, hands, gun and feet are - and every type is a different character:
    // astronaut, soldier, cyber ninja, engineer, robot sniper, armoured jumper,
    // cyber samurai.
    //
    //   rows 0-5    head        8 wide  at x=5
    //   rows 6-10   torso      10 wide  at x=4
    //   rows 7-10   back arm    4 wide  at x=2
    //   rows 7-11   front arm   6 wide  at x=11  (the hand grips the weapon)
    //   rows wy..   weapon      at (wx,wy)
    //   rows 11-19  legs       10 wide  at x=3

    // ---------------------------------------------------------- arms
    static readonly string[] BackArm =
    {
        ".kk.",
        "kddk",
        "kddk",
        ".kk.",
    };

    static readonly string[] FrontArm =
    {
        ".kkk..",
        "kmmmk.",
        "kmmmk.",
        "kmmmkk",
        ".kkkk.",
    };

    // ---------------------------------------------------------- heads
    static readonly string[] HeadAstro =           // player: sealed bubble helmet
    {
        "..kkkk..",
        ".kHHHHk.",
        "kHHvvvvk",
        "kHvvvvHk",
        "kHHHHHHk",
        ".kddddk.",
    };

    static readonly string[] HeadSoldier =         // shooter: helmet + face
    {
        "..kkkk..",
        ".kHHHHk.",
        "kkHHHHkk",
        "ksvssvsk",
        ".kssssk.",
        "..kddk..",
    };

    static readonly string[] HeadNinja =           // stabber: hood + neon visor
    {
        ".kk.....",
        "..kkkk..",
        ".kvvvvk.",
        "kvvvvvvk",
        "kHHHHHHk",
        ".kddddk.",
    };

    static readonly string[] HeadHardHat =         // trapper: engineer hard hat
    {
        "...kkk..",
        ".kHHHHk.",
        "kkkkkkkk",
        "ksvssvsk",
        ".kssssk.",
        "..kddk..",
    };

    static readonly string[] HeadRobot =           // piercer: camera head + lens
    {
        "....k...",
        ".kkkkkk.",
        "kddddddk",
        "kdvveevk",
        "kddddddk",
        ".kkkkkk.",
    };

    static readonly string[] HeadDome =            // bouncer: round armoured helm
    {
        "...kk...",
        "..kHHk..",
        ".kHHHHk.",
        ".kvHHvk.",
        ".kHHHHk.",
        "..kddk..",
    };

    static readonly string[] HeadKabuto =          // dasher: samurai helm + horns
    {
        "k......k",
        ".kkkkkk.",
        "kHHHHHHk",
        "kHvvvvHk",
        "kddddddk",
        ".kddddk.",
    };

    // ---------------------------------------------------------- torsos
    static readonly string[] TorsoSuit =           // player: astronaut suit
    {
        "..kkmmkk..",
        ".kmmmmmmk.",
        ".kdaaaamk.",
        ".kmmmmmmk.",
        "..kmmmmk..",
    };

    static readonly string[] TorsoSoldier =
    {
        "..kkmmkk..",
        ".kmmmmmmk.",
        ".kdmaamdk.",
        ".kmmmmmmk.",
        "..kmmmmk..",
    };

    static readonly string[] TorsoNinja =
    {
        "...kmmk...",
        ".kmmmmmmk.",
        ".kdmmmamk.",
        ".kmmmmmmk.",
        "...kmmk...",
    };

    static readonly string[] TorsoEngineer =
    {
        ".kkmmmmkk.",
        "kmmmmmmmmk",
        "kdmmmmaamk",
        "kmmmmmmmmk",
        ".kddddddk.",
    };

    static readonly string[] TorsoRobot =
    {
        ".kkkkkkkk.",
        "kmmddmmmmk",
        "kmaaaaamdk",
        "kmmddmmmmk",
        ".kkkkkkkk.",
    };

    static readonly string[] TorsoArmour =
    {
        "kkkmmmmkkk",
        "kmmmmmmmmk",
        "kdmmmaaamk",
        "kmmmmmmmmk",
        ".kddddddk.",
    };

    static readonly string[] TorsoSamurai =
    {
        "..kkmmkk..",
        ".kmmmamkk.",
        ".kmmmaamk.",
        ".kmmmamkk.",
        "..kmmmmk..",
    };

    // ---------------------------------------------------------- legs
    static readonly string[] LegsStandard =
    {
        ".kdd..ddk.",
        ".kdd..ddk.",
        ".kdd..ddk.",
        ".kdd..ddk.",
        ".kdd..ddk.",
        ".kdd..ddk.",
        ".kdd..ddk.",
        "kkddkkddkk",
        "kwwwk.wwwk",
    };

    static readonly string[] LegsThin =
    {
        ".kd...dk..",
        ".kd...dk..",
        ".kd...dk..",
        ".kd...dk..",
        ".kd...dk..",
        ".kd...dk..",
        ".kd...dk..",
        "kkdk.kkdk.",
        "kwwk.kwwk.",
    };

    static readonly string[] LegsWide =
    {
        ".kddd.dddk",
        ".kddd.dddk",
        ".kddd.dddk",
        ".kddd.dddk",
        ".kddd.dddk",
        ".kddd.dddk",
        ".kddd.dddk",
        "kkddk.kddk",
        "kwwwk.kwwk",
    };

    // ---------------------------------------------------------- weapons
    // a fist closed around the grip, drawn on top of the weapon so the hands
    // are always readable
    static readonly string[] GripHand =
    {
        "kkk",
        "kss",
        "kss",
        "kkk",
    };

    static readonly string[] WpnRifle =
    {
        "..kkkk..",
        ".kggggk.",
        ".kaaaggk",
        ".kkkkkk.",
    };

    static readonly string[] WpnSniper =           // long thin barrel + scope
    {
        "k.......",
        "kgggggk.",
        "kgaaaggk",
        ".kkkkkk.",
    };

    static readonly string[] WpnBlade =            // ninja short blade
    {
        "..kkk...",
        ".kgwwwk.",
        "...kkk..",
    };

    static readonly string[] WpnKatana =           // samurai katana
    {
        "k.......",
        "kgwwwwk.",
        ".kwwwk..",
        "..kkk...",
        "...k....",
    };

    static readonly string[] WpnLauncher =         // bouncer's round pod
    {
        "..kkk..",
        ".kgggk.",
        "kggaggk",
        "kggaggk",
        ".kgggk.",
        "..kkk..",
    };

    static readonly string[] WpnCannon =           // engineer's trap cannon
    {
        "..kkkk..",
        ".kggggk.",
        "kggaaggk",
        ".kggggk.",
        "..kkkk..",
    };

    // body cloth/armour, trim (visor/neon), skin, eye glow
    static Dictionary<char, Color> UnitPal(Color body, Color trim, Color skin, Color eye)
    {
        return Pal(
            'k', C(0.03f, 0.04f, 0.05f),
            'm', body, 'd', Color.Lerp(body, Color.black, 0.5f),
            'H', Color.Lerp(body, Color.white, 0.28f),
            'g', C(0.34f, 0.37f, 0.43f), 'w', C(0.93f, 0.95f, 0.98f),
            's', skin, 'v', trim, 'a', trim, 'e', eye);
    }

    // ---------------------------------------------------------- 2-way units
    // Pokemon-style: the body is drawn standing upright in the world and
    // never spins. Only two views - the face (shown down, and mirrored for
    // left/right) and the back of the head (up). The weapon is a separate
    // sprite that rides the body and points wherever the unit aims, so no
    // pose in the game is ever a flat side profile.
    public struct UnitArt
    {
        public Sprite down, up;
        public Sprite gun;               // drawn pointing right
        public float gunAx, gunAy;       // orbit radius: across / up-down
    }

    // the same helmet seen from behind: trim and faces go dark, skin goes cloth
    static string[] BackOf(string[] head)
    {
        var r = new string[head.Length];
        for (int y = 0; y < head.Length; y++)
        {
            var sb = new System.Text.StringBuilder(head[y]);
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                if (c == 'v' || c == 'a' || c == 'e' || c == 'g') sb[i] = 'd';
                else if (c == 's' || c == 'r') sb[i] = 'm';
                else if (c == 'w') sb[i] = 'H';
            }
            r[y] = sb.ToString();
        }
        return r;
    }

    static UnitArt Unit3(string key, Dictionary<char, Color> pal,
                         string[] head, string[] torso, string[] legs,
                         string[] weapon, int wx, int wy)
    {
        // facing the camera: both arms down at the sides, hands empty - the
        // weapon is drawn separately so it can swing with the aim
        var down = PixelArt.Get(key + "_d", PixelArt.Compose(20, 20,
            PixelArt.At(head, 5, 0),
            PixelArt.At(BackArm, 2, 7),
            PixelArt.At(PixelArt.MirrorH(BackArm), 12, 7),
            PixelArt.At(torso, 4, 6),
            PixelArt.At(legs, 3, 11)), pal);

        // walking away: same body, the back of the head
        var up = PixelArt.Get(key + "_u", PixelArt.Compose(20, 20,
            PixelArt.At(BackOf(head), 5, 0),
            PixelArt.At(BackArm, 2, 7),
            PixelArt.At(PixelArt.MirrorH(BackArm), 12, 7),
            PixelArt.At(torso, 4, 6),
            PixelArt.At(legs, 3, 11)), pal);

        var a = new UnitArt();
        a.down = down; a.up = up;
        a.gun = PixelArt.Get(key + "_g", weapon, pal);

        // where the weapon used to be held, kept as an orbit radius around the
        // body's centre: wide enough across that the muzzle clears the arms,
        // a little further up and down so it clears the head and the feet
        int ww = 0;
        for (int i = 0; i < weapon.Length; i++) ww = Mathf.Max(ww, weapon[i].Length);
        a.gunAx = (wx + ww * 0.5f - 10f) / 16f;      // sprite pivot is centred, ppu 16
        a.gunAy = a.gunAx * 1.4f;
        return a;
    }

    public static UnitArt Player()                 // astronaut, blue suit
    { return Unit3("u_player", UnitPal(PlayerFlesh, C(0.25f, 0.90f, 1f), Color.white, Color.white),
                   HeadAstro, TorsoSuit, LegsStandard, WpnRifle, 12, 8); }

    // SHEPARD: your own model, worn by something else - same suit, same
    // helmet, same rifle, every color traded for red
    public static UnitArt Shepard()
    { return Unit3("u_shepard", UnitPal(C(0.80f, 0.50f, 0.40f), C(0.82f, 0.10f, 0.10f),
                                        C(0.34f, 0.05f, 0.07f), C(1f, 0.45f, 0.35f)),
                   HeadAstro, TorsoSuit, LegsStandard, WpnRifle, 12, 8); }

    public static UnitArt Shooter()                // soldier in fatigues + rifle
    { return Unit3("u_shoot", UnitPal(ShooterFlesh, C(0.62f, 0.55f, 0.32f), C(0.85f, 0.65f, 0.48f), C(0.95f, 0.85f, 0.4f)),
                   HeadSoldier, TorsoSoldier, LegsStandard, WpnRifle, 12, 8); }

    public static UnitArt Stabber()                // cyber ninja, black + red neon
    { return Unit3("u_stab", UnitPal(StabberFlesh, C(1f, 0.18f, 0.22f), C(0.15f, 0.15f, 0.18f), C(1f, 0.25f, 0.3f)),
                   HeadNinja, TorsoNinja, LegsThin, WpnBlade, 12, 9); }

    public static UnitArt Trapper()                // yellow engineer
    { return Unit3("u_trap", UnitPal(TrapperFlesh, C(0.98f, 0.85f, 0.25f), C(0.82f, 0.60f, 0.42f), C(0.2f, 0.2f, 0.2f)),
                   HeadHardHat, TorsoEngineer, LegsStandard, WpnCannon, 12, 8); }

    public static UnitArt Piercer()                // robot sniper, black + cyan lens
    { return Unit3("u_pierce", UnitPal(PiercerFlesh, C(0.25f, 0.85f, 1f), C(0.35f, 0.38f, 0.42f), C(0.4f, 0.95f, 1f)),
                   HeadRobot, TorsoRobot, LegsStandard, WpnSniper, 12, 8); }

    public static UnitArt Bouncer()                // white armoured jumper + pod
    { return Unit3("u_bounce", UnitPal(BouncerFlesh, C(0.90f, 0.18f, 0.18f), Color.white, C(1f, 0.3f, 0.25f)),
                   HeadDome, TorsoArmour, LegsWide, WpnLauncher, 12, 7); }

    public static UnitArt Dasher()                 // cyber samurai, red + katana
    { return Unit3("u_dash", UnitPal(DasherFlesh, C(0.95f, 0.90f, 0.88f), C(0.85f, 0.62f, 0.5f), C(1f, 0.35f, 0.25f)),
                   HeadKabuto, TorsoSamurai, LegsThin, WpnKatana, 12, 8); }

    // ============================================================ SPIDER
    static readonly string[] SpiderLegUp =
    {
        "..k.",
        ".kk.",
        "kkg.",
        "kkg.",
        ".kk.",
        "..k.",
    };

    static readonly string[] SpiderBody =
    {
        "...kkkkkk...",
        "..kmmmmmmk..",
        ".kmmmmmmmmk.",
        ".kmmrrmmmmk.",
        ".kmmrrmmmmk.",
        ".kmmmmmmmmk.",
        ".kmmmmmmmmk.",
        "..kmmmmmmk..",
        "...kkkkkk...",
        "....kkkk....",
    };

    static readonly string[] SpiderAbdomen =
    {
        "..kkk.",
        ".kmmk.",
        "kmmmmk",
        "kmmmmk",
        ".kmmk.",
        "..kkk.",
    };

    static readonly string[] SpiderWaist = { "kkkk", "kmmk", "kkkk" };

    public static Sprite Spider()
    {
        var legDn = PixelArt.MirrorV(SpiderLegUp);
        var stamps = new List<PixelArt.Stamp>
        {
            PixelArt.At(SpiderAbdomen, 0, 8),
            PixelArt.At(SpiderWaist,  5, 8),
            PixelArt.At(SpiderBody,   8, 6),
            PixelArt.At(SpiderLegUp,  10, 1),
            PixelArt.At(SpiderLegUp,  13, 0),
            PixelArt.At(SpiderLegUp,  16, 1),
            PixelArt.At(legDn,        10, 15),
            PixelArt.At(legDn,        13, 16),
            PixelArt.At(legDn,        16, 15),
        };
        var pal = Pal('k', C(0.03f, 0.03f, 0.04f), 'm', C(0.16f, 0.16f, 0.20f),
                      'r', C(1f, 0.28f, 0.18f), 'g', C(0.3f, 0.3f, 0.35f));
        return PixelArt.Get("u_spider", PixelArt.Compose(20, 22, stamps.ToArray()), pal);
    }

    // ============================================================ MECHS
    static readonly string[] MechBody =
    {
        "....kkkkkkkk....",
        "..kkddddddddkk..",
        ".kddddddddddddk.",
        ".kddgddddddgddk.",
        ".kddddddddddddk.",
        ".kddggggggggddk.",
        ".kddddddddddddk.",
        "..kddddddddddk..",
        "...kddddddddk...",
        "....kddddddk....",
        "...kkkkkkkkkk...",
        "..kdk......kdk..",
    };

    static readonly string[] MechLeg =
    {
        "kkk.",
        "kddk",
        "kddk",
        ".kk.",
    };

    static readonly string[] MortarTube =
    {
        "..kk..",
        ".kkkk.",
        "kggggk",
        "kggggk",
        ".kkkk.",
        "..kk..",
    };

    static readonly string[] Beacon =
    {
        "..k..",
        ".kak.",
        "kaaak",
        ".kak.",
        "..k..",
    };

    // hull tinted toward the unit's colour (green panzer, orange artillery)
    static Dictionary<char, Color> MechPalette(Color light)
    {
        var p = new Dictionary<char, Color>(MechPal);
        p['a'] = light;
        p['m'] = Color.Lerp(C(0.15f, 0.18f, 0.21f), light, 0.38f);
        return p;
    }

    // ============================================================ TANK
    // The Bulwark is an actual tank: turret + long muzzle with a brake at the
    // tip, a green armour plate welded to its front, and tracked wheels below.
    static readonly string[] Tank =
    {
        ".....kkkkkkkk.......",
        ".....kmmmmmmk.......",
        ".....kmmaammk.......",
        ".....kmmmmmmkggggaak",
        ".....kmmmmmmkggggaak",
        ".....kddddddk...kkkk",
        ".kmmmmmmmmmmmmmmkaak",
        ".kmmmmmaaaammmmmkaak",
        ".kmmmmmmmmmmmmmmkaak",
        ".kddddddddddddddkaak",
        ".kmmmmmmmmmmmmmmkaak",
        ".kkkkkkkkkkkkkkkkkkk",
        "kgkgkgkgkgkgkgkgkgkg",
        "kddwwddddwwddddwwddk",
        "kgkgkgkgkgkgkgkgkgkg",
        "kkkkkkkkkkkkkkkkkkkk",
    };

    public static Sprite Bulwark()
    {
        return PixelArt.Get("u_bulwark", Tank, MechPalette(C(0.35f, 1f, 0.45f)));
    }

    public static Sprite Artillery()
    {
        var stamps = new[]
        {
            PixelArt.At(MortarTube, 8, 0),
            PixelArt.At(MechBody, 0, 4),
            PixelArt.At(MechLeg, 1, 14),
            PixelArt.At(MechLeg, 11, 14),
            PixelArt.At(Beacon, 2, 2),
        };
        return PixelArt.Get("u_artillery", PixelArt.Compose(16, 18, stamps), MechPalette(C(1f, 0.55f, 0.15f)));
    }

    // ============================================================ SMASHER
    // The first boss: a wall of armour with two smash fists out front and a
    // furnace core burning between them. Faces right like the other mechs.
    static readonly string[] SmasherGrid =
    {
        "...kkkkkkkk................",
        "..kddddddddk....kkkkkkkk...",
        ".kdddddddddk..kdddddddddk..",
        ".kdddddddddk.kdddddddddddk.",
        ".kdddddddddk.kdmaaaaaaaamdk",
        ".kdddddddddk.kdmaaggaaaamdk",
        ".kdddddddddk.kdmaaggaaaamdk",
        ".kdddddddddk.kdmaaaaaaaamdk",
        ".kdddddddddk.kdddddddddddk.",
        "..kddddddddk..kdddddddddk..",
        "...kkkkkkkk....kkkkkkkk....",
        "..kmmmmmmmmkkkkmmmmmmmmk...",
        ".kmmmmmmmmmmmmmmmmmmmmmmk..",
        ".kmmgggmmmmmmmmmmmgggmmmk..",
        ".kmgggggmmmmmmmmmgggggmmk..",
        ".kmggggggmmmmmmmggggggmmk..",
        ".kmgggggmmmmmmmmmgggggmmk..",
        ".kmmgggmmmmmmmmmmmgggmmmk..",
        ".kmmmmmmmmmmmmmmmmmmmmmmk..",
        "..kmmmmmmmmmmmmmmmmmmmmk...",
        "..kkkkkkkkkkkkkkkkkkkkkk...",
        "....kmmmkk........kkmmmk...",
        "....kkkkk..........kkkkk...",
    };

    public static Sprite Smasher()
    {
        var pal = Pal('k', C(0.04f, 0.03f, 0.03f),
                      'm', C(0.40f, 0.13f, 0.10f),
                      'd', C(0.30f, 0.32f, 0.38f),
                      'g', C(1f, 0.45f, 0.12f),
                      'a', C(1f, 0.72f, 0.25f));
        return PixelArt.Get("u_smasher", SmasherGrid, pal);
    }

    // ============================================================ GRABBER
    // Eldritch horror: a crown of raised feelers over an eye-studded mass,
    // trailing four twitching tentacles instead of legs.
    static readonly string[] Grabber =
    {
        "....k......k....",
        "....k......k....",
        "...kk......kk...",
        "..kkk......kkk..",
        "..kddddddddddk..",
        ".kddddddddddddk.",
        "kddiiddiiddiiddk",
        "kddddddddddddddk",
        "kddiiiiiiiiiiddk",
        "kddddddddddddddk",
        ".kddddddddddddk.",
        "..kkddddddddkk..",
        ".k...kkkk...k...",
        ".k..k....k..k...",
        ".kk.k..k..k.kk..",
        "...k.kk.kk.k....",
        "....k.k..k.k....",
        ".....kk..kk.....",
    };

    public static Sprite GrabberBot()
    {
        var pal = Pal('k', C(0.03f, 0.03f, 0.05f), 'd', GrabberFlesh,
                      'i', C(0.80f, 1f, 0.40f), 'g', C(0.3f, 0.3f, 0.35f));
        return PixelArt.Get("u_grab", Grabber, pal);
    }

    // ============================================================ TRAPS
    // Each trap paints its own trigger ring, so it reads as an area on the
    // floor instead of a dot: green goo puddle, blinking bomb, tesla node.
    static readonly string[] TrapSlow =
    {
        "................",
        "................",
        "......kkkk......",
        "....kksssskk....",
        "...kssssssssk...",
        "..kssssssssssk..",
        "..ksswwssssssk..",
        ".kssssssssssssk.",
        ".kssssssskssssk.",
        ".ksssswwssssssk.",
        "..kssssssssdsk..",
        ".kssssssssssssk.",
        "...kssssssssk...",
        "....kksssskk....",
        "......kkkk......",
        "................",
    };

    static readonly string[] TrapBoom =
    {
        "................",
        ".........kk.....",
        ".........kaw....",
        "..........kaw...",
        "........kkk.....",
        "....kkkkkkkk....",
        "..kksssssssskk..",
        ".kssssssssssssk.",
        ".ksasasasasasak.",
        ".kasasasasasask.",
        ".ksasasasasasak.",
        ".kssssssssssssk.",
        "..kksssssssskk..",
        "....kkkkkkkk....",
        "................",
        "................",
    };

    static readonly string[] TrapStun =
    {
        "......kkkk......",
        ".....kssssk.....",
        ".....ksaask.....",
        ".....ksaask.....",
        "....kksssskk....",
        "...kkksssskkk...",
        "...k.kssssk.k...",
        "......kssk......",
        ".....kssssk.....",
        "....kksssskk....",
        "...kssssssssk...",
        "..kksssssssskk..",
        "..kssssssssssk..",
        "..kssssssssssk..",
        "...kkkkkkkkkk...",
        "................",
    };

    // little lightning bolts flickered around stunned traps and players
    static readonly string[] Zap0 =
    {
        "..w........",
        "..ww.......",
        "...ww......",
        "....ww.....",
        ".....ww....",
        "......www..",
        ".....ww....",
        "....ww.....",
        "...ww......",
        "..ww.......",
        ".w.........",
    };

    static readonly string[] Zap1 =
    {
        ".........w.",
        "........ww.",
        ".......ww..",
        "......ww...",
        ".....ww....",
        "...www.....",
        "....ww.....",
        ".....ww....",
        "......ww...",
        ".......ww..",
        "........w..",
    };

    static readonly string[] Zap2 =
    {
        "..w...w....",
        "..ww.ww....",
        "...www.....",
        "....ww.....",
        "....www....",
        ".....ww....",
        "....www....",
        "....ww.....",
        "...ww......",
        "..ww.......",
        ".w.........",
    };

    public static Sprite Zap(int i)
    {
        string[] g = i == 0 ? Zap0 : i == 1 ? Zap1 : Zap2;
        return PixelArt.Get("fx_zap" + i, g, Pal('w', Color.white));
    }

    // a dashed circle (dashes = 0 gives a solid ring), generated from a
    // radius formula so it is actually round
    static string[] BuildRing(int size, float radius, int dashes)
    {
        var rows = new string[size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            var sb = new System.Text.StringBuilder(size);
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                bool on = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - radius) < 0.55f;
                if (on && dashes > 0 && Mathf.Sin(Mathf.Atan2(dy, dx) * dashes) < -0.25f) on = false;
                sb.Append(on ? 'r' : '.');
            }
            rows[y] = sb.ToString();
        }
        return rows;
    }

    static readonly string[] TrapRing = BuildRing(20, 9.1f, 7);

    static readonly Dictionary<char, Color> ShockPal = Pal('r', Color.white);

    // the expanding shockwave used by explosions
    public static Sprite FxShock() { return PixelArt.Get("fx_shock", BuildRing(32, 15f, 0), ShockPal); }

    // the signature colour of each trap (used for the palette and its vfx)
    public static Color TrapColor(TrapKind kind)
    {
        switch (kind)
        {
            case TrapKind.Boom: return C(1f, 0.45f, 0.10f);
            case TrapKind.Stun: return C(0.30f, 0.85f, 1f);
            default:            return C(0.35f, 1f, 0.40f);
        }
    }

    public static Color TrapHi(TrapKind kind)
    {
        switch (kind)
        {
            case TrapKind.Boom: return C(1f, 0.92f, 0.35f);
            case TrapKind.Stun: return C(0.85f, 0.97f, 1f);
            default:            return C(0.72f, 1f, 0.55f);
        }
    }

    public static Sprite Trap(TrapKind kind)
    {
        Color c = TrapColor(kind), hi = TrapHi(kind);
        var pal = Pal('k', C(0.04f, 0.05f, 0.06f),
                      's', c, 'd', Color.Lerp(c, Color.black, 0.55f),
                      'a', hi, 'w', Color.Lerp(hi, Color.white, 0.6f),
                      'r', new Color(c.r, c.g, c.b, 0.45f));          // trigger ring

        string[] body = kind == TrapKind.Boom ? TrapBoom
                      : kind == TrapKind.Stun ? TrapStun : TrapSlow;
        return PixelArt.Get("trap_" + kind, PixelArt.Compose(20, 20,
            PixelArt.At(TrapRing, 0, 0),
            PixelArt.At(body, 2, 2)), pal);
    }

    // ============================================================ GORE
    // Drawn in white + grey only, so the renderer color tints them per unit
    // (red blood, black oil, orange spider ichor, cyan coolant...).
    // Two sizes: small chunks (blood spray) and full body parts - a head, an
    // arm with a hand, a leg with a boot, a ribbed torso, a loose hand - so a
    // corpse visibly explodes into pieces you can name.
    static readonly Dictionary<char, Color> GorePal = Pal(
        'w', Color.white, 'r', C(0.58f, 0.52f, 0.52f),
        'k', C(0.16f, 0.13f, 0.13f), 'b', C(0.07f, 0.06f, 0.06f));

    static readonly string[] DotArt =
    {
        ".w.",
        "www",
        ".w.",
    };

    static readonly string[] Splat0 =
    {
        "...www.....",
        "..wwwww....",
        ".wwwrrwww..",
        "wwwwrwwwww.",
        ".wwwrrwww..",
        "..wwwww....",
        "...www.....",
    };

    static readonly string[] Splat1 =
    {
        "..w...w..",
        ".www.wwww",
        "wwrrrwwww",
        ".wwwww...",
        "..www..w.",
        ".......w.",
    };

    static readonly string[] Splat2 =
    {
        "....www....",
        "..wwwww....",
        ".wwrrwwww..",
        "wwwwrwwwwww",
        ".wwwrrwww..",
        "..wwwww..w.",
        "...www.....",
    };

    static readonly string[] Splat3 =
    {
        ".www..",
        "wwrwww",
        "wrrrww",
        "wwrwww",
        ".wwww.",
        "..w.w.",
    };

    static readonly string[] Gib0 =
    {
        "..ww..",
        ".wwww.",
        "wwrwww",
        ".wwww.",
        "..ww..",
    };

    static readonly string[] Gib1 =
    {
        "..www..",
        ".wwwww.",
        "wwwrwww",
        ".wwwww.",
        "..www..",
    };

    static readonly string[] Gib2 =
    {
        "..ww..",
        ".wwww.",
        "wwwwww",
        "wwrwww",
        ".wwww.",
        "..ww..",
    };

    static readonly string[] Gib3 =
    {
        ".wwwwww.",
        "wwrrwwww",
        "wwwrwwww",
        ".wwwwww.",
    };

    // ---------------------------------------------------------- body parts
    static readonly string[] PartHead =
    {
        "...kkkkk...",
        "..kwwwwwk..",
        ".kwwwwwwwk.",
        ".kwbbwbbwk.",
        ".kwwwwwwwk.",
        ".kwwrrrwwk.",
        "..kwwwwwk..",
        "...kkkkk...",
        "....www....",
    };

    static readonly string[] PartArm =
    {
        "...kkkkk...",
        "..kwwwwwk..",
        ".kwwwwwwwk.",
        ".kwrrrrrwwk",
        "..kwwwwwkwk",
        "..kkkwwkkk.",
        ".....kw....",
    };

    static readonly string[] PartLeg =
    {
        "..kkkk..",
        ".kwwwwk.",
        ".kwwwwk.",
        ".kwrrwk.",
        ".kwwwwk.",
        ".kwwwwk.",
        ".kwwwwk.",
        ".kwwwwk.",
        ".kwwwwk.",
        ".kwwwkkk",
        "kwwwwkkk",
        "kkkkkkkk",
    };

    static readonly string[] PartTorso =
    {
        "..kkkkkkkk..",
        ".kwwwwwwwwk.",
        "kwrrwwwwrrwk",
        "kwwrrwwrrwwk",
        "kwrrwwwwrrwk",
        "kwwrrwwrrwwk",
        "kwrrwwwwrrwk",
        ".kwwwwwwwwk.",
        "..kkkkkkkk..",
        "....kwwk....",
    };

    static readonly string[] PartHand =
    {
        ".k.k.k.",
        "kwwwwwk",
        "kwwwwwk",
        "kwrrrwk",
        ".kwwwk.",
        "..kkk..",
        "...k...",
    };

    public static Sprite BloodDot() { return PixelArt.Get("gore_dot", DotArt, GorePal); }

    public static Sprite BloodSplat(int i)
    {
        string[] g = i == 0 ? Splat0 : i == 1 ? Splat1 : i == 2 ? Splat2 : Splat3;
        return PixelArt.Get("gore_splat" + i, g, GorePal);
    }

    public static Sprite Gib(int i)
    {
        string[] g = i == 0 ? Gib0 : i == 1 ? Gib1 : i == 2 ? Gib2 : Gib3;
        return PixelArt.Get("gore_gib" + i, g, GorePal);
    }

    // recognisable limbs: 0 head, 1 arm+hand, 2 leg+boot, 3 torso, 4 hand
    public static Sprite BodyPart(int i)
    {
        string[] g = i == 0 ? PartHead : i == 1 ? PartArm : i == 2 ? PartLeg
                    : i == 3 ? PartTorso : PartHand;
        return PixelArt.Get("gore_part" + i, g, GorePal);
    }

    // ============================================================ MAP
    static readonly string[] Panel =
    {
        "llllllllllllllld",
        "lmmmmmmmmmmmmmmd",
        "lmDmmmmmmmmmDmmd",
        "lmmmmmmmmmmmmmmd",
        "lmmmmmmmmmmmmmmd",
        "lmmmmmmmmmmmmmmd",
        "lmmmmmmmmmmmmmmd",
        "lmmmmmmmmmmmmmmd",
        "lmmmmmmmmmmmmmmd",
        "lmmmmmmmmmmmmmmd",
        "lmmmmmmmmmmmmmmd",
        "lmmmmmmmmmmmmmmd",
        "lmmmmmmmmmmmmmmd",
        "lmDmmmmmmmmmDmmd",
        "lmmmmmmmmmmmmmmd",
        "dddddddddddddddd",
    };

    static Dictionary<char, Color> PanelPal(Color baseC)
    {
        return Pal('m', baseC,
                   'l', Color.Lerp(baseC, Color.white, 0.14f),
                   'd', Color.Lerp(baseC, Color.black, 0.42f),
                   'D', Color.Lerp(baseC, Color.black, 0.62f));
    }

    public static Sprite FloorA() { return PixelArt.Get("map_panelA", Panel, PanelPal(C(0.16f, 0.19f, 0.26f))); }
    public static Sprite FloorB() { return PixelArt.Get("map_panelB", Panel, PanelPal(C(0.18f, 0.21f, 0.29f))); }

    static readonly string[] Hazard =
    {
        "ykkkykkkykkkykkk",
        "kkkykkkykkkykkky",
        "kykkkykkkykkkykk",
        "kkykkkykkkykkkyk",
        "ykkkykkkykkkykkk",
        "kkkykkkykkkykkky",
        "kykkkykkkykkkykk",
        "kkykkkykkkykkkyk",
    };

    public static Sprite HazardStripe()
    {
        var pal = Lab
            ? Pal('k', C(0.12f, 0.10f, 0.16f), 'y', C(1f, 0.30f, 0.80f))     // glowing magenta
            : Pal('k', C(0.09f, 0.09f, 0.11f), 'y', C(0.82f, 0.66f, 0.10f));
        return PixelArt.Get(Lab ? "map_hazard_lab" : "map_hazard", Hazard, pal);
    }

    static readonly string[] Void =
    {
        "vvvvvvvvvvvvvvvv",
        "vvPvvvvvvvvvvPvv",
        "vvvvvvvvPvvvvvvv",
        "vvvvvvvvvvvvvvvv",
        "vvvPvvvvvvvvvvvv",
        "vvvvvvvvvvvvPvvv",
        "vvvvvvvvvvvvvvvv",
        "vvvvPvvvvvvvvvvv",
        "vvvvvvvvvvvvvvvv",
        "vvvvvvvvvvPvvvvv",
        "vvvvvvvvvvvvvvvv",
        "vvPvvvvvvvvvvvPv",
        "vvvvvvvvvvvvvvvv",
        "vvvvvvvvPvvvvvvv",
        "vvvvvvvvvvvvvvvv",
        "vvvvvvvvvvvvvvvv",
    };

    public static Sprite VoidTile()
    {
        var pal = Pal('v', C(0.015f, 0.018f, 0.030f), 'P', C(0.06f, 0.08f, 0.13f));
        return PixelArt.Get("map_void", Void, pal);
    }

    static readonly string[] ChevronArt =
    {
        "k........k",
        "kk......kk",
        "kkk....kkk",
        "kkkk..kkkk",
        "kkkkkkkkkk",
        "kkkk..kkkk",
        "kkk....kkk",
        "kk......kk",
    };

    public static Sprite Chevron()
    {
        var pal = Pal('k', C(0.25f, 0.95f, 1f));
        return PixelArt.Get("map_chev", ChevronArt, pal);
    }

    static readonly string[] WallTileArt =
    {
        "RRRRRRRRRRRRRRRD",       // rim light: the top edge catches the light
        "GGDGGGGGGGGGGDGG",
        "GGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGG",
        "ykkkykkkykkkykkk",
        "kkkykkkykkkykkky",
        "GGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGG",
        "GGDGGGGGGGGGGDGG",
        "DDDDDDDDDDDDDDDD",
    };

    // the lab era flips the walls to clean-room white with a neon blue rim
    public static bool Lab;

    public static Sprite WallTile()
    {
        var pal = Lab
            ? Pal('R', C(0.35f, 0.78f, 1f),                  // neon blue rim
                  'G', C(0.72f, 0.78f, 0.88f),               // clean-room white
                  'D', C(0.45f, 0.50f, 0.62f),
                  'k', C(0.18f, 0.20f, 0.28f),
                  'y', C(1f, 0.30f, 0.80f))                  // magenta stripes
            : Pal('R', C(0.58f, 0.74f, 0.88f),     // the rim: bright grey-cyan
                  'G', C(0.26f, 0.29f, 0.36f),
                  'D', C(0.13f, 0.15f, 0.19f),
                  'k', C(0.09f, 0.09f, 0.11f),
                  'y', C(0.82f, 0.66f, 0.10f));
        return PixelArt.Get(Lab ? "map_wall_lab" : "map_wall", WallTileArt, pal);
    }

    static readonly string[] VatArt =
    {
        "..GGGGGGGGGGGG..",
        ".GGGGGGGGGGGGGG.",
        "GGGGGGGGGGGGGGGG",
        "GGggggggggggggGG",
        "GggggggggggggggG",
        "GggggggggggggggG",
        "GggggggggggggggG",
        "GgggggwwgggggggG",
        "GgggggwwgggggggG",
        "GggggwwwwwwggggG",
        "GggggwwwwwwggggG",
        "GgggggwwgggggggG",
        "GgggggwwgggggggG",
        "GggggggggggggggG",
        "GggggggggggggggG",
        "GGggggggggggggGG",
        "GGGGGGGGGGGGGGGG",
        ".GGGGGGGGGGGGGG.",
        "..GGGGGGGGGGGG..",
    };

    public static Sprite Vat()
    {
        var pal = Pal('G', C(0.30f, 0.34f, 0.42f),
                      'g', C(0.30f, 0.85f, 0.25f),
                      'w', C(0.90f, 1f, 0.90f));
        return PixelArt.Get("map_vat", VatArt, pal);
    }

    // ============================================================ UPGRADES
    // Every cyber-mod gets a badge: a dark plate with a 10x10 glyph on top,
    // tinted per upgrade so the level-up table reads at a glance.
    static readonly string[] IconPlate =
    {
        "................",
        ".kkkkkkkkkkkkkk.",
        ".kddddddddddddk.",
        ".kbbbbbbbbbbbbk.",
        ".kbbbbbbbbbbbbk.",
        ".kbbbbbbbbbbbbk.",
        ".kbbbbbbbbbbbbk.",
        ".kbbbbbbbbbbbbk.",
        ".kbbbbbbbbbbbbk.",
        ".kbbbbbbbbbbbbk.",
        ".kbbbbbbbbbbbbk.",
        ".kbbbbbbbbbbbbk.",
        ".kbbbbbbbbbbbbk.",
        ".kddddddddddddk.",
        ".kkkkkkkkkkkkkk.",
        "................",
    };

    // blood pump: a heart with a medical cross burned into it
    static readonly string[] GlyphVita =
    {
        "..gg..gg..",
        ".gggggggg.",
        "ggggwwgggg",
        "ggggwwgggg",
        "ggwwwwwwgg",
        "ggwwwwwwgg",
        ".gggwwggg.",
        ".gggwwggg.",
        "..ggwwgg..",
        "...gggg...",
    };

    // sandevistan: an up arrow
    static readonly string[] GlyphRail =
    {
        "....gg....",
        "...gggg...",
        "..gggggg..",
        ".gggggggg.",
        "gggggggggg",
        "....gg....",
        "....gg....",
        "....gg....",
        "...ssss...",
        "..........",
    };

    // celerity: a double chevron
    static readonly string[] GlyphSyn =
    {
        "g........g",
        ".g......g.",
        "..gg..gg..",
        "....gg....",
        "..........",
        "g........g",
        ".g......g.",
        "..gg..gg..",
        "....gg....",
        "..........",
    };

    // quen matrix: a hex shield
    static readonly string[] GlyphHalo = 
    {
        "...gggg...",
        "..g....g..",
        ".g......g.",
        "g........g",
        "g........g",
        "g........g",
        ".g......g.",
        "..g....g..",
        "...gggg...",
        "..........",
    };

    // guts: three lines diverging from one point
    static readonly string[] GlyphTrident =
    {
        "g...g....g",
        ".g..g...g.",
        ".g..g...g.",
        "..g.g..g..",
        "..g.g..g..",
        "...gg.g...",
        "...gg.g...",
        "...ggg....",
        "....gg....",
        "....gg....",
    };

    // bloody mess: an eight-way burst
    static readonly string[] GlyphCorpse =
    {
        "....g.....",
        ".g...g..g.",
        "..g.g..g..",
        "....g.....",
        "gg..gg..gg",
        "gg..gg..gg",
        "....g.....",
        "..g.g..g..",
        ".g...g..g.",
        "....g.....",
    };

    static readonly string[] GlyphHemo =
    {
        "....ww....",
        "....gg....",
        "...gggg...",
        "..gggggg..",
        "..gggggg..",
        ".gggyyggg.",
        ".gggggggg.",
        ".gggggggg.",
        "..ssssss..",
        "...ssss...",
    };

    // dead eye: a reticle - circle with a dot
    static readonly string[] GlyphRico =
    {
        "..gggggg..",
        ".g......g.",
        "g........g",
        "g...gg...g",
        "g...gg...g",
        "g...gg...g",
        "g...gg...g",
        "g........g",
        ".g......g.",
        "..gggggg..",
    };

    // fus ro dah: an arrow punching through a wall
    static readonly string[] GlyphPierce =
    {
        ".....g....",
        ".....g...g",
        ".....g..g.",
        ".....g.g..",
        "gggggggggg",
        "gggggggggg",
        ".....g.g..",
        ".....g..g.",
        ".....g...g",
        ".....g....",
    };

    // portal gun: two linked rings
    static readonly string[] GlyphChrono =
    {
        "..........",
        "..gg..gg..",
        ".g..gg..g.",
        "g...gg...g",
        "g...gg...g",
        "g...gg...g",
        "g...gg...g",
        ".g..gg..g.",
        "..gg..gg..",
        "..........",
    };

    // the incredible gassy: a wave
    static readonly string[] GlyphSolvent =
    {
        "..........",
        "..........",
        "...gg..gg.",
        "..g..gg..g",
        ".g......g.",
        "..g..gg..g",
        "...gg..gg.",
        "..........",
        "..........",
        "..........",
    };

    // maziodyne: a zigzag bolt
    static readonly string[] GlyphArc =
    {
        "......gg..",
        ".....gg...",
        "....gg....",
        "...ggggg..",
        ".....gg...",
        "....gg....",
        "...gg.....",
        "..gg......",
        ".gg.......",
        "..........",
    };

    // dogmeat: a paw print
    static readonly string[] GlyphHound =
    {
        ".g..gg..g.",
        ".g..gg..g.",
        ".gg.gg.gg.",
        "..gggggg..",
        ".gggggggg.",
        "gggggggggg",
        "gggggggggg",
        ".gggggggg.",
        "..gggggg..",
        "...gggg...",
    };

    // helios' judgment: a sun firing a beam
    static readonly string[] GlyphOrbital =
    {
        "...g..g...",
        ".g.gggg.g.",
        "gg.gggg.gg",
        "..gggggg..",
        ".g.gggg.g.",
        "...g..g...",
        "...gggg...",
        "..gggggg..",
        ".gggggggg.",
        "gggggggggg",
    };

    // PUSH PUSH PUSH: a silhouette flexing both arms
    static readonly string[] GlyphPush =
    {
        "...gggg...",
        "gg.gggg.gg",
        ".g.gggg.g.",
        ".g..gg..g.",
        ".gggggggg.",
        "..gggggg..",
        "...gggg...",
        "...gggg...",
        "..gg..gg..",
        ".ggg..ggg.",
    };

    // 225: a dragon head - horns, long snout, fangs
    static readonly string[] GlyphDragon =
    {
        ".......gg.",
        "......gg..",
        "...gggggg.",
        "..gggggggg",
        ".ggwgggggg",
        "gggggggggg",
        "ggggggggg.",
        ".g.g.gg...",
        "..gggg....",
        "...gg.....",
    };

    // astarion: an open mouth - blood-dark inside, two fangs up top
    static readonly string[] GlyphAstarion =
    {
        "..........",
        "..gggggg..",
        ".gyyyyyyg.",
        ".gywyywyg.",
        ".gywyywyg.",
        ".gyyyyyyg.",
        ".gyyyyyyg.",
        ".gyywwyyg.",
        "..gggggg..",
        "..........",
    };

    static Dictionary<char, Color> IconPal(Color accent, Color second)
    {
        return Pal('k', C(0.05f, 0.06f, 0.08f),
                   'd', C(0.10f, 0.12f, 0.16f),
                   'b', C(0.15f, 0.18f, 0.25f),
                   'g', accent, 's', Color.Lerp(accent, Color.black, 0.55f),
                   'w', Color.Lerp(accent, Color.white, 0.75f),
                   'y', second, 'e', Color.Lerp(second, Color.white, 0.5f));
    }

    // ============================================================ LAB ERA
    static readonly string[] HexPlatformArt =
    {
        "....cccccccc....",
        "..ccvvvvvvvvcc..",
        ".cvvvvvvvvvvvvc.",
        "cvvvvcvvcvvvvvvc",
        "cvvvvvvvvvvvvvvc",
        "cvvvcvvcvvvcvvvc",
        "cvvvvvvvvvvvvvvc",
        "cvvvvcvvcvvvvvvc",
        "cvvvvvvvvvvvvvvc",
        ".cvvvvvvvvvvvvc.",
        "..ccvvvvvvvvcc..",
        "....cccccccc....",
    };
    public static Sprite HexPlatform()
    {
        var pal = Pal('c', C(0.35f, 0.9f, 1f), 'v', C(0.15f, 0.09f, 0.30f));
        return PixelArt.Get("lab_hex", HexPlatformArt, pal);
    }

    static readonly string[] PylonArt =
    {
        "....cccc....",
        "..ccwwwwcc..",
        ".cwwddddwwc.",
        "cwddwddwddwc",
        "cwddddddddwc",
        "cwddddddddwc",
        "cwddwddwddwc",
        ".cwwddddwwc.",
        "..ccwwwwcc..",
        "....cccc....",
    };
    public static Sprite Pylon()
    {
        var pal = Pal('c', C(0.75f, 0.80f, 0.90f),
                      'w', C(0.35f, 0.90f, 1f),
                      'd', C(0.28f, 0.32f, 0.42f));
        return PixelArt.Get("lab_pylon", PylonArt, pal);
    }

    static readonly string[] VentArt =
    {
        "kkkkkkkkkkkk",
        "kddddddddddk",
        "kgkgkgkgkgkk",
        "kddddddddddk",
        "kgkgkgkgkgkk",
        "kkkkkkkkkkkk",
    };
    public static Sprite Vent()
    {
        var pal = Pal('k', C(0.10f, 0.10f, 0.13f),
                      'd', C(0.30f, 0.33f, 0.40f),
                      'g', C(1f, 0.50f, 0.15f));
        return PixelArt.Get("lab_vent", VentArt, pal);
    }

    // the herald: a thin blade of a figure, neon purple over a cyan core
    static readonly string[] HeraldArt =
    {
        ".......cc.......",
        "......cWWc......",
        "......cWWc......",
        ".....cpWWpc.....",
        ".....cpWWpc.....",
        "....cppWWppc....",
        "....cpWWWWpc....",
        "...cppWbbWppc...",
        "...cpWbbbbWpc...",
        "..cppWbbbbWppc..",
        "..cpWbbPPbbWpc..",
        ".cppWbPPPPbWppc.",
        ".cpWbPPWWPPbWpc.",
        "..cppWbPPbWppc..",
        "...cpWbbbbWpc...",
        "....cppWWppc....",
        ".....cpWWpc.....",
    };
    public static Sprite Herald()
    {
        var pal = Pal('c', C(0.35f, 0.95f, 1f),
                      'W', C(0.80f, 1f, 1f),
                      'p', C(0.70f, 0.35f, 1f),
                      'b', C(0.18f, 0.08f, 0.30f),
                      'P', C(1f, 0.45f, 0.95f));
        return PixelArt.Get("herald_body", HeraldArt, pal);
    }

    static readonly string[] LaserBotArt =
    {
        "...ccc...",
        "..cWWWc..",
        ".cpWWWpc.",
        "cppWWpppc",
        ".cpWWWpc.",
        "..cWWWc..",
        "...ccc...",
    };
    public static Sprite LaserBot()
    {
        var pal = Pal('c', C(0.35f, 0.95f, 1f),
                      'W', C(0.85f, 1f, 1f),
                      'p', C(0.70f, 0.35f, 1f));
        return PixelArt.Get("herald_bot", LaserBotArt, pal);
    }

    static readonly string[] SpikeArt =
    {
        "...w...",
        "...w...",
        "..www..",
        "..www..",
        ".wwwww.",
        ".wwwww.",
        "kkkkkkk",
    };
    public static Sprite Spike()
    {
        var pal = Pal('w', C(1f, 0.75f, 0.40f), 'k', C(0.25f, 0.10f, 0.05f));
        return PixelArt.Get("fx_spike", SpikeArt, pal);
    }

    static readonly string[] BoltArt =
    {
        "..w..",
        "..w..",
        ".ww..",
        "..ww.",
        "..ww.",
        ".ww..",
        "..w..",
        "..w..",
        ".ww..",
        "..ww.",
        "..w..",
        "..w..",
        ".ww..",
        "..ww.",
        "..ww.",
        "..ww.",
    };
    public static Sprite Bolt()
    {
        var pal = Pal('w', C(0.85f, 0.97f, 1f));
        return PixelArt.Get("fx_bolt", BoltArt, pal);
    }

    // the herald's new main body: a hextech hexagon - violet plate, cyan
    // rim, a white-hot socket in the middle where the eye lives
    static readonly string[] HeraldHexArt =
    {
        "....cccccccc....",
        "..ccvvvvvvvvcc..",
        ".cvvvvvvvvvvvvc.",
        "cvvvppppppppvvvc",
        "cvvppddddddppvvc",
        "cvvpddwwwwddpvvc",
        "cvvpdwwWWWWwdpvc",
        "cvvpdwWmmWwdpvvc",
        "cvvpdwWmmWwdpvvc",
        "cvvpdwwWWWWwdpvc",
        "cvvpddwwwwddpvvc",
        "cvvppddddddppvvc",
        "cvvvppppppppvvvc",
        ".cvvvvvvvvvvvvc.",
        "..ccvvvvvvvvcc..",
        "....cccccccc....",
    };
    public static Sprite HeraldHex()
    {
        var pal = Pal('c', C(0.35f, 0.95f, 1f),
                      'v', C(0.26f, 0.12f, 0.45f),
                      'p', C(0.70f, 0.35f, 1f),
                      'd', C(0.10f, 0.06f, 0.18f),
                      'w', C(0.75f, 0.95f, 1f),
                      'W', C(1f, 1f, 1f),
                      'm', C(1f, 0.30f, 0.80f));
        return PixelArt.Get("lab_herald_hex", HeraldHexArt, pal);
    }

    // the thing you actually shoot: a bright magenta eye
    static readonly string[] HeraldEyeArt =
    {
        "....m....",
        "...mWm...",
        "..mWWWm..",
        ".mWWWWWm.",
        "mWWWWWWWm",
        ".mWWWWWm.",
        "..mWWWm..",
        "...mWm...",
        "....m....",
    };
    public static Sprite HeraldEye()
    {
        var pal = Pal('m', C(1f, 0.30f, 0.80f), 'W', C(1f, 1f, 1f));
        return PixelArt.Get("lab_herald_eye", HeraldEyeArt, pal);
    }

    // hazard machines: one furnace, one tesla coil - proper machines, not dots
    static readonly string[] MachineFireArt =
    {
        "..kkkkkkkk..",
        ".kddddddddk.",
        ".kdwwwwwwdk.",
        "kddwyyyywddk",
        "kdffyffyffdk",
        "kdffyffyffdk",
        "kddffffffddk",
        "kdddyyyydddk",
        "kddddddddddk",
        ".kddddddddk.",
        ".kdwwwwwwdk.",
        "kkkkkkkkkkkk",
    };
    public static Sprite MachineFire()
    {
        var pal = Pal('k', C(0.12f, 0.10f, 0.10f),
                      'd', C(0.35f, 0.28f, 0.28f),
                      'w', C(0.55f, 0.48f, 0.46f),
                      'f', C(0.95f, 0.45f, 0.10f),
                      'y', C(1f, 0.85f, 0.35f));
        return PixelArt.Get("lab_machine_fire", MachineFireArt, pal);
    }

    static readonly string[] MachineElecArt =
    {
        ".....cc.....",
        "....cWWc....",
        "...cWWWWc...",
        "....cWWc....",
        ".....cc.....",
        "..kkkkkkkk..",
        ".kddddddddk.",
        ".kdccccccdk.",
        "kdccddddccdk",
        ".kdccccccdk.",
        ".kddddddddk.",
        "kkkkkkkkkkkk",
    };
    public static Sprite MachineElec()
    {
        var pal = Pal('k', C(0.10f, 0.10f, 0.13f),
                      'd', C(0.30f, 0.34f, 0.42f),
                      'c', C(0.35f, 0.95f, 1f),
                      'W', C(0.90f, 1f, 1f));
        return PixelArt.Get("lab_machine_elec", MachineElecArt, pal);
    }

    // a flame tongue for the floor fires
    static readonly string[] FlameArt =
    {
        "...r...",
        "..rr...",
        "..rrr..",
        ".rroor.",
        ".rooor.",
        "rooooor",
        "rooooor",
        ".ooooo.",
    };
    public static Sprite Flame()
    {
        var pal = Pal('r', C(1f, 0.38f, 0.08f), 'o', C(1f, 0.85f, 0.30f));
        return PixelArt.Get("lab_flame", FlameArt, pal);
    }

    // retro terminal: phosphor green glyphs on a dead-black screen
    static readonly string[] ComputerScreenArt =
    {
        "kkkkkkkkkk",
        "kddddddddk",
        "kdgggggddk",
        "kdgdggggdk",
        "kdgggdggdk",
        "kdgdggggdk",
        "kdgggggddk",
        "kkkkkkkkkk",
    };
    public static Sprite ComputerScreen()
    {
        var pal = Pal('k', C(0.07f, 0.08f, 0.10f),
                      'd', C(0.04f, 0.14f, 0.08f),
                      'g', C(0.35f, 1f, 0.50f));
        return PixelArt.Get("lab_console", ComputerScreenArt, pal);
    }

    // a little hazard vial: glass tube, glowing liquid inside
    static readonly string[] VialArt =
    {
        "..k..",
        "..k..",
        ".kgk.",
        ".gGg.",
        ".gGg.",
        ".gGg.",
        ".gGg.",
        ".ggg.",
        "kkkkk",
    };
    public static Sprite Vial(bool magenta)
    {
        var pal = Pal('k', C(0.15f, 0.15f, 0.18f),
                      'g', C(0.60f, 0.90f, 0.95f),
                      'G', magenta ? C(1f, 0.40f, 0.90f) : C(0.40f, 1f, 0.50f));
        return PixelArt.Get(magenta ? "lab_vial_m" : "lab_vial_g", VialArt, pal);
    }

    // shattered glass decals for the lab floor
    static readonly string[] GlassArt =
    {
        ".c......cc",
        "ccc...c.c.",
        ".cccc.c.cc",
        "..c.ccccc.",
        "....c.c...",
    };
    public static Sprite Glass()
    {
        var pal = Pal('c', C(0.72f, 0.95f, 1f));
        return PixelArt.Get("lab_glass", GlassArt, pal);
    }

    // ============================================================ BROKEN VESSEL
    // boss three, drawn as parts: a lopsided heap of scrap plates, bones and
    // machine. Same palette across every piece so it reads as one body even
    // though it is obviously five bodies stitched together.
    static Dictionary<char, Color> VesselPal()
    {
        return Pal('k', C(0.06f, 0.05f, 0.05f),
                   'r', C(0.44f, 0.27f, 0.14f),
                   'R', C(0.62f, 0.40f, 0.19f),
                   'd', C(0.21f, 0.17f, 0.15f),
                   'g', C(0.33f, 0.35f, 0.39f),
                   'G', C(0.50f, 0.53f, 0.58f),
                   'b', C(0.78f, 0.72f, 0.58f),
                   'w', C(0.93f, 0.89f, 0.76f),
                   's', C(0.40f, 0.14f, 0.12f),
                   'x', C(0.11f, 0.10f, 0.09f),
                   'a', C(0.50f, 0.95f, 0.20f),
                   'A', C(0.82f, 1f, 0.45f),
                   'p', C(0.85f, 0.67f, 0.53f),      // human skin: the part
                   'P', C(0.60f, 0.43f, 0.33f));     // that still remembers
    }

    // the hulk itself: a human torso on the left - ribcage under pale skin -
    // and a machine's engine bolted through the right side of the meat
    static readonly string[] VesselBodyArt =
    {
        "...kppppppk.kgggggggk...",
        "..kppppppppkkgGGGGGgk...",
        "..kpwkwpwppkkgGxxxxgk...",
        "..kppppppppkgGxGGGGxgk..",
        "..kpwkwpwppkgGxxxxxGgk..",
        "..kppppppppkgGGGGGGggk..",
        "..kppppppppkgGxxxxGggk..",
        "...kppppppkkgGGGGgggk...",
        "...kpwkwpwkkgggggggk....",
        "...kppppppkkgkkgxxxk....",
        "...kpppppppkkkkkgggk....",
        "....kpppkkrrrrrkkk......",
        "....kppkrrRRRRrrdk......",
        "...kppkrrrrrrrrrdk......",
        "...kpkkdrrrrrrrddk......",
        "...kpkdsddrrrrdddk......",
        "...kksddssdddddgddk.....",
        "....kdssdddgGGGgddk.....",
        "....kdddddgggxxxggk.....",
        ".....kkk...kkkkkk.......",
    };
    public static Sprite VesselBody()
    {
        return PixelArt.Get("bv_body", VesselBodyArt, VesselPal());
    }

    // the skull: it came with the arm, and it is still looking at you
    static readonly string[] VesselSkullArt =
    {
        "...kk...",
        "..kwwk..",
        ".kwwwwk.",
        "kwwwwwwk",
        "kkkwkkkk",
        "kkkwkkkk",
        ".kwwwwk.",
        ".kwkwkk.",
        "..kkkk..",
    };
    public static Sprite VesselSkull()
    {
        return PixelArt.Get("bv_skull", VesselSkullArt, VesselPal());
    }

    // the human arm: pale, five fingers, hanging off one shoulder - the most
    // honest thing on the whole body
    static readonly string[] VesselHumanArmArt =
    {
        "..kkkk..",
        ".kppppk.",
        ".kppppk.",
        ".kPpppk.",
        ".kppppk.",
        ".kppppk.",
        "..kppk..",
        "..kppk..",
        ".kppppk.",
        "kppppppk",
        "kpkppkpk",
        "kpkppkpk",
        "kkpkpkkk",
        "...kk...",
    };
    public static Sprite VesselHumanArm()
    {
        return PixelArt.Get("bv_arm_human", VesselHumanArmArt, VesselPal());
    }

    // the gear: still turning, bolted where the other shoulder used to be
    static readonly string[] VesselGearArt =
    {
        ".k.k.k.k.",
        "kgggggggk",
        ".kgxxxgk.",
        "kggkkgggk",
        ".kgxxxgk.",
        "kgggggggk",
        ".k.k.k.k.",
    };
    public static Sprite VesselGear()
    {
        return PixelArt.Get("bv_gear", VesselGearArt, VesselPal());
    }

    // the oversized slab of an arm: plates over bone, one claw left
    static readonly string[] VesselArmArt =
    {
        "..kkkk..",
        ".kRRRRk.",
        "kRGggRRk",
        "kGRxgGRk",
        "kRGGgRRk",
        "kRRRRRRk",
        ".kRRddk.",
        "..kdddk.",
        ".kdxxdk.",
        ".kdgxgk.",
        ".kdxxdk.",
        ".kbbbbk.",
        "..kbbbk.",
        "..kbwvk.",
        "...kkk..",
    };
    public static Sprite VesselArm()
    {
        return PixelArt.Get("bv_arm", VesselArmArt, VesselPal());
    }

    // the machine chunk bolted high on the other shoulder: vents, a broken
    // corner, something still spinning inside
    static readonly string[] VesselMechArt =
    {
        "...kgGk....",
        "..kGggGk...",
        ".kGgxggGk..",
        "kgGgxxgGgk.",
        "kdgxGGxgdk.",
        "kdgxxxxgdk.",
        "kdgxggxgdk.",
        ".kggggggk..",
        "..kkkkkkk..",
    };
    public static Sprite VesselMech()
    {
        return PixelArt.Get("bv_mech", VesselMechArt, VesselPal());
    }

    // the tank strapped to its side: what it is about to throw up
    static readonly string[] VesselTankArt =
    {
        "..kkkk..",
        ".kgxxgk.",
        ".kaAAak.",
        ".kaAAak.",
        ".kAaaAk.",
        ".kaAAak.",
        ".kAaaAk.",
        ".kggggk.",
        "..kkkk..",
    };
    public static Sprite VesselTank()
    {
        return PixelArt.Get("bv_tank", VesselTankArt, VesselPal());
    }

    // the maw: one bright green mouth stitched into the chest plate. This is
    // where the acid comes from, and it pulses like it is breathing
    static readonly string[] VesselMawArt =
    {
        "...kkk...",
        "..kaaak..",
        ".kAaaaak.",
        "kaAaaaAak",
        ".kAaaaak.",
        "..kAaak..",
        "...kkk...",
    };
    public static Sprite VesselMaw()
    {
        return PixelArt.Get("bv_maw", VesselMawArt, VesselPal());
    }

    // the leg: human from the knee down, foot and all - it drags behind
    // because nothing taught it to walk again
    static readonly string[] VesselLegArt =
    {
        "..kkkk..",
        ".kppppk.",
        ".kppppk.",
        ".kPpppk.",
        ".kppppk.",
        "..kppk..",
        "..kppk..",
        ".kpppk..",
        "..kppk..",
        "..kppk..",
        "..kppppk",
        ".kpppppk",
        "kppppppk",
        "kkkkkkkk",
    };
    public static Sprite VesselLeg()
    {
        return PixelArt.Get("bv_leg", VesselLegArt, VesselPal());
    }

    // ============================================================ THE FLESH
    // the fourth reality: everything comes back as meat and eyes
    static Dictionary<char, Color> FleshPal()
    {
        return Pal('k', C(0.05f, 0.02f, 0.03f),
                   'm', C(0.42f, 0.10f, 0.12f),
                   'M', C(0.62f, 0.20f, 0.21f),
                   'v', C(0.24f, 0.05f, 0.08f),
                   'b', C(0.86f, 0.80f, 0.70f),
                   'e', C(0.97f, 0.93f, 0.85f),
                   'w', C(0.78f, 0.34f, 0.32f),
                   'p', C(0.95f, 0.60f, 0.62f),
                   'g', C(0.55f, 0.20f, 0.60f),
                   'G', C(0.78f, 0.45f, 0.95f));
    }

    // THE BUTCHER: the flesh era's tank - a wall of meat with a bone blade,
    // teeth where the face should be, and eyes that never learned to blink
    static readonly string[] ButcherArt =
    {
        ".....k.bk.....",
        "....kb.bbk....",
        "...kb..kbbk...",
        "..kbk.kk.kbk..",
        ".kbk..kmmk.kk.",
        ".kbk.kmmmmkbk.",
        "kkbkkmmMmmkkbk",
        "kmbkmmMMMMmmkb",
        "kmmbkeMkMkemmk",
        "kmmmekkkkkemmk",
        "kmmmmkkkkkmmmk",
        "kvmMmmmmmmmMvk",
        ".kvmMbbbbmMvk.",
        ".kvmmbwkbwmbvk",
        "..kvmmkkkmmvk.",
        "..kvmMMMMMmvk.",
        "...kvvvvvvvk..",
        "....kkkkkkk...",
    };
    public static Sprite Butcher()
    {
        return PixelArt.Get("butcher", ButcherArt, FleshPal());
    }

    // the flesh artillery: a tube of meat with teeth at the muzzle
    static readonly string[] FleshArtilleryArt =
    {
        "...kkk.....",
        "..kmbmk....",
        ".kmmmbmk...",
        "kmmkmbmmk..",
        "kmeekmmmmk.",
        "kmmekkmmmmk",
        "kmmmmmmmmmk",
        "kmmmmmmmmmk",
        "kmmmbbbmmmk",
        "kmmmbkbmmmk",
        ".kmmmbmmmk.",
        "..kvmvmvk..",
        "...kkkkk...",
    };
    public static Sprite FleshArtillery()
    {
        return PixelArt.Get("flesh_artillery", FleshArtilleryArt, FleshPal());
    }

    // the grabber rebuilt as meat: one big eye, bone crown, wet tendrils
    static readonly string[] FleshGrabberArt =
    {
        ".kb.bk.bk.",
        "kb.bbbk.bb",
        ".kbbkbbbk.",
        "kbbkkkbbb.",
        "kmmkeekmmk",
        "kmmeekemmk",
        "kmmeekemmk",
        "kmmkeekmmk",
        ".kmmmmmkk.",
        ".kvmvmvmk.",
        "..kvmvmk..",
        "...kvmk...",
        "....kvk...",
        ".....k....",
    };
    public static Sprite FleshGrabber()
    {
        return PixelArt.Get("flesh_grabber", FleshGrabberArt, FleshPal());
    }

    // the eyes that watch from the flesh floor
    static readonly string[] CosmicEyeArt =
    {
        "...kkkkkk...",
        "..kwwwwwwk..",
        ".kwwwwwwwwk.",
        "kwwwkkkwwwk.",
        "kwkkkeekkwwk",
        "kwkkkeekkwwk",
        "kwwwkkkwwwwk",
        ".kwwwwwwwwk.",
        "..kwwwwwwk..",
        "...kkkkkk...",
    };
    public static Sprite CosmicEye()
    {
        return PixelArt.Get("cosmic_eye", CosmicEyeArt, FleshPal());
    }

    // ============================================================ FONT
    // the whole game speaks sans-serif: one OS font, every hud style wears it
    static Font guiFont;
    public static Font GuiFont()
    {
        if (guiFont == null)
            guiFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Segoe UI", "Verdana", "Tahoma", "Arial" }, 16);
        return guiFont;
    }

    // ============================================================ HUD ICONS
    // Drawn near-white so GUI.color tints them per bar: outline in grey
    // multiplies down to a dark version of the bar colour.
    static readonly string[] HudHeartArt =
    {
        ".RR.RR.",
        "RWWRRWR",
        "RWWWWWR",
        "RWWWWWR",
        ".RWWWR.",
        "..RWR..",
        "...R...",
    };
    static readonly string[] HudClockArt =
    {
        "..RRR..",
        ".RwwwwR",
        "RwwRwww",
        "RwwRwww",
        "RwwwwwR",
        ".RwwwwR",
        "..RRR..",
    };
    static readonly string[] HudBootArt =
    {
        ".RR....",
        ".RwwR..",
        ".RwwR..",
        ".RwwR..",
        ".RwwwRR",
        ".RwwwwR",
        ".RRRRRR",
    };
    static readonly string[] HudStarArt =
    {
        "...R...",
        "..RwR..",
        "..RwR..",
        "RRRwRRR",
        "..RwR..",
        "..R.R..",
        ".......",
    };

    static Dictionary<char, Color> HudIconPal = Pal('R', C(0.42f, 0.42f, 0.46f),
                                                     'w', Color.white,
                                                     'W', C(0.92f, 0.92f, 0.95f));

    public static Sprite IconHeart() { return PixelArt.Get("hud_heart", HudHeartArt, HudIconPal); }
    public static Sprite IconClock() { return PixelArt.Get("hud_clock", HudClockArt, HudIconPal); }
    public static Sprite IconBoot()  { return PixelArt.Get("hud_boot", HudBootArt, HudIconPal); }
    public static Sprite IconStar()  { return PixelArt.Get("hud_star", HudStarArt, HudIconPal); }

    public static Sprite UpgradeIcon(UpgradeKind k)
    {
        string[] g;
        switch (k)
        {
            case UpgradeKind.Kinetic:    g = GlyphRail; break;
            case UpgradeKind.Synapse:    g = GlyphSyn; break;
            case UpgradeKind.Halo:       g = GlyphHalo; break;
            case UpgradeKind.Trident:    g = GlyphTrident; break;
            case UpgradeKind.Corpse:     g = GlyphCorpse; break;
            case UpgradeKind.Hemotoxin:  g = GlyphHemo; break;
            case UpgradeKind.Ricochet:   g = GlyphRico; break;
            case UpgradeKind.PhaseSlug:  g = GlyphPierce; break;
            case UpgradeKind.Chrono:     g = GlyphChrono; break;
            case UpgradeKind.Solvent:    g = GlyphSolvent; break;
            case UpgradeKind.Arc:        g = GlyphArc; break;
            case UpgradeKind.Hound:      g = GlyphHound; break;
            case UpgradeKind.Orbital:    g = GlyphOrbital; break;
            case UpgradeKind.Push:       g = GlyphPush; break;
            case UpgradeKind.Dragon:     g = GlyphDragon; break;
            case UpgradeKind.Astarion:   g = GlyphAstarion; break;
            default:                     g = GlyphVita; break;
        }
        return PixelArt.Get("up_" + k, PixelArt.Compose(16, 16,
            PixelArt.At(IconPlate, 0, 0),
            PixelArt.At(g, 3, 3)), IconPal(Upgrade.Accent(k), Upgrade.Second(k)));
    }

    // the halo ward: a dashed ring that spins around the player
    static readonly Dictionary<char, Color> AuraPal = Pal('r', new Color(0.45f, 0.95f, 1f, 0.55f));

    public static Sprite AuraRing() { return PixelArt.Get("fx_aura", BuildRing(24, 11.2f, 5), AuraPal); }

    // ============================================================ ASTARION KI
    // A rising flame spike: the red super-saiyan burn that climbs off the
    // suit while the thirst is drinking.
    static readonly string[] KiFlameGrid =
    {
        "..f..",
        "..f..",
        ".ff.f",
        ".fFf.",
        "ffFff",
        "fFFFf",
        "fffff",
        ".fff.",
    };

    public static Sprite KiFlame()
    {
        var pal = Pal('f', new Color(0.95f, 0.22f, 0.30f), 'F', new Color(1f, 0.62f, 0.55f));
        return PixelArt.Get("fx_kiflame", KiFlameGrid, pal);
    }

    // HOUND PACK minion: a dog - upright ears, visor eye, a muzzle that sticks
    // out over a neck gap, raised tail, four legs. Bright alloy + cyan collar so
    // the pack stays findable inside a firefight.
    static readonly string[] DogArt =
    {
        "............k...k...",
        "............kk.kk...",
        ".k..........kmmmmk..",
        ".kmm.......kmmmeek..",
        ".kmm.......kmmmmmmmk",
        ".kkkkkkkkkkkkkkkkkkk",
        ".kmmmmmmmmmmttmmmk..",
        ".kmmmmmmmmmmmmmmmk..",
        "...kttttttttkmmmk...",
        "....mm.mm....mm.mm..",
        "....mm.mm....mm.mm..",
        "....mm.mm....mm.mm..",
        "....kk.kk....kk.kk..",
    };

    public static Sprite CyberDog()
    {
        var pal = Pal('k', C(0.04f, 0.05f, 0.07f),                    // outline / nose
                      'm', C(0.66f, 0.72f, 0.84f),                    // bright alloy
                      't', C(0.30f, 0.95f, 1f),                       // cyan collar + underglow
                      'e', C(0.35f, 1f, 1f),                          // glowing visor eye
                      'w', C(0.85f, 0.90f, 0.98f));
        return PixelArt.Get("unit_dog", DogArt, pal);
    }

    // ============================================================ HELPERS
    // Rect-based part, same idea as the old Humanoid.Part.
    public static SpriteRenderer Part(Transform parent, string name, Sprite sprite, Vector2 pos,
                                      Vector2 size, Color color, int order, float rot = 0f)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0, 0, rot);
        g.transform.localScale = size;
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    // Factory machine/console prop (pure decoration, no collision).
    public static void Machine(Transform parent, Vector2 pos, Vector2 size, Color accent)
    {
        var root = new GameObject("Machine").transform;
        root.SetParent(parent, false);
        root.position = pos;

        Color body = C(0.24f, 0.27f, 0.34f);
        Sprite sq = SpriteFactory.Square;

        Part(root, "Shadow", sq, new Vector2(0.12f, -0.14f), size, new Color(0, 0, 0, 0.40f), -8);
        Part(root, "Base", sq, Vector2.zero, size, Color.Lerp(body, Color.black, 0.45f), -7);
        Part(root, "Top", sq, new Vector2(0, 0.08f), size - new Vector2(0.16f, 0.18f), body, -6);
        Part(root, "Screen", sq, new Vector2(size.x * 0.16f, size.y * 0.14f),
             new Vector2(Mathf.Max(0.3f, size.x - 0.7f), Mathf.Max(0.2f, size.y - 0.55f)),
             C(0.05f, 0.09f, 0.08f), -5);
        Part(root, "Glow", sq, new Vector2(size.x * 0.16f, size.y * 0.14f),
             new Vector2(Mathf.Max(0.22f, size.x - 0.85f), Mathf.Max(0.12f, size.y - 0.7f)),
             new Color(accent.r, accent.g, accent.b, 0.85f), -4);
        Part(root, "LampA", sq, new Vector2(-size.x * 0.32f, size.y * 0.30f), new Vector2(0.14f, 0.14f), accent, -4);
        Part(root, "LampB", sq, new Vector2(-size.x * 0.32f, size.y * 0.06f), new Vector2(0.14f, 0.14f),
             C(1f, 0.62f, 0.15f), -4);
    }
}

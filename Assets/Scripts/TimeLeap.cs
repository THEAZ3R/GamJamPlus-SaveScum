using System.Collections.Generic;
using UnityEngine;

// TIMELEAP: the commander's two-minute window. Press T and the last three
// seconds unwind - you, your wounds, and everything in the arena snap back
// to where they stood. Every leap makes the horde stronger, stingier and
// sharper, closes the map in around you, and (from the third) lets the
// Smasher into the ring.
public class TimeLeap : MonoBehaviour
{
    public static TimeLeap Instance;

    public const float Duration = 120f;             // the full bar
    public const float HalfRefillFloor = 60f;       // past here the refill shrinks

    public static int Leaps;
    public static bool Map2;                          // the lab era after the Smasher
    public static int Map2StartLeap = -1;
    public static bool Map3;                          // the ruin after the herald
    public static int Map3StartLeap = -1;
    public static bool Map4;                          // the flesh after the vessel
    public static int Map4StartLeap = -1;
    public static float XpFloor = 0.25f;              // xp stops shrinking two leaps in

    // what every leap does to the field
    public static float PowerMul { get { return 1f + 0.15f * Leaps; } }        // +15% per leap
    public static float XpMul
    {
        get
        {
            float v = Mathf.Max(0.25f, 2f - 0.1f * Leaps);
            if (Leaps <= 1) v += 0.15f;               // the opening two leaps pay a touch better
            return Map2 ? Mathf.Max(XpFloor, v) : v;
        }
    }
    // they learn to dodge - but the lesson has a ceiling: past 0.7 the
    // sidestep stops being a tell and starts being a wall of misses
    public static float EnemySkill { get { return Mathf.Min(0.7f, 0.3f * Leaps); } }     // they learn to dodge

    public static bool BossAlive
    {
        get { return Smasher.BossAlive || Herald.BossAlive || BrokenVessel.BossAlive ||
                      Shepard.BossAlive || MirrorFighter.BossAlive; }
    }
    public float Clock { get { return clock; } }

    float clock = Duration;
    float snapT, cdT, flashT, bannerT, bossCd;
    bool pendingHerald, pendingVessel, pendingShepard, shepardSent;
    string banner = "";
    GUIStyle barStyle, bannerStyle;

    struct Snapshot
    {
        public Vector2 playerPos;
        public float playerHp;
        public List<Unit> units;
    }

    struct Unit
    {
        public Enemy e;                              // may be a dead reference by rewind time
        public EnemyKind kind;
        public Vector2 pos;
        public float hp;
    }

    readonly List<Snapshot> snaps = new List<Snapshot>(36);

    void Awake()
    {
        Instance = this;
        Leaps = 0;
        Map2 = false;
        Map2StartLeap = -1;
        Map3 = false;
        Map3StartLeap = -1;
        Map4 = false;
        Map4StartLeap = -1;
        XpFloor = 0.25f;
        clock = Duration;
        Enemy.Skill = 0f;
        Smasher.Active = null;
        Herald.Active = null;
        BrokenVessel.Active = null;
        Shepard.Active = null;
        MirrorFighter.Active = null;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Announce(string msg, float time)
    {
        banner = msg;
        bannerT = Mathf.Max(bannerT, time);
    }

    // test tooling: after a manual boss jump nothing stays queued to answer
    public static void DevClearPending()
    {
        if (Instance == null) return;
        Instance.bossCd = 0f;
        Instance.pendingHerald = false;
        Instance.pendingVessel = false;
        Instance.pendingShepard = false;
    }

    void Update()
    {
        if (GameManager.IsOver || LevelUpUI.IsOpen) return;
        if (Cutscenes.Running || GameMenu.Open) return;   // nobody leaps mid-line
        if (GameManager.IntroT > 0f) return;          // the opening card has the room

        float dt = Time.deltaTime;

        // the timeline is hijacked: while the ring belongs to a boss (and
        // while something is falling toward it) the commander's clock holds
        bool hijacked = BossAlive || bossCd > 0f;
        if (bossCd > 0f)
        {
            bossCd -= dt;
            if (bossCd <= 0f && !BossAlive)
            {
                if (pendingShepard)
                {
                    pendingShepard = false;
                    shepardSent = true;
                    var gm2 = GameManager.Instance;
                    if (gm2 != null) gm2.MeltTheHorde();      // the horde becomes sludge...
                    Shepard.Spawn(EnemySpawner.Difficulty);   // ...and something steps into it
                }
                else if (pendingVessel) { pendingVessel = false; BrokenVessel.Spawn(EnemySpawner.Difficulty); }
                else if (pendingHerald) { pendingHerald = false; Herald.Spawn(EnemySpawner.Difficulty); }
                else Smasher.Spawn(EnemySpawner.Difficulty);
            }
        }

        if (!hijacked)
        {
            clock -= dt;                             // the commander is leaving
            if (clock <= 0f)
            {
                clock = 0f;
                GameManager.EndRun("THE COMMANDER ESCAPED");
                return;
            }
        }

        if (cdT > 0f) cdT -= dt;
        if (flashT > 0f) flashT -= dt;
        if (bannerT > 0f) bannerT -= dt;

        snapT -= dt;
        if (snapT <= 0f) { snapT = 0.1f; Record(); }

        if (Bindings.Down(Bindings.Action.Timeleap)) TryLeap();
    }

    // ------------------------------------------------------------ tape
    // a frame every tenth of a second, three seconds long
    void Record()
    {
        var p = PlayerController.Instance;
        if (p == null || p.Health == null) return;

        var s = new Snapshot
        {
            playerPos = p.transform.position,
            playerHp = p.Health.current,
            units = new List<Unit>(Enemy.All.Count),
        };
        for (int i = 0; i < Enemy.All.Count; i++)
        {
            var e = Enemy.All[i];
            if (e == null || e.Health == null) continue;
            s.units.Add(new Unit { e = e, kind = e.KindOf, pos = e.transform.position, hp = e.Health.current });
        }

        snaps.Add(s);
        if (snaps.Count > 30) snaps.RemoveAt(0);     // exactly three seconds back
    }

    // ------------------------------------------------------------ rewind
    void TryLeap()
    {
        if (BossAlive)
        {
            string who = BrokenVessel.BossAlive ? "THE VESSEL HOLDS REALITY"
                       : Herald.BossAlive ? "THE HERALD HOLDS THE LINE"
                       : Smasher.BossAlive ? "THE SMASHER HOLDS THE RING"
                       : MirrorFighter.BossAlive ? "THE MIRROR HOLDS THE GUN"
                       : "SHEPARD HOLDS THE TAPE";
            Announce("TIMELEAP LOCKED - " + who, 1.8f);
            return;
        }
        if (cdT > 0f || snaps.Count == 0) return;

        var p = PlayerController.Instance;
        if (p == null || p.Health == null || p.Health.IsDead) return;

        Snapshot s = snaps[0];                       // three seconds ago

        // the room rewinds with you: the freshly killed stand back up, the
        // freshly spawned walk out of the timeline
        for (int i = 0; i < s.units.Count; i++)
        {
            var u = s.units[i];
            if (u.e != null)
            {
                u.e.RewindTo(u.pos, u.hp);
            }
            else
            {
                var e = Enemy.Spawn(u.kind, u.pos, EnemySpawner.Difficulty);
                if (e != null)
                {
                    if (e.Health != null) e.Health.Restore(u.hp);
                    u.e = e;                          // adopt the revived body
                    s.units[i] = u;
                }
            }
        }

        // anything that was not on the tape yet walks out of the timeline
        var live = Enemy.All.ToArray();
        foreach (var e in live)
        {
            if (e == null) continue;
            bool known = false;
            for (int i = 0; i < s.units.Count; i++)
                if (s.units[i].e == e) { known = true; break; }
            if (!known) e.Despawn();
        }

        p.RewindTo(s.playerPos, s.playerHp);
        Sfx.PlayAt(p.transform.position, "rewind", 0.85f);

        // the bar pays out: full before the last minute, and from there it
        // slides down to exactly half at the very last second
        clock = clock >= HalfRefillFloor ? Duration : Mathf.Min(Duration, HalfRefillFloor + clock);

        Leaps++;
        Enemy.Skill = EnemySkill;
        for (int i = 0; i < Enemy.All.Count; i++)
            if (Enemy.All[i] != null) Enemy.All[i].ApplyLeapBuff();

        var gm = GameManager.Instance;
        if (gm != null) gm.ApplyLeapMap(Leaps);

        // which boss answers this leap: the arena from the third, the lab from
        // its third, the ruin two leaps after the herald's fall, and the flesh
        // answers its first leap with the one who wears your face
        bool bossComing = false;
        if (Map4 && !BossAlive)
        {
            int m4 = Leaps - Map4StartLeap;
            if (m4 >= 1 && !shepardSent)
            {
                bossComing = true;
                pendingShepard = true;
                pendingVessel = false;
                pendingHerald = false;
            }
        }
        else if (Map3 && !BossAlive)
        {
            int m3 = Leaps - Map3StartLeap;           // two leaps of ruin, then it takes form
            if (m3 >= 2 && (m3 - 2) % 3 == 0)
            {
                bossComing = true;
                pendingVessel = true;
                pendingHerald = false;
            }
        }
        else if (!Map2 && Leaps >= 3 && !BossAlive)
        {
            bossComing = true;
            pendingHerald = false;
            pendingVessel = false;
        }
        else if (Map2 && !BossAlive)
        {
            int m = Leaps - Map2StartLeap;             // leaps since the glitch
            if (m >= 2 && (m - 2) % 3 == 0)           // one leap of lab, then evolution
            {
                bossComing = true;
                pendingHerald = true;
                pendingVessel = false;
            }
        }
        if (bossComing) bossCd = 2.6f;

        // the leap itself: a violet bell through the whole arena
        Vector2 here = p.transform.position;
        Fx.Ring(here, new Color(0.75f, 0.45f, 1f), 0.4f, 7f, 0.6f, 30);
        Fx.Burst(here, new Color(0.75f, 0.45f, 1f), 26, 9f);
        Fx.Sparks(here, Color.white, 20, 13f);
        GameManager.Shake(0.55f);
        if (gm != null) gm.HitStop(0.12f);

        flashT = 0.45f;
        cdT = 2f;
        snaps.Clear();                               // the tape starts fresh
        Announce("TIMELEAP " + Leaps + " - THE HORDE SHARPENS", 2f);
        if (bossComing)
        {
            Announce(pendingShepard
                ? "!! SOMETHING WEARS YOUR FACE !!"
                : pendingVessel
                ? "!! SOMETHING IS CHEWING THROUGH REALITY !!"
                : pendingHerald
                ? "!! PREPARE FOR THE GLORIOUS EVOLUTION !!"
                : "!! SOMETHING HAS HIJACKED THE TIMELINE !!", 2.8f);
            var pp = PlayerController.Instance;
            Sfx.PlayAt(pp != null ? (Vector2)pp.transform.position : Vector2.zero, "horn", 0.9f,
                      pendingShepard ? 0.72f : pendingVessel ? 0.8f : pendingHerald ? 1.25f : 1f);
        }
    }

    // the Smasher falls: the system glitches and the lab recompiles
    public void OnSmasherDown()
    {
        Map2 = true;
        Map2StartLeap = Leaps;
        XpFloor = Mathf.Max(0.25f, 2f - 0.1f * (Leaps + 2));   // xp sinks two more leaps, then holds
        if (GameManager.Instance != null) GameManager.Instance.GlitchToMap2();
    }

    // the herald falls: the lab loses its grip and the ruin takes the room
    public void OnHeraldDown()
    {
        Map3 = true;
        Map3StartLeap = Leaps;
        if (GameManager.Instance != null) GameManager.Instance.GlitchToMap3();
    }

    // the vessel falls: the last reality lets go, and the flesh takes the room
    public void OnVesselDown()
    {
        Map4 = true;
        Map4StartLeap = Leaps;
        if (GameManager.Instance != null) GameManager.Instance.GlitchToMap4();
    }

    // ------------------------------------------------------------ hud
    // the bar rides the top centre - health is top left, the clock is opposite
    void OnGUI()
    {
        if (PlayerController.Instance == null || GameManager.IsOver) return;
        if (Cutscenes.Running || GameMenu.Open) return;   // the tape covers the hud

        if (barStyle == null)
            barStyle = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(), fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
        if (bannerStyle == null)
            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(), fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };

        // top right corner: health owns the left, the clock owns the right
        float w = Mathf.Min(520f, Screen.width * 0.38f);
        float x = Screen.width - w - 16f, y = 14f;
        float frac = Mathf.Clamp01(clock / Duration);

        // a dark plate under the corner: the dashed floor lines that run
        // through this area can never eat the clock's words
        GUI.color = new Color(0f, 0f, 0f, 0.62f);
        GUI.DrawTexture(new Rect(x - 6f, y - 4f, w + 12f, 58f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // the clock wears the same frame as the bars on the left
        bool hijackedNow = BossAlive || bossCd > 0f;
        bool panic = clock < 15f && !hijackedNow;
        GameManager.GuiBar(new Rect(x, y, w, 26f),
            panic ? (Mathf.Sin(Time.unscaledTime * 10f) > 0f ? new Color(1f, 0.22f, 0.15f) : new Color(0.65f, 0.1f, 0.1f))
                  : new Color(0.55f, 0.9f, 1f),
            frac, Art.IconClock());

        GUI.color = Color.white;
        barStyle.fontSize = w >= 380f ? 14 : 12;      // narrow screens keep every word
        GUI.Label(new Rect(x, y + 28f, w, 20f),
                  "TIMELEAP " + (int)clock + "s | LEAPS " + Leaps +
                  (hijackedNow ? " | TIMELINE HIJACKED" :
                   (BossAlive ? " | [" + Bindings.Label(Bindings.Action.Timeleap) + "] LOCKED"
                              : " | [" + Bindings.Label(Bindings.Action.Timeleap) + "] REWIND 3s")), barStyle);

        if (bannerT > 0f)
        {
            GUI.color = new Color(0.85f, 0.6f, 1f, Mathf.Clamp01(bannerT * 1.5f));
            GUI.Label(new Rect(0, Screen.height * 0.24f, Screen.width, 60f), banner, bannerStyle);
        }
        if (flashT > 0f)
        {
            GUI.color = new Color(0.8f, 0.55f, 1f, flashT * 0.9f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }
}

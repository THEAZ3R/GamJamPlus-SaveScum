using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public static bool IsOver { get; private set; }
    public static bool EndPrompt { get; private set; } // the big T is up: you may still walk
    public static bool Won;                           // the mirror falls: you win
    public static string EndMessage;                 // what the run ended on
    public static int Kills;
    public static float RunT;                        // seconds spent actually in the ring
    public static Vector2 ArenaHalf = new Vector2(16f, 9f);   // shepard shrinks it
    public static Vector2 CamTarget;                  // where the tape wants the lens
    public static bool Lab;                          // era two: the clean room
    public static bool Crumble;                      // era three: reality broken
    public static bool Flesh;                        // era four: the cosmic body
    public static float DesatT;                      // stasis: the color leaves

    static Vector2 camFocus;                          // eased toward CamTarget
    static float shake, glitchT;
    float hitStopT, hitStopPrev = 1f;               // hit-stop remembers the room it paused
    public static float IntroT;                     // the five-second opening card
    bool cursorFree;                                // the pointer is only ever free for a menu
    Camera cam;
    bool aimDebug;                                 // F6: the whole aim chain, laid out on screen
    Transform mapRoot;

    // what every map object hangs off (debris, decals - torn down with it)
    public static Transform Root { get { return Instance != null ? Instance.mapRoot : null; } }

    public static void Shake(float amount) { shake = Mathf.Max(shake, amount); }

    // BOSS HP: the wall grows with the run, but past difficulty 3 it stops
    // climbing - damage still scales the whole way, so a slow run with a
    // weak build drags the fight out without ever making it endless
    public static float HpScale(float difficulty) { return Mathf.Min(difficulty, 3f); }

    void Awake()
    {
        Instance = this;
        IsOver = false;
        Won = false;
        EndMessage = null;
        Kills = 0;
        RunT = 0f;
        shake = 0f;
        CamTarget = Vector2.zero;
        camFocus = Vector2.zero;
        Time.timeScale = 1f;
        IntroT = 0f;                                // BeginRun decides whether the card shows
        EndPrompt = false;
        ArenaHalf = new Vector2(16f, 9f);            // the room starts whole
        DesatT = 0f;

        Enemy.Alive = 0;
        Enemy.BigAlive = 0;
        Enemy.Bulwarks.Clear();
        Enemy.All.Clear();
        Enemy.Skill = 0f;
        Smasher.Active = null;
        Herald.Active = null;
        BrokenVessel.Active = null;
        Shepard.Active = null;
        MirrorFighter.Active = null;
        Shepard.VhsActive = false;                    // no tape left paused here
        Shepard.VhsCountdown = 0f;
        Lab = false;
        Crumble = false;
        Flesh = false;
        Art.Lab = false;
        glitchT = 0f;
        WallPieces.Clear();
        Booster.All.Clear();
        Pit.Reset();
        Gore.Reset();
        Rubble.ClearAll();
        AcidRain.Reset();

        if (GetComponent<TimeLeap>() == null) gameObject.AddComponent<TimeLeap>();

        cam = Camera.main;
        cam.orthographic = true;
        cam.backgroundColor = new Color(0.02f, 0.025f, 0.04f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0, 0, -10);

        Sfx.Ensure();                       // the rig and its one listener, moved
        // out of the shot before the first frame can draw their gizmos in it
        Music.Ensure();                     // and the other half of the silence:
        // the chiptune rig, parked with it, silent until the run starts

        BuildArena();
    }

    void Start()
    {
        // all Awakes have run by now, so the map is guaranteed to exist:
        // drop the player onto clear ground instead of the centre wall
        var p = PlayerController.Instance;
        if (p != null)
        {
            var rb = p.GetComponent<Rigidbody2D>();
            p.transform.position = new Vector2(0f, -2.4f);
            rb.position = new Vector2(0f, -2.4f);
            rb.SetVel(Vector2.zero);
        }

        // where a run comes from: the menu on launch, the tape once per play
        // session, and straight back into the ring after a restart
        if (GameFlow.MenuOnStart) GameMenu.Show();
        else if (!Cutscenes.IntroSeen) Cutscenes.PlayIntro();
        else BeginRun(true);
    }

    // ------------------------------------------------------------ RUN START
    // the how-to card holds the room for five seconds - or not at all, when
    // the intro tape has already said its piece
    public void BeginRun(bool card)
    {
        IntroT = card ? 5f : 0f;
        if (card) return;
        Time.timeScale = 1f;
        Sfx.Play("levelup");
    }

    // the tape offers the only button left
    public static void SetEndPrompt(bool on)
    {
        EndPrompt = on;
        if (!on) return;
        var p = PlayerController.Instance;
        if (p != null && p.Health != null) p.Health.invulnerable = true;
    }

    // ---------------------------------------------------------------- MAP
    void BuildArena()
    {
        Nav.Reset();
        mapRoot = new GameObject("Map").transform;

        float w = ArenaHalf.x, h = ArenaHalf.y;

        BuildFloor(w, h);
        BuildMachines(w, h);
        BuildVats();
        BuildWallsAndTrim(w, h);

        // ---- cover in the centre: 6 shootable walls + 2 unbreakable pillars,
        //      laid out as a small bunker - every gap between them is wider
        //      than the player, so you can always slip behind the next piece
        Wall(new Vector2(0f, 4.6f), new Vector2(6f, 1.2f));        // centre block (kept)
        Wall(new Vector2(-8.5f, 3.8f), new Vector2(1.2f, 4.5f));   // flanks pushed out for the wider holes
        Wall(new Vector2(8.5f, -3.8f), new Vector2(1.2f, 4.5f));
        Wall(new Vector2(0f, 2.4f), new Vector2(2.6f, 1f));        // header above the bunker
        Wall(new Vector2(-2.6f, -2.6f), new Vector2(1f, 1.8f));    // low cover beside the drop-in
        Wall(new Vector2(2.6f, -2.6f), new Vector2(1f, 1.8f));
        SolidWall(new Vector2(-1.7f, -0.2f), new Vector2(1.2f, 1.8f));   // permanent pillars
        SolidWall(new Vector2(1.7f, -0.2f), new Vector2(1.2f, 1.8f));

        // ---- 4 holes flanking the bunker (each 2.6 wide vs a 3.7 dash reach,
        //      so every gap is still crossable, and the rims clear the walls)
        PitZone(new Vector2(-5.6f, 3f), new Vector2(2.6f, 2.6f));
        PitZone(new Vector2(5.6f, 3f), new Vector2(2.6f, 2.6f));
        PitZone(new Vector2(-5.6f, -3f), new Vector2(2.6f, 2.6f));
        PitZone(new Vector2(5.6f, -3f), new Vector2(2.6f, 2.6f));

        // ---- 4 boosters: the side pair feeds the open cross band, the vertical
        //      pair drops you into the 2.2-wide lane between the two pillars
        Booster.Create(mapRoot, new Vector2(-10.5f, 0f), new Vector2(4f, 2.2f), Vector2.right);
        Booster.Create(mapRoot, new Vector2(10.5f, 0f), new Vector2(4f, 2.2f), Vector2.left);
        Booster.Create(mapRoot, new Vector2(0f, 7.2f), new Vector2(2.2f, 2.8f), Vector2.down);
        Booster.Create(mapRoot, new Vector2(0f, -7.2f), new Vector2(2.2f, 2.8f), Vector2.up);

        Nav.Bake(ArenaHalf);
    }

    void BuildFloor(float w, float h)
    {
        Sprite fa = Art.FloorA(), fb = Art.FloorB();
        Color tileFade = new Color(1f, 1f, 1f, 0.74f);   // sit back into the floor
        for (int x = -(int)w; x < (int)w; x += 2)
            for (int y = -(int)h; y < (int)h; y += 2)
            {
                bool alt = (((x + y) / 2) & 1) == 0;
                var g = new GameObject("Tile");
                g.transform.SetParent(mapRoot, false);
                g.transform.position = new Vector2(x + 1, y + 1);
                g.transform.localScale = new Vector3(2.02f, 2.02f, 1f);
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = alt ? fa : fb;
                sr.color = tileFade;
                sr.sortingOrder = -20;
            }

        // toxic floor pools (decoration only)
        Color slime = new Color(0.28f, 0.68f, 0.22f, 0.42f);
        Block("Slime", SpriteFactory.Square, new Vector2(-14.6f, 2.6f), new Vector2(2.4f, 1.6f), slime, -18);
        Block("Slime", SpriteFactory.Square, new Vector2(14.6f, -2.6f), new Vector2(2.4f, 1.6f), slime, -18);
    }

    void BuildMachines(float w, float h)
    {
        Color cyan = new Color(0.25f, 0.9f, 1f);
        Color green = new Color(0.4f, 1f, 0.45f);

        // machine rows hugging the top / bottom walls (decoration only)
        for (int i = -2; i <= 2; i++)
        {
            if (i == 0) continue;
            float x = i * 5.5f;
            Art.Machine(mapRoot, new Vector2(x, h - 1.0f), new Vector2(3.2f, 1.7f), i % 2 == 0 ? green : cyan);
            Art.Machine(mapRoot, new Vector2(x, -h + 1.0f), new Vector2(3.2f, 1.7f), i % 2 == 0 ? cyan : green);
        }

        // small consoles in the corners
        Art.Machine(mapRoot, new Vector2(-w + 1.4f, h - 1.4f), new Vector2(1.8f, 1.3f), cyan);
        Art.Machine(mapRoot, new Vector2(w - 1.4f, h - 1.4f), new Vector2(1.8f, 1.3f), green);
        Art.Machine(mapRoot, new Vector2(-w + 1.4f, -h + 1.4f), new Vector2(1.8f, 1.3f), green);
        Art.Machine(mapRoot, new Vector2(w - 1.4f, -h + 1.4f), new Vector2(1.8f, 1.3f), cyan);
    }

    void BuildVats()
    {
        Vector2[] spots =
        {
            new Vector2(-14.6f, 5.4f), new Vector2(-14.6f, -5.4f),
            new Vector2(14.6f, 5.4f), new Vector2(14.6f, -5.4f),
        };
        foreach (var p in spots)
        {
            var g = new GameObject("Vat");
            g.transform.SetParent(mapRoot, false);
            g.transform.position = p;
            g.transform.localScale = Vector3.one * 1.3f;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Vat();
            sr.sortingOrder = -10;
        }
    }

    void BuildWallsAndTrim(float w, float h)
    {
        // outer boundary (solid, invisible)
        Color wall = new Color(0.14f, 0.16f, 0.22f);
        Color neon = new Color(0.25f, 0.85f, 1f);
        Block("WallTop", SpriteFactory.Square, new Vector2(0, h + 1.5f), new Vector2(w * 2 + 6, 3), wall, -6, true);
        Block("WallBottom", SpriteFactory.Square, new Vector2(0, -h - 1.5f), new Vector2(w * 2 + 6, 3), wall, -6, true);
        Block("WallLeft", SpriteFactory.Square, new Vector2(-w - 1.5f, 0), new Vector2(3, h * 2 + 6), wall, -6, true);
        Block("WallRight", SpriteFactory.Square, new Vector2(w + 1.5f, 0), new Vector2(3, h * 2 + 6), wall, -6, true);

        Block("TrimTop", SpriteFactory.Square, new Vector2(0, h + 0.07f), new Vector2(w * 2, 0.14f), neon, -4);
        Block("TrimBottom", SpriteFactory.Square, new Vector2(0, -h - 0.07f), new Vector2(w * 2, 0.14f), neon, -4);
        Block("TrimLeft", SpriteFactory.Square, new Vector2(-w - 0.07f, 0), new Vector2(0.14f, h * 2), neon, -4);
        Block("TrimRight", SpriteFactory.Square, new Vector2(w + 0.07f, 0), new Vector2(0.14f, h * 2), neon, -4);

        // hazard stripes inside the top / bottom edges
        Sprite hz = Art.HazardStripe();
        for (float x = -w; x < w; x += 2f)
        {
            Tile("Haz", hz, new Vector2(x + 1f, h - 0.55f), new Vector2(2f, 0.55f), -16);
            Tile("Haz", hz, new Vector2(x + 1f, -h + 0.55f), new Vector2(2f, 0.55f), -16);
        }

        // pipe runs along the side walls
        Color pipe = new Color(0.22f, 0.25f, 0.32f);
        for (float y = -h + 1.4f; y < h - 1f; y += 2.2f)
        {
            Block("PipeL", SpriteFactory.Square, new Vector2(-w + 0.55f, y), new Vector2(0.45f, 1.5f), pipe, -12);
            Block("PipeR", SpriteFactory.Square, new Vector2(w - 0.55f, y + 1.1f), new Vector2(0.45f, 1.5f), pipe, -12);
        }
    }

    void Wall(Vector2 pos, Vector2 size)
    {
        Nav.AddObstacle(pos, size);     // blocks movement (also for holes)
        Nav.AddSight(pos, size);        // blocks line of sight

        var wall = DestructibleWall.Create(pos, size, 260f, -3);
        wall.transform.SetParent(mapRoot, false);
        RegisterWall(pos, size, false, wall.gameObject, wall);
    }

    // cover you cannot shoot apart: same steel tile in a colder tint, no bar
    // to chip away - bullets simply stop on it
    void SolidWall(Vector2 pos, Vector2 size)
    {
        Nav.AddObstacle(pos, size);
        Nav.AddSight(pos, size);

        var sr = Block("HardWall", Art.WallTile(), pos, size, new Color(0.60f, 0.70f, 0.92f), -3, true);
        RegisterWall(pos, size, true, sr.gameObject, null);
    }

    // every map wall, permanent or not - the Smasher asks for them by place
    struct WallPiece
    {
        public Vector2 pos, size;
        public bool solid;
        public GameObject go;
        public DestructibleWall wall;                // null for the permanent ones
    }
    static readonly List<WallPiece> WallPieces = new List<WallPiece>();

    void RegisterWall(Vector2 pos, Vector2 size, bool solid, GameObject go, DestructibleWall wall)
    {
        WallPieces.Add(new WallPiece { pos = pos, size = size, solid = solid, go = go, wall = wall });
    }

    // a phase two leap lands like a meteor: everything in the path is scrap
    public static void SmashWallsNear(Vector2 pos, float radius)
    {
        bool any = false;
        for (int i = WallPieces.Count - 1; i >= 0; i--)
        {
            var wp = WallPieces[i];
            if (wp.go == null) { WallPieces.RemoveAt(i); continue; }
            if (Vector2.Distance(wp.pos, pos) > radius) continue;

            Nav.RemoveObstacle(wp.pos, wp.size);
            Nav.RemoveSight(wp.pos, wp.size);
            Fx.Burst(wp.pos, wp.solid ? new Color(0.85f, 0.9f, 1f) : new Color(0.7f, 0.75f, 0.85f), 24, 9f);
            Fx.Sparks(wp.pos, Color.white, 16, 13f);
            Sfx.PlayAt(wp.pos, "wall_smash", 0.8f);
            Destroy(wp.go);
            WallPieces.RemoveAt(i);
            any = true;
        }
        if (any) Nav.Bake(ArenaHalf);
    }

    void PitZone(Vector2 center, Vector2 size)
    {
        Nav.AddObstacle(center, size);  // enemies cannot path over holes
        Pit.Add(center, size);

        // caution rim behind, dark void on top
        Block("PitRim", Art.HazardStripe(), center, size + new Vector2(0.5f, 0.5f), Color.white, -15);
        Block("Pit", Art.VoidTile(), center, size, Color.white, -14);

        // and a bright lip around the hole: black voids read as voids on any
        // floor - violet, purple, meat - so the edge is never a surprise
        Color lip = new Color(1f, 1f, 1f, 0.85f);
        float t = 0.13f;
        Block("PitEdge", SpriteFactory.Square,
              center + Vector2.up * (size.y * 0.5f + t * 0.5f),
              new Vector2(size.x + t, t), lip, -13);
        Block("PitEdge", SpriteFactory.Square,
              center - Vector2.up * (size.y * 0.5f + t * 0.5f),
              new Vector2(size.x + t, t), lip, -13);
        Block("PitEdge", SpriteFactory.Square,
              center + Vector2.right * (size.x * 0.5f + t * 0.5f),
              new Vector2(t, size.y + t), lip, -13);
        Block("PitEdge", SpriteFactory.Square,
              center - Vector2.right * (size.x * 0.5f + t * 0.5f),
              new Vector2(t, size.y + t), lip, -13);
    }

    // ---------------------------------------------------------------- TIMELEAP
    // Every leap tightens the arena: first the boosters go dark, then new hard
    // walls and fresh holes close the open ground around the bunker.
    // Pairs per leap - mirrored so neither side gets a free corner.
    static readonly Vector2[] LeapWallPos =
    {
        new Vector2(-13f, 5.6f),  new Vector2(13f, -5.6f),     // leap 3
        new Vector2(-7.5f, -6.6f), new Vector2(7.5f, 6.6f),    // leap 4
        new Vector2(-13f, -5.6f), new Vector2(13f, 5.6f),      // leap 5
        new Vector2(-11.5f, 7f),  new Vector2(11.5f, -7f),     // leap 6
        new Vector2(-4.6f, 6.8f), new Vector2(4.6f, -6.8f),    // leap 7
        new Vector2(-14.2f, 0.9f),new Vector2(14.2f, -0.9f),   // leap 8
        new Vector2(-9.2f, 0.9f), new Vector2(9.2f, -0.9f),    // leap 9
    };
    static readonly Vector2[] LeapWallSize =
    {
        new Vector2(1.4f, 3.6f),  new Vector2(1.4f, 3.6f),
        new Vector2(3.6f, 1.4f),  new Vector2(3.6f, 1.4f),
        new Vector2(1.4f, 3.6f),  new Vector2(1.4f, 3.6f),
        new Vector2(2.8f, 1.2f),  new Vector2(2.8f, 1.2f),
        new Vector2(1.2f, 2.6f),  new Vector2(1.2f, 2.6f),
        new Vector2(1f, 4.4f),    new Vector2(1f, 4.4f),
        new Vector2(1f, 4f),      new Vector2(1f, 4f),
    };
    static readonly Vector2[] LeapPitPos =
    {
        new Vector2(-12.5f, -0.2f), new Vector2(12.5f, 0.2f),  // leap 3
        new Vector2(-3.2f, -5.9f),  new Vector2(3.2f, 5.9f),   // leap 4
        new Vector2(-11.6f, 4.6f),  new Vector2(11.6f, -4.6f), // leap 5
        new Vector2(-0.4f, -6.6f),  new Vector2(0.4f, 6.6f),   // leap 6
        new Vector2(-14f, -6.9f),   new Vector2(14f, 6.9f),    // leap 7
        new Vector2(-6.9f, 0.4f),   new Vector2(6.9f, -0.4f),  // leap 8
        new Vector2(-2.9f, -7f),    new Vector2(2.9f, 7f),     // leap 9
    };
    static readonly Vector2[] LeapPitSize =
    {
        new Vector2(2.4f, 2.2f),  new Vector2(2.4f, 2.2f),
        new Vector2(2.4f, 2f),    new Vector2(2.4f, 2f),
        new Vector2(2.4f, 2.4f),  new Vector2(2.4f, 2.4f),
        new Vector2(2.4f, 2f),    new Vector2(2.4f, 2f),
        new Vector2(2.2f, 2f),    new Vector2(2.2f, 2f),
        new Vector2(2.4f, 2.4f),  new Vector2(2.4f, 2.4f),
        new Vector2(2.6f, 1.8f),  new Vector2(2.6f, 1.8f),
    };

    public void ApplyLeapMap(int leap)
    {
        if (TimeLeap.Map4) { ApplyFleshLeap(leap - TimeLeap.Map4StartLeap); return; }
        if (TimeLeap.Map3) { ApplyCrumbleLeap(leap - TimeLeap.Map3StartLeap); return; }
        if (Lab) { ApplyLabLeap(leap - TimeLeap.Map2StartLeap); return; }

        Color ghost = new Color(0.55f, 0.95f, 1f);

        if (leap == 1) KillBoosters(new Vector2(1f, 0f));      // the side pair dies
        else if (leap == 2) KillBoosters(new Vector2(0f, 1f)); // then the vertical pair

        int i = leap - 3;                                       // walls and holes start at leap 3
        if (i >= 0 && i + 1 < LeapWallPos.Length)
        {
            SolidWall(LeapWallPos[i], LeapWallSize[i]);
            Fx.Ring(LeapWallPos[i], ghost, 0.3f, 3.4f, 0.5f, 5);
            Fx.Ring(LeapWallPos[i + 1], ghost, 0.3f, 3.4f, 0.5f, 5);
        }
        if (i >= 0 && i + 1 < LeapPitPos.Length)
        {
            PitZone(LeapPitPos[i], LeapPitSize[i]);
            PitZone(LeapPitPos[i + 1], LeapPitSize[i + 1]);
        }
        if (leap >= 3) Nav.Bake(ArenaHalf);                     // the field itself got smaller
    }

    // the pads spark once and go dark - the arena stops handing out speed
    void KillBoosters(Vector2 axis)
    {
        var kill = new List<Booster>();
        foreach (var b in Booster.All)
            if (b != null && Mathf.Abs(Vector2.Dot(b.Dir, axis)) > 0.5f) kill.Add(b);
        foreach (var b in kill)
        {
            Fx.Burst(b.transform.position, new Color(0.25f, 0.95f, 1f), 8, 4f);
            Destroy(b.gameObject);
        }
    }

    // ---------------------------------------------------------------- LAB
    // the glitch after the Smasher: the old world is torn down and the lab
    // recompiles around you - violet floor, circuit grid, clean-room walls.
    public void GlitchToMap2()
    {
        glitchT = 4.2f;
        Sfx.Play("glitch");                           // the system starts eating itself
        StartCoroutine(RebuildLabAfter(2.3f));
    }

    IEnumerator RebuildLabAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        RebuildLab();
    }

    void RebuildLab()
    {
        Lab = true;
        Art.Lab = true;
        cam.backgroundColor = new Color(0.03f, 0.02f, 0.065f);

        // the old world lifts off the tape
        FluidPool.Clear();
        HazardMachine.ClearAll();
        Vent.ClearAll();
        Trap.ClearAll();

        // the pack the lab does not know goes with it (no xp - they never died)
        foreach (var e in Enemy.All.ToArray())
        {
            if (e == null) continue;
            if (e.KindOf == EnemyKind.Bouncer || e.KindOf == EnemyKind.Dasher ||
                e.KindOf == EnemyKind.Spider) continue;
            e.Despawn();
        }

        Destroy(mapRoot.gameObject);
        Pit.Reset();
        Nav.Reset();
        WallPieces.Clear();

        mapRoot = new GameObject("Map").transform;
        float w = ArenaHalf.x, h = ArenaHalf.y;

        BuildLabFloor(w, h);
        BuildLabWallsAndTrim(w, h);
        BuildHexPlatform();
        BuildLabPylons();
        BuildLabDecor(w, h);

        Nav.Bake(ArenaHalf);

        var p = PlayerController.Instance;
        if (p != null)
        {
            Vector2 pos = p.transform.position;
            Fx.Ring(pos, new Color(0.55f, 0.95f, 1f), 0.4f, 6f, 0.55f, 30);
            Fx.Burst(pos, new Color(0.7f, 0.4f, 1f), 24, 9f);
            Sfx.PlayAt(pos, "chrono", 0.7f);          // the world recompiles around you
        }
        if (TimeLeap.Instance != null)
            TimeLeap.Instance.Announce("SYSTEM RECALIBRATED // THE LAB", 2.4f);
    }

    void BuildLabFloor(float w, float h)
    {
        Sprite fa = Art.FloorA(), fb = Art.FloorB();
        Color tile = new Color(0.60f, 0.48f, 1.15f, 0.82f);          // deep violet
        Color grid = new Color(0.3f, 0.9f, 1f, 0.10f);               // circuit traces

        for (int x = -(int)w; x < (int)w; x += 2)
            for (int y = -(int)h; y < (int)h; y += 2)
            {
                bool alt = (((x + y) / 2) & 1) == 0;
                var g = new GameObject("Tile");
                g.transform.SetParent(mapRoot, false);
                g.transform.position = new Vector2(x + 1, y + 1);
                g.transform.localScale = new Vector3(2.02f, 2.02f, 1f);
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = alt ? fa : fb;
                sr.color = tile;
                sr.sortingOrder = -20;
            }

        // the grid: faint glowing traces, like a circuit board
        for (float x = -w; x <= w; x += 2f)
            Block("GridV", SpriteFactory.Square, new Vector2(x, 0f), new Vector2(0.06f, h * 2f), grid, -19);
        for (float y = -h; y <= h; y += 2f)
            Block("GridH", SpriteFactory.Square, new Vector2(0f, y), new Vector2(w * 2f, 0.06f), grid, -19);
    }

    void BuildLabWallsAndTrim(float w, float h)
    {
        Color wall = new Color(0.16f, 0.12f, 0.28f);                 // deep indigo
        Color neon = new Color(0.35f, 0.9f, 1f);                     // clean cyan rim
        Block("WallTop", SpriteFactory.Square, new Vector2(0, h + 1.5f), new Vector2(w * 2 + 6, 3), wall, -6, true);
        Block("WallBottom", SpriteFactory.Square, new Vector2(0, -h - 1.5f), new Vector2(w * 2 + 6, 3), wall, -6, true);
        Block("WallLeft", SpriteFactory.Square, new Vector2(-w - 1.5f, 0), new Vector2(3, h * 2 + 6), wall, -6, true);
        Block("WallRight", SpriteFactory.Square, new Vector2(w + 1.5f, 0), new Vector2(3, h * 2 + 6), wall, -6, true);

        Block("TrimTop", SpriteFactory.Square, new Vector2(0, h + 0.07f), new Vector2(w * 2, 0.14f), neon, -4);
        Block("TrimBottom", SpriteFactory.Square, new Vector2(0, -h - 0.07f), new Vector2(w * 2, 0.14f), neon, -4);
        Block("TrimLeft", SpriteFactory.Square, new Vector2(-w - 0.07f, 0), new Vector2(0.14f, h * 2), neon, -4);
        Block("TrimRight", SpriteFactory.Square, new Vector2(w + 0.07f, 0), new Vector2(0.14f, h * 2), neon, -4);

        // magenta hazard stripes inside the edges
        Sprite hz = Art.HazardStripe();
        for (float x = -w; x < w; x += 2f)
        {
            Tile("Haz", hz, new Vector2(x + 1f, h - 0.55f), new Vector2(2f, 0.55f), -16);
            Tile("Haz", hz, new Vector2(x + 1f, -h + 0.55f), new Vector2(2f, 0.55f), -16);
        }
    }

    // the glowing hexagon: the platform boss two fights you on
    void BuildHexPlatform()
    {
        Block("HexGlow", SpriteFactory.Circle, Vector2.zero, new Vector3(8.6f, 4.6f, 1f),
              new Color(0.35f, 0.9f, 1f, 0.12f), -19);
        Block("HexCore", Art.HexPlatform(), Vector2.zero, new Vector2(7.6f, 3.9f), Color.white, -18);
    }

    // server pylons: hexagonal cover that reads as clean-room equipment
    void BuildLabPylons()
    {
        LabPylon(new Vector2(-9f, 4.6f));
        LabPylon(new Vector2(9f, -4.6f));
        LabPylon(new Vector2(-9f, -4.6f));
        LabPylon(new Vector2(9f, 4.6f));
        LabPylon(new Vector2(0f, 6f));
        LabPylon(new Vector2(0f, -6f));

        // floor vents: the herald shares a nervous system with these
        Vent.Spawn(new Vector2(-4.8f, 2.4f));
        Vent.Spawn(new Vector2(4.8f, -2.4f));
        Vent.Spawn(new Vector2(4.8f, 2.4f));
        Vent.Spawn(new Vector2(-4.8f, -2.4f));
        Vent.Spawn(new Vector2(0f, 3.6f));
        Vent.Spawn(new Vector2(0f, -3.6f));
    }

    void LabPylon(Vector2 pos)
    {
        Nav.AddObstacle(pos, new Vector2(1.6f, 1.6f));
        Nav.AddSight(pos, new Vector2(1.6f, 1.6f));

        Block("PylonGlow", SpriteFactory.Circle, pos, new Vector3(2.4f, 2.4f, 1f),
              new Color(0.35f, 0.9f, 1f, 0.10f), -5);
        var sr = Block("Pylon", Art.Pylon(), pos, new Vector3(1.9f, 1.9f, 1f), Color.white, -3, true);
        RegisterWall(pos, new Vector2(1.6f, 1.6f), true, sr.gameObject, null);
    }

    // the lab's clutter: dead terminals still talking, shattered glass on
    // the tilework, little vials of things that used to matter
    void BuildLabDecor(float w, float h)
    {
        // retro terminals, facing the room from the edges
        ConsoleScreen.Spawn(new Vector2(-w + 1.6f, h - 2.2f));
        ConsoleScreen.Spawn(new Vector2(w - 1.6f, h - 2.2f));
        ConsoleScreen.Spawn(new Vector2(-w + 1.6f, -h + 2.2f));
        ConsoleScreen.Spawn(new Vector2(w - 1.6f, -h + 2.2f));
        ConsoleScreen.Spawn(new Vector2(-3.4f, h - 1.9f));
        ConsoleScreen.Spawn(new Vector2(3.4f, -h + 1.9f));

        // glass on the tilework: never over the hexagon platform
        for (int i = 0; i < 16; i++)
        {
            Vector2 pos = new Vector2(Random.Range(-w + 1f, w - 1f), Random.Range(-h + 1f, h - 1f));
            if (pos.x * pos.x / 20f + pos.y * pos.y / 6f < 1f) continue;
            Tile("Glass", Art.Glass(), pos,
                 new Vector2(Random.Range(0.8f, 1.5f), Random.Range(0.5f, 0.9f)), -17);
        }

        // hazard vials in little clusters near the walls
        for (int i = 0; i < 9; i++)
        {
            Vector2 pos = i % 2 == 0
                ? new Vector2(Random.Range(-w + 1.5f, w - 1.5f), Random.value < 0.5f ? h - 1.6f : -h + 1.6f)
                : new Vector2(Random.value < 0.5f ? -w + 1.6f : w - 1.6f, Random.Range(-h + 1.5f, h - 1.5f));
            if (pos.x * pos.x / 20f + pos.y * pos.y / 6f < 1f) continue;
            bool magenta = Random.value < 0.4f;
            Tile("Vial", Art.Vial(magenta), pos, new Vector2(0.45f, 0.75f), -6);
            if (Random.value < 0.6f)
                Tile("Vial", Art.Vial(!magenta),
                     pos + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, 0.2f)),
                     new Vector2(0.45f, 0.75f), -6);
        }
    }

    // the second leap of the lab: the hazard plumbing wakes up
    void ApplyLabLeap(int m)
    {
        Color ghost = new Color(0.7f, 0.4f, 1f);

        if (m == 2)
        {
            HazardMachine.Spawn(new Vector2(-11.5f, 2.4f), true);
            HazardMachine.Spawn(new Vector2(11.5f, -2.4f), true);
            HazardMachine.Spawn(new Vector2(-11.5f, -2.4f), false);
            HazardMachine.Spawn(new Vector2(11.5f, 2.4f), false);
        }
        else if (m > 2)
        {
            // the lab keeps closing in around the platform
            int i = Mathf.Min(m - 3, LabPylonLeapPos.Length / 2 - 1);
            LabPylon(LabPylonLeapPos[i * 2]);
            LabPylon(LabPylonLeapPos[i * 2 + 1]);
            Fx.Ring(LabPylonLeapPos[i * 2], ghost, 0.3f, 3.4f, 0.5f, 5);
            Fx.Ring(LabPylonLeapPos[i * 2 + 1], ghost, 0.3f, 3.4f, 0.5f, 5);
            Nav.Bake(ArenaHalf);

            // the second loop: two more machines climb out of the floor
            if (m == 4)
            {
                HazardMachine.Spawn(new Vector2(-13f, -1.5f), true);
                HazardMachine.Spawn(new Vector2(13f, 1.5f), false);
            }
        }
    }

    static readonly Vector2[] LabPylonLeapPos =
    {
        new Vector2(-5.4f, 4.4f),  new Vector2(5.4f, -4.4f),
        new Vector2(-5.4f, -4.4f), new Vector2(5.4f, 4.4f),
        new Vector2(-12.6f, 0f),   new Vector2(12.6f, 0f),
    };

    // ---------------------------------------------------------------- CRUMBLE
    // the glitch after the herald: both dead maps torn down and rebuilt
    // broken together - arena dirt and lab tile in one floor, the bunker and
    // the pylons in one cover plan, and bites of the world already missing.
    // Its trap is the sky: acid rain, because the traps drowned with the lab.
    public void GlitchToMap3()
    {
        glitchT = 4.2f;
        Sfx.Play("glitch");                           // reality loses its grip
        StartCoroutine(RebuildCrumbleAfter(2.3f));
    }

    IEnumerator RebuildCrumbleAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        RebuildCrumble();
    }

    void RebuildCrumble()
    {
        Crumble = true;
        cam.backgroundColor = new Color(0.014f, 0.011f, 0.02f);

        // the third reality's voice, built before the first drop falls
        Sfx.Warm("roar", "drag", "sludge", "acid_splash", "acid_bubble",
                 "rust_beam", "shatter", "turret_up", "collapse");

        // the old world lifts off the tape
        FluidPool.Clear();
        HazardMachine.ClearAll();
        Vent.ClearAll();
        Trap.ClearAll();
        Rubble.ClearAll();

        // only the piercer, the bulwark and the dasher belong to this ruin
        foreach (var e in Enemy.All.ToArray())
        {
            if (e == null) continue;
            if (e.KindOf == EnemyKind.Piercer || e.KindOf == EnemyKind.Bulwark ||
                e.KindOf == EnemyKind.Dasher) continue;
            e.Despawn();                              // no xp: they never died
        }

        Destroy(mapRoot.gameObject);
        Pit.Reset();
        Nav.Reset();
        WallPieces.Clear();

        mapRoot = new GameObject("Map").transform;
        float w = ArenaHalf.x, h = ArenaHalf.y;

        BuildCrumbleFloor(w, h);
        BuildCrumbleWalls(w, h);
        BuildCrumbleCover(w, h);
        BuildCrumbleDecor(w, h);

        Nav.Bake(ArenaHalf);

        AcidRain.Ensure();                            // the sky starts dripping
        AcidRain.Reset();

        var p = PlayerController.Instance;
        if (p != null)
        {
            Vector2 pos = p.transform.position;
            Fx.Ring(pos, new Color(0.55f, 0.95f, 1f), 0.4f, 6f, 0.55f, 30);
            Fx.Burst(pos, new Color(0.5f, 0.45f, 0.4f), 24, 9f);
            Sfx.PlayAt(pos, "chrono", 0.7f);          // the world recompiles around you
        }
        if (TimeLeap.Instance != null)
            TimeLeap.Instance.Announce("THE THIRD REALITY // THE FLOOR GIVES WAY", 2.4f);
    }

    // a floor of two dead maps: dirt on one third of it, violet lab tile on
    // another third, and holes where reality never finished rebuilding
    void BuildCrumbleFloor(float w, float h)
    {
        Sprite fa = Art.FloorA(), fb = Art.FloorB();
        Color dirt = new Color(0.70f, 0.63f, 0.52f, 0.74f);      // the arena's floor
        Color violet = new Color(0.60f, 0.48f, 1.15f, 0.70f);    // the lab's tile
        Color grid = new Color(0.3f, 0.9f, 1f, 0.09f);

        for (int x = -(int)w; x < (int)w; x += 2)
            for (int y = -(int)h; y < (int)h; y += 2)
            {
                Vector2 at = new Vector2(x + 1, y + 1);
                // reality fell through here first: no tile at all, just dark
                if (Random.value < 0.06f && Vector2.Distance(at, new Vector2(0f, -2.4f)) > 3.5f)
                    continue;
                bool alt = (((x + y) / 2) & 1) == 0;
                var g = new GameObject("Tile");
                g.transform.SetParent(mapRoot, false);
                g.transform.position = at;
                g.transform.localScale = new Vector3(2.02f, 2.02f, 1f);
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = alt ? fa : fb;
                sr.color = Random.value < 0.34f ? violet : dirt;
                sr.sortingOrder = -20;
            }

        // torn circuit patches: the lab's grid leaks through in places
        for (int i = 0; i < 6; i++)
        {
            Vector2 c = new Vector2(Random.Range(-w + 3f, w - 3f), Random.Range(-h + 3f, h - 3f));
            for (float gx = -2f; gx <= 2f; gx += 2f)
                Block("CrumbGridV", SpriteFactory.Square, c + new Vector2(gx, 0f),
                      new Vector2(0.06f, 4f), grid, -19);
            for (float gy = -2f; gy <= 2f; gy += 2f)
                Block("CrumbGridH", SpriteFactory.Square, c + new Vector2(0f, gy),
                      new Vector2(4f, 0.06f), grid, -19);
        }

        // the splits: dark cracks running across whatever floor is left
        for (int i = 0; i < 16; i++)
            RubbleStrip(new Vector2(Random.Range(-w + 1f, w - 1f), Random.Range(-h + 1f, h - 1f)),
                        Random.Range(1.6f, 4.2f), Random.Range(-90f, 90f),
                        new Color(0.02f, 0.02f, 0.03f, 0.85f), -17, Random.Range(0.08f, 0.16f));
    }

    // a rotated slab of anything: cracks, scrap plates, hanging pipes
    void RubbleStrip(Vector2 pos, float len, float rot, Color color, int order, float thick)
    {
        var g = new GameObject("Rubble");
        g.transform.SetParent(mapRoot, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, 0, rot);
        g.transform.localScale = new Vector3(len, thick, 1f);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Square;
        sr.color = color;
        sr.sortingOrder = order;
    }

    // the boundary wears both walls at once, alternating broken segments,
    // and half the trim snapped off before anyone came to look
    void BuildCrumbleWalls(float w, float h)
    {
        Color steel = new Color(0.14f, 0.16f, 0.22f);      // arena slab
        Color indigo = new Color(0.16f, 0.12f, 0.28f);     // lab slab
        Color neon = new Color(0.3f, 0.85f, 1f);
        Color pipe = new Color(0.22f, 0.25f, 0.32f);

        int segs = 8;
        float segW = w * 2f / segs + 0.02f, segH = h * 2f / segs + 0.02f;
        for (int i = 0; i < segs; i++)
        {
            float f = (i + 0.5f) / segs;
            Color cA = (i & 1) == 0 ? steel : indigo;
            Color cB = (i & 1) == 0 ? indigo : steel;
            Block("WallTop", SpriteFactory.Square, new Vector2(-w + f * w * 2f, h + 1.5f),
                  new Vector2(segW, 3f), cA, -6, true);
            Block("WallBottom", SpriteFactory.Square, new Vector2(w - f * w * 2f, -h - 1.5f),
                  new Vector2(segW, 3f), cB, -6, true);
            Block("WallLeft", SpriteFactory.Square, new Vector2(-w - 1.5f, -h + f * h * 2f),
                  new Vector2(3f, segH), cA, -6, true);
            Block("WallRight", SpriteFactory.Square, new Vector2(w + 1.5f, h - f * h * 2f),
                  new Vector2(3f, segH), cB, -6, true);
        }

        // the neon trim, what is left of it
        for (float x = -w; x < w; x += 2.4f)
        {
            if (Random.value < 0.7f)
            {
                Block("Trim", SpriteFactory.Square, new Vector2(x + 1.1f, h + 0.07f),
                      new Vector2(2.2f, 0.14f), neon, -4);
                Block("Trim", SpriteFactory.Square, new Vector2(-x - 1.1f, -h - 0.07f),
                      new Vector2(2.2f, 0.14f), neon, -4);
            }
        }

        // hazard stripes: the arena's amber, and the lab's magenta between
        Sprite hz = Art.HazardStripe();
        for (float x = -w; x < w; x += 2f)
        {
            if (Random.value < 0.85f)
                Tile("Haz", hz, new Vector2(x + 1f, h - 0.55f), new Vector2(2f, 0.55f), -16);
            if (Random.value < 0.85f)
                Tile("Haz", hz, new Vector2(x + 1f, -h + 0.55f), new Vector2(2f, 0.55f), -16);
        }
        for (float x = -w + 1f; x < w; x += 4f)
            RubbleStrip(new Vector2(x, Random.value < 0.5f ? h - 1.4f : -h + 1.4f),
                        2f, 0f, new Color(0.85f, 0.3f, 0.95f), -16, 0.5f);

        // the arena's plumbing on the sides, half the runs hanging loose
        for (float y = -h + 1.4f; y < h - 1f; y += 2.2f)
        {
            if (Random.value < 0.6f)
                RubbleStrip(new Vector2(-w + 0.55f, y), 1.5f, 90f, pipe, -12, 0.45f);
            if (Random.value < 0.6f)
                RubbleStrip(new Vector2(w - 0.55f, y + 1.1f), 1.5f, 90f, pipe, -12, 0.45f);
        }
    }

    // cover from both maps, and both maps' holes: the bunker half caved in,
    // pylons ripped out of the floor and dropped in the dirt, void bites
    void BuildCrumbleCover(float w, float h)
    {
        Wall(new Vector2(0f, 4.6f), new Vector2(4.4f, 1.2f));
        Wall(new Vector2(-8.5f, 3.8f), new Vector2(1.2f, 4.5f));
        Wall(new Vector2(8.5f, -3.8f), new Vector2(1.2f, 4.5f));
        Wall(new Vector2(-2.6f, -2.6f), new Vector2(1f, 1.8f));

        LabPylon(new Vector2(-9f, 4.6f));
        LabPylon(new Vector2(9f, -4.6f));
        LabPylon(new Vector2(0f, 6f));
        LabPylon(new Vector2(0f, -6f));

        SolidWall(new Vector2(-1.7f, -0.2f), new Vector2(1.2f, 1.8f));
        SolidWall(new Vector2(1.7f, -0.2f), new Vector2(1.2f, 1.8f));

        PitZone(new Vector2(-5.6f, 3f), new Vector2(2.6f, 2.6f));
        PitZone(new Vector2(5.6f, -3f), new Vector2(2.6f, 2.6f));
        PitZone(new Vector2(-13.2f, -6f), new Vector2(2.2f, 2.4f));
        PitZone(new Vector2(13.2f, 6f), new Vector2(2.2f, 2.4f));

        // two boosters still fire: the arena's last tricks
        Booster.Create(mapRoot, new Vector2(-10.5f, 0f), new Vector2(4f, 2.2f), Vector2.right);
        Booster.Create(mapRoot, new Vector2(10.5f, 0f), new Vector2(4f, 2.2f), Vector2.left);
    }

    // what the void left lying around: bent plate, shards of both maps that
    // never landed, terminals still talking, one vat cracked open
    void BuildCrumbleDecor(float w, float h)
    {
        for (int i = 0; i < 10; i++)
            RubbleStrip(new Vector2(Random.Range(-w + 1.5f, w - 1.5f), Random.Range(-h + 1.5f, h - 1.5f)),
                        Random.Range(0.9f, 2.2f), Random.Range(0f, 180f),
                        new Color(0.30f, 0.32f, 0.36f, 0.85f), -16, Random.Range(0.3f, 0.6f));

        for (int i = 0; i < 14; i++)
            CrumbleDebris.Spawn(new Vector2(Random.Range(-w + 1f, w - 1f), Random.Range(-h + 1f, h - 1f)));

        ConsoleScreen.Spawn(new Vector2(-w + 1.6f, h - 2.2f));
        ConsoleScreen.Spawn(new Vector2(w - 1.6f, -h + 2.2f));
        ConsoleScreen.Spawn(new Vector2(3.4f, h - 1.9f));

        Vector2[] vats = { new Vector2(-w + 1.4f, -h + 1.6f), new Vector2(w - 1.4f, h - 1.6f) };
        foreach (var p in vats)
        {
            var g = new GameObject("BrokenVat");
            g.transform.SetParent(mapRoot, false);
            g.transform.position = p;
            g.transform.localScale = Vector3.one * 1.3f;
            g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-14f, 14f));
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Vat();
            sr.color = new Color(0.75f, 0.7f, 0.68f, 0.9f);
            sr.sortingOrder = -10;
        }
    }

    // every leap in the third reality: the void takes another bite, rubble
    // piles into new cover, and the rain gets heavier
    void ApplyCrumbleLeap(int m)
    {
        if (m < 1) return;
        Color ghost = new Color(0.55f, 0.95f, 1f);
        int i = m - 1;

        if (i < CrumbleWallPos.Length)
        {
            Wall(CrumbleWallPos[i], CrumbleWallSize[i]);
            Fx.Ring(CrumbleWallPos[i], ghost, 0.3f, 3.4f, 0.5f, 5);
        }
        if (i < CrumblePitPos.Length)
        {
            PitZone(CrumblePitPos[i], CrumblePitSize[i]);
            Fx.Ring(CrumblePitPos[i], ghost, 0.3f, 3.4f, 0.5f, 5);
        }

        AcidRain.Intensity = m;                       // the sky gets angrier
        Nav.Bake(ArenaHalf);
    }

    static readonly Vector2[] CrumbleWallPos =
    {
        new Vector2(-13.6f, 0.6f),  new Vector2(13.6f, -1f),
        new Vector2(-6.6f, -6.4f), new Vector2(6.6f, 6.4f),
        new Vector2(-13.4f, 4.6f), new Vector2(14.2f, -5.6f),
        new Vector2(-4.4f, 6.8f),  new Vector2(4.4f, -7.4f),
        new Vector2(-11.5f, 7f),   new Vector2(11.5f, -7f),
        new Vector2(-9.4f, -4.4f), new Vector2(7.4f, 2f),
    };
    static readonly Vector2[] CrumbleWallSize =
    {
        new Vector2(1.4f, 3.6f),  new Vector2(1.4f, 3.6f),
        new Vector2(3.6f, 1.4f),  new Vector2(3.6f, 1.4f),
        new Vector2(1f, 4.4f),     new Vector2(1f, 4.4f),
        new Vector2(1.2f, 2.6f),   new Vector2(1.2f, 2.6f),
        new Vector2(2.8f, 1.2f),   new Vector2(2.8f, 1.2f),
        new Vector2(2.6f, 1.2f),   new Vector2(2.6f, 1.2f),
    };
    static readonly Vector2[] CrumblePitPos =
    {
        new Vector2(-11.6f, 4.6f), new Vector2(11.6f, -4.6f),
        new Vector2(-3.2f, -5.9f), new Vector2(3.6f, 6.2f),
        new Vector2(-14f, -6.9f),  new Vector2(14f, 6.9f),
        new Vector2(-6.5f, 0f),    new Vector2(6.5f, 0f),
        new Vector2(-2.9f, -7f),   new Vector2(2.9f, 7f),
        new Vector2(-3f, 2.6f),    new Vector2(3f, -4.6f),
    };
    static readonly Vector2[] CrumblePitSize =
    {
        new Vector2(2.4f, 2.4f),  new Vector2(2.4f, 2.4f),
        new Vector2(2.4f, 2f),    new Vector2(2.4f, 2f),
        new Vector2(2.2f, 2f),    new Vector2(2.2f, 2f),
        new Vector2(2.4f, 2.4f),  new Vector2(2.4f, 2.4f),
        new Vector2(2.6f, 1.8f),  new Vector2(2.6f, 1.8f),
        new Vector2(2.4f, 2.2f),  new Vector2(2.4f, 2.2f),
    };

    // THE VESSEL'S ENTRANCE: when it takes form the map collapses - parts
    // of the floor peel away and are swallowed by the void, cover and
    // blood and anything unlucky standing there included
    public void CollapseEvent(Vector2 bossPos)
    {
        Sfx.Play("collapse");
        StartCoroutine(CollapseRoutine(bossPos));
    }

    IEnumerator CollapseRoutine(Vector2 bossPos)
    {
        var p = PlayerController.Instance;
        Vector2 half = ArenaHalf - Vector2.one * 2f;

        for (int i = 0; i < 7; i++)
        {
            yield return new WaitForSeconds(i == 0 ? 0.2f : 0.38f);

            Vector2 at = bossPos;
            for (int tries = 0; tries < 30; tries++)
            {
                at = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
                if (Pit.Inside(at)) continue;
                if (p != null && Vector2.Distance(at, p.transform.position) < 4.2f) continue;
                if ((at - bossPos).magnitude < 4.6f) continue;
                break;
            }
            SwallowCluster(at, new Vector2(Random.Range(2.8f, 4.2f), Random.Range(2.4f, 3.6f)));
            Shake(0.55f);
        }
        Nav.Bake(ArenaHalf);
    }

    void Swallow(Vector2 at, Vector2 size)
    {
        // cover first: the radius loses its walls before the floor goes
        SmashWallsNear(at, Mathf.Max(size.x, size.y) * 0.7f + 0.5f);
        Rubble.ClearNear(at, size.x * 0.8f);

        foreach (var f in FluidPool.All.ToArray())
            if (f != null && Vector2.Distance(f.transform.position, at) < size.x * 0.7f)
                Destroy(f.gameObject);

        // reality does not care whose blood it is
        foreach (var e in Enemy.All.ToArray())
        {
            if (e == null) continue;
            if (Vector2.Distance(e.transform.position, at) >= size.x * 0.7f) continue;
            Fx.Burst(e.transform.position, new Color(0.3f, 0.15f, 0.5f), 14, 6f);
            e.Despawn();
        }

        Fx.Ring(at, new Color(0.55f, 0.3f, 0.95f, 0.9f), 0.3f, size.x * 2.2f, 0.5f, 30);
        Fx.Burst(at, new Color(0.18f, 0.1f, 0.32f), 24, 7f);
        Fx.Burst(at, new Color(0.45f, 0.4f, 0.38f), 12, 9f);      // the floor coming up
        Sfx.PlayAt(at, "collapse", 0.7f);
        PitZone(at, size);                              // the void keeps it
    }

    // THE VOID EATS IN CHUNKS: each bite is a cluster of adjacent holes, so
    // what is left behind reads as a part of the map that is simply gone
    void SwallowCluster(Vector2 at, Vector2 size)
    {
        Swallow(at, size);
        int extra = Random.Range(1, 3);
        for (int i = 0; i < extra; i++)
        {
            Vector2 side = Random.insideUnitCircle.normalized;
            Vector2 sat = at + side * (Mathf.Max(size.x, size.y) * 0.62f);
            Vector2 satSize = size * Random.Range(0.5f, 0.75f);
            sat.x = Mathf.Clamp(sat.x, -ArenaHalf.x + 1.5f, ArenaHalf.x - 1.5f);
            sat.y = Mathf.Clamp(sat.y, -ArenaHalf.y + 1.5f, ArenaHalf.y - 1.5f);
            Swallow(sat, satSize);
        }
    }

    // ============================================================ THE FLESH
    // the glitch after the vessel: the last reality peels away and what is
    // underneath is meat. blood-and-gore floor, watching eyes, ribs where
    // the cover used to be, and void falling through the cracks. no traps -
    // the flesh does not need tricks, it has butchers
    public void GlitchToMap4()
    {
        glitchT = 4.2f;
        Sfx.Play("glitch");                           // the vessel's hold lets go
        StartCoroutine(RebuildFleshAfter(2.3f));
    }

    IEnumerator RebuildFleshAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        RebuildFlesh();
    }

    void RebuildFlesh()
    {
        Flesh = true;
        cam.backgroundColor = new Color(0.035f, 0.006f, 0.03f);

        // the fourth reality's voice, built before the first melt
        Sfx.Warm("shepard_rifle", "shepard_sniper", "blade_flash", "blade_slash",
                 "clunk", "stasis_fire", "ghost", "vhs_beep", "vhs_stop", "melt",
                 "butcher_gun", "shepard_leap", "tick");

        // the old world lifts off the tape - all of it, no exceptions
        FluidPool.Clear();
        HazardMachine.ClearAll();
        Vent.ClearAll();
        Trap.ClearAll();                              // no traps in the flesh
        Rubble.ClearAll();

        foreach (var e in Enemy.All.ToArray())
            if (e != null) e.Despawn();

        Destroy(mapRoot.gameObject);
        Pit.Reset();
        Nav.Reset();
        WallPieces.Clear();

        mapRoot = new GameObject("FleshMap").transform;
        float w = ArenaHalf.x, h = ArenaHalf.y;

        BuildFleshFloor(w, h);
        BuildFleshWalls(w, h);
        BuildFleshCover(w, h);
        BuildFleshDecor(w, h);

        Nav.Bake(ArenaHalf);

        var p = PlayerController.Instance;
        if (p != null)
        {
            Vector2 pos = p.transform.position;
            Fx.Ring(pos, new Color(0.9f, 0.22f, 0.3f), 0.4f, 6f, 0.55f, 30);
            Fx.Burst(pos, new Color(0.6f, 0.12f, 0.16f), 26, 9f);
            Sfx.PlayAt(pos, "melt", 0.8f);
        }
        if (TimeLeap.Instance != null)
            TimeLeap.Instance.Announce("THE FOURTH REALITY // THE FLESH REMEMBERS", 2.6f);
    }

    // the floor: raw meat tiles with veins running through them, gore that
    // never dried, ribs pressed into the ground, and void where the meat
    // never grew back
    void BuildFleshFloor(float w, float h)
    {
        Sprite fa = Art.FloorA(), fb = Art.FloorB();
        Color meatDark = new Color(0.42f, 0.13f, 0.15f, 0.92f);
        Color meatRaw = new Color(0.55f, 0.20f, 0.20f, 0.9f);
        Color meatPale = new Color(0.62f, 0.34f, 0.30f, 0.85f);
        Color vein = new Color(0.72f, 0.10f, 0.14f, 0.8f);
        Color veinDark = new Color(0.30f, 0.04f, 0.08f, 0.85f);

        for (int x = -(int)w; x < (int)w; x += 2)
            for (int y = -(int)h; y < (int)h; y += 2)
            {
                Vector2 at = new Vector2(x + 1, y + 1);
                // the meat never covered everything: these spots stay void
                if (Random.value < 0.05f && Vector2.Distance(at, new Vector2(0f, -2.4f)) > 4f)
                    continue;
                bool alt = (((x + y) / 2) & 1) == 0;
                var g = new GameObject("MeatTile");
                g.transform.SetParent(mapRoot, false);
                g.transform.position = at;
                g.transform.localScale = new Vector3(2.02f, 2.02f, 1f);
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = alt ? fa : fb;
                float roll = Random.value;
                sr.color = roll < 0.5f ? meatDark : roll < 0.85f ? meatRaw : meatPale;
                sr.sortingOrder = -20;
            }

        // the veins: bright red lines wandering across the meat
        for (int i = 0; i < 22; i++)
        {
            Vector2 c = new Vector2(Random.Range(-w + 1f, w - 1f), Random.Range(-h + 1f, h - 1f));
            float rot = Random.Range(0f, 360f);
            RubbleStrip(c, Random.Range(2f, 5.5f), rot, vein, -17, Random.Range(0.08f, 0.16f));
            if (Random.value < 0.6f)                  // the vein branches, like veins do
                RubbleStrip(c + (Vector2)(Random.insideUnitCircle * 1.4f),
                            Random.Range(1f, 2.4f), rot + Random.Range(-70f, 70f),
                            veinDark, -17, Random.Range(0.06f, 0.12f));
        }

        // gore that predates you: stains, splats and things that were people
        for (int i = 0; i < 16; i++)
        {
            Vector2 at = new Vector2(Random.Range(-w + 1.5f, w - 1.5f), Random.Range(-h + 1.5f, h - 1.5f));
            Gore.Stain(at, new Color(0.33f, 0.05f, 0.08f), Random.Range(1.6f, 3.2f));
        }
        for (int i = 0; i < 26; i++)
        {
            Vector2 at = new Vector2(Random.Range(-w + 1f, w - 1f), Random.Range(-h + 1f, h - 1f));
            var g = new GameObject("FloorGore");
            g.transform.SetParent(mapRoot, false);
            g.transform.position = at;
            g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Random.value < 0.5f ? Art.BloodSplat(Random.Range(0, 4)) : Art.Gib(Random.Range(0, 4));
            sr.color = Random.value < 0.7f ? new Color(0.45f, 0.09f, 0.12f, 0.9f)
                                            : new Color(0.75f, 0.66f, 0.55f, 0.9f);   // bone among the meat
            sr.sortingOrder = -15;
            float s = Random.Range(0.5f, 1.3f);
            g.transform.localScale = new Vector3(s, s, 1f);
        }
    }

    // the boundary: walls of layered meat with bone trim on the inner face
    void BuildFleshWalls(float w, float h)
    {
        Color meat = new Color(0.36f, 0.09f, 0.11f);
        Color meatLight = new Color(0.48f, 0.15f, 0.16f);
        Color bone = new Color(0.80f, 0.74f, 0.62f);
        Color vein = new Color(0.70f, 0.10f, 0.14f);

        int segs = 8;
        float segW = w * 2f / segs + 0.02f, segH = h * 2f / segs + 0.02f;
        for (int i = 0; i < segs; i++)
        {
            float f = (i + 0.5f) / segs;
            Color cA = (i & 1) == 0 ? meat : meatLight;
            Color cB = (i & 1) == 0 ? meatLight : meat;
            Block("MeatWallT", SpriteFactory.Square, new Vector2(-w + f * w * 2f, h + 1.5f),
                  new Vector2(segW, 3f), cA, -6, true);
            Block("MeatWallB", SpriteFactory.Square, new Vector2(w - f * w * 2f, -h - 1.5f),
                  new Vector2(segW, 3f), cB, -6, true);
            Block("MeatWallL", SpriteFactory.Square, new Vector2(-w - 1.5f, -h + f * h * 2f),
                  new Vector2(3f, segH), cA, -6, true);
            Block("MeatWallR", SpriteFactory.Square, new Vector2(w + 1.5f, h - f * h * 2f),
                  new Vector2(3f, segH), cB, -6, true);
        }

        // the bone trim: ribs pressed into the inner face, half of them snapped
        for (float x = -w; x < w; x += 1.6f)
        {
            if (Random.value < 0.8f)
            {
                Block("BoneTrim", SpriteFactory.Square, new Vector2(x + 0.8f, h - 0.25f),
                      new Vector2(1.3f, 0.32f), bone, -5);
                Block("BoneTrim", SpriteFactory.Square, new Vector2(-x - 0.8f, -h + 0.25f),
                      new Vector2(1.3f, 0.32f), bone, -5);
            }
            if (Random.value < 0.5f)                  // veins dripping down the wall
                RubbleStrip(new Vector2(x + 0.4f, h + 1.2f), 2.2f, 90f, vein, -5, 0.24f);
        }
        for (float y = -h; y < h; y += 1.9f)
        {
            if (Random.value < 0.7f)
            {
                Block("BoneTrim", SpriteFactory.Square, new Vector2(-w + 0.25f, y + 0.9f),
                      new Vector2(0.32f, 1.5f), bone, -5);
                Block("BoneTrim", SpriteFactory.Square, new Vector2(w - 0.25f, -y - 0.9f),
                      new Vector2(0.32f, 1.5f), bone, -5);
            }
        }
    }

    // cover in the flesh: mounds of meat with bone spines, and void bites
    // where even the flesh is afraid to grow
    void BuildFleshCover(float w, float h)
    {
        FleshMound(new Vector2(0f, 4.6f), new Vector2(4.2f, 1.3f));
        FleshMound(new Vector2(-8.5f, 3.6f), new Vector2(1.3f, 4.4f));
        FleshMound(new Vector2(8.5f, -3.6f), new Vector2(1.3f, 4.4f));
        FleshSpine(new Vector2(-3.4f, -2.8f));
        FleshSpine(new Vector2(3.4f, 2.4f));

        PitZone(new Vector2(-5.6f, 3f), new Vector2(2.4f, 2.4f));
        PitZone(new Vector2(5.6f, -3f), new Vector2(2.4f, 2.4f));
        PitZone(new Vector2(-12.6f, -5.4f), new Vector2(2.2f, 2.2f));
        PitZone(new Vector2(12.6f, 5.4f), new Vector2(2.2f, 2.2f));
        PitZone(new Vector2(0f, -6.4f), new Vector2(3f, 1.8f));
    }

    // a wall of meat: no plate, no stripes - just a lump with veins in it
    void FleshMound(Vector2 pos, Vector2 size)
    {
        Nav.AddObstacle(pos, size);
        Nav.AddSight(pos, size);

        var sr = Block("MeatMound", Art.WallTile(), pos, size, new Color(0.5f, 0.14f, 0.15f), -4, true);
        Block("MeatMoundTop", SpriteFactory.Square, pos + new Vector2(0f, size.y * 0.28f),
              new Vector2(size.x * 0.86f, size.y * 0.3f), new Color(0.64f, 0.24f, 0.22f), -4);
        RegisterWall(pos, size, true, sr.gameObject, null);
    }

    // a spine of bone standing where a pillar used to be
    void FleshSpine(Vector2 pos)
    {
        Vector2 size = new Vector2(0.8f, 2.6f);
        Nav.AddObstacle(pos, size);
        Nav.AddSight(pos, size);

        var sr = Block("BoneSpine", Art.WallTile(), pos, size, new Color(0.82f, 0.76f, 0.64f), -4, true);
        Block("BoneSpineNotch", SpriteFactory.Square, pos, new Vector2(0.34f, size.y + 0.5f),
              new Color(0.5f, 0.42f, 0.34f), -4);
        RegisterWall(pos, size, true, sr.gameObject, null);
    }

    // what watches from the walls: eyes, ribs, cracked vats of blood, and
    // chunks of the two maps that never finished falling
    void BuildFleshDecor(float w, float h)
    {
        Vector2[] eyeSpots =
        {
            new Vector2(-w + 2.4f, h - 2.6f), new Vector2(w - 2.4f, -h + 2.6f),
            new Vector2(0f, h - 2f), new Vector2(-w + 3f, -h + 3.4f), new Vector2(w - 3.6f, 3.2f),
        };
        foreach (var p in eyeSpots)
        {
            var g = new GameObject("WatchingEye");
            g.transform.SetParent(mapRoot, false);
            g.transform.position = p;
            g.transform.localScale = Vector3.one * Random.Range(1.1f, 1.7f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Art.CosmicEye();
            sr.sortingOrder = -9;
        }

        for (int i = 0; i < 12; i++)
            RubbleStrip(new Vector2(Random.Range(-w + 1.5f, w - 1.5f), Random.Range(-h + 1.5f, h - 1.5f)),
                        Random.Range(1.2f, 3f), Random.Range(0f, 180f),
                        new Color(0.80f, 0.74f, 0.62f, 0.85f), -16, Random.Range(0.16f, 0.34f));   // shed ribs

        Vector2[] vats = { new Vector2(-w + 1.4f, -h + 1.6f), new Vector2(w - 1.4f, h - 1.6f) };
        foreach (var p in vats)
        {
            var g = new GameObject("BloodVat");
            g.transform.SetParent(mapRoot, false);
            g.transform.position = p;
            g.transform.localScale = Vector3.one * 1.3f;
            g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-14f, 14f));
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Vat();
            sr.color = new Color(0.55f, 0.10f, 0.12f, 0.95f);
            sr.sortingOrder = -10;
        }

        for (int i = 0; i < 8; i++)
            CrumbleDebris.Spawn(new Vector2(Random.Range(-w + 1f, w - 1f), Random.Range(-h + 1f, h - 1f)));
    }

    // every leap in the flesh: the floor sweats, sludge pools bubble up, and
    // the meat keeps staring
    void ApplyFleshLeap(int m)
    {
        if (m < 1) return;
        Color ghost = new Color(0.9f, 0.35f, 0.4f);

        for (int i = 0; i < 2; i++)
        {
            Vector2 at = new Vector2(Random.Range(-ArenaHalf.x + 3f, ArenaHalf.x - 3f),
                                     Random.Range(-ArenaHalf.y + 3f, ArenaHalf.y - 3f));
            if (Pit.Inside(at)) continue;
            FluidPool.Spawn(at, FluidKind.Slime, Random.Range(1.4f, 2.2f));
            Fx.Ring(at, ghost, 0.3f, 3.2f, 0.5f, 20);
            Gore.Stain(at, new Color(0.3f, 0.05f, 0.08f), 2.2f);
        }
        Nav.Bake(ArenaHalf);
    }

    // AFTER ONE LEAP IN THE FLESH: the horde stops being enemies - every
    // monster melts where it stands, and the ground keeps what is left
    public void MeltTheHorde()
    {
        int n = 0;
        foreach (var e in Enemy.All.ToArray())
        {
            if (e == null) continue;
            Vector2 at = e.transform.position;
            FluidPool.Spawn(at, FluidKind.Slime, Random.Range(1.1f, 2f));
            Fx.Burst(at, new Color(0.6f, 0.15f, 0.2f), 18, 6.5f);
            Fx.Burst(at, new Color(0.35f, 0.06f, 0.1f), 10, 3.5f);
            Gore.Stain(at, new Color(0.32f, 0.06f, 0.09f), 1.7f);
            Sfx.PlayAt(at, "melt", 0.65f);
            e.Despawn();                              // no xp: nothing died, it left
            n++;
        }
        if (n > 0)
        {
            Shake(0.6f);
            if (TimeLeap.Instance != null)
                TimeLeap.Instance.Announce("THE HORDE MELTS INTO SLUDGE", 2.4f);
        }
    }

    // PHASE TWO: shepard's time-leap takes a bite of the arena - the boundary
    // walks inward and the strips that just died become dark, meatless void
    public void ShrinkArena(float by)
    {
        Vector2 old = ArenaHalf;
        ArenaHalf = new Vector2(Mathf.Max(7f, old.x - by), Mathf.Max(4.5f, old.y - by * 0.6f));
        Vector2 a = ArenaHalf;

        Color meat = new Color(0.40f, 0.10f, 0.12f);
        Color dead = new Color(0.045f, 0.008f, 0.03f);
        Color bone = new Color(0.80f, 0.74f, 0.62f);

        // the strips that just died: no meat, no floor, nothing
        Block("DeadStrip", SpriteFactory.Square, new Vector2(0f, (a.y + old.y) * 0.5f),
              new Vector2(old.x * 2f + 8f, old.y - a.y), dead, -8);
        Block("DeadStrip", SpriteFactory.Square, new Vector2(0f, -(a.y + old.y) * 0.5f),
              new Vector2(old.x * 2f + 8f, old.y - a.y), dead, -8);
        Block("DeadStrip", SpriteFactory.Square, new Vector2((a.x + old.x) * 0.5f, 0f),
              new Vector2(old.x - a.x, old.y * 2f), dead, -8);
        Block("DeadStrip", SpriteFactory.Square, new Vector2(-(a.x + old.x) * 0.5f, 0f),
              new Vector2(old.x - a.x, old.y * 2f), dead, -8);

        // the new edge: flesh walls with bone trim on the inside
        Block("FleshEdge", SpriteFactory.Square, new Vector2(0f, a.y + 1.5f),
              new Vector2(a.x * 2f + 8f, 3f), meat, -6, true);
        Block("FleshEdge", SpriteFactory.Square, new Vector2(0f, -a.y - 1.5f),
              new Vector2(a.x * 2f + 8f, 3f), meat, -6, true);
        Block("FleshEdge", SpriteFactory.Square, new Vector2(a.x + 1.5f, 0f),
              new Vector2(3f, a.y * 2f + 8f), meat, -6, true);
        Block("FleshEdge", SpriteFactory.Square, new Vector2(-a.x - 1.5f, 0f),
              new Vector2(3f, a.y * 2f + 8f), meat, -6, true);

        for (float x = -a.x; x < a.x; x += 1.6f)
        {
            Block("BoneTrim", SpriteFactory.Square, new Vector2(x + 0.8f, a.y - 0.2f),
                  new Vector2(1.3f, 0.3f), bone, -5);
            Block("BoneTrim", SpriteFactory.Square, new Vector2(-x - 0.8f, -a.y + 0.2f),
                  new Vector2(1.3f, 0.3f), bone, -5);
        }

        Nav.Bake(ArenaHalf);

        var p = PlayerController.Instance;
        if (p != null)
        {
            Vector2 c = p.transform.position;
            c.x = Mathf.Clamp(c.x, -a.x + 1f, a.x - 1f);
            c.y = Mathf.Clamp(c.y, -a.y + 1f, a.y - 1f);
            p.TeleportTo(c);                          // the room pulls you inside it
        }

        Fx.Ring(Vector2.zero, new Color(0.9f, 0.3f, 0.35f, 0.9f), 0.4f, 9f, 0.7f, 30);
        Fx.Burst(Vector2.zero, new Color(0.5f, 0.1f, 0.14f), 30, 10f);
        Shake(0.85f);
        Sfx.Play("collapse");
    }

    // the hud loses its grip for a few seconds: neon slices, static bands
    public void HudGlitch(float dur) { glitchT = Mathf.Max(glitchT, dur); }

    // the mirror falls: the run ends the only good way it can
    public static void Win(string message)
    {
        if (IsOver) return;
        EndMessage = message;
        Won = true;
        IsOver = true;
        Time.timeScale = 1f;
        Sfx.Play("horn");
        Sfx.Play("herald_die");
        Endings.Begin();                             // and the tape offers the T
    }

    SpriteRenderer Block(string name, Sprite sprite, Vector2 pos, Vector2 size, Color color, int order,
                         bool collider = false)
    {
        var g = new GameObject(name);
        g.transform.SetParent(mapRoot, false);
        g.transform.position = pos;
        g.transform.localScale = size;
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (collider) g.AddComponent<BoxCollider2D>().size = Vector2.one;
        return sr;
    }

    void Tile(string name, Sprite sprite, Vector2 pos, Vector2 size, int order)
    {
        var g = new GameObject(name);
        g.transform.SetParent(mapRoot, false);
        g.transform.position = pos;
        g.transform.localScale = size;
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
    }

    // ---------------------------------------------------------------- LOOP
    public void GameOver() { EndRun("YOU DIED"); }

    // TIMELEAP: the commander's clock runs out - a different way to lose
    public static void EndRun(string message)
    {
        if (IsOver) return;
        EndMessage = message;
        IsOver = true;
        Sfx.Play("horn");                              // the last bell
    }

    // HIT-STOP: a beat of frozen impact. It remembers the timescale it
    // found (so it can never cut through Shepard's paused tape or the
    // level-up table by hard-setting 1), stacks instead of racing, and
    // counts down on unscaled time.
    public void HitStop(float duration)
    {
        if (IsOver || LevelUpUI.IsOpen || Shepard.VhsActive || IntroT > 0f || Cutscenes.Running) return;
        if (hitStopT <= 0f && Time.timeScale > 0f) hitStopPrev = Time.timeScale;
        hitStopT = Mathf.Max(hitStopT, duration);
        Time.timeScale = 0f;
    }

    void Update()
    {
        if (hitStopT > 0f)
        {
            hitStopT -= Time.unscaledDeltaTime;
            if (hitStopT <= 0f && !LevelUpUI.IsOpen && !Shepard.VhsActive && !Cutscenes.Running)
                Time.timeScale = hitStopPrev;       // resume exactly what was running
        }

        // the reticle follows the pointer EVERY frame, whoever owns the body
        // (the intro card, a cutscene or the level-up table all freeze the
        // player's Update, which used to leave the crosshair stranded)
        Aim.Tick(Screen.width, Screen.height);

        // the pointer belongs to the game - a menu (or the end) hands it back.
        // it is never locked: the reticle is read straight off the pointer's
        // position every frame. The OS cursor is enforced EVERY frame instead
        // of only when 'free' flips: a scene restart or a cutscene that touches
        // Cursor.visible left the arrow showing next to the crosshair
        bool free = GameMenu.Open || LevelUpUI.IsOpen || (IsOver && !EndPrompt);
        cursorFree = free;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = free;

        // the opening card holds the room for five seconds of reading
        if (IntroT > 0f)
        {
            Time.timeScale = 0f;
            IntroT -= Time.unscaledDeltaTime;
            if (IntroT <= 0f)
            {
                IntroT = 0f;
                Time.timeScale = 1f;
                Sfx.Play("levelup");                // and we are live
            }
        }

        // the run's clock: only time actually spent in the ring counts. The
        // menu, the how-to card and the tape's cutscenes take the player out
        // of it, and everything that freezes the game - hit-stop, the level-up
        // table, Shepard's paused tape - sits at timeScale 0 and reads as 0
        if (!IsOver && IntroT <= 0f && !GameMenu.Open && !Cutscenes.Running)
            RunT += Time.deltaTime;

        if (!IsOver && PlayerController.Instance != null)
            Nav.Tick(Time.deltaTime, PlayerController.Instance.transform.position);

        if (Input.GetKeyDown(KeyCode.M)) Sfx.ToggleMute();   // the room goes quiet

        if (DesatT > 0f) DesatT = Mathf.Max(0f, DesatT - Time.unscaledDeltaTime);   // stasis holds the color

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.G))                    // god mode for test runs
        {
            PlayerController.GodMode = !PlayerController.GodMode;
            var gp = PlayerController.Instance;
            if (PlayerController.GodMode && gp != null && gp.Health != null)
                gp.Health.Restore(gp.Health.max);           // step into godhood whole
        }

        // test hops: straight to each boss without farming the tape first
        if (IntroT <= 0f)
        {
            if (Input.GetKeyDown(KeyCode.F1)) DevJump(1);
            if (Input.GetKeyDown(KeyCode.F2)) DevJump(2);
            if (Input.GetKeyDown(KeyCode.F3)) DevJump(3);
            if (Input.GetKeyDown(KeyCode.F4)) DevJump(4);
        }

        if (Input.GetKeyDown(KeyCode.F6)) aimDebug = !aimDebug;   // where is the aim, and why
#endif

        if (IsOver && !EndPrompt && !Won && Input.GetKeyDown(KeyCode.R))
            GameFlow.Restart(false);                 // straight back into the ring (a loss)
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // F1 the smasher - F2 the herald - F3 the vessel - F4 shepard. Every key
    // is a full stage jump: whoever holds the ring is dismissed on the spot
    // (F2 kills the smasher, F3 the herald, F4 the vessel), the eras before
    // the target are taken over silently, and the target map recompiles
    // around the new boss.
    void DevJump(int stage)
    {
        var tl = TimeLeap.Instance;
        if (IsOver || tl == null || LevelUpUI.IsOpen) return;
        if (Cutscenes.Running || GameMenu.Open) return;   // never over the tape or the menu

        // whoever holds the ring steps out on the spot - no need to finish
        // a fight before testing the next one
        Smasher.DevDismiss();
        Herald.DevDismiss();
        BrokenVessel.DevDismiss();
        Shepard.DevDismiss();
        MirrorFighter.DevDismiss();
        TimeLeap.DevClearPending();

        // earlier eras are taken over silently, so no two rebuilds race
        if (stage >= 2 && !TimeLeap.Map2)
        { TimeLeap.Map2 = true; TimeLeap.Map2StartLeap = TimeLeap.Leaps; }
        if (stage >= 3 && !TimeLeap.Map3)
        { TimeLeap.Map3 = true; TimeLeap.Map3StartLeap = TimeLeap.Leaps; }
        if (stage >= 4 && !TimeLeap.Map4)
        { TimeLeap.Map4 = true; TimeLeap.Map4StartLeap = TimeLeap.Leaps; }

        switch (stage)
        {
            case 1: Smasher.Spawn(EnemySpawner.Difficulty); break;
            case 2: GlitchToMap2(); Herald.Spawn(EnemySpawner.Difficulty); break;
            case 3: GlitchToMap3(); BrokenVessel.Spawn(EnemySpawner.Difficulty); break;
            case 4:
                GlitchToMap4();
                MeltTheHorde();
                Cutscenes.SeenShepard = false;        // the jump always plays his words
                Cutscenes.SeenBare = false;           // ...and the augments coming out
                Shepard.Spawn(EnemySpawner.Difficulty);
                break;
        }
    }
#endif

    void LateUpdate()
    {
        cam.orthographicSize = Mathf.Max(ArenaHalf.y + 1.2f, (ArenaHalf.x + 1.2f) / cam.aspect);

        // while the tape runs a conversation it drifts the lens so the people
        // in it hold the middle of the picture - the box at the bottom edge
        // must never end up sitting on them
        camFocus = Vector2.Lerp(camFocus, CamTarget, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));

        Vector2 off = Random.insideUnitCircle * shake;
        cam.transform.position = new Vector3(camFocus.x + off.x, camFocus.y + off.y, -10f);
        shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime * 2.5f);
    }

    void OnGUI()
    {
        var p = PlayerController.Instance;
        if (p == null) return;
        if (GameMenu.Open || Cutscenes.Running) return;   // the menu / the tape owns the picture

        float hp = Mathf.Clamp01(p.Health.current / p.Health.max);

        // a dark plate under the left cluster: the floor's props never eat
        // the bars, whatever colour the room is wearing
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(10f, 6f, 434f, 86f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(438f, 58f, 330f, 30f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // health: dark container, thin border, the heart riding inside
        GuiBar(new Rect(18f, 14f, 414f, 36f),
               hp > 0.3f ? new Color(0.88f, 0.14f, 0.14f) : new Color(1f, 0.35f, 0.1f),
               hp, Art.IconHeart());

        // xp: kills fill the bar, a full bar stops the world for a level-up
        var up = PlayerUpgrades.Instance;
        if (up != null)
        {
            float xp = Mathf.Clamp01(up.Xp / Mathf.Max(1f, up.XpNeed));
            GuiBar(new Rect(18f, 58f, 414f, 30f), new Color(0.85f, 0.35f, 1f), xp, Art.IconStar());
            if (hudStyle == null)
                hudStyle = new GUIStyle(GUI.skin.label)
                {
                    font = Art.GuiFont(),
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
            GUI.color = Color.white;
            GUI.Label(new Rect(442f, 62f, 320f, 26f), "LV " + up.Level + "    Kills: " + Kills, hudStyle);
            DrawModPips(up);
        }
        else
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(20f, 60f, 300f, 30f), "Kills: " + Kills);
        }

        // status pips
        int pip = 0;
        if (p.IsSlowed) { Pip(20, ref pip, new Color(0.4f, 1f, 0.45f), "SLOW"); }
        if (p.IsStunned) { Pip(20, ref pip, new Color(0.3f, 0.85f, 1f), "STUN"); }
        if (p.IsCorroded) { Pip(20, ref pip, new Color(0.62f, 0.95f, 0.25f), "CORRODED -50%"); }

        if (p.IsGrabbed)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(),
                fontSize = 30,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            GUI.color = new Color(1f, 0.35f, 0.25f);
            GUI.Label(new Rect(0, Screen.height * 0.5f - 120, Screen.width, 60),
                      "TIED UP - DASH " + p.GrabBonds + " MORE!", style);
            GUI.color = Color.white;
        }

        if (IsOver && !EndPrompt && !Won)             // a won run: the tape owns the ending
        {
            string msg = string.IsNullOrEmpty(EndMessage) ? "YOU DIED" : EndMessage;
            var style = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(),
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.color = Won || msg.Contains("COMMANDER") ? new Color(1f, 0.85f, 0.2f) : Color.white;
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), msg + "\nPress R to restart", style);
            GUI.color = Color.white;
        }

        // STASIS: the color leaves the room while the orb holds you
        if (DesatT > 0f)
        {
            GUI.color = new Color(0.5f, 0.5f, 0.5f, Mathf.Min(0.62f, DesatT * 0.6f));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        DrawGlitch();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (PlayerController.GodMode)                  // the tester's crown
        {
            if (godStyle == null)
                godStyle = new GUIStyle(GUI.skin.label)
                {
                    font = Art.GuiFont(), fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
                };
            GUI.color = new Color(1f, 0.85f, 0.2f, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f));
            GUI.Label(new Rect(Screen.width * 0.5f - 100f, 10f, 200f, 30f), "GOD MODE", godStyle);
            GUI.color = Color.white;
        }
#endif

        if (IntroT > 0f) DrawIntro();

        // the pointer is locked, so the aim cursor is drawn here - without it
        // there is nothing to shoot at
        if (!IsOver && !LevelUpUI.IsOpen) DrawCrosshair();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (aimDebug) DrawAimDebug();
#endif
    }

    // a gunsight, not an icon: an open ring, four ticks reaching past it and a
    // dot at the exact point the round leaves from - orange over a dark halo,
    // so it reads over blood, over meat and over the void
    void DrawCrosshair()
    {
        Vector2 c = Aim.Gui;                        // GUI space is Y-down; Aim.Screen is Y-up
        float r = 13f;                              // the ring
        float t = 2f;
        float inner = 8f, outer = 18f;              // the ticks, gap, then reach

        var parts = new Rect[24];

        // the ring: twenty-four short chords, thin enough to read as a circle
        for (int i = 0; i < 20; i++)
        {
            float a = i / 20f * Mathf.PI * 2f;
            parts[i] = new Rect(c.x + Mathf.Cos(a) * r - 2f, c.y + Mathf.Sin(a) * r - 1f, 4f, 2f);
        }

        parts[20] = new Rect(c.x - outer, c.y - t * 0.5f, outer - inner, t);   // left
        parts[21] = new Rect(c.x + inner, c.y - t * 0.5f, outer - inner, t);   // right
        parts[22] = new Rect(c.x - t * 0.5f, c.y - outer, t, outer - inner);  // up
        parts[23] = new Rect(c.x - t * 0.5f, c.y + inner, t, outer - inner);  // down

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        foreach (var k in parts)
            GUI.DrawTexture(new Rect(k.x - 1f, k.y - 1f, k.width + 2f, k.height + 2f), Texture2D.whiteTexture);

        GUI.color = new Color(1f, 0.62f, 0.14f, 0.95f);
        foreach (var k in parts) GUI.DrawTexture(k, Texture2D.whiteTexture);

        GUI.color = new Color(1f, 0.95f, 0.75f);
        GUI.DrawTexture(new Rect(c.x - 1.5f, c.y - 1.5f, 3f, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // F6: the whole aim chain on one screen - what the cursor thinks, what the
    // mouse is reporting, whether Update even ran this frame, and a reference
    // line drawn from the player dead-straight to the crosshair. The game's own
    // orange aim line must lie exactly on top of that cyan line; if it does
    // not, a screenshot says precisely where the two part company.
    GUIStyle dbgStyle;
    void DrawAimDebug()
    {
        if (dbgStyle == null)
            dbgStyle = new GUIStyle(GUI.skin.label) { font = Art.GuiFont(), fontSize = 14 };

        int oldDepth = GUI.depth;
        GUI.depth = -150;                            // over everything, even the end card

        var p = PlayerController.Instance;
        float px = 0f, py = 0f, ang = 0f;
        if (p != null && cam != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(p.transform.position);
            px = sp.x; py = sp.y;
            ang = Mathf.Atan2(p.AimDir.y, p.AimDir.x) * Mathf.Rad2Deg;
        }

        string second = p != null
            ? "PLAYER " + px.ToString("0") + "," + py.ToString("0") +
              "   AIMDIR " + ang.ToString("0") +
              "   UPDATE AGE " + (Time.frameCount - PlayerController.TickFrame) + "f"
            : "PLAYER none";

        string text = Aim.Diag() + "\n" + second;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(9, 9, Screen.width, 52), text, dbgStyle);
        GUI.color = new Color(0.55f, 1f, 1f, 1f);
        GUI.Label(new Rect(8, 8, Screen.width, 52), text, dbgStyle);
        GUI.color = Color.white;

        // the reference line: player -> crosshair, in a cyan that no effect uses
        if (p != null && cam != null)
        {
            var from = new Vector2(px, Screen.height - py);   // world->screen is Y-up, GUI is Y-down
            var to = Aim.Gui;
            float len = Vector2.Distance(from, to);
            if (len > 2f)
            {
                float a = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
                GUIUtility.RotateAroundPivot(a, from);
                GUI.color = new Color(0.3f, 1f, 1f, 0.9f);
                GUI.DrawTexture(new Rect(from.x, from.y - 1f, len, 2f), Texture2D.whiteTexture);
                GUIUtility.RotateAroundPivot(-a, from);
                GUI.color = Color.white;
            }
        }

        GUI.depth = oldDepth;
    }
#endif

    // ------------------------------------------------------------ intro
    // five seconds of the rules before the room is allowed to move: how you
    // move, how you shoot, how you dash, what T does, and what the clock is
    GUIStyle introTitle, introBody, introHint;
    void DrawIntro()
    {
        if (introTitle == null)
        {
            introTitle = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(),
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            introBody = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(),
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            introHint = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(),
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };
        }

        int oldDepth = GUI.depth;
        GUI.depth = -100;                             // the card sits over every other hud

        GUI.color = new Color(0.01f, 0.015f, 0.03f, 0.78f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        GUI.color = new Color(0.35f, 0.95f, 1f);
        GUI.Label(new Rect(0, cy - 128f, Screen.width, 60f), "SAVE_SCUM", introTitle);

        GUI.color = Color.white;
        GUI.Label(new Rect(0, cy - 56f, Screen.width, 30f),
                  Bindings.Name(Bindings.Key(Bindings.Action.MoveUp)) + " / " +
                  Bindings.Name(Bindings.Key(Bindings.Action.MoveDown)) + " / " +
                  Bindings.Name(Bindings.Key(Bindings.Action.MoveLeft)) + " / " +
                  Bindings.Name(Bindings.Key(Bindings.Action.MoveRight)) + " - MOVE      " +
                  "MOUSE - AIM & SHOOT      " +
                  Bindings.Name(Bindings.Key(Bindings.Action.Dash)) + " - DASH", introBody);
        GUI.color = new Color(0.85f, 0.6f, 1f);
        GUI.Label(new Rect(0, cy - 24f, Screen.width, 30f),
                  Bindings.Name(Bindings.Key(Bindings.Action.Timeleap)) +
                  " - TIMELEAP: rewind the last 3 seconds  //  every leap sharpens the horde", introBody);

        GUI.color = new Color(0.9f, 0.93f, 0.97f);
        GUI.Label(new Rect(0, cy + 14f, Screen.width, 30f),
                  "the clock top-right is your escape timer: it hits 0 and THE COMMANDER ESCAPED", introBody);

        GUI.color = new Color(0.35f, 0.95f, 1f, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f));
        GUI.Label(new Rect(0, cy + 58f, Screen.width, 26f),
                  "starting in " + Mathf.Max(1, Mathf.CeilToInt(IntroT)) + "...", introHint);
        GUI.color = Color.white;

        GUI.depth = oldDepth;
    }

    // the system tearing itself apart and recompiling: neon slices, tearing
    // bands, and a recompile notice that flickers with the damage
    GUIStyle glitchStyle;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    GUIStyle godStyle;
#endif
    void DrawGlitch()
    {
        if (glitchT <= 0f) return;
        glitchT -= Time.unscaledDeltaTime;

        float a = Mathf.Clamp01(glitchT / 4.2f);
        float t = Time.unscaledTime;

        for (int i = 0; i < 18; i++)
        {
            float y = Mathf.Floor(Random.value * Screen.height / 3f) * 3f;
            float h = Random.Range(2f, 24f);
            var c = Color.HSVToRGB(Random.Range(0.45f, 0.92f), Random.Range(0.4f, 0.9f), 1f);
            c.a = Random.Range(0.03f, 0.13f) * (0.4f + a);
            GUI.color = c;
            GUI.DrawTexture(new Rect(0, y, Screen.width, h), Texture2D.whiteTexture);
        }

        // displaced bands of pure static, jittering
        for (int i = 0; i < 5; i++)
        {
            float y = (Mathf.PerlinNoise(t * 6f, i * 7.3f)) * Screen.height;
            float x = (Mathf.PerlinNoise(i * 3.1f, t * 5f) - 0.5f) * 90f;
            GUI.color = new Color(0.05f, 0.04f, 0.09f, 0.55f * a);
            GUI.DrawTexture(new Rect(x, y, Screen.width, Random.Range(8f, 30f)), Texture2D.whiteTexture);
        }

        if (glitchStyle == null)
            glitchStyle = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(),
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        GUI.color = new Color(0.55f, 0.95f, 1f, a * (0.55f + 0.45f * Mathf.Sin(t * 34f)));
        GUI.Label(new Rect(0, Screen.height * 0.42f, Screen.width, 60f),
                  "SYSTEM RECALIBRATING", glitchStyle);
        GUI.color = new Color(0.85f, 0.45f, 1f, a * (0.55f + 0.45f * Mathf.Sin(t * 21f + 2f)));
        GUI.Label(new Rect(6f, Screen.height * 0.42f + 2f, Screen.width, 60f),
                  "SYSTEM RECALIBRATING", glitchStyle);
        GUI.color = Color.white;
    }

    // Every bar in the game wears the same frame: dark container, thin
    // border, gloss pass, then the fill - with an optional icon on the left.
    public static void GuiBar(Rect r, Color fill, float frac, Sprite icon = null)
    {
        frac = Mathf.Clamp01(frac);

        GUI.color = new Color(0f, 0f, 0f, 0.85f);            // container
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.18f);            // thin border
        DrawBorder(r);

        float pad = 4f;
        var fr = new Rect(r.x + pad, r.y + pad,
                          (r.width - pad * 2f) * frac, r.height - pad * 2f);
        GUI.color = fill;
        GUI.DrawTexture(fr, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.22f);            // top gloss
        GUI.DrawTexture(new Rect(fr.x, fr.y, fr.width, fr.height * 0.3f), Texture2D.whiteTexture);

        if (icon != null)                                    // icon on a dark patch
        {
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(r.x + 3f, r.y + 2f, r.height - 4f, r.height - 4f),
                            Texture2D.whiteTexture);
            GUI.color = fill;
            float s = r.height - 12f;
            GUI.DrawTexture(new Rect(r.x + 3f + (r.height - 4f - s) * 0.5f,
                                     r.y + 2f + (r.height - 4f - s) * 0.5f, s, s), icon.texture);
        }
        GUI.color = Color.white;
    }

    static void DrawBorder(Rect r)
    {
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), Texture2D.whiteTexture);
    }

    void Pip(int x, ref int row, Color c, string label)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(x, 98 + row * 24, 14, 14), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 20, 94 + row * 24, 120, 24), label);
        row++;
    }

    // the mods this run has installed, down the right edge: icon + level
    GUIStyle hudStyle, pipStyle;
    void DrawModPips(PlayerUpgrades up)
    {
        if (pipStyle == null)
            pipStyle = new GUIStyle(GUI.skin.label)
            {
                font = Art.GuiFont(),
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };

        float x = Screen.width - 42f, y = 82f;        // under the clock, down the edge
        for (int i = 0; i < Upgrade.Count; i++)
        {
            var kind = (UpgradeKind)i;
            int lvl = up.LevelOf(kind);
            if (lvl <= 0) continue;

            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(x, y, 30, 30), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x + 3, y + 3, 24, 24), Art.UpgradeIcon(kind).texture);
            GUI.Label(new Rect(x - 42, y, 40, 30), lvl.ToString(), pipStyle);

            y += 34f;
            if (y > Screen.height - 46f) break;
        }
        GUI.color = Color.white;
    }
}
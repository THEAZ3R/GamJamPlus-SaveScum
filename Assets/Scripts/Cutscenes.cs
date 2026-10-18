using System.Collections;
using UnityEngine;

// ============================================================ CUTSCENES
// Three taped conversations, played Undertale-style: a black box at the
// bottom of the screen, white text typing itself out letter by letter with a
// blip on every character, ENTER to move on. Nothing else moves while a line
// is up - the whole game sits at timeScale 0 - except the opening tape, which
// holds the player still and lets the body walk into the arena on its own.
//
// 1. the opening tape (once per play session): black, a faint tick, static
//    clearing onto the arena, you running in on the man who sold you out.
// 2. the first time SHEPARD steps into the frame: the tape stops for words.
// 3. when the run is stripped bare for the 1V1: the hud eats itself and the
//    picture rewinds back into the fight.
public class Cutscenes : MonoBehaviour
{
    public static bool Running { get; private set; }
    public static bool IntroSeen;                     // per play session, not per run
    public static bool SeenShepard;                   // per run
    public static bool SeenBare;                      // per run

    static Cutscenes instance;

    static readonly Color YouCol = new Color(0.55f, 0.95f, 1f);
    static readonly Color ShepCol = new Color(1f, 0.42f, 0.35f);
    static readonly Color DirCol = new Color(0.62f, 0.66f, 0.74f);

    // dialogue state, read by OnGUI
    string curWho = "";
    string curText = "";
    Color curColor = Color.white;
    int typed;
    float typedF;
    float pauseT;
    bool lineDone, boxShown, boxShake, boxQuiet;

    // overlays
    float blackA, staticA, flashA;
    Color flashCol = Color.white;

    Transform shepardStandIn;

    static Cutscenes Instance
    {
        get
        {
            if (instance == null)
            {
                var g = new GameObject("Cutscenes");
                instance = g.AddComponent<Cutscenes>();
            }
            return instance;
        }
    }

    // ------------------------------------------------------------ entry points
    public static void PlayIntro() { Instance.StartCoroutine(Instance.Intro()); }

    // called the moment SHEPARD first walks into the frame
    public static void OnShepardSpawn()
    {
        if (SeenShepard || !IntroSeen) return;           // no words before the opening tape
        SeenShepard = true;
        Instance.StartCoroutine(Instance.ShepardWords());
    }

    // the run goes bare: augments out, mirror in. Waits for the words.
    public static IEnumerator BareWait()
    {
        if (SeenBare) yield break;
        SeenBare = true;
        Instance.StartCoroutine(Instance.BareWords());
        while (Running) yield return null;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
    }

    static object Real(float t) { return new WaitForSecondsRealtime(t); }

    static bool AdvanceDown()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
               Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(Bindings.Key(Bindings.Action.Confirm));
    }

    static bool AdvanceHeld()
    {
        return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) ||
               Input.GetKey(KeyCode.Space) || Input.GetKey(Bindings.Key(Bindings.Action.Confirm));
    }

    float skipT;                                        // holding ESC fast-forwards the tape

    // ------------------------------------------------------------ the run
    IEnumerator Play(Beat[] beats, bool freeze)
    {
        Running = true;
        Time.timeScale = freeze ? 0f : 1f;
        boxShown = false;
        skipT = 0f;

        while (LevelUpUI.IsOpen) yield return null;      // never talk over the table

        for (int i = 0; i < beats.Length; i++)
        {
            var b = beats[i];

            if (b.who.Length == 0)                       // silence / stage direction
            {
                boxShown = b.text.Length > 0;
                curWho = "";
                curText = b.text;
                curColor = DirCol;
                typed = curText.Length;
                lineDone = true;
                boxQuiet = true;
                boxShake = false;
                if (b.onShow != null) b.onShow();
                yield return WaitOrSkip(b.wait);
                if (skipped) break;
                continue;
            }

            // a spoken line
            boxShown = true;
            boxShake = b.shake;
            boxQuiet = b.quiet;
            curWho = b.who;
            curColor = b.shake ? new Color(1f, 0.30f, 0.22f)
                       : b.quiet ? new Color(0.78f, 0.80f, 0.86f) : Color.white;
            curText = b.text;
            typed = 0;
            typedF = 0f;
            pauseT = 0f;
            lineDone = false;
            if (b.onShow != null) b.onShow();

            // the typewriter: one blip on every letter, one letter every
            // tenth of a second - the cadence of somebody actually speaking.
            // ENTER fast-forwards the line, then the next ENTER moves on
            bool ff = false;
            while (typed < curText.Length)
            {
                if (SkipHeld()) break;
                if (AdvanceDown()) { ff = true; break; }

                // punctuation buys a beat: a period breathes, a comma
                // hesitates, a line break lets the sentence land
                if (pauseT > 0f)
                {
                    pauseT -= Time.unscaledDeltaTime;
                    yield return null;
                    continue;
                }

                // a float clock, so the rate really is the rate: the old
                // Max(1, ...) forced a letter per frame and ran away with it
                int before = (int)typedF;
                typedF += b.cps * Time.unscaledDeltaTime;
                typed = Mathf.Min(curText.Length, (int)typedF);

                for (int k = before; k < typed; k++)    // one voice tick per letter
                {
                    if (curText[k] != '\n') VoiceTick(k, b);
                    char c = curText[k];
                    if (c == '.' || c == '?' || c == '!') pauseT = 0.26f;
                    else if (c == ',' || c == ';' || c == ':') pauseT = 0.14f;
                    else if (c == '\n') pauseT = 0.20f;
                }
                yield return null;
            }
            typed = curText.Length;
            lineDone = true;
            if (skipped) break;

            while (AdvanceHeld()) yield return null;      // never eat the next line
            if (!ff)
            {
                while (!AdvanceDown())
                {
                    if (SkipHeld()) break;
                    yield return null;
                }
            }
            while (AdvanceHeld() || SkipHeld()) yield return null;
            if (skipped) break;

            // a breath before the next voice: nobody talks at the speed of a
            // held ENTER
            yield return WaitOrSkip(0.45f + b.hold);
            if (skipped) break;
        }

        boxShown = false;
        boxShake = false;
    }

    // one letter, in the voice of whoever is saying it. every letter takes its
    // own pitch like a real voice does, and a word shouted in capitals comes
    // out as a scream - you can tell who is talking with the picture covered
    void VoiceTick(int i, Beat b)
    {
        bool shout = Screaming(curText, i);

        string clip = b.shepardVoice ? (shout ? "scream_shep" : "blip_shep")
                                     : (shout ? "scream" : "blip");

        // the range each character lives in: you are higher and wider, he
        // sits low and never quite raises his voice
        float pitch = b.shepardVoice ? Random.Range(0.70f, 0.95f)
                                     : Random.Range(1.00f, 1.40f);

        float vol = b.quiet ? 0.16f
                  : shout ? (b.shepardVoice ? 0.50f : 0.55f)
                          : (b.shepardVoice ? 0.28f : 0.30f);

        Sfx.PlayVoice(clip, vol, pitch);
    }

    // a word typed entirely in capitals is being shouted; a lone capital at
    // the start of a sentence is just how sentences start
    static bool Screaming(string s, int i)
    {
        if (i < 0 || i >= s.Length || !char.IsUpper(s[i])) return false;

        int a = i, b = i;
        while (a > 0 && WordChar(s[a - 1])) a--;
        while (b + 1 < s.Length && WordChar(s[b + 1])) b++;
        if (b - a < 1) return false;                    // one letter: not a shout

        for (int k = a; k <= b; k++)
            if (char.IsLower(s[k])) return false;
        return true;
    }

    static bool WordChar(char c) { return char.IsLetterOrDigit(c) || c == '_' || c == '\''; }

    bool skipped;

    IEnumerator WaitOrSkip(float t)
    {
        float end = Time.unscaledTime + t;
        while (Time.unscaledTime < end)
        {
            if (SkipHeld()) break;
            yield return null;
        }
    }

    bool SkipHeld()
    {
        if (skipped) return true;
        if (Input.GetKey(KeyCode.Escape)) skipT += Time.unscaledDeltaTime;
        else skipT = Mathf.Max(0f, skipT - Time.unscaledDeltaTime * 2f);
        if (skipT < 0.45f) return false;
        Sfx.Play("vhs_stop");
        skipped = true;
        return true;
    }

    void Freeze()
    {
        Running = true;
        Time.timeScale = 0f;
        boxShown = false;
        blackA = 0f;
        staticA = 0f;
        flashA = 0f;
    }

    void Release()
    {
        Running = false;
        Time.timeScale = 1f;
        boxShown = false;
        blackA = 0f;
        staticA = 0f;
        flashA = 0f;
        GameManager.CamTarget = Vector2.zero;         // and the lens drifts home
    }

    // ================================================================ framing
    // the box hangs off the bottom edge, so on a short window it eats a big
    // slice of the picture - while a talk runs, the shot drifts until the
    // people in it clear that edge, no matter where in the room they stand
    const float BoxTopPx = 186f;                      // DrawBox's top, in pixels

    public static void FrameActors()
    {
        var p = PlayerController.Instance;
        Vector2 a = p != null ? (Vector2)p.transform.position : Vector2.zero;
        Vector2 b = a;
        if (Shepard.Active != null) b = Shepard.Active.transform.position;
        else if (instance != null && instance.shepardStandIn != null) b = instance.shepardStandIn.position;

        Vector2 mid = (a + b) * 0.5f;
        float feet = Mathf.Min(a.y, b.y) - 0.9f;      // the lowest body in frame

        // the same size the lens takes in LateUpdate, so the drift lands right
        float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
        float ortho = Mathf.Max(GameManager.ArenaHalf.y + 1.2f,
                                (GameManager.ArenaHalf.x + 1.2f) / aspect);

        // aim so the box's top edge ends up a breath under those feet
        float clear = ortho * (1f - 2f * BoxTopPx / Mathf.Max(1f, Screen.height));
        float y = Mathf.Clamp(feet + clear - 1.2f, -6.5f, 6.5f);

        GameManager.CamTarget = new Vector2(Mathf.Clamp(mid.x, -6f, 6f), y);
    }

    // ============================================================ 1 - INTRO
    // black screen, a faint tick like a tape rewinding, static clearing onto
    // the first arena, the body running in - and the words that start it all
    IEnumerator Intro()
    {
        IntroSeen = true;
        Running = true;
        Time.timeScale = 1f;
        blackA = 1f;
        staticA = 0f;
        flashA = 0f;

        var gm = GameManager.Instance;
        var p = PlayerController.Instance;

        // put the body at the arena's left edge, in the open lane under the bunker
        Vector2 from = new Vector2(-GameManager.ArenaHalf.x + 1.2f, -5.5f);
        Vector2 mark = new Vector2(-1.6f, -5.5f);
        if (p != null) { p.CutscenePlace(from); p.CutsceneFace(Vector2.right); }

        // the man himself, waiting on the right: he is not in the fight yet,
        // so the tape stands a copy of him up for the length of the talk
        SpawnStandIn(new Vector2(2.6f, -5.5f));

        // 1) in the dark, the tape rewinds
        for (int i = 0; i < 6; i++)
        {
            Sfx.PlayAt(Vector2.zero, "tape_tick", 0.30f + i * 0.05f, 0.72f + i * 0.08f);
            yield return Real(0.34f);
        }
        Sfx.PlayAt(Vector2.zero, "rewind", 0.5f, 1.3f);

        // 2) the static clears and the arena is standing there
        staticA = 0.85f;
        Sfx.Play("vhs_stop");
        float t = 0f;
        while (t < 1.5f)
        {
            t += Time.unscaledDeltaTime;
            blackA = Mathf.Clamp01(1f - t / 0.8f);
            staticA = Mathf.Clamp01(0.85f - t / 1.5f);
            yield return null;
        }
        blackA = 0f; staticA = 0f;

        // 3) you run in - footsteps and all
        if (p != null)
        {
            float step = 0f;
            Vector2 prev = from;
            while (Vector2.Distance(p.transform.position, mark) > 0.35f)
            {
                Vector2 dir = (mark - (Vector2)p.transform.position).normalized;
                p.CutsceneVel = dir * 6.6f;
                p.CutsceneFace(dir);

                step += Vector2.Distance(prev, p.transform.position);
                prev = p.transform.position;
                if (step > 0.85f)
                {
                    step = 0f;
                    Sfx.PlayAt(prev, "step", 0.34f, Random.Range(0.94f, 1.08f));
                }
                yield return null;
            }
            p.CutsceneVel = Vector2.zero;
            Vector2 look = (Vector2)shepardStandIn.position - (Vector2)p.transform.position;
            p.CutsceneFace(look.normalized);
        }
        yield return Real(0.5f);

        // 4) the words - and the shot settles onto the two of them first
        FrameActors();
        yield return Play(IntroBeats(), false);

        // 5) he time-leaps away: the tape takes him and the room goes white
        if (shepardStandIn != null)
        {
            Vector2 at = shepardStandIn.position;
            Sfx.PlayAt(at, "shepard_leap", 1f, 1f);
            Sfx.PlayAt(at, "rewind", 0.9f, 0.9f);
            for (int i = 0; i < 4; i++)
            {
                Fx.Ring(at, new Color(0.78f, 0.45f, 1f), 0.4f, 6.5f + i, 0.28f, 30);
                Fx.Sparks(at, Color.white, 16, 12f);
                yield return Real(0.16f);
            }
            Destroy(shepardStandIn.gameObject);
            shepardStandIn = null;
        }
        GameManager.Shake(0.7f);

        flashCol = Color.white;
        flashA = 0.9f;
        Sfx.Play("glitch");
        t = 0f;
        while (t < 0.6f) { t += Time.unscaledDeltaTime; flashA = Mathf.Clamp01(0.9f - t * 1.5f); yield return null; }
        flashA = 0f;

        Release();
        if (gm != null) gm.BeginRun(false);              // the tape ends, the game starts
    }

    void SpawnStandIn(Vector2 at)
    {
        var g = new GameObject("ShepardTape");
        g.transform.position = at;
        var art = Art.Shepard();
        var visual = Humanoid.Build(g.transform, art, 1.55f, 25);
        var glow = new GameObject("Glow");
        glow.transform.SetParent(g.transform, false);
        glow.transform.localScale = Vector3.one * 1.7f;
        var gr = glow.AddComponent<SpriteRenderer>();
        gr.sprite = SpriteFactory.Circle;
        gr.color = new Color(0.9f, 0.15f, 0.15f, 0.16f);
        gr.sortingOrder = 18;

        var anim = visual.GetComponent<HumanoidAnim>();
        if (anim != null)
        {
            anim.SetFacing(Vector2.left);
            anim.SetAim(Vector2.left);
        }
        shepardStandIn = g.transform;
    }

    static Beat[] IntroBeats()
    {
        return new[]
        {
            You("Finally caught up to you."),
            You("Why did you betray us?\nWhy did you betray Unit SAVE_SCUM?"),
            Shep("I had no other choice."),
            You("No other choice?\nYou were our commander. We trusted you."),
            You("How much did the enemy pay you?\nWhat was the price?"),
            You("You could have ended the war.\nWe were this close to ending the madness."),
            Shep("Ending the madness?"),
            Shep("....", hold: 1.0f),
            Shep("hahahaha", hold: 0.8f),
            Shep("It was only the beginning."),
            Shep("A power like ours had to be destroyed."),
            Shep("I left you some friends to play with.", hold: 1.2f),
        };
    }

    // ============================================================ 2 - HE ARRIVES
    // the first time he steps into the arena the tape stops for a word with you
    IEnumerator ShepardWords()
    {
        Freeze();
        FrameActors();
        var gm = GameManager.Instance;
        if (gm != null) gm.HudGlitch(2.2f);
        Sfx.Play("vhs_stop");
        yield return Real(0.5f);

        yield return Play(new[]
        {
            Dir("the tape stops", 1.1f),
            You("SHEPARD! I'M GOING TO KILL YOU!",
                onShow: () =>
                {
                    // the loudest the tape ever gets: red across the picture,
                    // the room shaking under it, and a shout with no voice in it
                    flashCol = new Color(1f, 0.12f, 0.08f);
                    flashA = 0.55f;
                    GameManager.Shake(1.15f);
                    Sfx.Play("rage");
                    Sfx.PlayAt(Vector2.zero, "horn", 0.8f, 1.35f);
                    if (gm != null) gm.HudGlitch(2.6f);
                }),
            Shep("This is exactly why the Unit had to be destroyed."),
            Shep("Time, as a tool? How foolish we were."),
            Shep("Of course reality would crumble to dust.\n(sigh)",
                 onShow: () => Sfx.PlayAt(Vector2.zero, "sigh", 0.6f)),
            Shep("I should have made sure to dispose of you."),
            Shep("You're too far gone. You kept chasing me,\neven after watching reality crumble in front of your eyes."),
            Shep("All for what?"),
            Shep("Look around you. Even if you kill me,\nyou're already lost in a void with no way out."),
            Shep("Is this the revenge you wanted?"),
            Dir("", 2.4f),
            You("...I'm going to kill you.", quiet: true, cps: 10f, hold: 1.4f),
        }, true);

        Sfx.Play("vhs_stop");
        Release();
    }

    // ============================================================ 3 - BARE HANDS
    // the mods are gone, the hud eats itself, and the picture rewinds back
    // into the pure 1V1
    IEnumerator BareWords()
    {
        Freeze();
        FrameActors();
        var gm = GameManager.Instance;
        if (gm != null) gm.HudGlitch(3.5f);
        Sfx.Play("glitch");
        Sfx.Play("vhs_stop");
        yield return Real(0.7f);

        yield return Play(new[]
        {
            You("What...?"),
            You("What did you do?"),
            Dir("he clicks his tongue", 0.8f),
            Shep("Tsk.", onShow: () => Sfx.PlayAt(Vector2.zero, "tick", 0.9f, 1.6f), hold: 0.8f),
            Shep("Everything around us crumbled. Even your powers."),
            Shep("Do you still insist on fighting?"),
            Dir("", 2.2f),
            Dir("(you stare at the empty rifle in your hands)", 1.8f),
        }, true);

        // the tape rewinds the picture back into the fight
        staticA = 0.9f;
        Sfx.Play("rewind");
        Sfx.Play("shepard_leap");
        float t = 0f;
        while (t < 0.85f)
        {
            t += Time.unscaledDeltaTime;
            staticA = Mathf.Clamp01(0.9f - t * 1.05f);
            if (gm != null && Random.value < 0.25f) gm.HudGlitch(0.35f);
            yield return null;
        }
        flashCol = Color.white;
        flashA = 0.5f;
        t = 0f;
        while (t < 0.4f) { t += Time.unscaledDeltaTime; flashA = Mathf.Clamp01(0.5f - t * 1.3f); yield return null; }
        flashA = 0f;
        staticA = 0f;

        Release();
    }

    // ------------------------------------------------------------ lines
    struct Beat
    {
        public string who;
        public string text;
        public bool shake, quiet, shepardVoice;
        public float cps, hold, wait;
        public System.Action onShow;
    }

    // YOU: the one still chasing
    static Beat You(string t, bool shake = false, bool quiet = false, float cps = 10f,
                    float hold = 0f, System.Action onShow = null)
    {
        return new Beat
        {
            who = "YOU", text = t, shake = shake, quiet = quiet,
            cps = cps, hold = hold, onShow = onShow
        };
    }

    // SHEPARD: the one who left
    static Beat Shep(string t, float hold = 0f, float cps = 10f, System.Action onShow = null)
    {
        return new Beat
        {
            who = "SHEPARD", text = t, shepardVoice = true,
            cps = cps, hold = hold, onShow = onShow
        };
    }

    // grey stage direction, or plain silence
    static Beat Dir(string t, float wait = 0.6f) { return new Beat { who = "", text = t, wait = wait }; }
    static Beat Dir() { return new Beat { who = "", text = "", wait = 0.8f }; }

    // ------------------------------------------------------------ drawing
    GUIStyle boxWho, boxText, boxHint;

    void EnsureStyles()
    {
        if (boxWho != null) return;
        boxWho = new GUIStyle(GUI.skin.label) { font = Art.GuiFont(), fontSize = 20, fontStyle = FontStyle.Bold };
        boxText = new GUIStyle(GUI.skin.label) { font = Art.GuiFont(), fontSize = 21, wordWrap = true };
        boxHint = new GUIStyle(GUI.skin.label) { font = Art.GuiFont(), fontSize = 13, fontStyle = FontStyle.Bold };
    }

    void OnGUI()
    {
        if (!Running) return;
        EnsureStyles();

        int old = GUI.depth;
        GUI.depth = -120;

        if (staticA > 0f) DrawStatic(staticA);
        if (blackA > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, blackA);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        if (flashA > 0f)
        {
            GUI.color = new Color(flashCol.r, flashCol.g, flashCol.b, flashA);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;

        GUI.depth = -121;
        if (boxShown) DrawBox();

        GUI.depth = old;
        GUI.color = Color.white;
    }

    // rolling bands of tape static, the same noise the paused VHS wears
    void DrawStatic(float a)
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < 22; i++)
        {
            float y = Mathf.Floor(Random.value * Screen.height / 3f) * 3f;
            float h = Random.Range(2f, 22f);
            float g = Random.Range(0f, 1f);
            GUI.color = new Color(g, g, g, Random.Range(0.05f, 0.34f) * a);
            GUI.DrawTexture(new Rect(0f, y, Screen.width, h), Texture2D.whiteTexture);
        }
        for (int i = 0; i < 6; i++)                       // the rgb fringe of a worn tape
        {
            float y = Mathf.Floor(Random.value * Screen.height / 4f) * 4f;
            GUI.color = Random.value < 0.5f
                ? new Color(1f, 0.2f, 0.2f, 0.14f * a) : new Color(0.2f, 0.4f, 1f, 0.14f * a);
            GUI.DrawTexture(new Rect(Mathf.Sin(t * 40f + i) * 6f, y, Screen.width, Random.Range(2f, 14f)),
                            Texture2D.whiteTexture);
        }
        GUI.color = new Color(0f, 0f, 0f, 0.35f * a);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    // the Undertale box: black plate, hard border, the name in the speaker's
    // own colour, and the line typing itself out under it
    void DrawBox()
    {
        float w = Mathf.Min(1080f, Screen.width - 64f);
        float h = 152f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - h - 34f;

        Vector2 shake = boxShake
            ? new Vector2(PingPong(Time.unscaledTime * 47f), PingPong(Time.unscaledTime * 41f + 0.5f)) * 5f
            : Vector2.zero;
        var r = new Rect(x + shake.x, y + shake.y, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.93f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = boxShake ? new Color(1f, 0.25f, 0.2f) : Color.white;
        float t = 4f;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);

        boxWho.fontSize = Mathf.RoundToInt(h * 0.13f);
        boxText.fontSize = Mathf.RoundToInt(h * 0.14f);

        GUI.color = curColor;
        GUI.Label(new Rect(r.x + 22f, r.y + 12f, r.width - 44f, 26f), curWho, boxWho);

        int n = Mathf.Min(typed, curText.Length);
        GUI.Label(new Rect(r.x + 22f, r.y + 44f, r.width - 44f, h - 66f), curText.Substring(0, n), boxText);

        if (lineDone && !boxQuiet)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.35f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            boxHint.alignment = TextAnchor.LowerRight;
            GUI.Label(new Rect(r.x + 22f, r.y + h - 30f, r.width - 44f, 22f), "▼", boxHint);
            GUI.color = new Color(0.7f, 0.75f, 0.82f, 0.7f);
            boxHint.alignment = TextAnchor.LowerLeft;
            GUI.Label(new Rect(r.x + 22f, r.y + h - 30f, r.width - 44f, 22f),
                      "[" + Bindings.Name(Bindings.Key(Bindings.Action.Confirm)) + "]  next", boxHint);
            boxHint.alignment = TextAnchor.MiddleCenter;
        }
        GUI.color = Color.white;
    }

    static float PingPong(float t) { return Mathf.PingPong(t, 1f) - 0.5f; }
}

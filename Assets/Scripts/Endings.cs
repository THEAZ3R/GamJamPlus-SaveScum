using System.Collections;
using UnityEngine;

// ============================================================ THE END
// SHEPARD falls and the tape offers you the only button left: a huge T to
// rewind the whole thing. You can still walk around while it is up. Ignore it
// and the prompt flickers out, the picture fades, and the last page of the
// tape plays - you on a cliff next to the body, watching reality crumble - up
// to the kill count and the level the run ended on.
public class Endings : MonoBehaviour
{
    static Endings instance;
    static Texture2D cliff;                   // the last shot of the game

    float promptT, flickerT, fadeA, holdT, waitT;
    bool prompting, epilogue, card;
    int page = -1, typed;
    float typedF, epiPause;
    string pageText = "";
    int kills;
    int level;

    GUIStyle bigT, label, body, cardTitle, cardStat;

    public static void Begin()
    {
        if (instance == null)
        {
            var g = new GameObject("Endings");
            instance = g.AddComponent<Endings>();
        }
        instance.kills = GameManager.Kills;
        var up = PlayerUpgrades.Instance;
        instance.level = up != null ? up.Level : 1;

        var p = PlayerController.Instance;
        if (p != null && p.Health != null) p.Health.invulnerable = true;   // nothing lands on the last walk

        GameManager.SetEndPrompt(true);
        instance.StartCoroutine(instance.Run());
    }

    IEnumerator Run()
    {
        yield return null;                                            // never despawn inside a death
        foreach (var e in Enemy.All.ToArray())                        // the room empties with him
            if (e != null) e.Despawn();

        // ---------------------------------------------------- the button
        prompting = true;
        promptT = 5f;
        float tick = 0f;
        while (promptT > 0f)
        {
            promptT -= Time.unscaledDeltaTime;

            tick -= Time.unscaledDeltaTime;
            if (tick <= 0f)
            {
                tick = promptT < 1.5f ? 0.22f : 0.5f;                  // the tape speeds up
                Sfx.PlayAt(Vector2.zero, "tape_tick", 0.55f, promptT < 1.5f ? 1.6f : 1f);
            }

            if (Input.GetKeyDown(KeyCode.T)) { yield return Rewind(); yield break; }
            yield return null;
        }

        // ------------------------------------------- flicker out, then fade
        flickerT = 1.4f;
        while (flickerT > 0f)
        {
            flickerT -= Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.T)) { yield return Rewind(); yield break; }
            yield return null;
        }
        prompting = false;
        GameManager.SetEndPrompt(false);

        float t = 0f;
        while (t < 1.3f)                                             // the screen goes
        {                                                            // down like a tape
            t += Time.unscaledDeltaTime;
            fadeA = Mathf.Clamp01(t / 1.3f);
            if (Random.value < 0.06f) Sfx.PlayAt(Vector2.zero, "tape_tick", 0.18f, 0.7f + Random.value * 0.5f);
            yield return null;
        }
        fadeA = 1f;
        Sfx.Play("vhs_stop");

        // ---------------------------------------------------- the last page
        epilogue = true;
        for (page = 0; page < Pages.Length; page++)
        {
            pageText = Pages[page];
            typed = 0;
            typedF = 0f;
            epiPause = 0f;
            while (typedF < pageText.Length)
            {
                // the tape's own cadence: a letter every tenth of a second,
                // one voice tick under every letter, a beat on the punctuation
                if (epiPause > 0f)
                {
                    epiPause -= Time.unscaledDeltaTime;
                    yield return null;
                    continue;
                }

                int before = (int)typedF;
                typedF += 10f * Time.unscaledDeltaTime;
                typed = Mathf.Min(pageText.Length, (int)typedF);
                for (int k = before; k < typed; k++)
                {
                    if (pageText[k] != '\n')
                        Sfx.PlayVoice("blip", 0.16f, Random.Range(1.0f, 1.4f));
                    char c = pageText[k];
                    if (c == '.' || c == '?' || c == '!') epiPause = 0.26f;
                    else if (c == ',' || c == ';' || c == ':') epiPause = 0.14f;
                }
                yield return null;
            }

            // the tape runs on without you: ENTER skips ahead, but the page
            // turns by itself so the ending always gets told
            waitT = 0f;
            while (waitT < 6f && !Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter) &&
                   !Input.GetKeyDown(KeyCode.Space) && !Input.GetKeyDown(Bindings.Key(Bindings.Action.Confirm)))
            {
                waitT += Time.unscaledDeltaTime;
                yield return null;
            }
            while (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) ||
                   Input.GetKey(KeyCode.Space) || Input.GetKey(Bindings.Key(Bindings.Action.Confirm)))
                yield return null;
        }
        epilogue = false;

        // ---------------------------------------------------- and the card
        card = true;
        Sfx.Play("levelup");
        holdT = 0f;
        while (true)
        {
            holdT += Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.T)) { yield return Rewind(); yield break; }
            if (Input.GetKeyDown(KeyCode.R)) { GameFlow.Restart(false); yield break; }
            yield return null;
        }
    }

    IEnumerator Rewind()                                            // back to the first line
    {
        prompting = false; card = false; epilogue = false;
        Sfx.Play("rewind");
        Sfx.Play("shepard_leap");
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.unscaledDeltaTime;
            fadeA = Mathf.Clamp01(t / 0.8f);
            if (Random.value < 0.3f)
                Sfx.PlayAt(Vector2.zero, "tape_tick", 0.4f, 0.6f + Random.value);
            yield return null;
        }
        GameFlow.NewGame();
    }

    static readonly string[] Pages =
    {
        "You sat down next to Shepard's body\nand watched reality crumble away.",
        "You smirked at him.\n\u201cNow I've done it.\u201d",
        "You wondered if one more rewind\ncould save this doomed world.",
        "But you thought about the war-torn world,\nabout everyone you had lost,\nabout everyone you had loved,\nand how tired you were.",
        "Maybe ending it all wasn't so bad.",
        "At least you had front-row seats,\nand a friend's company,\nto watch the madness unfold.",
    };

    // ------------------------------------------------------------ drawing
    void EnsureStyles()
    {
        if (bigT != null) return;
        bigT = new GUIStyle(GUI.skin.label)
        {
            font = Art.GuiFont(), fontSize = 240, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        };
        label = new GUIStyle(GUI.skin.label)
        {
            font = Art.GuiFont(), fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        };
        body = new GUIStyle(GUI.skin.label)
        {
            font = Art.GuiFont(), fontSize = 22, alignment = TextAnchor.UpperCenter, wordWrap = true
        };
        cardTitle = new GUIStyle(GUI.skin.label)
        {
            font = Art.GuiFont(), fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        };
        cardStat = new GUIStyle(GUI.skin.label)
        {
            font = Art.GuiFont(), fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        };
    }

    void OnGUI()
    {
        if (!prompting && !epilogue && !card && fadeA <= 0f) return;
        EnsureStyles();
        int old = GUI.depth;
        GUI.depth = -140;                            // over every other card in the game

        if (prompting) DrawPrompt();
        if (fadeA > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, fadeA);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        if (epilogue) DrawEpilogue();
        if (card) DrawCard();

        GUI.depth = old;
        GUI.color = Color.white;
    }

    // the only button left: a huge T, pulsing like a rewind key
    void DrawPrompt()
    {
        float a = flickerT > 0f
            ? (Mathf.Sin(Time.unscaledTime * 38f) > -0.2f ? Mathf.Clamp01(flickerT / 0.6f) : 0.15f)
            : 1f;
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.42f;

        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.55f * a);
        GUI.DrawTexture(new Rect(cx - 130f, cy - 130f, 260f, 260f), Texture2D.whiteTexture);

        GUI.color = new Color(0.85f, 0.55f, 1f, a);
        bigT.fontSize = 240;
        GUI.Label(new Rect(cx - 130f, cy - 148f, 260f, 260f), "T", bigT);

        GUI.color = new Color(1f, 1f, 1f, a);
        label.fontSize = 26;
        GUI.Label(new Rect(0f, cy + 130f, Screen.width, 40f), "<< REWIND THE TAPE", label);
        GUI.color = new Color(0.7f, 0.75f, 0.85f, a * 0.8f);
        GUI.Label(new Rect(0f, cy + 166f, Screen.width, 30f),
                  "press T to start over, or let the tape run out (" + Mathf.Max(0f, promptT).ToString("0.0") + "s)",
                  label);
        GUI.color = Color.white;
    }

    // you, sitting on the edge of it, next to him - and the words under it
    void DrawEpilogue()
    {
        if (cliff == null) cliff = BuildCliff();

        // the words get their slice of the frame FIRST: the plate is guaranteed
        // to stay on screen no matter how tall the shot above it turns out
        float hintH = 34f;                                          // the ENTER line keeps the floor
        float textH = Mathf.Clamp(Screen.height * 0.30f, 120f, 300f);
        float y = Screen.height * 0.06f;

        float h = Mathf.Max(80f, Screen.height - y - 16f - textH - hintH);
        float w = h * cliff.width / cliff.height;
        float maxW = Screen.width * 0.92f;
        if (w > maxW) { w = maxW; h = w * cliff.height / cliff.width; }
        float x = (Screen.width - w) * 0.5f;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(x, y, w, h), cliff);

        // the last page, on a plate of its own so nothing can ever eat it
        float ty = y + h + 16f;
        float th = textH;

        GUI.color = new Color(0.01f, 0.012f, 0.028f, 0.94f);
        GUI.DrawTexture(new Rect(x, ty, w, th), Texture2D.whiteTexture);
        GUI.color = new Color(0.85f, 0.55f, 1f, 0.55f);
        GUI.DrawTexture(new Rect(x, ty, w, 2f), Texture2D.whiteTexture);

        int n = Mathf.Min(typed, pageText.Length);
        string str = pageText.Substring(0, n);

        // the words fit the plate: the font gives way before a line can fall
        // off the bottom of the screen - long pages just set smaller
        float boxW = w - 44f, boxH = th - 16f;
        body.fontSize = 30;
        while (body.fontSize > 13 && body.CalcHeight(new GUIContent(str), boxW) > boxH)
            body.fontSize--;

        GUI.color = new Color(0.93f, 0.95f, 0.99f);
        GUI.Label(new Rect(x + 22f, ty + 10f, boxW, boxH), str, body);

        // page count, and the key that turns it early
        label.fontSize = 16;
        GUI.color = new Color(0.75f, 0.8f, 0.9f, 0.75f);
        GUI.Label(new Rect(x, ty + 4f, w - 16f, 22f),
                  (page + 1) + " / " + Pages.Length, new GUIStyle(label) { alignment = TextAnchor.MiddleRight });

        GUI.color = new Color(1f, 1f, 1f, 0.3f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f));
        label.fontSize = 20;
        GUI.Label(new Rect(0f, Screen.height - 32f, Screen.width, 24f),
                  "[ENTER] NEXT   -   the tape turns on its own in " + Mathf.Max(0f, 6f - waitT).ToString("0") + "s", label);
        GUI.color = Color.white;
    }

    void DrawCard()
    {
        float cx = Screen.width * 0.5f;
        float a = Mathf.Clamp01(holdT * 1.4f);

        GUI.color = new Color(0.35f, 0.95f, 1f, a);
        cardTitle.fontSize = 54;
        GUI.Label(new Rect(0f, Screen.height * 0.24f, Screen.width, 70f), "CONGRATS. IT'S OVER.", cardTitle);

        GUI.color = new Color(1f, 1f, 1f, a);
        cardStat.fontSize = 28;
        GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 40f), "KILLS   " + kills, cardStat);
        GUI.Label(new Rect(0f, Screen.height * 0.45f, Screen.width, 40f), "LEVEL   " + level, cardStat);

        GUI.color = new Color(0.85f, 0.6f, 1f, a * (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f)));
        label.fontSize = 22;
        GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 30f), "[T]  REWIND THE TAPE - from the first cutscene", label);
        GUI.color = new Color(0.8f, 0.85f, 0.92f, a * 0.8f);
        GUI.Label(new Rect(0f, Screen.height * 0.62f + 32f, Screen.width, 30f), "[R]  RESTART - straight into the ring", label);
        GUI.color = Color.white;
    }

    // ============================================================ THE CLIFF
    // the last shot of the game, drawn from math: a dead sky over the edge of
    // a cliff, you sitting on it, the body beside you, and the world taking
    // itself apart on the horizon
    static Texture2D BuildCliff()
    {
        const int W = 256, H = 132;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var rng = new System.Random(1977);

        int horizon = Mathf.RoundToInt(H * 0.60f);          // from the bottom up
        for (int y = 0; y < H; y++)
        {
            float f = Mathf.Clamp01((y - horizon) / (float)(H - horizon));   // 0 at the horizon
            var sky = Color.Lerp(new Color(0.26f, 0.10f, 0.11f), new Color(0.035f, 0.030f, 0.062f), f);
            for (int x = 0; x < W; x++) tex.SetPixel(x, y, sky);
        }

        // stars, only high enough that the glow does not eat them
        for (int i = 0; i < 90; i++)
        {
            int x = rng.Next(0, W), y = rng.Next(horizon + 26, H);
            float v = Mathf.Lerp(0.35f, 1f, (float)rng.NextDouble());
            tex.SetPixel(x, y, new Color(v, v, v * 1.05f));
        }

        // the sun that is not a sun any more, and its bruise of a glow
        float sx = W * 0.66f, sy = horizon + 4f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = Mathf.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy));
                if (d > 42f) continue;
                var c = tex.GetPixel(x, y);
                float glow = Mathf.Clamp01(1f - d / 42f) * 0.55f;
                var g = new Color(0.85f, 0.32f, 0.20f);
                tex.SetPixel(x, y, Color.Lerp(c, g, glow));
            }
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = Mathf.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy));
                if (d > 11f) continue;
                var c = tex.GetPixel(x, y);
                tex.SetPixel(x, y, Color.Lerp(c, new Color(0.95f, 0.55f, 0.30f), Mathf.Clamp01(1f - d / 11f)));
            }

        // the skyline of a world eating itself: blocks missing, blocks floating
        var voidC = new Color(0.015f, 0.012f, 0.030f);
        for (int x = 0; x < W; x++)
        {
            int hgt = 4 + (int)(Mathf.PerlinNoise(x * 0.06f, 3.1f) * 16f);
            for (int y = horizon; y < horizon + hgt; y++)
                tex.SetPixel(x, y, voidC);
            if (rng.NextDouble() < 0.10)                       // a bite out of the skyline
                for (int y = horizon; y < horizon + rng.Next(2, hgt + 2); y++) tex.SetPixel(x, y, Color.clear);
            if (rng.NextDouble() < 0.05)                       // a scrap that never fell
                for (int y = horizon + hgt + rng.Next(2, 12); y < horizon + hgt + rng.Next(13, 22); y++)
                    if (y < H) tex.SetPixel(x, y, voidC);
        }
        for (int i = 0; i < 5; i++)                            // the glitch in the distance
        {
            int gx = rng.Next(0, W), gy = rng.Next(horizon + 2, horizon + 26), gw = rng.Next(6, 26);
            var gc = i % 2 == 0 ? new Color(0.25f, 0.85f, 1f, 0.55f) : new Color(0.95f, 0.35f, 0.95f, 0.5f);
            for (int x = gx; x < Mathf.Min(W, gx + gw); x++)
                tex.SetPixel(x, gy, Color.Lerp(tex.GetPixel(x, gy), gc, 0.7f));
        }

        // the cliff: solid ground until it simply is not
        float edge = W * 0.78f;
        for (int x = 0; x < W; x++)
        {
            float top = horizon - 12f + Mathf.Sin(x * 0.09f) * 1.2f;   // the plateau
            if (x > edge) top += (x - edge) * 1.9f;                     // it falls away right
            int ty = Mathf.RoundToInt(top);
            for (int y = 0; y < H; y++)
            {
                if (y > ty) continue;
                if (y == ty) tex.SetPixel(x, y, new Color(0.16f, 0.15f, 0.19f));
                else tex.SetPixel(x, y, new Color(0.045f, 0.045f, 0.060f));
            }
        }

        int ground = horizon - 13;                            // the plateau line: where you sit

        Stamp(tex, Sit, W * 0.44f, ground, '#', new Color(0.02f, 0.02f, 0.03f));
        Stamp(tex, Sit, W * 0.44f, ground, 'f', new Color(0.20f, 0.45f, 0.55f));
        Stamp(tex, Lying, W * 0.51f, ground, '#', new Color(0.055f, 0.03f, 0.05f));
        Stamp(tex, Lying, W * 0.51f, ground, 'f', new Color(0.55f, 0.13f, 0.11f));
        Stamp(tex, Lying, W * 0.51f, ground, 'r', new Color(0.28f, 0.06f, 0.08f));

        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    // you, sitting on the edge: knees up, elbows on them, looking out
    static readonly string[] Sit =
    {
        "....####....",
        "...##ff##...",
        "...##..##...",
        "...######...",
        "....####....",
        "...######...",
        "..########..",
        "..##ffff##..",
        "..##....##..",
        "..##....##..",
        "..##.....#..",
        ".###.....#..",
        ".#########..",
        ".##########.",
    };

    // him, on his back beside you, and what came out of him
    static readonly string[] Lying =
    {
        "....##..................",
        "...####..............###",
        "..######...##########...",
        "..ff####..############..",
        "...####...###########...",
        "....##.....#######......",
        "............###.........",
        "..............r.........",
        "...........rrrrr........",
        "..........rrrrrrrr......",
        "..........rrrrrrrrr.....",
    };

    static void Stamp(Texture2D tex, string[] rows, float fx, int top, char c, Color col)
    {
        int ox = Mathf.RoundToInt(fx);
        for (int y = 0; y < rows.Length; y++)
        {
            int py = top + rows.Length - 1 - y;                // rows are listed top-first
            if (py < 0 || py >= tex.height) continue;
            for (int x = 0; x < rows[y].Length; x++)
            {
                if (rows[y][x] != c) continue;
                int px = ox + x;
                if (px < 0 || px >= tex.width) continue;
                tex.SetPixel(px, py, col);
            }
        }
    }
}

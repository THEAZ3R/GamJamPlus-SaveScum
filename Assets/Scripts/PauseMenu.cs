using UnityEngine;

// THE PAUSE: ESC stops the tape. The ring sits at timeScale 0 behind the
// same veil the main menu wears, with three ways out - keep going, put the
// tape back on the shelf, or stop the game entirely.
//
// It refuses to open over anything that already owns the screen (the menu,
// the tape's dialogue, the level-up table, the how-to card, Shepard's frozen
// moment) and over a run that has ended, so the timescale can always be
// handed straight back. Everything it needs - the cursor, the crosshair,
// the body, the music's volume - is wired at those call sites by whoever
// asks for PauseMenu.Opened.
public static class PauseMenu
{
    public static bool Opened { get; private set; }

    static int sel;                                  // the row the keyboard is on
    static GUIStyle title, small, hint;
    static int lastW, lastH;

    // ------------------------------------------------------------- states
    public static void Reset()                        // every scene load starts unpaused
    {
        Opened = false;
        sel = 0;
    }

    static bool CanOpen()
    {
        if (Opened) return false;
        if (GameMenu.Open || LevelUpUI.IsOpen) return false;     // someone owns the screen
        if (GameManager.IsOver || GameManager.IntroT > 0f) return false;  // the run isn't live
        if (Cutscenes.Running || Shepard.VhsActive) return false;         // the tape is talking
        return true;
    }

    public static void Poll()                         // called from GameManager.Update
    {
        if (!Opened)
        {
            if (Input.GetKeyDown(KeyCode.Escape) && CanOpen())
            {
                Opened = true;
                sel = 0;
                Time.timeScale = 0f;
                Sfx.PlayAt(Vector2.zero, "menu_go", 0.7f);       // the deck stops
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) { Resume(); return; }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            sel = (sel + 2) % 3;
            Sfx.PlayAt(Vector2.zero, "menu_move", 0.55f);
        }
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            sel = (sel + 1) % 3;
            Sfx.PlayAt(Vector2.zero, "menu_move", 0.55f);
        }

        bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                       Input.GetKeyDown(KeyCode.Space) || Bindings.Down(Bindings.Action.Confirm);
        if (confirm) Activate(sel);
    }

    // ------------------------------------------------------------ the ways out
    static void Resume()
    {
        if (!Opened) return;
        Opened = false;
        // the level-up table would have frozen the world under us, and it
        // owns the timescale from there - otherwise the ring runs again
        Time.timeScale = LevelUpUI.IsOpen ? 0f : 1f;
        Sfx.PlayAt(Vector2.zero, "menu_go", 0.7f);
    }

    static void Activate(int which)
    {
        if (which == 0) Resume();
        else if (which == 1) ToMenu();
        else Quit();
    }

    static void ToMenu()
    {
        Opened = false;
        Sfx.PlayAt(Vector2.zero, "menu_go", 0.7f);
        GameFlow.ToMenu();                             // reloads the scene and rebuilds the run
    }

    static void Quit()
    {
        Sfx.Play("vhs_stop");                          // the tape gives the picture back one last time
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------- drawing
    static void Ensure()
    {
        if (title != null && lastW == Screen.width && lastH == Screen.height) return;
        lastW = Screen.width; lastH = Screen.height;
        title = new GUIStyle(GUI.skin.label)
        {
            font = Art.GuiFont(), fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        };
        small = new GUIStyle(GUI.skin.label)
        {
            font = Art.GuiFont(), fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        };
        hint = new GUIStyle(GUI.skin.label) { font = Art.GuiFont(), fontSize = 15, alignment = TextAnchor.MiddleCenter };
    }

    public static void Draw()                          // called from GameManager.OnGUI
    {
        if (!Opened) return;
        Ensure();
        int old = GUI.depth;
        GUI.depth = -160;                              // over the hud, the cards, everything

        // the tape holds the picture: the main menu's veil, not as heavy
        GUI.color = new Color(0.01f, 0.014f, 0.028f, 0.86f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.025f);
        for (float y = 0f; y < Screen.height; y += 4f)
            GUI.DrawTexture(new Rect(0f, y, Screen.width, 1f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float cx = Screen.width * 0.5f;

        GUI.color = new Color(0.35f, 0.95f, 1f);
        GUI.Label(new Rect(0f, Screen.height * 0.24f, Screen.width, 80f), "PAUSED", title);
        GUI.color = new Color(0.85f, 0.6f, 1f, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.4f));
        GUI.Label(new Rect(0f, Screen.height * 0.24f + 76f, Screen.width, 30f), "THE TAPE IS STOPPED", small);
        GUI.color = Color.white;

        float w = 320f, h = 52f, gap = 14f;
        float y0 = Screen.height * 0.45f;
        string[] what = { "RESUME", "RETURN TO MENU", "QUIT" };

        for (int i = 0; i < what.Length; i++)
        {
            var r = new Rect(cx - w * 0.5f, y0 + (h + gap) * i, w, h);
            if (Row(r, what[i], i)) Activate(i);
        }

        GUI.color = new Color(0.8f, 0.85f, 0.92f, 0.5f);
        GUI.Label(new Rect(0f, Screen.height - 40f, Screen.width, 26f),
                  "ESC - KEEP GOING        UP / DOWN - PICK        ENTER - CHOOSE", hint);
        GUI.color = Color.white;

        GUI.depth = old;
    }

    // one row of the stack: lit by the pointer or by the keyboard, and the
    // two always agree on which row is selected
    static bool Row(Rect r, string text, int index)
    {
        Vector2 mouse = Event.current.mousePosition;
        bool hot = r.Contains(mouse);
        if (hot) sel = index;
        bool lit = hot || sel == index;
        bool down = hot && Event.current.type == EventType.MouseDown;

        GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = lit ? new Color(0.35f, 0.95f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.35f);
        float t = lit ? 2.5f : 1.5f;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);

        GUI.color = lit ? new Color(0.45f, 0.98f, 1f) : Color.white;
        GUI.Label(r, text, small);
        GUI.color = Color.white;

        if (down) { Event.current.Use(); return true; }
        return false;
    }
}

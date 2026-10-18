using UnityEngine;
using UnityEngine.SceneManagement;

// WHERE A RUN COMES FROM: the menu on the first launch, the intro tape once
// per play session, and straight back into the ring after a death. Both flags
// are static, so they survive the scene reload a restart does.
public static class GameFlow
{
    public static bool MenuOnStart = true;      // false after a plain R restart

    public static void Restart(bool withIntro)
    {
        Cutscenes.IntroSeen = !withIntro;
        MenuOnStart = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static void NewGame()                // the tape's own rewind: all of it again
    {
        Cutscenes.IntroSeen = false;
        MenuOnStart = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

// THE MENU: start, controls (with rebinding and mouse sensitivity) and exit,
// drawn over whatever the arena is doing behind it - which is nothing, since
// the whole game is held at timeScale 0 while this is up.
public class GameMenu : MonoBehaviour
{
    public static GameMenu Instance;
    public static bool Open { get; private set; }

    enum Page { Main, Controls }
    Page page = Page.Main;
    Bindings.Action? rebind;                    // which row is waiting for a key

    GUIStyle title, small, hint, rowStyle;
    GUIStyleState hover, plain;
    int lastW, lastH;

    public static GameMenu Show()
    {
        if (Instance == null)
        {
            var g = new GameObject("GameMenu");
            Instance = g.AddComponent<GameMenu>();
        }
        Open = true;
        Instance.page = Page.Main;
        Instance.rebind = null;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Aim.SyncToMouse();
        return Instance;
    }

    public static void Close()
    {
        Open = false;
        if (Instance != null) Instance.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!Open) return;

        // a key press while a row is listening becomes that row's key
        if (rebind.HasValue)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { rebind = null; return; }
            foreach (var k in System.Enum.GetValues(typeof(KeyCode)))
            {
                var kc = (KeyCode)k;
                if (kc == KeyCode.Escape || kc == KeyCode.None) continue;
                if (kc >= KeyCode.Mouse0) continue;              // mouse buttons are not bindings
                if (!Input.GetKeyDown(kc)) continue;
                Bindings.SetKey(rebind.Value, kc);
                Sfx.Play("pickup");
                rebind = null;
                return;
            }
            return;
        }

        if (Bindings.Down(Bindings.Action.Confirm) && page == Page.Main) BeginGame();
    }

    void BeginGame()
    {
        Bindings.Save();
        Close();
        var gm = GameManager.Instance;
        if (!Cutscenes.IntroSeen) Cutscenes.PlayIntro();
        else if (gm != null) gm.BeginRun(true);
    }

    // ------------------------------------------------------------ gui
    void EnsureStyles()
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
        rowStyle = new GUIStyle(GUI.skin.label)
        {
            font = Art.GuiFont(), fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft
        };
        hover = new GUIStyleState { textColor = new Color(0.45f, 0.98f, 1f) };
        plain = new GUIStyleState { textColor = Color.white };
    }

    void OnGUI()
    {
        if (!Open) return;
        EnsureStyles();

        // the tape holds the picture: dark veil + scanlines over the arena
        GUI.color = new Color(0.01f, 0.014f, 0.028f, 0.88f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.025f);
        for (float y = 0f; y < Screen.height; y += 4f)
            GUI.DrawTexture(new Rect(0f, y, Screen.width, 1f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float cx = Screen.width * 0.5f;

        GUI.color = new Color(0.35f, 0.95f, 1f);
        GUI.Label(new Rect(0f, Screen.height * 0.08f, Screen.width, 80f), "SAVE_SCUM", title);
        GUI.color = new Color(0.85f, 0.6f, 1f, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.4f));
        GUI.Label(new Rect(0f, Screen.height * 0.08f + 72f, Screen.width, 26f),
                  "PRESS ENTER TO START", small);
        GUI.color = Color.white;

        if (page == Page.Main) DrawMain(cx);
        else DrawControls();

        GUI.color = new Color(0.8f, 0.85f, 0.92f, 0.5f);
        GUI.Label(new Rect(0f, Screen.height - 30f, Screen.width, 24f), "M - MUTE", hint);
        GUI.color = Color.white;
    }

    // ------------------------------------------------------------ main page
    void DrawMain(float cx)
    {
        float w = 300f, h = 52f, gap = 14f;
        float y = Screen.height * 0.40f;

        if (Button(new Rect(cx - w * 0.5f, y, w, h), "START", false)) BeginGame();
        if (Button(new Rect(cx - w * 0.5f, y + (h + gap), w, h), "CONTROLS", false))
        { page = Page.Controls; Sfx.Play("pickup"); }
        if (Button(new Rect(cx - w * 0.5f, y + (h + gap) * 2f, w, h), "EXIT", false))
        {
            Sfx.Play("vhs_stop");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    // ------------------------------------------------------------ controls
    void DrawControls()
    {
        float cx = Screen.width * 0.5f;
        float top = Screen.height * 0.20f;
        float rowH = 34f;
        float colW = 420f;
        float x = cx - colW - 40f;

        GUI.color = new Color(0.35f, 0.95f, 1f);
        GUI.Label(new Rect(0f, top - 56f, Screen.width, 40f), "CONTROLS", title);
        GUI.color = Color.white;

        Vector2 mouse = Event.current.mousePosition;
        bool down = Event.current.type == EventType.MouseDown;

        for (int i = 0; i < Bindings.Count; i++)
        {
            var a = (Bindings.Action)i;
            var r = new Rect(x, top + i * rowH, colW, rowH - 4f);
            bool listening = rebind.HasValue && rebind.Value == a;
            bool hot = r.Contains(mouse);

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = listening ? new Color(1f, 0.5f, 0.2f, 0.9f)
                       : hot ? new Color(0.35f, 0.95f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.14f);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1.5f, r.width, 1.5f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 12f, r.y, r.width * 0.55f, r.height), Bindings.Label(a), rowStyle);

            GUI.color = listening ? new Color(1f, 0.6f, 0.25f) : new Color(0.55f, 0.95f, 1f);
            var ks = new GUIStyle(rowStyle) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(r.x + r.width * 0.45f, r.y, r.width * 0.5f - 12f, r.height),
                      listening ? "PRESS A KEY..." : Bindings.Name(Bindings.Key(a)), ks);
            GUI.color = Color.white;

            if (down && r.Contains(mouse)) { rebind = a; Sfx.Play("pickup"); Event.current.Use(); }
        }

        // mouse sensitivity: a slider, and the value it is on
        float sy = top + Bindings.Count * rowH + 22f;
        var sr = new Rect(x, sy, colW, rowH - 4f);
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(sr, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(sr.x + 12f, sr.y, sr.width * 0.6f, sr.height), "MOUSE SENSITIVITY", rowStyle);

        var slider = new Rect(sr.x + sr.width * 0.52f, sr.y + 9f, sr.width * 0.32f, sr.height - 18f);
        float v = GUI.HorizontalSlider(slider, Bindings.Sensitivity, 0.15f, 3.5f);
        if (!Mathf.Approximately(v, Bindings.Sensitivity))
        {
            Bindings.Sensitivity = v;
            Bindings.Save();
            if (Event.current.type == EventType.MouseDrag) Sfx.PlayAt(Vector2.zero, "tick", 0.12f, 0.8f + v * 0.4f);
        }
        GUI.color = new Color(0.55f, 0.95f, 1f);
        GUI.Label(new Rect(sr.x + sr.width - 52f, sr.y, 44f, sr.height),
                  Bindings.Sensitivity.ToString("0.00"), new GUIStyle(rowStyle) { alignment = TextAnchor.MiddleRight });
        GUI.color = Color.white;

        // what the rest of the keys do, spelled out
        float ny = sy + rowH + 10f;
        GUI.color = new Color(0.88f, 0.92f, 0.96f);
        var note = new GUIStyle(hint) { wordWrap = true, alignment = TextAnchor.UpperCenter };
        GUI.Label(new Rect(cx - colW, ny, colW * 2f, 150f),
                  "MOUSE - AIM & SHOOT        ENTER - advance the cutscene dialogue\n" +
                  "ARROW KEYS - always move, whatever the bindings say\n" +
                  "The clock top-right is the escape timer: it hits 0 and THE COMMANDER ESCAPED.\n" +
                  "M - mute the tape", note);
        GUI.color = Color.white;

        var back = new Rect(cx - 110f, Screen.height - 110f, 220f, 44f);
        if (Button(back, rebind.HasValue ? "CANCEL" : "BACK", true))
        {
            if (rebind.HasValue) rebind = null;
            else { page = Page.Main; Sfx.Play("pickup"); }
        }
    }

    bool Button(Rect r, string label, bool quiet)
    {
        Vector2 mouse = Event.current.mousePosition;
        bool hot = r.Contains(mouse);
        bool down = hot && Event.current.type == EventType.MouseDown;

        GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = hot ? new Color(0.35f, 0.95f, 1f) : (quiet ? new Color(1f, 1f, 1f, 0.25f) : new Color(1f, 1f, 1f, 0.4f));
        float t = hot ? 2.5f : 1.5f;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);

        GUI.color = hot ? new Color(0.45f, 0.98f, 1f) : Color.white;
        GUI.Label(r, label, small);
        GUI.color = Color.white;

        if (down) { Event.current.Use(); return true; }
        return false;
    }
}

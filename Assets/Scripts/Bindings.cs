using UnityEngine;

// REMAPPABLE CONTROLS: every key the player owns lives here, saved between
// sessions. Movement keeps the arrow keys as an unremappable alias (they are
// baked into the old input axes), dash keeps LeftShift, confirm keeps the
// numpad enter - so a rebound layout can never lock anyone out of the game.
public static class Bindings
{
    public enum Action { MoveUp, MoveDown, MoveLeft, MoveRight, Dash, Timeleap, Confirm }
    public const int Count = 7;

    static readonly KeyCode[] Defaults =
    {
        KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D,
        KeyCode.Space, KeyCode.T, KeyCode.Return
    };

    static readonly KeyCode[] MoveAlias =
    {
        KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
        KeyCode.None, KeyCode.None, KeyCode.None
    };

    static KeyCode[] keys;
    public static float Sensitivity = 1f;

    // ------------------------------------------------------------ state
    static void Ensure()
    {
        if (keys != null) return;
        keys = new KeyCode[Count];
        for (int i = 0; i < Count; i++)
        {
            int v = PlayerPrefs.GetInt("bind_" + i, (int)Defaults[i]);
            keys[i] = System.Enum.IsDefined(typeof(KeyCode), v) ? (KeyCode)v : Defaults[i];
        }
        Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("sens", 1f), 0.15f, 3.5f);
    }

    public static void Save()
    {
        Ensure();
        for (int i = 0; i < Count; i++) PlayerPrefs.SetInt("bind_" + i, (int)keys[i]);
        PlayerPrefs.SetFloat("sens", Sensitivity);
        PlayerPrefs.Save();
    }

    public static void Defaults_()                    // the controls page's reset
    {
        keys = null;
        Sensitivity = 1f;
        Save();
    }

    // ------------------------------------------------------------ reading
    public static KeyCode Key(Action a) { Ensure(); return keys[(int)a]; }

    public static bool Held(Action a)
    {
        Ensure();
        int i = (int)a;
        return Input.GetKey(keys[i]) || (MoveAlias[i] != KeyCode.None && Input.GetKey(MoveAlias[i]));
    }

    public static bool Down(Action a)
    {
        Ensure();
        int i = (int)a;
        return Input.GetKeyDown(keys[i]) || (MoveAlias[i] != KeyCode.None && Input.GetKeyDown(MoveAlias[i]));
    }

    // the whole stick at once: remapped keys first, arrows always
    public static Vector2 Move()
    {
        Ensure();
        var v = new Vector2(
            (Held(Action.MoveRight) ? 1f : 0f) - (Held(Action.MoveLeft) ? 1f : 0f),
            (Held(Action.MoveUp) ? 1f : 0f) - (Held(Action.MoveDown) ? 1f : 0f));
        if (v.sqrMagnitude > 1f) v.Normalize();
        return v;
    }

    public static void SetKey(Action a, KeyCode k)     // a swap, never a duplicate
    {
        Ensure();
        int i = (int)a;
        for (int j = 0; j < Count; j++)
            if (j != i && keys[j] == k) { keys[j] = keys[i]; break; }
        keys[i] = k;
        Save();
    }

    public static string Label(Action a)
    {
        switch (a)
        {
            case Action.MoveUp: return "MOVE UP";
            case Action.MoveDown: return "MOVE DOWN";
            case Action.MoveLeft: return "MOVE LEFT";
            case Action.MoveRight: return "MOVE RIGHT";
            case Action.Dash: return "DASH";
            case Action.Timeleap: return "TIMELEAP";
            default: return "CONFIRM / NEXT LINE";
        }
    }

    public static string Name(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Return: return "ENTER";
            case KeyCode.KeypadEnter: return "NUM ENTER";
            case KeyCode.LeftShift: return "L-SHIFT";
            case KeyCode.RightShift: return "R-SHIFT";
            case KeyCode.LeftControl: return "L-CTRL";
            case KeyCode.RightControl: return "R-CTRL";
            case KeyCode.LeftAlt: return "L-ALT";
            case KeyCode.RightAlt: return "R-ALT";
            case KeyCode.Space: return "SPACE";
            case KeyCode.None: return "-";
        }
        string s = k.ToString();
        if (s.StartsWith("Alpha")) s = s.Substring(5);
        if (s.StartsWith("Keypad")) s = "NUM " + s.Substring(6);
        return s.Replace("Arrow", "").ToUpperInvariant();
    }
}

// THE AIM CURSOR: the mouse pointer IS the crosshair. Every frame the game
// reads where the pointer actually sits and puts the reticle right there -
// no delta to lose, no cursor lock to refuse, nothing that can drift, stick
// or lag behind your hand. The sensitivity slider scales how far the reticle
// swings from the middle of the frame for a given hand motion: at 1.0 it
// sits exactly under the pointer. Menus hand the real cursor back to the OS
// for their own use; nothing has to be re-synced because the position is
// read fresh on the first frame of play again.
public static class Aim
{
    public static Vector2 Screen = new Vector2(400f, 300f);

    // kept for callers: the read is absolute now, there is nothing to sync
    public static void SyncToMouse() { }

    // park the reticle in the middle of the frame (only used when the update
    // loop is frozen and the pointer is not being read at all)
    public static void Center(int w, int h)
    {
        Screen = new Vector2(w * 0.5f, h * 0.5f);
    }

    public static void Tick(int w, int h)
    {
        Vector2 mp = Input.mousePosition;
        Vector2 centre = new Vector2(w * 0.5f, h * 0.5f);
        Vector2 p = new Vector2(Mathf.Clamp(mp.x, 0f, w), Mathf.Clamp(mp.y, 0f, h));
        Vector2 s = centre + (p - centre) * Bindings.Sensitivity;
        Screen = new Vector2(Mathf.Clamp(s.x, 1f, w - 1f), Mathf.Clamp(s.y, 1f, h - 1f));
    }

    // F6: where the aim actually is - one line, so a screenshot of it settles
    // any argument about lag (reticle, pointer and lock state side by side)
    public static string Diag()
    {
        Vector2 mp = Input.mousePosition;
        return "AIM " + Screen.x.ToString("0") + "," + Screen.y.ToString("0") +
               "   MOUSE " + mp.x.ToString("0") + "," + mp.y.ToString("0") +
               "   SENS " + Bindings.Sensitivity.ToString("0.00") +
               "   LOCK " + Cursor.lockState + " VIS " + Cursor.visible;
    }

    // the same point in OnGUI space: origin top-left, Y pointing DOWN.
    // Screen above is Input-space (origin bottom-left), so anything drawn with
    // GUI.* must use this, never Screen directly
    public static Vector2 Gui
    {
        get { return new Vector2(Screen.x, UnityEngine.Screen.height - Screen.y); }
    }

    public static Vector3 World(Camera cam)
    {
        if (cam == null) cam = Camera.main;          // a stale / destroyed camera must never turn pixels into world units
        return cam != null ? cam.ScreenToWorldPoint(new Vector3(Screen.x, Screen.y, 10f)) : (Vector3)Screen;
    }
}
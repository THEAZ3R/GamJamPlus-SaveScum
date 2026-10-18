using UnityEngine;

// The three-card level-up table. Freezes the game (timeScale 0) until a mod
// is picked; if the player banked more than one level it re-rolls in place.
public class LevelUpUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    static LevelUpUI active;

    PlayerUpgrades owner;
    readonly UpgradeKind[] choices = new UpgradeKind[3];
    int choiceCount;

    GUIStyle titleStyle, nameStyle, descStyle, keyStyle, chipStyle;

    public static void Open(PlayerUpgrades owner)
    {
        if (active == null)
        {
            var g = new GameObject("LevelUpUI");
            active = g.AddComponent<LevelUpUI>();
        }
        active.owner = owner;
        active.gameObject.SetActive(true);
        IsOpen = true;
        Sfx.Play("levelup");                       // the moment the bar fills
        Time.timeScale = 0f;
        active.Roll();
    }

    public static void Close()
    {
        IsOpen = false;
        if (active != null) active.gameObject.SetActive(false);
        if (!GameManager.IsOver) Time.timeScale = 1f;
    }

    // three distinct mods from whatever is still installable, with a gentle
    // nudge toward the ones already installed: a build wants to become itself
    void Roll()
    {
        var pool = new System.Collections.Generic.List<UpgradeKind>();
        var weight = new System.Collections.Generic.List<float>();
        for (int i = 0; i < Upgrade.Count; i++)
        {
            var k = (UpgradeKind)i;
            if (Upgrade.IsMaxed(k, owner.LevelOf(k))) continue;
            if (owner.IsLocked(k)) continue;               // welded-shut mods never show
            pool.Add(k);
            weight.Add((1f + 0.35f * Mathf.Min(owner.LevelOf(k), 4)) * Upgrade.Rarity(k));
        }

        // the table never deals fewer than three: when the installable pool
        // runs thin, the build's finished modules fill the last seats (locked
        // ones still never do - they are not content, they are a closed door)
        if (pool.Count < 3)
        {
            for (int i = 0; i < Upgrade.Count && pool.Count < 3; i++)
            {
                var k = (UpgradeKind)i;
                if (pool.Contains(k) || owner.IsLocked(k)) continue;
                if (!Upgrade.IsMaxed(k, owner.LevelOf(k))) continue;
                pool.Add(k);
                weight.Add(0.5f);
            }
        }

        choiceCount = Mathf.Min(3, pool.Count);
        for (int c = 0; c < choiceCount; c++)              // weighted, without repeats
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++) total += weight[i];

            float r = Random.value * total, acc = 0f;
            int pick = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++)
            {
                acc += weight[i];
                if (r < acc) { pick = i; break; }
            }
            choices[c] = pool[pick];
            pool.RemoveAt(pick);
            weight.RemoveAt(pick);
        }

        if (choiceCount == 0) Close();         // nothing installable: never trap the run
    }

    void Update()
    {
        if (!IsOpen) return;
        if (GameManager.IsOver) { Close(); return; }

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Pick(0);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Pick(1);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Pick(2);
    }

    void Pick(int i)
    {
        if (i < 0 || i >= choiceCount) return;
        if (owner.IsLocked(choices[i])) { Roll(); return; }   // double-check: never install a locked mod

        owner.PickUpgrade(choices[i]);
        Sfx.Play("levelup");
        if (owner.Pending > 0 && !GameManager.IsOver) Roll();   // banked another level
        else Close();
    }

    void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            font = Art.GuiFont(), fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        };
        nameStyle = new GUIStyle(GUI.skin.label)
        {
            font = Art.GuiFont(), fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true
        };
        descStyle = new GUIStyle(GUI.skin.label) { font = Art.GuiFont(), fontSize = 12, alignment = TextAnchor.UpperCenter, wordWrap = true };
        chipStyle = new GUIStyle(GUI.skin.label) { font = Art.GuiFont(), fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        keyStyle = new GUIStyle(GUI.skin.label) { font = Art.GuiFont(), fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    void OnGUI()
    {
        if (!IsOpen || owner == null) return;
        EnsureStyles();

        // a thin veil, not a blackout: the arena stays readable behind the table
        GUI.color = new Color(0.01f, 0.015f, 0.03f, 0.45f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        GUI.color = new Color(0.35f, 0.95f, 1f);
        GUI.Label(new Rect(0, 14, Screen.width, 36), "SYSTEM OVERCLOCK   //   LEVEL " + owner.Level, titleStyle);

        float cw = 208f, ch = 248f, gap = 16f;
        float x0 = (Screen.width - (cw * 3f + gap * 2f)) * 0.5f;
        float y0 = (Screen.height - ch) * 0.5f;

        Vector2 mouse = Event.current.mousePosition;
        bool down = Event.current.type == EventType.MouseDown;

        for (int i = 0; i < choiceCount; i++)
        {
            var r = new Rect(x0 + i * (cw + gap), y0, cw, ch);
            DrawCard(i, r, r.Contains(mouse));

            if (down && r.Contains(mouse))
            {
                Pick(i);
                Event.current.Use();
                break;
            }
        }

        GUI.color = new Color(1f, 1f, 1f, 0.45f);
        GUI.Label(new Rect(0, y0 + ch + 8f, Screen.width, 22),
                  "click a module   or   press 1 / 2 / 3", descStyle);
        GUI.color = Color.white;
    }

    void DrawCard(int i, Rect r, bool hot)
    {
        var kind = choices[i];
        var accent = Upgrade.Accent(kind);
        int lvl = owner.LevelOf(kind);

        GUI.color = new Color(0.05f, 0.07f, 0.11f, 0.92f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        GUI.color = hot ? accent : new Color(accent.r, accent.g, accent.b, 0.4f);
        DrawBorder(r, hot ? 3f : 1.5f);

        GUI.color = new Color(1f, 1f, 1f, hot ? 0.10f : 0.03f);
        GUI.DrawTexture(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), Texture2D.whiteTexture);

        float icon = 60f;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(r.x + (r.width - icon) * 0.5f, r.y + 12f, icon, icon),
                        Art.UpgradeIcon(kind).texture);

        GUI.color = accent;
        GUI.Label(new Rect(r.x + 6f, r.y + 76f, r.width - 12f, 38f), Upgrade.Name(kind), nameStyle);

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(r.x + r.width * 0.5f - 40f, r.y + 116f, 80f, 18f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x, r.y + 117f, r.width, 18f),
                  lvl <= 0 ? "NEW MODULE" : Upgrade.IsMaxed(kind, lvl) ? "MAXED" :
                  "LV " + lvl + "  >  " + (lvl + 1), chipStyle);

        GUI.color = new Color(0.90f, 0.93f, 0.97f);
        GUI.Label(new Rect(r.x + 10f, r.y + 140f, r.width - 20f, 84f), Upgrade.Desc(kind, lvl), descStyle);

        GUI.color = hot ? accent : new Color(1f, 1f, 1f, 0.35f);
        GUI.Label(new Rect(r.x, r.y + r.height - 26f, r.width, 22f), "[" + (i + 1) + "]", keyStyle);
        GUI.color = Color.white;
    }

    static void DrawBorder(Rect r, float t)
    {
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
    }
}

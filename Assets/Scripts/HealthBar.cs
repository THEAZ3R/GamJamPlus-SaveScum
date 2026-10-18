using UnityEngine;

// Tiny bar that pops up above a unit only after it takes damage, then fades.
public class HealthBar : MonoBehaviour
{
    const float ShowTime = 2.2f;

    SpriteRenderer bg, fill;
    float width, show;

    public static HealthBar Attach(Transform parent, float width, Color color, float offsetY)
    {
        var g = new GameObject("HPBar");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(0, offsetY, 0);

        var b = g.AddComponent<HealthBar>();
        b.width = width;

        b.bg = Art.Part(g.transform, "HPBarBG", SpriteFactory.Square, Vector2.zero,
                        new Vector2(width + 0.06f, 0.15f), new Color(0.02f, 0.02f, 0.03f, 0.9f), 200);
        b.fill = Art.Part(g.transform, "HPBarFill", SpriteFactory.Square, Vector2.zero,
                          new Vector2(width, 0.09f), color, 201);

        b.bg.enabled = false;
        b.fill.enabled = false;
        return b;
    }

    public void Set(float frac)
    {
        frac = Mathf.Clamp01(frac);
        show = ShowTime;

        float w = Mathf.Max(0.0001f, width * frac);
        fill.transform.localScale = new Vector3(w, 0.09f, 1f);
        fill.transform.localPosition = new Vector3(-width * 0.5f + w * 0.5f, 0f, 0f);

        bg.enabled = true;
        fill.enabled = frac > 0.001f;
    }

    void Update()
    {
        if (show <= 0f) return;
        show -= Time.deltaTime;

        float a = show < 0.4f ? show / 0.4f : 1f;
        SetAlpha(bg, a);
        SetAlpha(fill, a);

        if (show <= 0f) { bg.enabled = false; fill.enabled = false; }
    }

    static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        var c = sr.color;
        c.a = a * (sr.gameObject.name == "HPBarBG" ? 0.9f : 1f);
        sr.color = c;
    }
}

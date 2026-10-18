using UnityEngine;

public static class SpriteFactory
{
    static Sprite square, circle;

    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                var t = new Texture2D(1, 1);
                t.SetPixel(0, 0, Color.white);
                t.Apply();
                t.filterMode = FilterMode.Point;
                t.hideFlags = HideFlags.HideAndDontSave;
                square = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return square;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                int s = 64;
                var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
                float r = s / 2f;
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                        t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
                    }
                t.Apply();
                t.wrapMode = TextureWrapMode.Clamp;
                t.hideFlags = HideFlags.HideAndDontSave;
                circle = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            }
            return circle;
        }
    }
}
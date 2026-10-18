using System.Collections.Generic;
using UnityEngine;

// Turns hand-drawn text grids into crisp point-filtered sprites.
// Rows are listed TOP to BOTTOM. '.' (or ' ') = transparent.
public static class PixelArt
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string key, string[] rows, IDictionary<char, Color> palette, int ppu = 16)
    {
        Sprite sp;
        if (cache.TryGetValue(key, out sp) && sp != null) return sp;

        int h = rows.Length, w = 0;
        for (int i = 0; i < h; i++) w = Mathf.Max(w, rows[i].Length);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < h; y++)
        {
            string row = rows[h - 1 - y];           // flip: list is top-first
            for (int x = 0; x < w; x++)
            {
                char c = x < row.Length ? row[x] : '.';
                Color col;
                if (c == '.' || c == ' ') col = Color.clear;
                else if (!palette.TryGetValue(c, out col)) col = Color.magenta;
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;

        sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        cache[key] = sp;
        return sp;
    }

    // ---------------------------------------------------------- composing
    public struct Stamp
    {
        public string[] rows;
        public int x, y;
        public Stamp(string[] rows, int x, int y) { this.rows = rows; this.x = x; this.y = y; }
    }

    public static Stamp At(string[] rows, int x, int y) { return new Stamp(rows, x, y); }

    // Stamps are painted onto a shared grid; '.' pixels are see-through so
    // layers stack. Out-of-bounds stamps are clipped.
    public static string[] Compose(int w, int h, params Stamp[] stamps)
    {
        var g = new char[h][];
        for (int y = 0; y < h; y++)
        {
            g[y] = new char[w];
            for (int x = 0; x < w; x++) g[y][x] = '.';
        }

        foreach (var s in stamps)
            for (int y = 0; y < s.rows.Length; y++)
            {
                int gy = s.y + y;
                if (gy < 0 || gy >= h) continue;
                string row = s.rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    int gx = s.x + x;
                    if (gx < 0 || gx >= w) continue;
                    if (row[x] != '.') g[gy][gx] = row[x];
                }
            }

        var outRows = new string[h];
        for (int y = 0; y < h; y++) outRows[y] = new string(g[y]);
        return outRows;
    }

    public static string[] MirrorV(string[] rows)
    {
        var r = new string[rows.Length];
        for (int y = 0; y < rows.Length; y++) r[rows.Length - 1 - y] = rows[y];
        return r;
    }

    // left/right mirror (rows are strings, so it is rebuilt per row)
    public static string[] MirrorH(string[] rows)
    {
        var r = new string[rows.Length];
        for (int y = 0; y < rows.Length; y++)
        {
            var chars = rows[y].ToCharArray();
            System.Array.Reverse(chars);
            r[y] = new string(chars);
        }
        return r;
    }
}

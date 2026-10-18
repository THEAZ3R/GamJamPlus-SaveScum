using System.Collections.Generic;
using UnityEngine;

public class FlowField
{
    public const float Cell = 0.5f;

    readonly int w, h;
    readonly Vector2 origin;
    readonly bool[] blocked;
    readonly bool[] inQ;
    readonly float[] dist;
    readonly Queue<int> queue = new Queue<int>();

    static readonly int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
    static readonly int[] dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

    public FlowField(Vector2 half, List<Rect> obstacles, float inflate)
    {
        w = Mathf.CeilToInt(half.x * 2f / Cell);
        h = Mathf.CeilToInt(half.y * 2f / Cell);
        origin = -half;
        blocked = new bool[w * h];
        inQ = new bool[w * h];
        dist = new float[w * h];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 c = origin + new Vector2((x + 0.5f) * Cell, (y + 0.5f) * Cell);
                bool b = Mathf.Abs(c.x) > half.x - inflate || Mathf.Abs(c.y) > half.y - inflate;
                if (!b)
                    foreach (var r in obstacles)
                        if (c.x > r.xMin - inflate && c.x < r.xMax + inflate &&
                            c.y > r.yMin - inflate && c.y < r.yMax + inflate) { b = true; break; }
                blocked[y * w + x] = b;
            }
    }

    bool ToCell(Vector2 p, out int x, out int y)
    {
        x = Mathf.FloorToInt((p.x - origin.x) / Cell);
        y = Mathf.FloorToInt((p.y - origin.y) / Cell);
        return x >= 0 && y >= 0 && x < w && y < h;
    }

    Vector2 CellCenter(int x, int y) => origin + new Vector2((x + 0.5f) * Cell, (y + 0.5f) * Cell);

    public bool IsBlockedAt(Vector2 p)
    {
        int x, y;
        if (!ToCell(p, out x, out y)) return true;
        return blocked[y * w + x];
    }

    public bool Clear(Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return true;
        d /= len;
        for (float t = 0.6f; t < len - 0.3f; t += 0.25f)
            if (IsBlockedAt(a + d * t)) return false;
        return true;
    }

    public void Compute(Vector2 target)
    {
        for (int i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; inQ[i] = false; }

        int tx, ty;
        ToCell(target, out tx, out ty);
        tx = Mathf.Clamp(tx, 0, w - 1);
        ty = Mathf.Clamp(ty, 0, h - 1);
        int s = ty * w + tx;
        dist[s] = 0f;
        queue.Clear();
        queue.Enqueue(s);
        inQ[s] = true;

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            inQ[i] = false;
            int x = i % w, y = i / w;
            for (int k = 0; k < 8; k++)
            {
                int nx = x + dx[k], ny = y + dy[k];
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int ni = ny * w + nx;
                if (blocked[ni]) continue;
                bool diag = k >= 4;
                if (diag && (blocked[y * w + nx] || blocked[ny * w + x])) continue;
                float nd = dist[i] + (diag ? 1.414f : 1f);
                if (nd < dist[ni])
                {
                    dist[ni] = nd;
                    if (!inQ[ni]) { queue.Enqueue(ni); inQ[ni] = true; }
                }
            }
        }
    }

    public bool Dir(Vector2 pos, out Vector2 dir)
    {
        dir = Vector2.zero;
        int x, y;
        if (!ToCell(pos, out x, out y)) return false;

        float bd = blocked[y * w + x] ? float.MaxValue : dist[y * w + x];
        int best = -1;
        for (int k = 0; k < 8; k++)
        {
            int nx = x + dx[k], ny = y + dy[k];
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            int ni = ny * w + nx;
            if (blocked[ni]) continue;
            if (k >= 4 && (blocked[y * w + nx] || blocked[ny * w + x])) continue;
            if (dist[ni] < bd) { bd = dist[ni]; best = ni; }
        }
        if (best < 0) return false;
        dir = (CellCenter(best % w, best / w) - pos).normalized;
        return true;
    }
}

public static class Nav
{
    static readonly List<Rect> rects = new List<Rect>();
    static readonly List<Rect> sight = new List<Rect>();
    public static FlowField Small, Large;
    static float timer;

    public static void Reset() { rects.Clear(); sight.Clear(); Small = null; Large = null; timer = 0f; }

    public static void AddObstacle(Vector2 center, Vector2 size)
    {
        rects.Add(new Rect(center - size / 2f, size));
    }

    // Blockers that stop bullets/vision (walls) but not holes.
    public static void AddSight(Vector2 center, Vector2 size)
    {
        sight.Add(new Rect(center - size / 2f, size));
    }

    // the Smasher smashes walls out of the world: the grid has to forget them
    public static void RemoveObstacle(Vector2 center, Vector2 size)
    {
        Remove(rects, new Rect(center - size / 2f, size));
    }

    public static void RemoveSight(Vector2 center, Vector2 size)
    {
        Remove(sight, new Rect(center - size / 2f, size));
    }

    static void Remove(List<Rect> list, Rect r)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var o = list[i];
            if (Mathf.Abs(o.x - r.x) < 0.01f && Mathf.Abs(o.y - r.y) < 0.01f &&
                Mathf.Abs(o.width - r.width) < 0.01f && Mathf.Abs(o.height - r.height) < 0.01f)
            { list.RemoveAt(i); return; }
        }
    }

    // Line of sight blocked by walls only: bullets fly over holes.
    public static bool SightClear(Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return true;
        d /= len;
        for (float t = 0.4f; t < len - 0.3f; t += 0.3f)
        {
            Vector2 p = a + d * t;
            for (int i = 0; i < sight.Count; i++)
                if (sight[i].Contains(p)) return false;
        }
        return true;
    }

    public static void Bake(Vector2 half)
    {
        Small = new FlowField(half, rects, 0.42f);
        Large = new FlowField(half, rects, 0.95f);
    }

    public static void Tick(float dt, Vector2 target)
    {
        if (Small == null) return;
        timer -= dt;
        if (timer > 0f) return;
        timer = 0.15f;
        Small.Compute(target);
        Large.Compute(target);
    }

    public static bool IsFree(Vector2 p, bool large)
    {
        var f = large ? Large : Small;
        return f != null && !f.IsBlockedAt(p);
    }

    public static Vector2 Steer(Vector2 pos, Vector2 target, bool large)
    {
        Vector2 direct = (target - pos).normalized;
        var f = large ? Large : Small;
        if (f == null || f.Clear(pos, target)) return direct;
        Vector2 d;
        return f.Dir(pos, out d) ? d : direct;
    }
}
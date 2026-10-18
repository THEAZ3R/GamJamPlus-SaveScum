using System.Collections.Generic;
using UnityEngine;

// Hole zones: you cannot walk into them, but a dash carries you across.
// They are registered so Nav can block enemies and the player can be
// stopped (or nudged out) at the rim.
public static class Pit
{
    static readonly List<Rect> Zones = new List<Rect>();

    public static void Reset() { Zones.Clear(); }

    public static void Add(Vector2 center, Vector2 size)
    {
        Zones.Add(new Rect(center - size / 2f, size));
    }

    public static bool Inside(Vector2 p)
    {
        for (int i = 0; i < Zones.Count; i++)
            if (Zones[i].Contains(p)) return true;
        return false;
    }

    // Would a step from 'from' toward 'to' walk into a hole? Used to slide
    // along rims instead of getting stuck.
    public static bool Blocks(Vector2 from, Vector2 to)
    {
        for (int i = 0; i < Zones.Count; i++)
        {
            var z = Zones[i];
            if (z.Contains(to))
            {
                // stepping out of the hole from inside is always allowed
                if (!z.Contains(from)) return true;
            }
        }
        return false;
    }

    // Safety net: if a dash ends while still over a hole, pop to the rim.
    public static Vector2 ClampOutOf(Vector2 p)
    {
        for (int i = 0; i < Zones.Count; i++)
        {
            var z = Zones[i];
            if (!z.Contains(p)) continue;

            float dxL = p.x - z.xMin, dxR = z.xMax - p.x;
            float dyB = p.y - z.yMin, dyT = z.yMax - p.y;
            float m = Mathf.Min(dxL, Mathf.Min(dxR, Mathf.Min(dyB, dyT)));

            if (m == dxL) p.x = z.xMin - 0.05f;
            else if (m == dxR) p.x = z.xMax + 0.05f;
            else if (m == dyB) p.y = z.yMin - 0.05f;
            else p.y = z.yMax + 0.05f;
        }
        return p;
    }
}

using System.Collections.Generic;
using UnityEngine;

// Sonic-style booster pad: standing on it shoves you along its direction.
public class Booster : MonoBehaviour
{
    public static readonly List<Booster> All = new List<Booster>();

    public Vector2 Dir { get; private set; }
    public const float BoostSpeed = 15f;

    Vector2 size;
    float phase;
    Transform[] chevrons;

    public static Booster Create(Transform parent, Vector2 pos, Vector2 size, Vector2 dir)
    {
        var g = new GameObject("Booster");
        g.transform.SetParent(parent, false);
        g.transform.position = pos;

        var b = g.AddComponent<Booster>();
        b.size = size;
        b.Dir = dir.normalized;

        bool horiz = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);

        // pad plate
        Art.Part(g.transform, "Pad", SpriteFactory.Square, Vector2.zero, size, new Color(0.05f, 0.07f, 0.10f), -7);
        Art.Part(g.transform, "PadEdge", SpriteFactory.Square, Vector2.zero,
                 size - new Vector2(0.12f, 0.12f), new Color(0.10f, 0.16f, 0.22f), -6);

        // sliding chevrons
        int n = 3;
        b.chevrons = new Transform[n];
        float span = (horiz ? size.x : size.y) - 0.55f;
        for (int i = 0; i < n; i++)
        {
            var sr = Art.Part(g.transform, "Chev", Art.Chevron(), Vector2.zero,
                              new Vector2(0.40f, 0.32f), new Color(0.25f, 0.95f, 1f, 0.9f), -5);
            if (!horiz) sr.transform.localRotation = Quaternion.Euler(0, 0, dir.y > 0f ? 90f : -90f);
            b.chevrons[i] = sr.transform;
        }

        var col = g.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = size;

        All.Add(b);
        return b;
    }

    void OnDisable() { All.Remove(this); }

    public static bool PushAt(Vector2 pos, out Vector2 dir)
    {
        for (int i = 0; i < All.Count; i++)
        {
            var b = All[i];
            if (Mathf.Abs(pos.x - b.transform.position.x) <= b.size.x * 0.5f &&
                Mathf.Abs(pos.y - b.transform.position.y) <= b.size.y * 0.5f)
            {
                dir = b.Dir;
                return true;
            }
        }
        dir = Vector2.zero;
        return false;
    }

    void Update()
    {
        phase += Time.deltaTime * 2.6f;
        bool horiz = Mathf.Abs(Dir.x) > Mathf.Abs(Dir.y);
        float len = (horiz ? size.x : size.y) - 0.7f;
        if (len < 0.3f) len = 0.3f;

        for (int i = 0; i < chevrons.Length; i++)
        {
            float t = Mathf.Repeat(phase + i * (1f / chevrons.Length), 1f) - 0.5f;
            float off = t * len;
            Vector2 p = horiz ? new Vector2(off * Dir.x, 0f) : new Vector2(0f, off * Dir.y);
            chevrons[i].localPosition = p;

            float a = 1f - Mathf.Abs(t) * 1.3f;
            var sr = chevrons[i].GetComponent<SpriteRenderer>();
            var c = sr.color;
            c.a = Mathf.Clamp01(a);
            sr.color = c;
        }
    }
}

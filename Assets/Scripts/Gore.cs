using System.Collections.Generic;
using UnityEngine;

// Fake gore: blood spray on hits, corpses that visibly burst into recognisable
// body parts (head, arm, leg, torso, hand), a blood mist, and bloodstains that
// stay soaked into the floor for the rest of the run (capped, oldest recycled).
//
// Everything is tinted per unit, so a spider sprays black ichor, the mechs
// drip black oil and the player sprays red out of a white suit.
//
// Sizes are in world units (a soldier is ~1.2 units tall), so a part is a big
// readable limb, a fleck is a small drop - not the other way round.
public static class Gore
{
    const int MaxStains = 400;
    static readonly Queue<GameObject> stains = new Queue<GameObject>();

    // BLOODTINGE.EXE: when installed, blood stops living on the floor - every
    // stain dries up and lifts off after a few seconds
    public static bool FadingBlood;

    public static void Reset()
    {
        FadingBlood = false;
        while (stains.Count > 0)
        {
            var g = stains.Dequeue();
            if (g != null) Object.Destroy(g);
        }
    }

    // ---------------------------------------------------------- on a hit
    // spray of drops along `dir` (away from whatever made the hole)
    public static void Hit(Vector2 pos, Vector2 dir, Color blood, float power)
    {
        if (dir.sqrMagnitude < 0.001f) dir = Random.insideUnitCircle.normalized;
        dir.Normalize();

        float p = Mathf.Clamp(power / 25f, 0.3f, 1.7f);

        int drops = 5 + Mathf.RoundToInt(Random.Range(3f, 8f) * p);
        for (int i = 0; i < drops; i++)
            Drop(pos, dir, blood, Random.Range(0.22f, 0.42f) * (1f + p * 0.5f),
                 Random.Range(2.5f, 6.5f) * (0.7f + p));

        // torn-off scraps, and an immediate smear where the bullet went in
        if (Random.value < 0.45f + 0.35f * p)
            Chunk(pos, dir, blood, blood, Random.Range(0.32f, 0.52f), Random.Range(2f, 4.5f), 4f);
        if (Random.value < 0.5f)
            Stain(pos, blood, Random.Range(0.35f, 0.65f) * (0.8f + p * 0.4f));
    }

    // ---------------------------------------------------------- on a death
    // the corpse comes apart: recognisable limbs tumble out, flecks and mist
    // fly everywhere, and the floor ends up soaked in a pool of blood
    public static void Burst(Vector2 pos, Color blood, Color flesh, int pieces, float power)
    {
        float pw = Mathf.Sqrt(Mathf.Max(power, 0.6f));

        // 1) the floor soaks: a pool under the corpse, spray around it
        Stain(pos, blood, Random.Range(0.8f, 1.25f) * Mathf.Min(power, 1.5f));
        int pools = 3 + Random.Range(0, 4);
        for (int i = 0; i < pools; i++)
            Stain(pos + Random.insideUnitCircle * Random.Range(0.4f, 1.5f), blood,
                  Random.Range(0.45f, 1.0f));

        // 2) a hanging mist of blood
        Mist(pos, blood, Random.Range(0.7f, 1.1f));
        if (Random.value < 0.5f) Mist(pos, blood, Random.Range(0.5f, 0.8f));

        // 3) recognisable body parts: head / arm / leg / torso / hand
        int parts = Mathf.Clamp(pieces / 2, 3, 6);
        for (int i = 0; i < parts; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            // guarantee a head fairly often - it is the part you look for
            int kind = (i == 0 && Random.value < 0.55f) ? RollPart(true) : RollPart(false);
            Part(pos, dir, blood, flesh, kind, Random.Range(2.6f, 6.5f) * pw);
        }

        // 4) flecks + smaller chunks
        int drops = 12 + Mathf.RoundToInt(Random.Range(4f, 9f) * power);
        for (int i = 0; i < drops; i++)
            Drop(pos, Random.insideUnitCircle.normalized, blood,
                 Random.Range(0.26f, 0.5f) * pw, Random.Range(2f, 6f) * power);

        for (int i = 0; i < pieces; i++)
            Chunk(pos, Random.insideUnitCircle.normalized, blood, flesh,
                  Random.Range(0.34f, 0.55f) * pw, Random.Range(3f, 7f) * power, 6f);
    }

    static int RollPart(bool forceHead)
    {
        if (forceHead && Random.value < 0.75f) return 0;
        float r = Random.value;                 // 0 head, 1 arm, 2 leg, 3 torso, 4 hand
        return r < 0.18f ? 0 : r < 0.45f ? 1 : r < 0.72f ? 2 : r < 0.88f ? 3 : 4;
    }

    static readonly float[] PartSize = { 0.50f, 0.52f, 0.48f, 0.56f, 0.46f };

    // ---------------------------------------------------------- internals
    static void Drop(Vector2 pos, Vector2 dir, Color blood, float size, float speed)
    {
        var g = new GameObject("BloodDrop");
        g.transform.position = pos;
        g.transform.localScale = Vector3.one * size;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.BloodDot();
        sr.color = blood;
        sr.sortingOrder = 15;

        g.AddComponent<GoreDrop>().Init(Random.insideUnitCircle.normalized * speed * 0.45f + dir * speed,
                                        blood, size);
    }

    static void Chunk(Vector2 pos, Vector2 dir, Color blood, Color flesh, float size, float speed, float life)
    {
        var g = new GameObject("Gib");
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        g.transform.localScale = Vector3.one * size;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.Gib(Random.Range(0, 4));
        sr.color = Color.Lerp(flesh, blood, Random.Range(0.15f, 0.55f));
        sr.sortingOrder = 16;

        g.AddComponent<GoreChunk>().Init(Random.insideUnitCircle.normalized * speed * 0.5f + dir * speed,
                                         blood, life);
    }

    // a torn-off limb that spins away trailing blood
    static void Part(Vector2 pos, Vector2 dir, Color blood, Color flesh, int kind, float speed)
    {
        var g = new GameObject(kind == 0 ? "SeveredHead" : "BodyPart");
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        g.transform.localScale = Vector3.one * PartSize[kind];

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.BodyPart(kind);
        sr.color = Color.Lerp(flesh, blood, Random.Range(0.1f, 0.45f));
        sr.sortingOrder = 16;

        g.AddComponent<GoreChunk>().Init(Random.insideUnitCircle.normalized * speed * 0.5f + dir * speed,
                                         blood, 6f);
    }

    // a short-lived puff of blood hanging where the body came apart
    static void Mist(Vector2 pos, Color blood, float size)
    {
        var g = new GameObject("BloodMist");
        g.transform.position = (Vector3)pos + (Vector3)(Random.insideUnitCircle * 0.2f);
        g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        g.transform.localScale = Vector3.one * size;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.BloodSplat(Random.Range(0, 4));
        sr.color = blood;
        sr.sortingOrder = 17;

        g.AddComponent<GoreChunk>().Init(Random.insideUnitCircle * 0.9f, blood, 0.45f, 0.55f);
    }

    // a soaked-in splat: this is the part that stays on the floor for the run
    public static void Stain(Vector2 pos, Color blood, float size)
    {
        var g = new GameObject("Bloodstain");
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        g.transform.localScale = Vector3.one * size;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.BloodSplat(Random.Range(0, 4));
        // darkened toward black instead of multiplied, so black oil stays black
        sr.color = Color.Lerp(blood, Color.black, 0.4f) * new Color(1f, 1f, 1f, 0.92f);
        sr.sortingOrder = -17;          // on the floor: above tiles, below units and pits

        if (FadingBlood) g.AddComponent<GoreFade>().Init(Random.Range(7f, 11f));

        stains.Enqueue(g);
        while (stains.Count > MaxStains)
        {
            var old = stains.Dequeue();
            if (old != null) Object.Destroy(old);
        }
    }
}

using UnityEngine;

public class Fx : MonoBehaviour
{
    Vector2 vel;
    float life, maxLife;
    SpriteRenderer sr;
    Color baseColor;
    float baseScale, aspect = 1f;

    public static void Burst(Vector2 pos, Color color, int count = 10, float speed = 6f)
    {
        for (int i = 0; i < count; i++)
        {
            var g = new GameObject("Shard");
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            float size = Random.Range(0.08f, 0.2f);
            g.transform.localScale = Vector3.one * size;

            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square;
            sr.color = color;
            sr.sortingOrder = 40;

            var fx = g.AddComponent<Fx>();
            fx.vel = Random.insideUnitCircle.normalized * Random.Range(speed * 0.4f, speed);
            fx.maxLife = fx.life = Random.Range(0.25f, 0.55f);
            fx.sr = sr;
            fx.baseColor = color;
            fx.baseScale = size;
        }
    }

    // thin fast streaks flying straight: sparks, muzzle sparks, death sparks
    public static void Sparks(Vector2 pos, Color color, int count = 14, float speed = 14f)
    {
        for (int i = 0; i < count; i++)
        {
            var g = new GameObject("Spark");
            g.transform.position = pos;
            float size = Random.Range(0.18f, 0.38f);
            g.transform.localScale = new Vector3(size * 0.22f, size, 1f);

            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square;
            sr.color = color;
            sr.sortingOrder = 41;

            var fx = g.AddComponent<Fx>();
            fx.vel = Random.insideUnitCircle.normalized * Random.Range(speed * 0.5f, speed);
            fx.maxLife = fx.life = Random.Range(0.16f, 0.38f);
            fx.sr = sr;
            fx.baseColor = color;
            fx.baseScale = size;
            fx.aspect = 0.22f;
            // local Y is the long axis: point it down the flight path
            g.transform.rotation = Quaternion.Euler(0, 0,
                Mathf.Atan2(fx.vel.y, fx.vel.x) * Mathf.Rad2Deg - 90f);
        }
    }

    // expanding shockwave ring
    public static void Ring(Vector2 pos, Color color, float fromSize, float toSize,
                             float duration = 0.35f, int order = 41)
    {
        var g = new GameObject("Shockwave");
        g.transform.position = pos;
        g.transform.localScale = Vector3.one * fromSize;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Art.FxShock();
        sr.color = color;
        sr.sortingOrder = order;

        g.AddComponent<FxGrow>().Init(color, duration, fromSize, toSize);
    }

    // soft expanding flash: the hot core of an explosion or a muzzle flash
    public static void Flash(Vector2 pos, Color color, float size, float duration = 0.22f, int order = 42)
    {
        var g = new GameObject("Flash");
        g.transform.position = pos;
        g.transform.localScale = Vector3.one * size * 0.4f;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = color;
        sr.sortingOrder = order;

        g.AddComponent<FxGrow>().Init(color, duration, size * 0.4f, size);
    }

    // a static sprite that simply fades out in place: lightning links, lock
    // reticles, anything that should "pop" without flying anywhere
    public static void Flicker(Vector2 pos, Sprite sprite, Color color, float size,
                               float duration = 0.16f, float rot = 0f, int order = 41)
    {
        var g = new GameObject("Flick");
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, 0, rot);
        g.transform.localScale = Vector3.one * size;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;

        g.AddComponent<FxGrow>().Init(color, duration, size, size * 0.6f);
    }

    // a full pixel explosion: white-hot core, shockwave, shard sprays, sparks
    public static void Explosion(Vector2 pos, Color core, Color hot, float scale = 1f)
    {
        Flash(pos, Color.Lerp(hot, Color.white, 0.65f), 2.2f * scale, 0.24f);
        Ring(pos, hot, 0.4f * scale, 3.4f * scale, 0.4f);
        Burst(pos, core, Mathf.RoundToInt(22 * scale), 11f * scale);
        Burst(pos, hot, Mathf.RoundToInt(14 * scale), 7f * scale);
        Sparks(pos, Color.Lerp(hot, Color.white, 0.7f), Mathf.RoundToInt(14 * scale), 15f * scale);
    }

    // 225: the cut that finishes a body - white-hot core, violet ring, and a
    // spray of thin slash sparks thrown out of the corpse
    public static void Executed(Vector2 pos, Color accent)
    {
        Flash(pos, Color.Lerp(accent, Color.white, 0.75f), 1.7f, 0.18f);
        Ring(pos, accent, 0.3f, 2.6f, 0.28f, 41);
        Ring(pos, Color.white, 0.15f, 1.2f, 0.16f, 42);
        Sparks(pos, Color.Lerp(accent, Color.white, 0.5f), 18, 16f);
        Burst(pos, accent, 14, 9f);
    }

    void Update()
    {
        life -= Time.unscaledDeltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        float t = life / maxLife;
        transform.position += (Vector3)(vel * Time.deltaTime);
        vel *= 1f - 4f * Time.deltaTime;
        transform.localScale = new Vector3(baseScale * aspect, baseScale, 1f) * t;
        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, t);
    }
}
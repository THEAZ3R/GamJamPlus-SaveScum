using UnityEngine;

// An expanding shockwave ring or fireball flash: scales up and fades out.
// Used by explosions, trap triggers and big deaths.
public class FxGrow : MonoBehaviour
{
    float life, maxLife, from, to;
    SpriteRenderer sr;
    Color color;

    public void Init(Color c, float duration, float fromScale, float toScale)
    {
        color = c;
        maxLife = life = duration;
        from = fromScale;
        to = toScale;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        life -= Time.unscaledDeltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        float t = 1f - life / maxLife;                 // 0 = just spawned
        transform.localScale = Vector3.one * Mathf.Lerp(from, to, t);
        float a = (1f - t) * (1f - t);                 // quick fade as it grows
        if (sr != null) sr.color = new Color(color.r, color.g, color.b, color.a * a);
    }
}

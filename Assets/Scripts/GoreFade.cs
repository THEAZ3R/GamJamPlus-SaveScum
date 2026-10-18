using UnityEngine;

// A bloodstain under BLOODTINGE.EXE: it stays soaked into the floor for a
// few seconds, then dries up and lifts off instead of living there forever.
public class GoreFade : MonoBehaviour
{
    float life, maxLife;
    SpriteRenderer sr;
    Color col;

    public void Init(float seconds)
    {
        life = maxLife = seconds;
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) col = sr.color;
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        float hold = maxLife * 0.55f;               // soak, then dry out
        if (life < hold && sr != null)
            sr.color = new Color(col.r, col.g, col.b, col.a * (life / hold));
    }
}

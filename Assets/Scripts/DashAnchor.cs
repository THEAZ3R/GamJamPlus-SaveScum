using UnityEngine;

// CHRONO ANCHOR: a spot of frozen time left where a dash started. Shoot it
// and you blink back to it; higher levels rupture whatever is standing there.
public class DashAnchor : MonoBehaviour
{
    public static DashAnchor Active;

    float life, maxLife, burst, burstRadius;
    SpriteRenderer ring, core;

    public static void Create(Vector2 pos, float duration, float rupture, float radius)
    {
        // one portal at a time: a fresh dash collapses the old spot into the new one
        if (Active != null)
        {
            Fx.Burst(Active.transform.position, new Color(0.8f, 0.46f, 1f), 10, 4f);
            Fx.Ring(Active.transform.position, new Color(0.8f, 0.46f, 1f), 0.9f, 0.2f, 0.2f, 13);
            Object.Destroy(Active.gameObject);
            Active = null;
        }

        var g = new GameObject("ChronoSpot");
        g.transform.position = pos;

        var rr = new GameObject("Ring");
        rr.transform.SetParent(g.transform, false);
        rr.transform.localScale = Vector3.one * 1.5f;
        var rsr = rr.AddComponent<SpriteRenderer>();
        rsr.sprite = Art.FxShock();
        rsr.color = new Color(0.8f, 0.46f, 1f, 0.8f);
        rsr.sortingOrder = 13;                          // on the floor, under the units

        var cc = new GameObject("Core");
        cc.transform.SetParent(g.transform, false);
        cc.transform.localScale = Vector3.one * 0.34f;
        var csr = cc.AddComponent<SpriteRenderer>();
        csr.sprite = SpriteFactory.Square;
        csr.color = new Color(0.95f, 0.75f, 1f, 0.95f);
        csr.sortingOrder = 14;

        var col = g.AddComponent<CircleCollider2D>();   // triggers on bullets
        col.isTrigger = true;
        col.radius = 0.55f;

        var a = g.AddComponent<DashAnchor>();
        a.ring = rsr;
        a.core = csr;
        a.maxLife = a.life = duration;
        a.burst = rupture;
        a.burstRadius = radius;
        Active = a;

        Fx.Ring(pos, new Color(0.8f, 0.46f, 1f), 0.3f, 2f, 0.35f, 13);
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { if (Active == this) Active = null; Object.Destroy(gameObject); return; }

        transform.Rotate(0, 0, 90f * Time.deltaTime);

        float t = life / maxLife;
        ring.color = new Color(0.8f, 0.46f, 1f,
                               Mathf.Lerp(0.3f, 0.85f, t) * (Mathf.Sin(Time.time * 8f) * 0.2f + 0.8f));
        core.transform.localScale = Vector3.one * (0.34f * (1f + Mathf.Sin(Time.time * 10f) * 0.15f));
        if (t < 0.3f) core.enabled = Mathf.Sin(Time.time * 20f) > 0f;   // blink out
    }

    // called by the bullet that collected the spot
    public void Teleport()
    {
        var p = PlayerController.Instance;
        Vector2 pos = transform.position;

        if (p != null)
        {
            Fx.Burst(p.transform.position, new Color(0.8f, 0.46f, 1f), 16, 6f);
            p.TeleportTo(pos);
            Fx.Ring(pos, new Color(0.8f, 0.46f, 1f), 0.4f, 2.6f, 0.4f, 29);

            if (burst > 0f)
            {
                Fx.Explosion(pos, new Color(0.8f, 0.46f, 1f), Color.white, burstRadius * 0.39f);
                GameManager.Shake(Mathf.Min(0.6f, burstRadius * 0.16f));
                var hits = Physics2D.OverlapCircleAll(pos, burstRadius);
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].isTrigger || hits[i].CompareTag("Player")) continue;
                    var hp = hits[i].GetComponent<Health>();
                    if (hp != null)
                        hp.TakeDamage(burst, ((Vector2)hits[i].transform.position - pos).normalized);
                }
            }
        }

        if (Active == this) Active = null;
        Object.Destroy(gameObject);
    }
}

using UnityEngine;

// ORBITAL SPECTER: a targeting ring blinks onto a random enemy for half a
// second, then a beam lances down from orbit and erases what it was aimed at.
public class OrbitalBeam : MonoBehaviour
{
    const float Charge = 0.65f, Flash = 0.30f;

    float charge = Charge, flash = -1f, dmg, radius;
    Vector2 lastPos;
    Transform target;
    SpriteRenderer reticle, beam, core;

    public static void Lock(Vector2 pos, Transform target, float damage)
    {
        var g = new GameObject("OrbitalLock");
        g.transform.position = pos;

        var r = new GameObject("Reticle");
        r.transform.SetParent(g.transform, false);
        r.transform.localScale = Vector3.one * 2.2f;
        var rsr = r.AddComponent<SpriteRenderer>();
        rsr.sprite = Art.FxShock();
        rsr.color = new Color(1f, 0.42f, 0.62f, 0.85f);
        rsr.sortingOrder = 29;

        var b = new GameObject("Beam");
        b.transform.SetParent(g.transform, false);
        b.transform.localPosition = new Vector3(0f, 10f, 0f);
        b.transform.localScale = new Vector3(0.9f, 20f, 1f);
        var bsr = b.AddComponent<SpriteRenderer>();
        bsr.sprite = SpriteFactory.Square;
        bsr.color = new Color(1f, 0.42f, 0.62f, 0.55f);
        bsr.sortingOrder = 50;                          // over everything
        bsr.enabled = false;

        var c = new GameObject("Core");
        c.transform.SetParent(g.transform, false);
        c.transform.localPosition = new Vector3(0f, 10f, 0f);
        c.transform.localScale = new Vector3(0.34f, 20f, 1f);
        var csr = c.AddComponent<SpriteRenderer>();
        csr.sprite = SpriteFactory.Square;
        csr.color = new Color(1f, 0.95f, 1f, 0.95f);
        csr.sortingOrder = 51;
        csr.enabled = false;

        var o = g.AddComponent<OrbitalBeam>();
        o.reticle = rsr;
        o.beam = bsr;
        o.core = csr;
        o.target = target;
        o.lastPos = pos;
        o.dmg = damage;
        o.radius = 1.9f;

        Fx.Ring(pos, new Color(1f, 0.42f, 0.62f), 0.3f, 2.4f, 0.4f, 29);
        Sfx.PlayAt(pos, "orbital", 1f);              // charge whine, then heaven answers
    }

    void Update()
    {
        Vector2 pos = transform.position;

        if (charge > 0f)                                    // ---- painting the target
        {
            charge -= Time.deltaTime;
            if (target != null)                             // it walks, the lock follows
            {
                lastPos = target.position;
                transform.position = lastPos;
            }
            reticle.transform.Rotate(0, 0, -300f * Time.deltaTime);
            reticle.color = new Color(1f, 0.42f, 0.62f, 0.55f + 0.4f * Mathf.Sin(Time.time * 22f));
            if (charge <= 0f) Fire(pos);
            return;
        }

        // ---- the beam: full white-hot for a beat, then it fades out
        flash -= Time.deltaTime;
        float a = Mathf.Clamp01(flash / Flash);
        beam.color = new Color(1f, 0.42f, 0.62f, 0.55f * a);
        core.color = new Color(1f, 0.95f, 1f, 0.95f * a);
        beam.transform.localScale = new Vector3(0.9f * a, 20f, 1f);
        core.transform.localScale = new Vector3(0.34f * a, 20f, 1f);
        if (flash <= 0f) Destroy(gameObject);
    }

    void Fire(Vector2 pos)
    {
        reticle.enabled = false;
        beam.enabled = true;
        core.enabled = true;
        flash = Flash;

        Fx.Explosion(pos, new Color(1f, 0.42f, 0.62f), Color.white, 1.15f);
        Fx.Ring(pos, new Color(1f, 0.42f, 0.62f), 0.5f, 4.6f, 0.45f, 45);
        Gore.Stain(pos, new Color(0.05f, 0.045f, 0.05f), 1.5f);      // scorch
        GameManager.Shake(0.55f);

        var hits = Physics2D.OverlapCircleAll(pos, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].isTrigger || hits[i].CompareTag("Player")) continue;
            var hp = hits[i].GetComponent<Health>();
            if (hp != null)
                hp.TakeDamage(dmg, ((Vector2)hits[i].transform.position - pos).normalized);
        }
    }
}

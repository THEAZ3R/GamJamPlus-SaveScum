using System.Collections.Generic;
using UnityEngine;

// A splash of solvent a SOLVENT WAKE bullet leaves behind: it sits on the
// floor and burns whatever enemy stands in it. Capped so a long fight cannot
// litter the arena (oldest pool recycled first).
public class CorrosivePool : MonoBehaviour
{
    const int MaxPools = 30;
    static readonly List<CorrosivePool> pools = new List<CorrosivePool>();

    float life = 1.8f, dps, tick, baseScale;
    SpriteRenderer sr;

    public static void Spawn(Vector2 pos, float damagePerSecond)
    {
        Drop(pos, damagePerSecond, 1.8f, 1f, true);
    }

    // THE INCREDIBLE GASSY: a bead of the wake the player drags around -
    // smaller and shorter-lived than a splash, and quiet
    public static void Wake(Vector2 pos, float damagePerSecond)
    {
        Drop(pos, damagePerSecond, 1.15f, 0.62f, false);
    }

    static void Drop(Vector2 pos, float damagePerSecond, float lifeTime, float sizeMul, bool loud)
    {
        for (int i = pools.Count - 1; i >= 0; i--)           // scene reloads leave nulls
            if (pools[i] == null) pools.RemoveAt(i);

        while (pools.Count >= MaxPools)
        {
            var old = pools[0];
            pools.RemoveAt(0);
            if (old != null) Object.Destroy(old.gameObject);
        }

        var g = new GameObject("AcidPool");
        g.transform.position = (Vector3)pos + (Vector3)(Random.insideUnitCircle * 0.18f * sizeMul);
        float s = Random.Range(0.95f, 1.3f) * sizeMul;
        g.transform.localScale = Vector3.one * s;

        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Circle;
        sr.color = new Color(0.45f, 1f, 0.25f, 0.42f);
        sr.sortingOrder = 12;                               // over the floor, under the units

        var p = g.AddComponent<CorrosivePool>();
        p.sr = sr;
        p.dps = damagePerSecond;
        p.baseScale = s;
        p.life = lifeTime;
        pools.Add(p);

        if (loud) Fx.Burst(pos, new Color(0.5f, 1f, 0.3f), 4, 2.5f);
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { pools.Remove(this); Object.Destroy(gameObject); return; }

        float w = Mathf.Sin(Time.time * 6f + transform.position.x);
        sr.color = new Color(0.45f, 1f, 0.25f,
                             (0.30f + 0.14f * (0.5f + 0.5f * w)) * Mathf.Clamp01(life * 2f));
        transform.localScale = Vector3.one * (baseScale * (1f + w * 0.05f));

        tick -= Time.deltaTime;
        if (tick <= 0f)
        {
            tick = 0.25f;
            var hits = Physics2D.OverlapCircleAll(transform.position, 0.62f * baseScale);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].isTrigger || hits[i].CompareTag("Player")) continue;
                var hp = hits[i].GetComponent<Health>();
                if (hp == null) continue;
                hp.Burn(dps * 0.25f);
                if (Random.value < 0.3f)
                    Fx.Burst(hits[i].transform.position, new Color(0.5f, 1f, 0.3f), 2, 2f);
            }
        }
    }
}

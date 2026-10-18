using UnityEngine;

// DOGMEAT: the pack circles you in formation. When something gets close they
// lunge out, bite, and fall back into the ring - and now and then one of them
// will dart in front of you and swat an incoming round out of the air.
public class CyberDog : MonoBehaviour
{
    const float BiteRange = 1.2f, BiteCd = 0.6f, LungeCd = 2.2f, Speed = 5.6f;
    const float Sight = 7f, Leash = 6.5f, OrbitRadius = 1.6f, OrbitRate = 28f;   // deg/s
    const float BaseScale = 1.15f;                          // matches Create's Body scale

    static int spawnCount;

    float biteT, lungeCd, swatCd, scanT, orbitPhase, barkT, stepT;
    Vector2 moveDir, lungeVel;
    Health target;                                  // bosses have health bars too
    Rigidbody2D rb;
    Transform visual;

    public static CyberDog Create(Vector2 pos)
    {
        var g = new GameObject("CyberDog");
        g.transform.position = pos;

        // ground shadow, so it reads as a body standing on the floor
        var sh = new GameObject("Shadow");
        sh.transform.SetParent(g.transform, false);
        sh.transform.localPosition = new Vector3(0.08f, -0.52f, 0f);
        sh.transform.localScale = new Vector3(1.35f, 0.68f, 1f);
        var ssr = sh.AddComponent<SpriteRenderer>();
        ssr.sprite = SpriteFactory.Circle;
        ssr.color = new Color(0, 0, 0, 0.42f);
        ssr.sortingOrder = -3;

        // soft cyan halo: makes the pack findable inside a firefight
        var gl = new GameObject("Glow");
        gl.transform.SetParent(g.transform, false);
        gl.transform.localScale = new Vector3(1.95f, 1.35f, 1f);
        var gsr = gl.AddComponent<SpriteRenderer>();
        gsr.sprite = SpriteFactory.Circle;
        gsr.color = new Color(0.30f, 0.95f, 1f, 0.17f);
        gsr.sortingOrder = 23;

        var v = new GameObject("Body");
        v.transform.SetParent(g.transform, false);
        v.transform.localScale = Vector3.one * 1.15f;
        var sr = v.AddComponent<SpriteRenderer>();
        sr.sprite = Art.CyberDog();
        sr.sortingOrder = 24;                                // above the units (20)

        var rb = g.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = g.AddComponent<CircleCollider2D>();
        col.radius = 0.4f;
        var pc = PlayerController.Instance;                // no bumping the master
        if (pc != null) Physics2D.IgnoreCollision(col, pc.GetComponent<CircleCollider2D>());

        var d = g.AddComponent<CyberDog>();
        d.rb = rb;
        d.visual = v.transform;
        d.orbitPhase = (spawnCount++ * 72f) % 360f;         // spread around the ring

        Fx.Burst(pos, new Color(0.35f, 0.9f, 1f), 10, 5f);
        return d;
    }

    void Update()
    {
        var p = PlayerController.Instance;
        if (GameManager.IsOver || p == null) { moveDir = Vector2.zero; return; }

        float dt = Time.deltaTime;
        Vector2 pos = transform.position;
        Vector2 packPos = p.transform.position;

        biteT -= dt;
        lungeCd -= dt;

        // ---- what are we doing this frame: swatting, hunting, or circling?
        Bullet threat = PickRound(packPos, dt);

        target = null;
        if (threat == null)
        {
            float best = Sight * Sight;
            for (int i = 0; i < Enemy.All.Count; i++)
            {
                var e = Enemy.All[i];
                if (e == null || e.Health == null) continue;
                float d2 = ((Vector2)e.transform.position - pos).sqrMagnitude;
                if (d2 < best) { best = d2; target = e.Health; }
            }

            // bosses count too: the pack goes after anything with a health bar
            var sb = Smasher.Active;
            var sh = sb != null ? sb.GetComponent<Health>() : null;
            if (sh != null && !sh.IsDead)
            {
                float d2 = ((Vector2)sb.transform.position - pos).sqrMagnitude;
                if (d2 < best) { best = d2; target = sh; }
            }
            var hb = Herald.Active;
            var hh = hb != null ? hb.GetComponent<Health>() : null;
            if (hh != null && !hh.IsDead)
            {
                float d2 = ((Vector2)hb.transform.position - pos).sqrMagnitude;
                if (d2 < best) { best = d2; target = hh; }
            }

            // leash: the pack never wanders off
            if (target != null && ((Vector2)packPos - pos).sqrMagnitude > Leash * Leash) target = null;
        }

        Vector2 goal = threat != null ? (Vector2)threat.transform.position
                     : target != null ? (Vector2)target.transform.position
                     : OrbitSlot(packPos);

        Vector2 to = goal - pos;
        float dist = to.magnitude;
        Vector2 dir = to / Mathf.Max(dist, 0.01f);
        bool busy = threat != null || target != null;

        if (to.sqrMagnitude > 0.04f)                          // face the way we run
            visual.localScale = new Vector3(to.x < 0f ? -BaseScale : BaseScale, BaseScale, 1f);

        moveDir = busy || dist > 0.45f ? Nav.Steer(pos, goal, false) : Vector2.zero;

        // ---- close the last stretch with a lunge
        if (target != null && lungeCd <= 0f && dist < 4.2f && dist > BiteRange)
        {
            lungeCd = LungeCd;
            lungeVel = dir * 9f;
            Fx.Burst(pos, new Color(0.35f, 0.95f, 1f), 4, 3f);
            Sfx.PlayAt(pos, "bark", 0.7f, Random.Range(0.95f, 1.1f));
        }

        // ---- swat an incoming round out of the air
        if (threat != null && dist < 0.55f)
        {
            Fx.Burst(pos, Color.white, 7, 4f);
            Fx.Sparks(pos, new Color(0.6f, 1f, 1f), 6, 8f);
            Fx.Ring(pos, new Color(0.6f, 1f, 1f), 0.2f, 1.1f, 0.2f, 30);
            if (threat != null) Destroy(threat.gameObject);
            lungeVel = -dir * 2.5f;                           // hop back into the ring
        }

        // ---- bite, then fall back
        if (target != null && dist <= BiteRange && biteT <= 0f)
        {
            biteT = BiteCd;
            var up = PlayerUpgrades.Instance;
            target.TakeDamage(up != null ? up.HoundBite : 10f, dir);
            Fx.Burst(goal, new Color(0.6f, 1f, 1f), 6, 4f);
            Fx.Sparks(goal, Color.white, 4, 7f);
            lungeVel = -dir * 3f;
            Sfx.PlayAt(goal, "bark", 0.65f, 1.18f);
        }

        // the pack keeps talking: an idle bark now and then
        barkT -= dt;
        if (barkT <= 0f)
        {
            barkT = Random.Range(4f, 9f);
            Sfx.PlayAt(pos, "bark", 0.4f, Random.Range(0.9f, 1.05f));
        }

        // mechanical movement: the joints complain in little whirs
        if (moveDir.sqrMagnitude > 0.1f)
        {
            stepT -= dt;
            if (stepT <= 0f)
            {
                stepT = 0.34f;
                Sfx.PlayAt(pos, "servo", 0.4f, Random.Range(0.95f, 1.15f));
            }
        }

        // little running bob
        float hop = Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.08f;
        visual.localPosition = new Vector3(0f, hop, 0f);
        visual.localScale = new Vector3(visual.localScale.x, BaseScale * (1f + hop * 0.6f), 1f);
    }

    // where this dog sits in the ring around the master
    Vector2 OrbitSlot(Vector2 packPos)
    {
        float a = (orbitPhase + Time.time * OrbitRate) * Mathf.Deg2Rad;
        return packPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * OrbitRadius;
    }

    // rare: a round closing on the master gets swatted (scanned a few times a
    // second, then it takes a proper breather before the next attempt)
    Bullet PickRound(Vector2 packPos, float dt)
    {
        if (swatCd > 0f) { swatCd -= dt; return null; }

        scanT -= dt;
        if (scanT > 0f) return null;
        scanT = 0.15f;

        var hits = Physics2D.OverlapCircleAll(packPos, 4.5f);
        Bullet found = null;
        float best = 99f;
        for (int i = 0; i < hits.Length; i++)
        {
            var b = hits[i].GetComponent<Bullet>();
            if (b == null || b.FromPlayer) continue;
            float d2 = ((Vector2)b.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (d2 < best) { best = d2; found = b; }
        }
        if (found != null) swatCd = Random.Range(2.5f, 5.5f); // it only does this now and then
        return found;
    }

    void FixedUpdate()
    {
        if (GameManager.IsOver) { rb.SetVel(Vector2.zero); return; }

        Vector2 v = moveDir * Speed + lungeVel;

        Vector2 p = rb.position;
        Vector2 next = p + v * Time.fixedDeltaTime;
        if (Pit.Blocks(p, next))
        {
            Vector2 nx = new Vector2(next.x, p.y), ny = new Vector2(p.x, next.y);
            if (!Pit.Blocks(p, nx)) v = new Vector2(v.x, 0f);
            else if (!Pit.Blocks(p, ny)) v = new Vector2(0f, v.y);
            else v = Vector2.zero;
        }

        rb.SetVel(v);
        lungeVel = Vector2.MoveTowards(lungeVel, Vector2.zero, 14f * Time.fixedDeltaTime);
    }
}

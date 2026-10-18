using UnityEngine;

// Every unit type gets its own movement signature: a lean-heavy stabber, a
// buzzing dasher, a scuttling spider, a springy bouncer, a stiff artillery
// piece, a hovering grabber... plus a recoil kick for attacks.
public enum AnimStyle { Standard, Hunch, Vibrate, Scuttle, Heavy, Sway, Stiff, Bounce, Hover }

// Squash & bob driven by the owning rigidbody's speed - reads as a walk/run
// cycle for the little pixel bodies without per-limb rigging.
public class HumanoidAnim : MonoBehaviour
{
    public AnimStyle style = AnimStyle.Standard;

    // 2-way facing (pokemon-style): the renderer swaps between these instead
    // of the whole body spinning. Only set for humanoids - mechs and monsters
    // keep a single rotating sprite and never call SetFacing.
    public Sprite faceDown, faceUp;

    // the weapon: a child of the body that rides along and points at the aim
    public Transform gun;
    public SpriteRenderer gunSr;
    public float gunAx, gunAy;

    // body size multiplier (Vitagraft grows you, Celerity shrinks you)
    public float sizeMul = 1f;

    Rigidbody2D rb;
    Transform body;                 // the sprite child: gets the lean/tilt
    SpriteRenderer bodySr;
    Vector3 baseScale;
    float phase, idleT, kick, hopT, hopPower, pop;
    bool captured;

    const float HopDur = 0.45f;     // how long one hop stays in the air

    public void Capture()
    {
        baseScale = transform.localScale;
        body = transform.childCount > 0 ? transform.GetChild(0) : null;
        bodySr = body != null ? body.GetComponent<SpriteRenderer>() : null;
        captured = true;
    }

    // which way the body stands: the face for everything except walking away,
    // mirrored when it moves left. It never turns into a flat profile.
    public void SetFacing(Vector2 dir)
    {
        if (bodySr == null || faceDown == null) return;
        if (dir.sqrMagnitude < 0.0001f) return;

        if (dir.y > Mathf.Abs(dir.x) * 1.1f)                 // clearly walking away
        {
            if (bodySr.sprite != faceUp) bodySr.sprite = faceUp;
            if (bodySr.flipX) bodySr.flipX = false;
        }
        else
        {
            if (bodySr.sprite != faceDown) bodySr.sprite = faceDown;
            bool f = dir.x < 0f;
            if (bodySr.flipX != f) bodySr.flipX = f;
        }
    }

    // the weapon rides an ellipse around the body: clear of the silhouette
    // when you aim across, poking past the head and the feet up and down
    public void SetAim(Vector2 dir)
    {
        if (gun == null || dir.sqrMagnitude < 0.0001f) return;
        gun.localPosition = new Vector3(dir.x * gunAx, dir.y * gunAy, 0f);
        gun.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    public void Kick(float power) { kick = Mathf.Max(kick, power); }

    // a real leap: the body arcs up for a moment, independent of walking
    public void Hop(float power) { hopT = 0.0001f; hopPower = power; }

    // a one-shot bulge, so growing or shrinking a size reads even at +5% a level
    public void Pop(float power) { pop = Mathf.Max(pop, power); }

    void Start()
    {
        if (!captured) Capture();
        rb = GetComponentInParent<Rigidbody2D>();
    }

    void Update()
    {
        if (!captured) Capture();
        if (rb == null) { rb = GetComponentInParent<Rigidbody2D>(); if (rb == null) return; }

        float dt = Time.deltaTime;
        float sp = rb.GetVel().magnitude;
        float k = Mathf.Clamp01(sp / 4f);

        idleT += dt;
        phase += sp * dt * StepRate() + dt * IdleRate();

        float bob = 0f, sq = 1f, tilt = 0f, jitter = 0f;
        switch (style)
        {
            case AnimStyle.Hunch:              // stabber: forward lean, nervous
                bob = Mathf.Abs(Mathf.Sin(phase)) * 0.05f * k;
                sq = 1f + Mathf.Sin(phase * 2f) * 0.07f * k;
                tilt = -9f * k;
                jitter = 1.5f * k;
                break;

            case AnimStyle.Vibrate:            // dasher: coiled, buzzing to go
                bob = Mathf.Abs(Mathf.Sin(phase * 1.8f)) * 0.06f * k;
                sq = 1f + Mathf.Sin(phase * 4f) * 0.05f * k;
                jitter = 2.5f * k;
                break;

            case AnimStyle.Scuttle:            // spider: fast little scissor step
                bob = Mathf.Abs(Mathf.Sin(phase * 1.6f)) * 0.07f * k;
                sq = 1f + Mathf.Sin(phase * 3.2f) * 0.09f * k;
                tilt = Mathf.Sin(phase * 1.6f) * 8f * k;
                break;

            case AnimStyle.Heavy:              // bulwark / artillery: slow tread
                bob = Mathf.Abs(Mathf.Sin(phase * 0.6f)) * 0.09f * k;
                sq = 1f + Mathf.Sin(phase * 1.2f) * 0.11f * k;
                break;

            case AnimStyle.Sway:               // trapper: busy engineer, fidgets
                {
                    // slow working sway, plus a quick "tug at the gear" every 1.5s
                    float w = Mathf.Sin(idleT * 2.2f);
                    float tug = Mathf.Max(0f, Mathf.PingPong(idleT * 1.3f, 1f) - 0.82f) / 0.18f;
                    bob = w * 0.03f + Mathf.Abs(Mathf.Sin(phase)) * 0.05f * k + tug * 0.05f;
                    sq = 1f + Mathf.Sin(phase * 2f) * 0.05f * k - tug * 0.12f;
                    tilt = w * 4f + Mathf.Sin(idleT * 14f) * tug * 6f;
                    jitter = tug * 2.5f;
                }
                break;

            case AnimStyle.Stiff:              // piercer: rigid, braced frame
                bob = Mathf.Abs(Mathf.Sin(phase * 0.8f)) * 0.04f * k;
                sq = 1f + Mathf.Sin(phase * 1.6f) * 0.03f * k;
                break;

            case AnimStyle.Bounce:             // bouncer: lands hard, hops again
                {
                    float hop = Mathf.Pow(Mathf.Abs(Mathf.Sin(phase * 1.3f)), 1.6f);
                    bob = hop * 0.15f * (0.5f + 0.5f * k) + Mathf.Abs(Mathf.Sin(phase)) * 0.02f * k;
                    sq = 1f + (hop - 0.45f) * 0.34f;   // squashed on landing, stretched in the air
                }
                break;

            case AnimStyle.Hover:              // grabber: floats, no legs
                bob = Mathf.Sin(idleT * 2.6f) * 0.07f + Mathf.Abs(Mathf.Sin(phase)) * 0.03f * k;
                sq = 1f + Mathf.Sin(idleT * 2.6f) * 0.02f;
                break;

            default:                           // standard run cycle
                bob = Mathf.Abs(Mathf.Sin(phase)) * 0.06f * k;
                sq = 1f + Mathf.Sin(phase * 2f) * 0.06f * k;
                break;
        }

        kick = Mathf.Max(0f, kick - dt * 7f);          // recoil settles fast
        pop = Mathf.Max(0f, pop - dt * 1.6f);          // size bulge settles too

        // airborne arc of a leap: rises, hangs, then lands with a squash
        float hopY = 0f, hopAir = 0f;
        if (hopT > 0f)
        {
            hopT += dt;
            float t = hopT / HopDur;
            if (t >= 1f) { hopT = 0f; }
            else { hopAir = Mathf.Sin(t * Mathf.PI); hopY = hopAir * hopPower; }
        }

        float jx = 0f, jy = 0f;
        if (jitter > 0f)
        {
            jx = (Mathf.PingPong(idleT * 37f, 1f) - 0.5f) * 2f * jitter * 0.012f;
            jy = (Mathf.PingPong(idleT * 43f + 0.5f, 1f) - 0.5f) * 2f * jitter * 0.012f;
        }

        transform.localPosition = new Vector3(-kick * 0.14f + jx, bob + jy + hopY, 0f);
        float hs = hopT > 0f ? 1f + (hopAir - 0.5f) * 0.34f : 1f;
        float sqh = sq * hs;
        float grow = (1f + pop) * sizeMul;
        transform.localScale = new Vector3(baseScale.x * (2f - sqh) * grow,
                                           baseScale.y * sqh * grow, baseScale.z);

        if (body != null)
            body.localRotation = Quaternion.Euler(0, 0, tilt - kick * 7f);
    }

    float StepRate()
    {
        if (style == AnimStyle.Scuttle) return 4.5f;
        if (style == AnimStyle.Heavy) return 1.6f;
        return 3.2f;
    }

    float IdleRate()
    {
        if (style == AnimStyle.Scuttle) return 3f;
        if (style == AnimStyle.Vibrate) return 2f;
        if (style == AnimStyle.Bounce) return 2f;   // even standing still, it keeps hopping
        return 0f;
    }
}

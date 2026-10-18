using UnityEngine;

// A torn-off body part: scatters on death, trails blood while it flies, sticks
// on the ground for a few seconds, then fades away. The bloodstains it left
// behind stay for the rest of the run.
public class GoreChunk : MonoBehaviour
{
    Vector2 vel;
    Color blood;
    float life, trail, alpha0 = 1f;
    SpriteRenderer sr;

    public void Init(Vector2 v, Color c, float seconds, float alpha = 1f)
    {
        vel = v; blood = c; life = seconds; alpha0 = alpha;
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        transform.position += (Vector3)(vel * dt);
        transform.Rotate(0, 0, vel.magnitude * 45f * dt);
        vel *= 1f - 3.2f * dt;

        // bleeding trail while it is still moving fast
        if (vel.sqrMagnitude > 6.25f)
        {
            trail -= dt;
            if (trail <= 0f)
            {
                trail = 0.28f;
                Gore.Stain(transform.position, blood, Random.Range(0.3f, 0.55f));
            }
        }

        life -= dt;
        if (life <= 0.5f)
        {
            if (sr != null) { var col = sr.color; col.a = alpha0 * Mathf.Clamp01(life / 0.5f); sr.color = col; }
            if (life <= 0f) Destroy(gameObject);
        }
    }
}

using UnityEngine;

// A flung blood drop: slides across the floor, slows, then soaks in as a
// permanent bloodstain wherever it came to rest.
public class GoreDrop : MonoBehaviour
{
    Vector2 vel;
    Color blood;
    float size, life = 2.2f;

    public void Init(Vector2 v, Color c, float s) { vel = v; blood = c; size = s; }

    void Update()
    {
        float dt = Time.deltaTime;
        transform.position += (Vector3)(vel * dt);
        vel *= 1f - 5.5f * dt;
        life -= dt;

        if (vel.sqrMagnitude < 0.09f || life <= 0f)
        {
            Gore.Stain(transform.position, blood, size * Random.Range(1.8f, 3.0f));
            Destroy(gameObject);
        }
    }
}

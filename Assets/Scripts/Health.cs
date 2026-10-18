using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour
{
    public float max = 100f;
    public float current = 100f;
    public bool invulnerable;
    public float hitInvulnTime = 0f;
    public Color bloodColor = new Color(0.8f, 0.05f, 0.05f);
    public System.Action<Health> OnDeath;

    [Header("Damage bar")]
    public bool showBar = false;
    public float barWidth = 0.8f;
    public float barOffsetY = 0.7f;
    public Color barColor = new Color(0.95f, 0.25f, 0.2f);

    float graceTimer;
    bool dead;
    HealthBar bar;
    SpriteRenderer[] rends;
    Color[] orig;

    public bool IsInvulnerable { get { return invulnerable || graceTimer > 0f; } }
    public bool IsDead { get { return dead; } }
    public float Grace { get { return graceTimer; } }

    public void Init(float m) { max = m; current = m; }

    void Start()
    {
        var list = new List<SpriteRenderer>();
        foreach (var r in GetComponentsInChildren<SpriteRenderer>())
        {
            string n = r.name;
            if (n == "Shadow" || n == "Glow" || n.StartsWith("HPBar")) continue;
            list.Add(r);
        }
        rends = list.ToArray();
        orig = new Color[rends.Length];
        for (int i = 0; i < rends.Length; i++) orig[i] = rends[i].color;

        if (showBar)
            bar = HealthBar.Attach(transform, barWidth, barColor, barOffsetY);
    }

    void Update()
    {
        if (graceTimer > 0f) graceTimer -= Time.deltaTime;
    }

    // `force` = direction the blood should spray (away from the attacker)
    public bool TakeDamage(float amount, Vector2 force = default(Vector2))
    {
        return Apply(amount, force, true);
    }

    // damage-over-time that skips gore, screenshake and the upgrade hooks
    // (the hemo-toxin burn, the solvent pools)
    public bool Burn(float amount)
    {
        return Apply(amount, Vector2.zero, false);
    }

    // brief invulnerability on top of whatever is already running
    public void GrantGrace(float t) { graceTimer = Mathf.Max(graceTimer, t); }

    // instant death without a hit behind it (the 225 execute): same path a
    // fatal blow takes, minus the damage - the bar drops and the death runs
    public void Kill()
    {
        if (dead) return;
        if (PlayerController.GodMode && CompareTag("Player")) return;
        current = 0f;
        dead = true;
        if (bar != null) bar.Set(0f);
        OnDeath?.Invoke(this);
    }

    // TIMELEAP: the bar is dragged back to an earlier moment by hand - a
    // rewound body heals to that number without dying on the way there
    public void Restore(float amount)
    {
        current = Mathf.Clamp(amount, 0f, max);
        if (dead && current > 0f) dead = false;
        if (bar != null) bar.Set(current / max);
    }

    bool Apply(float amount, Vector2 force, bool combat)
    {
        // GOD MODE: the tester stands above the timeline's nonsense
        if (PlayerController.GodMode && CompareTag("Player")) return false;

        if (dead || IsInvulnerable || amount <= 0f) return false;

        // the halo ward eats hits before the suit does
        if (combat && CompareTag("Player"))
        {
            var up = PlayerUpgrades.Instance;
            if (up != null && up.Absorb(ref amount))
            {
                if (amount <= 0f) { graceTimer = hitInvulnTime * 0.5f; return true; }
            }
        }

        current -= amount;
        graceTimer = hitInvulnTime;
        if (combat) Gore.Hit(transform.position, force, bloodColor, amount);
        if (CompareTag("Player")) GameManager.Shake(0.35f);

        if (rends != null)
        {
            StopAllCoroutines();
            StartCoroutine(Flash());
        }
        if (bar != null) bar.Set(current / max);

        if (current <= 0f)
        {
            current = 0f;
            dead = true;
            OnDeath?.Invoke(this);
        }

        // hemo-toxin: red blood keeps boiling after the hit
        if (combat && !CompareTag("Player"))
        {
            var up = PlayerUpgrades.Instance;
            if (up != null) up.OnEnemyDamaged(this, amount);
        }
        return true;
    }

    IEnumerator Flash()
    {
        foreach (var r in rends) if (r != null) r.color = Color.white;
        yield return new WaitForSeconds(0.06f);
        for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].color = orig[i];
    }
}

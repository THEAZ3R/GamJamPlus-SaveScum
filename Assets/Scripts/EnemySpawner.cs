using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public float spawnInterval = 1.6f;
    public int maxAlive = 8;            // hard cap: no more than 8 at once (trimmed by two)
    public float minDistFromPlayer = 7f;

    public static float Difficulty = 1f;

    float timer = 0.5f;

    void Awake() { Difficulty = 1f; }

    void Update()
    {
        if (GameManager.IsOver || GameManager.EndPrompt || Nav.Small == null) return;
        if (Cutscenes.Running || GameMenu.Open) return;   // the arena holds its breath
        if (TimeLeap.BossAlive) return;              // no new blood during a boss fight

        // gentle ramp so a long run keeps its teeth, then every TIMELEAP
        // multiplies the whole field
        Difficulty = TimeLeap.PowerMul * Mathf.Min(2.6f, 1f + Time.timeSinceLevelLoad / 150f);
        spawnInterval = Mathf.Max(0.85f, 1.6f - 0.1f * TimeLeap.Leaps);

        timer -= Time.deltaTime;
        if (timer <= 0f && Enemy.Alive < maxAlive)
        {
            timer = spawnInterval;
            Spawn();
        }
    }

    // kind, unlock time, weight — everything is in the mix within ~30s so a
    // run actually shows the whole roster instead of just stabbers.
    static readonly EnemyKind[] Kinds =
    {
        EnemyKind.Stabber, EnemyKind.Shooter, EnemyKind.Spider, EnemyKind.Dasher,
        EnemyKind.Trapper, EnemyKind.Piercer, EnemyKind.Bouncer, EnemyKind.Bulwark,
        EnemyKind.Artillery, EnemyKind.Grabber,
    };
    static readonly float[] Unlock = { 0f, 4f, 6f, 9f, 12f, 15f, 18f, 20f, 26f, 32f };
    static readonly float[] Weight = { 5f, 4f, 3.5f, 3f, 3f, 2.5f, 2.5f, 2f, 1.8f, 1.2f };

    EnemyKind Pick()
    {
        // the flesh sends its own shapes: grabbers, meat artillery, butchers
        if (TimeLeap.Map4)
        {
            float flesh = Random.value;
            if (flesh < 0.4f) return EnemyKind.Butcher;
            if (flesh < 0.7f) return EnemyKind.Artillery;
            return EnemyKind.Grabber;
        }

        // the ruin only knows three shapes: piercers, bulwarks, dashers
        if (TimeLeap.Map3)
        {
            float ruin = Random.value;
            if (ruin < 0.45f) return EnemyKind.Piercer;
            if (ruin < 0.75f) return EnemyKind.Bulwark;
            return EnemyKind.Dasher;
        }

        // the lab sends its own pack: springy bouncers, rushers, bombers
        if (TimeLeap.Map2)
        {
            float lab = Random.value;
            if (lab < 0.4f) return EnemyKind.Bouncer;
            if (lab < 0.72f) return EnemyKind.Dasher;
            return EnemyKind.Spider;
        }

        // until the first boss shows, the trio holds the fort - and the
        // trapper is rationed: two at a time, three on the leap before the boss
        if (TimeLeap.Leaps <= 2)
        {
            int trapperCap = TimeLeap.Leaps <= 1 ? 2 : 3;
            float roll = Random.value;
            if (roll < 0.4f) return EnemyKind.Shooter;
            if (roll < 0.7f && CountKind(EnemyKind.Trapper) < trapperCap) return EnemyKind.Trapper;
            return EnemyKind.Stabber;
        }

        float t = Time.timeSinceLevelLoad;

        float total = 0f;
        for (int i = 0; i < Weight.Length; i++)
            if (t >= Unlock[i]) total += Weight[i];

        float r = Random.value * total;
        for (int i = 0; i < Weight.Length; i++)
        {
            if (t < Unlock[i]) continue;
            if (r < Weight[i]) return Kinds[i];
            r -= Weight[i];
        }
        return EnemyKind.Stabber;
    }

    // how many of a kind are alive right now (the trapper ration counts them)
    static int CountKind(EnemyKind kind)
    {
        int n = 0;
        foreach (var e in Enemy.All)
            if (e != null && e.KindOf == kind) n++;
        return n;
    }

    void Spawn()
    {
        var player = PlayerController.Instance;
        EnemyKind kind = Pick();
        bool large = kind == EnemyKind.Bulwark || kind == EnemyKind.Artillery ||
                     kind == EnemyKind.Grabber || kind == EnemyKind.Butcher;

        Vector2 half = GameManager.ArenaHalf - Vector2.one * 1.6f;
        for (int i = 0; i < 40; i++)
        {
            Vector2 pos = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
            if (player != null && Vector2.Distance(pos, player.transform.position) < minDistFromPlayer) continue;
            if (Pit.Inside(pos)) continue;
            if (!Nav.IsFree(pos, large)) continue;

            Fx.Burst(pos, new Color(0.35f, 0.95f, 1f), 9, 4f);
            Enemy.Spawn(kind, pos, Difficulty);
            return;
        }
    }
}

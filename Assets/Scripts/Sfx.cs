using System.Collections.Generic;
using UnityEngine;

// PROCEDURAL AUDIO: every sound in the game is synthesized on the spot from
// raw math - waveforms, noise, envelopes - so the run ships with zero audio
// assets and still has a voice for everything: guns, quakes, wheels, wet
// footsteps, tesla traps and the herald's dying scream.
public static class Sfx
{
    const int Rate = 44100;
    public const float Nominal = 0.7f;              // master trim: headroom for the pile-up
    public static float Volume = Nominal;           // M mutes, M brings it back

    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static AudioSource[] sources;
    static int next;

    // ------------------------------------------------------------ playback
    public static void Play(string name) { PlayAt(Vector2.zero, name, 1f); }

    public static void PlayAt(Vector2 pos, string name, float vol = 1f)
    {
        PlayAt(pos, name, vol, 1f);
    }

    public static void PlayAt(Vector2 pos, string name, float vol, float pitch)
    {
        var c = Get(name);
        if (c == null || sources == null) return;

        // distance fade: the horde keeps its voice but never drowns you out
        float atten = 1f;
        var p = PlayerController.Instance;
        if (p != null)
        {
            float d = Vector2.Distance(pos, p.transform.position);
            atten = 1f / (1f + d * 0.10f);
        }
        if (atten < 0.04f) return;

        var src = sources[next];
        next = (next + 1) % sources.Length;
        src.volume = Mathf.Clamp01(Volume * vol * atten);
        src.pitch = pitch * (1f + Random.Range(-0.05f, 0.05f));
        src.PlayOneShot(c);
    }

    // the tape's own voice: dialogue comes out of the speaker, not out of the
    // room, so it never fades with how far you have wandered from the middle
    public static void PlayVoice(string name, float vol, float pitch)
    {
        var c = Get(name);
        if (c == null || sources == null) return;

        var src = sources[next];
        next = (next + 1) % sources.Length;
        src.volume = Mathf.Clamp01(Volume * vol);
        src.pitch = pitch * (1f + Random.Range(-0.05f, 0.05f));
        src.PlayOneShot(c);
    }

    public static void Ensure()
    {
        // the host lives in the scene: pressing R destroys it while the
        // static array outlives it - so check the components are still alive
        // (not just the array) and rebuild the whole rig when the reload ate it
        if (sources != null && sources.Length > 0 && sources[0] != null) return;
        var g = new GameObject("Sfx");
        g.transform.position = new Vector3(0f, -600f, 0f);   // parked far below the
        // ring: every gizmo this rig or its sources could draw would otherwise
        // land in the dead middle of the arena
        sources = new AudioSource[14];
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = g.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].spatialBlend = 0f;             // 2D, we fade by hand
            sources[i].dopplerLevel = 0f;
        }
        next = 0;

        // one listener, standing with the rig instead of riding the camera.
        // every source is 2D and distance is faded by hand, so where the
        // listener stands changes nothing you can hear - but on the camera it
        // drew a speaker icon dead centre of the frame, every frame
        var ls = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (var l in ls)
            if (l != null && l.gameObject != g) UnityEngine.Object.Destroy(l);
        if (g.GetComponent<AudioListener>() == null) g.AddComponent<AudioListener>();

        // the sounds the first seconds lean on hardest
        Get("pew"); Get("enemy_shot"); Get("step"); Get("step_wet");
        Get("dash"); Get("spawn"); Get("die_small"); Get("crawl");
        Get("fuse_tick"); Get("bark"); Get("fire_cackle"); Get("elec_hum");
        Get("menu_go"); Get("menu_move");          // the menu is up before any of it
    }

    public static void ToggleMute()
    {
        Volume = Volume > 0f ? 0f : Nominal;
    }

    // build clips ahead of time so their first play never stutters
    public static void Warm(params string[] names)
    {
        foreach (var n in names) Get(n);
    }

    // ------------------------------------------------------------ synthesis
    static AudioClip Get(string name)
    {
        Ensure();
        AudioClip c;
        // a dead clip from a previous run reads as null - rebuild it
        if (clips.TryGetValue(name, out c) && c != null) return c;
        return Build(name);
    }

    static float[] Buf(float dur) { return new float[Mathf.CeilToInt(dur * Rate)]; }

    static AudioClip Make(string name, float[] b)
    {
        // normalise so nothing ever clips
        float peak = 0f;
        for (int i = 0; i < b.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(b[i]));
        if (peak > 0.92f)
            for (int i = 0; i < b.Length; i++) b[i] *= 0.92f / peak;

        var clip = AudioClip.Create(name, b.Length, 1, Rate, false);
        clip.SetData(b, 0);
        clips[name] = clip;
        return clip;
    }

    // ------------------------------------------------------------- the voices
    // two speech voices for the tape's dialogue, built to be heard hundreds of
    // times without tiring: mono, clean, one letter each, 40-80ms. the clip is
    // generated at its own rate (22050 - a 70ms letter is ~1500 samples) and
    // Unity resamples it on playback. waveform, frequency, pulse width, detune
    // and decay are all parameters, so both characters come out of one builder
    static AudioClip MakeVoice(string name, float freq, float pulseWidth, bool sawtooth,
                               float decay, float attack, float detuneCents,
                               float dur, float noiseAmt, float drive)
    {
        const int r = 22050;
        int n = Mathf.CeilToInt(dur * r);
        var b = new float[n];

        float f2 = freq * Mathf.Pow(2f, detuneCents / 1200f);   // the second, detuned
        float nk = Mathf.Exp(-2f * Mathf.PI * 4500f / r);       // the tear's filter
        float ph = 0f, ph2 = 0f, ny = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)r;
            ph += 2f * Mathf.PI * freq / r;
            ph2 += 2f * Mathf.PI * f2 / r;

            float x = ph / (2f * Mathf.PI), x2 = ph2 / (2f * Mathf.PI);
            float w = Wave(x, sawtooth, pulseWidth);
            float mix = detuneCents > 0f ? (w + Wave(x2, sawtooth, pulseWidth)) * 0.5f : w;

            // fast attack, exponential decay - the whole shape of a blip
            float a = t < attack ? t / attack : Mathf.Exp(-(t - attack) * decay);

            float s = mix;
            if (noiseAmt > 0f)
            {
                ny = Random.Range(-1f, 1f) * (1f - nk) + ny * nk;
                s += ny * noiseAmt;
            }
            if (drive > 0f) s = (float)System.Math.Tanh(s * drive);   // the shout's rasp

            b[i] = s * a;
        }

        float peak = 0f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(b[i]));
        if (peak > 0.85f)
            for (int i = 0; i < n; i++) b[i] *= 0.85f / peak;

        var clip = AudioClip.Create(name, n, 1, r, false);
        clip.SetData(b, 0);
        clips[name] = clip;
        return clip;
    }

    // one sample of a pulse-width square or a sawtooth, from a cycle position
    static float Wave(float x, bool sawtooth, float pulseWidth)
    {
        if (sawtooth) return 2f * (x - Mathf.Floor(x + 0.5f));
        return (x - Mathf.Floor(x)) < pulseWidth ? 1f : -1f;
    }

    // a tone with arbitrary frequency and amplitude envelopes; wave picks
    // the shape (default sine)
    static void AddTone(float[] b, float t0, float dur, System.Func<float, float> freq,
                        System.Func<float, float> amp, System.Func<float, float, float> wave = null)
    {
        int s = (int)(t0 * Rate), n = (int)(dur * Rate);
        float ph = 0f;
        for (int i = 0; i < n && s + i < b.Length; i++)
        {
            float t = i / (float)Rate;
            ph += 2f * Mathf.PI * freq(t) / Rate;
            float w = wave == null ? Mathf.Sin(ph) : wave(ph, t);
            b[s + i] += w * amp(t);
        }
    }

    // filtered noise: lp is the one-pole cutoff in Hz
    static void AddNoise(float[] b, float t0, float dur, System.Func<float, float> amp, float lp)
    {
        int s = (int)(t0 * Rate), n = (int)(dur * Rate);
        float y = 0f, k = Mathf.Exp(-2f * Mathf.PI * lp / Rate);
        for (int i = 0; i < n && s + i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float x = Random.Range(-1f, 1f);
            y = x * (1f - k) + y * k;
            b[s + i] += y * amp(t);
        }
    }

    // brown noise: the body of earthquakes and rolling chassis
    static void AddBrown(float[] b, float t0, float dur, System.Func<float, float> amp, float lp)
    {
        int s = (int)(t0 * Rate), n = (int)(dur * Rate);
        float y = 0f, prev = 0f, k = Mathf.Exp(-2f * Mathf.PI * lp / Rate);
        for (int i = 0; i < n && s + i < b.Length; i++)
        {
            float t = i / (float)Rate;
            prev = 0.98f * prev + 0.02f * Random.Range(-1f, 1f);
            y = prev * (1f - k) + y * k;
            b[s + i] += y * amp(t) * 6f;              // brown is quiet: makeup
        }
    }

    static System.Func<float, float> Exp(float k) { return t => Mathf.Exp(-t * k); }
    static System.Func<float, float> Const(float x) { return t => x; }

    // exponential frequency sweep, the classic laser/pew shape
    static System.Func<float, float> Sw(float f0, float f1, float dur)
    {
        return t => f0 * Mathf.Pow(f1 / f0, Mathf.Clamp01(t / dur));
    }

    // bell-shaped amplitude: attack to c, fade by c + w
    static System.Func<float, float> Bell(float c, float w)
    {
        return t => Mathf.Exp(-Mathf.Pow((t - c) / w, 2f) * 3f);
    }

    static float Saw(float ph, float t) { float x = ph / (2f * Mathf.PI); return 2f * (x - Mathf.Floor(x + 0.5f)); }
    static float Sqr(float ph, float t) { return Mathf.Sin(ph) >= 0f ? 1f : -1f; }

    // ------------------------------------------------------------ recipes
    static AudioClip Build(string name)
    {
        float[] b;
        switch (name)
        {
            // ---- guns
            case "pew":                                     // the sidearm
                b = Buf(0.11f);
                AddTone(b, 0f, 0.10f, Sw(1500f, 240f, 0.10f), t => 0.5f * Mathf.Exp(-t * 34f), Saw);
                AddTone(b, 0f, 0.10f, Sw(760f, 120f, 0.10f), t => 0.18f * Mathf.Exp(-t * 40f));
                AddNoise(b, 0f, 0.012f, t => 0.35f * Mathf.Exp(-t * 300f), 5000f);
                return Make(name, b);

            case "laser_shot":                              // piercer slug: a lance of sound
                b = Buf(0.2f);
                AddTone(b, 0f, 0.18f, t => 920f + 280f * Mathf.Sin(2f * Mathf.PI * 42f * t),
                        t => (t < 0.015f ? t / 0.015f : Mathf.Exp(-(t - 0.015f) * 16f)) * 0.42f, Sqr);
                AddTone(b, 0f, 0.18f, Sw(1840f, 900f, 0.18f), t => 0.1f * Mathf.Exp(-t * 24f));
                AddNoise(b, 0f, 0.18f, t => 0.14f * Mathf.Exp(-t * 20f), 7000f);
                return Make(name, b);

            case "enemy_shot":                              // their guns are cruder
                b = Buf(0.14f);
                AddTone(b, 0f, 0.13f, Sw(460f, 120f, 0.13f), t => 0.42f * Mathf.Exp(-t * 26f), Saw);
                AddNoise(b, 0f, 0.01f, t => 0.22f * Mathf.Exp(-t * 400f), 3500f);
                return Make(name, b);

            case "beam":                                    // laser bot / herald sweep
                b = Buf(0.3f);
                AddTone(b, 0f, 0.28f, t => 150f + 8f * Mathf.Sin(2f * Mathf.PI * 9f * t),
                        t => (t < 0.02f ? t * 15f : Mathf.Max(0f, 1f - (t - 0.02f) / 0.26f)) * 0.34f, Sqr);
                AddTone(b, 0f, 0.28f, Const(300f), t => 0.16f * Mathf.Max(0f, 1f - t / 0.28f));
                AddNoise(b, 0f, 0.28f, t => 0.12f * Mathf.Max(0f, 1f - t / 0.28f), 6000f);
                return Make(name, b);

            case "orbital":                                 // HELIOS' JUDGMENT: charge, then heaven
                b = Buf(1.55f);
                AddTone(b, 0f, 0.65f, Sw(200f, 2000f, 0.65f),
                        t => 0.30f * (t / 0.65f) * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 30f * t)));
                AddNoise(b, 0f, 0.65f, t => 0.10f * (t / 0.65f), 9000f);
                AddNoise(b, 0.65f, 0.7f, t => 0.55f * Mathf.Exp(-t * 10f), 4000f);
                AddNoise(b, 0.65f, 0.6f, t => 0.6f * Mathf.Exp(-t * 7f), 500f);
                AddTone(b, 0.65f, 0.7f, Sw(1900f, 90f, 0.7f), t => 0.7f * Mathf.Exp(-t * 8f), Saw);
                AddTone(b, 0.65f, 0.6f, Sw(120f, 42f, 0.6f), t => 0.8f * Mathf.Exp(-t * 6f));
                return Make(name, b);

            // ---- movement
            case "step":                                    // steel tick
                b = Buf(0.08f);
                AddNoise(b, 0f, 0.07f, t => 0.5f * Mathf.Exp(-t * 55f), 750f);
                AddTone(b, 0f, 0.06f, Sw(150f, 85f, 0.06f), t => 0.22f * Mathf.Exp(-t * 45f));
                return Make(name, b);

            case "step_wet":                                // oil and blood squelch
                b = Buf(0.16f);
                AddNoise(b, 0f, 0.14f, t => 0.42f * Mathf.Exp(-t * 26f), 2300f);
                AddTone(b, 0f, 0.09f, Sw(330f, 110f, 0.09f), t => 0.34f * Mathf.Exp(-t * 22f));
                AddTone(b, 0.07f, 0.07f, Sw(520f, 1500f, 0.07f), t => 0.18f * Mathf.Exp(-t * 40f));
                return Make(name, b);

            case "dash":                                    // air parted in a hurry
                b = Buf(0.25f);
                AddNoise(b, 0f, 0.24f, t => 0.5f * Bell(0.06f, 0.18f)(t), 4200f);
                AddNoise(b, 0f, 0.24f, t => -0.3f * Bell(0.06f, 0.18f)(t), 900f);
                AddTone(b, 0f, 0.22f, Sw(700f, 180f, 0.22f), t => 0.12f * Mathf.Exp(-t * 18f));
                return Make(name, b);

            case "sweep":                                   // herald's neon dash
                b = Buf(0.4f);
                AddNoise(b, 0f, 0.38f, t => 0.45f * Bell(0.12f, 0.16f)(t), 8000f);
                AddNoise(b, 0f, 0.38f, t => -0.25f * Bell(0.12f, 0.16f)(t), 1500f);
                AddTone(b, 0f, 0.18f, Sw(400f, 1500f, 0.18f), t => 0.14f * Bell(0.1f, 0.14f)(t), Sqr);
                AddTone(b, 0.18f, 0.2f, Sw(1500f, 500f, 0.2f), t => 0.14f * Bell(0.1f, 0.14f)(t), Sqr);
                return Make(name, b);

            case "boost":                                   // the pad throws you
                b = Buf(0.3f);
                AddTone(b, 0f, 0.28f, Sw(260f, 900f, 0.28f),
                        t => 0.25f * Mathf.Exp(-Mathf.Max(0f, t - 0.18f) * 14f));
                AddNoise(b, 0f, 0.28f, t => 0.25f * Mathf.Exp(-Mathf.Max(0f, t - 0.16f) * 12f), 3500f);
                return Make(name, b);

            // ---- melee and hurt
            case "stab":                                    // wet, quick, final
                b = Buf(0.11f);
                AddNoise(b, 0f, 0.05f, t => 0.6f * Mathf.Exp(-t * 90f), 9000f);
                AddTone(b, 0f, 0.09f, Sw(320f, 90f, 0.09f), t => 0.4f * Mathf.Exp(-t * 48f), Saw);
                AddTone(b, 0.02f, 0.08f, Sw(110f, 60f, 0.08f), t => 0.35f * Mathf.Exp(-t * 30f));
                return Make(name, b);

            case "die_small":                               // corpse comes apart - wet
                b = Buf(0.5f);
                AddNoise(b, 0f, 0.35f, t => 0.6f * Mathf.Exp(-t * 12f), 3200f);
                AddNoise(b, 0f, 0.3f, t => 0.35f * Mathf.Exp(-t * 20f), 700f);
                AddTone(b, 0f, 0.28f, Sw(160f, 55f, 0.28f), t => 0.45f * Mathf.Exp(-t * 12f));
                // the wet layer: meat coming apart, then the drips
                AddTone(b, 0f, 0.24f, Sw(260f, 70f, 0.24f),
                        t => 0.5f * Mathf.Exp(-t * 16f) * (1f + 0.5f * Mathf.Sin(2f * Mathf.PI * 21f * t)));
                AddNoise(b, 0.02f, 0.16f, t => 0.55f * Mathf.Exp(-t * 26f), 1100f);
                for (int i = 0; i < 8; i++)
                    AddNoise(b, Random.Range(0.03f, 0.34f), 0.012f, t => 0.3f * Mathf.Exp(-t * 300f), 4500f);
                for (int i = 0; i < 3; i++)
                    AddTone(b, Random.Range(0.12f, 0.4f), 0.05f, Sw(700f, 1400f, 0.05f),
                            t => 0.12f * Mathf.Exp(-t * 60f));
                return Make(name, b);

            case "die_big":                                 // something heavy fell over
                b = Buf(0.95f);
                AddNoise(b, 0f, 0.7f, t => 0.7f * Mathf.Exp(-t * 6.5f), 2600f);
                AddNoise(b, 0f, 0.6f, t => 0.4f * Mathf.Exp(-t * 10f), 600f);
                AddTone(b, 0f, 0.6f, Sw(110f, 36f, 0.6f), t => 0.7f * Mathf.Exp(-t * 7f));
                // the wet layer: guts hitting tile
                AddTone(b, 0.02f, 0.4f, Sw(190f, 48f, 0.4f),
                        t => 0.55f * Mathf.Exp(-t * 11f) * (1f + 0.45f * Mathf.Sin(2f * Mathf.PI * 15f * t)));
                AddNoise(b, 0.03f, 0.25f, t => 0.6f * Mathf.Exp(-t * 20f), 950f);
                for (int i = 0; i < 14; i++)
                    AddNoise(b, Random.Range(0.04f, 0.58f), 0.016f, t => 0.3f * Mathf.Exp(-t * 220f), 4200f);
                for (int i = 0; i < 5; i++)
                    AddTone(b, Random.Range(0.15f, 0.65f), 0.06f, Sw(600f, 1300f, 0.06f),
                            t => 0.14f * Mathf.Exp(-t * 55f));
                return Make(name, b);

            case "execute":                                 // ASTARION: the clean cut
                b = Buf(0.32f);
                AddNoise(b, 0f, 0.05f, t => 0.7f * Mathf.Exp(-t * 150f), 10000f);
                AddTone(b, 0f, 0.15f, Sw(900f, 200f, 0.15f), t => 0.3f * Mathf.Exp(-t * 40f), Saw);
                AddNoise(b, 0.03f, 0.25f, t => 0.5f * Mathf.Exp(-t * 18f), 2600f);
                AddTone(b, 0.03f, 0.2f, Sw(130f, 50f, 0.2f), t => 0.45f * Mathf.Exp(-t * 16f));
                return Make(name, b);

            // ---- the big machine
            case "quake":                                   // the floor remembers
                b = Buf(1.4f);
                AddBrown(b, 0f, 1.35f,
                         t => (t < 0.1f ? t * 10f : Mathf.Exp(-(t - 0.1f) * 2.6f)) * 0.8f, 400f);
                AddTone(b, 0f, 1.2f, t => 44f + 10f * Mathf.Sin(2f * Mathf.PI * 3.3f * t),
                        t => (t < 0.08f ? t * 12.5f : Mathf.Exp(-(t - 0.08f) * 2.2f)) * 0.65f);
                AddNoise(b, 0f, 0.12f, t => 0.6f * Mathf.Exp(-t * 30f), 1800f);      // the crack of it
                AddTone(b, 0f, 0.25f, Sw(90f, 30f, 0.25f), t => 0.5f * Mathf.Exp(-t * 10f), Saw);
                for (int i = 0; i < 12; i++)
                    AddNoise(b, Random.Range(0.05f, 1.1f), 0.02f,
                             t => 0.22f * Mathf.Exp(-t * 120f), 900f);
                return Make(name, b);

            case "slam":                                    // a landing you feel in the teeth
                b = Buf(0.7f);
                AddNoise(b, 0f, 0.5f, t => 0.85f * Mathf.Exp(-t * 9f), 2400f);
                AddNoise(b, 0f, 0.4f, t => 0.5f * Mathf.Exp(-t * 14f), 500f);
                AddTone(b, 0f, 0.5f, Sw(110f, 32f, 0.5f), t => 0.9f * Mathf.Exp(-t * 7f));
                AddBrown(b, 0f, 0.6f, t => 0.45f * Mathf.Exp(-t * 4.5f), 240f);
                AddTone(b, 0f, 0.18f, Sw(52f, 26f, 0.18f), t => 0.7f * Mathf.Exp(-t * 12f));    // sub hit
                AddTone(b, 0f, 0.06f, Sw(1500f, 380f, 0.06f), t => 0.35f * Mathf.Exp(-t * 60f), Saw); // grit
                return Make(name, b);

            case "leap":                                    // 1.5 tonnes leaving the floor
                b = Buf(0.5f);
                AddTone(b, 0f, 0.42f, Sw(80f, 200f, 0.42f), t => 0.5f * (t / 0.42f), Saw);
                AddNoise(b, 0f, 0.4f, t => 0.4f * Mathf.Pow(t / 0.4f, 2f), 1500f);
                AddBrown(b, 0f, 0.45f, t => 0.3f * (t / 0.45f), 180f);                  // the chassis groaning
                AddTone(b, 0f, 0.4f, t => 46f + 14f * (t / 0.4f), t => 0.35f * (t / 0.4f));
                return Make(name, b);

            case "shotgun":                                 // SCATTERGUN
                b = Buf(0.5f);
                AddNoise(b, 0f, 0.12f, t => 0.85f * Mathf.Exp(-t * 35f), 6500f);
                AddNoise(b, 0f, 0.35f, t => 0.6f * Mathf.Exp(-t * 12f), 1400f);
                AddTone(b, 0f, 0.3f, Sw(150f, 44f, 0.3f), t => 0.75f * Mathf.Exp(-t * 11f));
                AddTone(b, 0f, 0.14f, Sw(1100f, 240f, 0.14f), t => 0.4f * Mathf.Exp(-t * 40f), Saw);
                AddBrown(b, 0f, 0.3f, t => 0.35f * Mathf.Exp(-t * 9f), 260f);
                return Make(name, b);

            case "wheel":                                   // heavy chassis rolling
                b = Buf(0.55f);
                AddBrown(b, 0f, 0.5f,
                         t => 0.5f * (1f - t / 0.5f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 5.5f * t)), 260f);
                AddTone(b, 0f, 0.5f, t => 54f + 6f * Mathf.Sin(2f * Mathf.PI * 7f * t),
                        t => 0.2f * (1f - t / 0.5f));
                AddNoise(b, 0f, 0.5f,
                         t => 0.15f * (1f - t / 0.5f) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 8f * t)), 900f);
                return Make(name, b);

            case "crawl":                                   // a hundred tiny legs
                b = Buf(0.2f);
                for (int i = 0; i < 7; i++)
                {
                    float ti = i * 0.024f + Random.Range(0f, 0.008f);
                    AddNoise(b, ti, 0.004f, t => 0.4f * Mathf.Exp(-t * 900f), 5500f);
                    AddTone(b, ti, 0.008f, Const(2600f), t => 0.1f * Mathf.Exp(-t * 500f));
                }
                return Make(name, b);

            // ---- traps
            case "trap_throw":
                b = Buf(0.24f);
                AddNoise(b, 0f, 0.18f, t => 0.35f * Bell(0.07f, 0.09f)(t), 3800f);
                AddTone(b, 0.15f, 0.08f, Sw(420f, 180f, 0.08f), t => 0.3f * Mathf.Exp(-t * 40f));
                return Make(name, b);

            case "trap_slow":                               // goo takes hold
                b = Buf(0.34f);
                AddNoise(b, 0f, 0.28f, t => 0.5f * Mathf.Exp(-t * 16f), 1900f);
                AddTone(b, 0f, 0.25f, Sw(520f, 130f, 0.25f),
                        t => 0.42f * Mathf.Exp(-t * 14f) * (1f + 0.35f * Mathf.Sin(2f * Mathf.PI * 16f * t)));
                AddTone(b, 0.12f, 0.12f, Sw(300f, 700f, 0.12f), t => 0.15f * Mathf.Exp(-t * 30f));
                return Make(name, b);

            case "trap_boom_arm":                           // beep beep beep
                b = Buf(0.4f);
                for (int i = 0; i < 3; i++)
                    AddTone(b, i * 0.13f, 0.07f, Const(920f),
                            t => (t < 0.008f ? t / 0.008f : Mathf.Exp(-(t - 0.008f) * 60f)) * 0.35f, Sqr);
                return Make(name, b);

            case "trap_boom":
                b = Buf(0.5f);
                AddNoise(b, 0f, 0.4f, t => 0.75f * Mathf.Exp(-t * 11f), 3400f);
                AddNoise(b, 0f, 0.3f, t => 0.45f * Mathf.Exp(-t * 16f), 700f);
                AddTone(b, 0f, 0.3f, Sw(150f, 48f, 0.3f), t => 0.6f * Mathf.Exp(-t * 12f));
                return Make(name, b);

            case "trap_stun":                               // tesla node
                b = Buf(0.5f);
                AddTone(b, 0f, 0.45f, Const(120f), t => 0.12f * Mathf.Exp(-t * 7f), Sqr);
                AddTone(b, 0f, 0.4f, t => 2600f + 900f * Mathf.Sin(2f * Mathf.PI * 23f * t),
                        t => 0.1f * Mathf.Exp(-t * 6f) * (Mathf.Sin(2f * Mathf.PI * 37f * t) > 0f ? 1f : 0.4f), Sqr);
                for (int i = 0; i < 18; i++)
                    AddNoise(b, Random.Range(0f, 0.42f), 0.004f, t => 0.5f * Mathf.Exp(-t * 700f), 9000f);
                return Make(name, b);

            case "zap":                                     // one quick spark
                b = Buf(0.14f);
                AddNoise(b, 0f, 0.12f, t => 0.6f * Mathf.Exp(-t * 40f), 8500f);
                AddTone(b, 0f, 0.1f, Sw(1900f, 700f, 0.1f), t => 0.25f * Mathf.Exp(-t * 30f), Sqr);
                return Make(name, b);

            // ---- the lab era
            case "tele":                                    // herald goes thin, then thick
                b = Buf(0.4f);
                AddTone(b, 0f, 0.3f, Sw(1700f, 180f, 0.3f), t => 0.35f * Mathf.Exp(-t * 12f), Sqr);
                AddNoise(b, 0f, 0.35f, t => 0.25f * Mathf.Exp(-t * 9f), 9500f);
                AddTone(b, 0.2f, 0.18f, Sw(220f, 1200f, 0.18f), t => 0.15f * Mathf.Exp(-t * 20f));
                return Make(name, b);

            case "zeus":                                    // the sky comes down
                b = Buf(0.95f);
                AddNoise(b, 0f, 0.05f, t => 1f * Mathf.Exp(-t * 200f), 12000f);
                AddTone(b, 0f, 0.7f, Sw(160f, 40f, 0.7f), t => 0.85f * Mathf.Exp(-t * 7f));
                AddBrown(b, 0f, 0.8f, t => 0.5f * Mathf.Exp(-t * 5f), 500f);
                // the electric soul of it: a sputtering arc that keeps snapping
                AddTone(b, 0f, 0.55f, t => 2400f + 1200f * Mathf.Sin(2f * Mathf.PI * 41f * t),
                        t => 0.13f * Mathf.Exp(-t * 5f) * (Mathf.Sin(2f * Mathf.PI * 53f * t) > 0f ? 1f : 0.25f), Sqr);
                for (int i = 0; i < 16; i++)
                    AddNoise(b, 0.01f + Random.Range(0f, 0.55f), 0.008f, t => 0.4f * Mathf.Exp(-t * 400f), 10000f);
                return Make(name, b);

            case "flame":                                   // the herald breathes fire
                b = Buf(0.9f);
                AddNoise(b, 0f, 0.85f,
                         t => 0.55f * Bell(0.35f, 0.4f)(t) * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 11f * t)), 1100f);
                AddNoise(b, 0f, 0.85f, t => 0.25f * Bell(0.35f, 0.4f)(t), 4200f);
                AddTone(b, 0f, 0.8f, t => 70f + 20f * Mathf.Sin(2f * Mathf.PI * 6f * t), t => 0.1f * Bell(0.3f, 0.4f)(t));
                return Make(name, b);

            case "ignite":                                  // oil meets its friend
                b = Buf(0.35f);
                AddNoise(b, 0f, 0.32f,
                         t => 0.55f * (t < 0.1f ? t * 10f : Mathf.Exp(-(t - 0.1f) * 7f)), 2200f);
                AddTone(b, 0f, 0.3f, Sw(160f, 420f, 0.3f),
                        t => 0.2f * (t < 0.1f ? t * 10f : Mathf.Exp(-(t - 0.1f) * 6f)));
                return Make(name, b);

            case "glob":                                    // oil plop
                b = Buf(0.28f);
                AddTone(b, 0f, 0.2f, Sw(420f, 110f, 0.2f), t => 0.4f * Mathf.Exp(-t * 16f));
                AddNoise(b, 0f, 0.15f, t => 0.3f * Mathf.Exp(-t * 30f), 1800f);
                AddTone(b, 0.06f, 0.14f, Sw(240f, 900f, 0.14f), t => 0.12f * Mathf.Exp(-t * 28f));
                return Make(name, b);

            case "machine_spew":                            // the plumbing answers
                b = Buf(0.4f);
                AddTone(b, 0f, 0.06f, Const(240f), t => 0.35f * Mathf.Exp(-t * 80f), Sqr);
                AddNoise(b, 0.02f, 0.35f, t => 0.3f * Mathf.Exp(-t * 10f), 5200f);
                AddTone(b, 0f, 0.25f, Sw(180f, 90f, 0.25f), t => 0.2f * Mathf.Exp(-t * 20f));
                return Make(name, b);

            case "fire_cackle":                             // a fire keeping itself alive
                b = Buf(0.9f);
                AddNoise(b, 0f, 0.85f,
                         t => 0.22f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 7f * t)), 900f);
                for (int i = 0; i < 9; i++)
                    AddNoise(b, Random.Range(0f, 0.8f), 0.02f, t => 0.35f * Mathf.Exp(-t * 90f), 2600f);
                for (int i = 0; i < 4; i++)
                    AddTone(b, Random.Range(0.05f, 0.8f), 0.03f, Const(Random.Range(500f, 1100f)),
                            t => 0.08f * Mathf.Exp(-t * 120f));
                return Make(name, b);

            case "elec_hum":                                // current, just standing there
                b = Buf(1f);
                AddTone(b, 0f, 1f, Const(120f),
                        t => 0.09f * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 33f * t)), Sqr);
                AddTone(b, 0f, 1f, Const(240f),
                        t => 0.05f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 41f * t)), Sqr);
                for (int i = 0; i < 7; i++)
                    AddNoise(b, Random.Range(0f, 0.95f), 0.005f, t => 0.25f * Mathf.Exp(-t * 500f), 9000f);
                return Make(name, b);

            case "elec_crackle":                            // the current bites down hard
                b = Buf(0.38f);
                AddNoise(b, 0f, 0.34f,
                         t => 0.5f * (0.4f + 0.6f * Mathf.Sin(2f * Mathf.PI * 29f * t)), 7500f);
                AddTone(b, 0f, 0.3f, t => 2800f + 1500f * Mathf.Sin(2f * Mathf.PI * 47f * t),
                        t => 0.16f * (Mathf.Sin(2f * Mathf.PI * 61f * t) > 0f ? 1f : 0.3f), Sqr);
                for (int i = 0; i < 22; i++)
                    AddNoise(b, Random.Range(0f, 0.32f), 0.006f, t => 0.55f * Mathf.Exp(-t * 500f), 11000f);
                return Make(name, b);

            case "bark":                                    // dogmeat means it
                b = Buf(0.28f);
                for (int i = 0; i < 2; i++)
                {
                    float ti = i * 0.13f;
                    AddNoise(b, ti, 0.07f, t => 0.5f * Mathf.Exp(-t * 40f), 2800f);
                    AddTone(b, ti, 0.09f, Sw(i == 0 ? 340f : 300f, i == 0 ? 150f : 190f, 0.09f),
                            t => 0.45f * Mathf.Exp(-t * 26f) * (1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 38f * t)), Saw);
                }
                return Make(name, b);

            case "servo":                                   // a small joint complaining
                b = Buf(0.16f);
                AddTone(b, 0f, 0.14f, t => 320f + 260f * Mathf.Sin(2f * Mathf.PI * 13f * t),
                        t => 0.18f * Mathf.Exp(-t * 12f), Saw);
                AddNoise(b, 0f, 0.12f, t => 0.1f * Mathf.Exp(-t * 15f), 3400f);
                AddNoise(b, 0.11f, 0.02f, t => 0.25f * Mathf.Exp(-t * 250f), 5000f);
                return Make(name, b);

            case "fuse_tick":                               // the spider counts down
                b = Buf(0.08f);
                AddTone(b, 0f, 0.05f, Const(2100f), t => 0.4f * Mathf.Exp(-t * 130f), Sqr);
                AddNoise(b, 0f, 0.02f, t => 0.35f * Mathf.Exp(-t * 350f), 8000f);
                return Make(name, b);

            case "boom_small":                              // the spider arrives
                b = Buf(0.5f);
                AddNoise(b, 0f, 0.4f, t => 0.8f * Mathf.Exp(-t * 13f), 4200f);
                AddNoise(b, 0f, 0.3f, t => 0.5f * Mathf.Exp(-t * 18f), 800f);
                AddTone(b, 0f, 0.32f, Sw(180f, 52f, 0.32f), t => 0.7f * Mathf.Exp(-t * 11f));
                AddTone(b, 0f, 0.18f, Sw(700f, 180f, 0.18f), t => 0.25f * Mathf.Exp(-t * 24f), Saw);
                for (int i = 0; i < 5; i++)
                    AddNoise(b, Random.Range(0.02f, 0.3f), 0.014f, t => 0.35f * Mathf.Exp(-t * 260f), 5000f);
                return Make(name, b);

            case "machine_rise":                            // plumbing climbing out of the floor
                b = Buf(0.95f);
                AddNoise(b, 0f, 0.9f,
                         t => 0.4f * Mathf.Pow(t / 0.9f, 1.6f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 9f * t)), 1400f);
                AddTone(b, 0f, 0.9f, t => 70f + 90f * (t / 0.9f), t => 0.3f * Mathf.Pow(t / 0.9f, 1.4f), Saw);
                for (int i = 0; i < 8; i++)
                    AddNoise(b, Random.Range(0f, 0.85f), 0.02f, t => 0.25f * Mathf.Exp(-t * 150f), 2200f);
                return Make(name, b);

            case "machine_sink":                            // it goes back to waiting
                b = Buf(0.95f);
                AddNoise(b, 0f, 0.9f,
                         t => 0.4f * (1f - Mathf.Pow(t / 0.9f, 1.3f)) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 8f * t)), 1200f);
                AddTone(b, 0f, 0.9f, t => 150f - 90f * (t / 0.9f), t => 0.3f * (1f - Mathf.Pow(t / 0.9f, 1.2f)), Saw);
                for (int i = 0; i < 6; i++)
                    AddNoise(b, Random.Range(0f, 0.8f), 0.02f, t => 0.2f * Mathf.Exp(-t * 150f), 1800f);
                return Make(name, b);

            case "engine_chug":                             // a tank breathing in the dark
                b = Buf(1f);
                for (int i = 0; i < 4; i++)
                {
                    float ti = i * 0.24f;
                    AddBrown(b, ti, 0.2f,
                             t => 0.5f * Mathf.Exp(-t * 16f) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 9f * t)), 200f);
                    AddTone(b, ti, 0.16f, Sw(58f, 40f, 0.16f), t => 0.3f * Mathf.Exp(-t * 20f), Saw);
                }
                AddTone(b, 0f, 1f, Const(42f),
                        t => 0.12f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2.2f * t)));
                return Make(name, b);

            case "oil_throw":                               // a wide toss of machine oil
                b = Buf(0.55f);
                AddNoise(b, 0f, 0.5f, t => 0.45f * Mathf.Sin(Mathf.PI * t / 0.5f), 1300f);
                AddNoise(b, 0.1f, 0.4f, t => 0.25f * Mathf.Sin(Mathf.PI * (t + 0.05f) / 0.4f), 3600f);
                AddTone(b, 0f, 0.5f, Sw(140f, 300f, 0.25f), t => 0.15f * Mathf.Exp(-t * 8f));
                return Make(name, b);

            // ---- state and ui
            case "heal":                                    // PUSH PUSH PUSH
                b = Buf(0.55f);
                AddTone(b, 0f, 0.35f, Const(523.25f), t => 0.22f * Bell(0.1f, 0.16f)(t));
                AddTone(b, 0.12f, 0.4f, Const(659.25f), t => 0.2f * Bell(0.12f, 0.18f)(t));
                AddTone(b, 0.12f, 0.4f, Const(1318.5f), t => 0.07f * Bell(0.12f, 0.18f)(t));
                return Make(name, b);

            case "levelup":
                b = Buf(0.8f);
                AddTone(b, 0f, 0.35f, Const(523.25f), t => 0.2f * Bell(0.1f, 0.16f)(t));
                AddTone(b, 0.09f, 0.35f, Const(659.25f), t => 0.2f * Bell(0.1f, 0.16f)(t));
                AddTone(b, 0.18f, 0.35f, Const(783.99f), t => 0.2f * Bell(0.1f, 0.16f)(t));
                AddTone(b, 0.27f, 0.5f, Const(1046.5f), t => 0.22f * Bell(0.15f, 0.22f)(t));
                AddTone(b, 0.27f, 0.5f, Const(2093f), t => 0.06f * Bell(0.15f, 0.22f)(t));
                return Make(name, b);

            case "pickup":
                b = Buf(0.1f);
                AddTone(b, 0f, 0.08f, Sw(800f, 1300f, 0.08f), t => 0.25f * Mathf.Exp(-t * 40f));
                return Make(name, b);

            case "menu_go":                                 // the deck engages, then the cursor commits
                b = Buf(0.34f);
                AddNoise(b, 0f, 0.05f, t => 0.30f * Mathf.Exp(-t * 70f), 2600f);   // the button's clunk
                AddTone(b, 0f, 0.11f, Sw(240f, 84f, 0.11f), t => 0.24f * Mathf.Exp(-t * 30f), Saw);
                AddTone(b, 0.04f, 0.14f, Const(880f),                       // A
                        t => 0.24f * Bell(0.02f, 0.08f)(t), Sqr);
                AddTone(b, 0.13f, 0.21f, Const(1046.5f),                    // C: the leitmotif's first step,
                        t => 0.30f * Bell(0.03f, 0.12f)(t), Sqr);            // the same two notes the tape
                AddTone(b, 0.13f, 0.21f, Const(2093f),                      // is built out of
                        t => 0.07f * Bell(0.03f, 0.12f)(t));
                return Make(name, b);

            case "menu_move":                               // the cursor landing on a row
                b = Buf(0.07f);
                AddTone(b, 0f, 0.05f, Const(1568f), t => 0.15f * Mathf.Exp(-t * 62f), Sqr);
                AddTone(b, 0f, 0.04f, Const(3136f), t => 0.045f * Mathf.Exp(-t * 80f));
                AddNoise(b, 0f, 0.014f, t => 0.06f * Mathf.Exp(-t * 300f), 5200f);   // tape grit
                return Make(name, b);

            case "rewind":                                  // time runs backwards for a second
                b = Buf(0.95f);
                AddTone(b, 0f, 0.85f, Sw(180f, 1400f, 0.85f), t => 0.3f * (t / 0.85f));
                AddNoise(b, 0f, 0.9f, t => 0.2f * (t / 0.9f), 9000f);
                AddTone(b, 0.2f, 0.7f, Const(2093f), t => 0.05f * Bell(0.3f, 0.3f)(t));
                AddTone(b, 0.6f, 0.35f, Sw(1400f, 300f, 0.35f), t => 0.35f * Mathf.Exp(-t * 6f), Sqr);
                return Make(name, b);

            case "horn":                                    // something large just arrived
                b = Buf(1.5f);
                AddTone(b, 0f, 1.4f, t => 69f * (1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 4.5f * t)),
                        t => (t < 0.1f ? t * 10f : Mathf.Exp(-(t - 0.1f) * 1.6f)) * 0.5f, Saw);
                AddTone(b, 0f, 1.4f, Const(103.5f),
                        t => (t < 0.12f ? t * 8f : Mathf.Exp(-(t - 0.12f) * 1.9f)) * 0.3f, Saw);
                AddTone(b, 0f, 1.4f, Const(138f),
                        t => (t < 0.14f ? t * 7f : Mathf.Exp(-(t - 0.14f) * 2.2f)) * 0.18f, Saw);
                AddNoise(b, 0f, 1.2f, t => 0.06f * Mathf.Exp(-t * 2f), 1500f);
                return Make(name, b);

            case "glitch":                                  // the system eating itself
                b = Buf(0.85f);
                for (int i = 0; i < 10; i++)
                {
                    float t0 = i * 0.08f + Random.Range(0f, 0.02f);
                    AddNoise(b, t0, Random.Range(0.02f, 0.06f), t => Random.Range(0.15f, 0.45f),
                             Random.Range(600f, 6000f));
                    if (Random.value < 0.4f)
                        AddTone(b, t0, 0.03f, Const(Random.Range(200f, 2400f)), t => 0.15f * Mathf.Exp(-t * 60f), Sqr);
                }
                return Make(name, b);

            case "wall_smash":
                b = Buf(0.6f);
                AddNoise(b, 0f, 0.45f, t => 0.6f * Mathf.Exp(-t * 11f), 3000f);
                AddTone(b, 0f, 0.3f, Sw(120f, 50f, 0.3f), t => 0.4f * Mathf.Exp(-t * 14f));
                for (int i = 0; i < 8; i++)
                    AddNoise(b, Random.Range(0f, 0.4f), 0.015f, t => 0.3f * Mathf.Exp(-t * 200f), 5000f);
                return Make(name, b);

            case "shield_break":
                b = Buf(0.4f);
                AddNoise(b, 0f, 0.06f, t => 0.7f * Mathf.Exp(-t * 120f), 11000f);
                for (int i = 0; i < 6; i++)
                    AddTone(b, i * 0.045f + Random.Range(0f, 0.02f), 0.06f, Const(Random.Range(1800f, 3600f)),
                            t => 0.18f * Mathf.Exp(-t * 70f));
                AddTone(b, 0f, 0.25f, Sw(300f, 90f, 0.25f), t => 0.3f * Mathf.Exp(-t * 20f));
                return Make(name, b);

            case "shield_up":
                b = Buf(0.5f);
                AddTone(b, 0f, 0.45f, Sw(220f, 660f, 0.45f), t => 0.22f * Bell(0.2f, 0.24f)(t));
                AddTone(b, 0f, 0.45f, Const(880f), t => 0.07f * Bell(0.25f, 0.2f)(t));
                return Make(name, b);

            case "chrono":                                  // the bubble folds space
                b = Buf(0.9f);
                AddTone(b, 0f, 0.85f, t => 440f + 6f * Mathf.Sin(2f * Mathf.PI * 5f * t), t => 0.16f * Bell(0.35f, 0.4f)(t));
                AddTone(b, 0f, 0.85f, Const(660.5f), t => 0.12f * Bell(0.3f, 0.42f)(t));
                AddTone(b, 0.05f, 0.8f, Const(1760f), t => 0.05f * Bell(0.3f, 0.35f)(t));
                AddNoise(b, 0f, 0.6f, t => 0.1f * Mathf.Exp(-t * 3f), 9000f);
                return Make(name, b);

            case "aura":                                    // ASTARION drinks
                b = Buf(0.35f);
                AddTone(b, 0f, 0.32f, t => 88f + 8f * Mathf.Sin(2f * Mathf.PI * 7f * t), t => 0.2f * Bell(0.12f, 0.14f)(t));
                AddTone(b, 0f, 0.32f, Const(880f), t => 0.05f * Bell(0.14f, 0.12f)(t));
                return Make(name, b);

            case "stagger":                                 // the herald loses its train of thought
                b = Buf(0.35f);
                for (int i = 0; i < 6; i++)
                    AddTone(b, i * 0.04f + Random.Range(0f, 0.02f), 0.08f, Const(Random.Range(700f, 1900f)),
                            t => 0.16f * Mathf.Exp(-t * 60f), Sqr);
                AddNoise(b, 0f, 0.3f, t => 0.3f * Mathf.Exp(-t * 14f), 5000f);
                return Make(name, b);

            case "spawn":                                   // something climbs out of the static
                b = Buf(0.22f);
                AddTone(b, 0f, 0.18f, Sw(480f, 1150f, 0.18f),
                        t => 0.16f * (t / 0.18f) * Mathf.Exp(-Mathf.Max(0f, t - 0.12f) * 18f), Sqr);
                AddNoise(b, 0f, 0.15f, t => 0.12f * Mathf.Exp(-t * 24f), 5500f);
                return Make(name, b);

            case "bot_die":
                b = Buf(0.28f);
                AddNoise(b, 0f, 0.25f, t => 0.5f * Mathf.Exp(-t * 14f), 8000f);
                AddTone(b, 0f, 0.22f, Sw(1500f, 220f, 0.22f), t => 0.25f * Mathf.Exp(-t * 18f), Sqr);
                return Make(name, b);

            case "herald_die":                              // glorious evolution, terminated
                b = Buf(1.5f);
                AddTone(b, 0f, 1.2f, Sw(300f, 60f, 1.2f), t => 0.5f * Mathf.Exp(-t * 4f), Saw);
                AddNoise(b, 0f, 1.1f, t => 0.8f * Mathf.Exp(-t * 5f), 3000f);
                AddNoise(b, 0f, 0.9f, t => 0.45f * Mathf.Exp(-t * 8f), 700f);
                AddBrown(b, 0f, 1.3f, t => 0.5f * Mathf.Exp(-t * 3.5f), 350f);
                AddTone(b, 0.5f, 0.6f, Sw(1200f, 300f, 0.6f), t => 0.15f * Mathf.Exp(-t * 8f), Sqr);
                for (int i = 0; i < 12; i++)
                    AddNoise(b, 0.3f + Random.Range(0f, 1f), 0.01f, t => 0.35f * Mathf.Exp(-t * 400f), 9000f);
                return Make(name, b);

            case "grab":
                b = Buf(0.4f);
                AddTone(b, 0f, 0.35f, Sw(240f, 70f, 0.35f), t => 0.35f * Mathf.Exp(-t * 14f), Saw);
                AddNoise(b, 0f, 0.3f, t => 0.3f * Mathf.Exp(-t * 12f), 1400f);
                return Make(name, b);

            // ---- the broken vessel and the third reality
            case "roar":                                  // a throat that lost its owner
                b = Buf(1.15f);
                AddTone(b, 0f, 1.05f, Sw(150f, 58f, 1.05f),
                        t => 0.55f * (t < 0.05f ? t / 0.05f : Mathf.Exp(-(t - 0.05f) * 3f)), Saw);
                AddTone(b, 0f, 1.05f, t => 68f + 24f * Mathf.Sin(2f * Mathf.PI * 6.5f * t),
                        t => 0.5f * Mathf.Exp(-t * 3.2f));
                AddNoise(b, 0f, 0.95f,
                        t => 0.5f * (t < 0.04f ? t / 0.04f : Mathf.Exp(-(t - 0.04f) * 4.2f)), 1500f);
                AddBrown(b, 0f, 1.1f, t => 0.45f * Mathf.Exp(-t * 2.8f), 320f);
                for (int i = 0; i < 7; i++)              // the rattle in the rubble
                    AddNoise(b, 0.2f + Random.Range(0f, 0.75f), 0.02f,
                            t => 0.3f * Mathf.Exp(-t * 110f), 850f);
                return Make(name, b);

            case "drag":                                  // a heavy body over broken floor
                b = Buf(0.7f);
                AddNoise(b, 0f, 0.65f,
                        t => 0.42f * Mathf.Exp(-t * 3.4f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 13f * t)), 1300f);
                AddTone(b, 0f, 0.6f, t => 92f + 38f * Mathf.Sin(2f * Mathf.PI * 9f * t),
                        t => 0.24f * Mathf.Exp(-t * 4f), Saw);
                AddNoise(b, 0f, 0.6f, t => 0.2f * Mathf.Exp(-t * 3f), 4400f);   // grit
                return Make(name, b);

            case "sludge":                                // the vomit: wet, heavy, endless
                b = Buf(0.9f);
                AddNoise(b, 0f, 0.85f, t => 0.5f * Mathf.Exp(-t * 3.2f), 950f);
                AddTone(b, 0f, 0.85f, Sw(230f, 75f, 0.85f), t => 0.35f * Mathf.Exp(-t * 3.6f), Saw);
                for (int i = 0; i < 10; i++)             // lumps on the way out
                    AddTone(b, Random.Range(0f, 0.75f), 0.07f,
                           Sw(Random.Range(320f, 660f), Random.Range(90f, 160f), 0.07f),
                           t => 0.3f * Mathf.Exp(-t * 28f));
                return Make(name, b);

            case "acid_bubble":                          // the puddle keeps talking
                b = Buf(0.24f);
                AddTone(b, 0f, 0.18f, Sw(340f, 1250f, 0.12f), t => 0.32f * Mathf.Exp(-t * 15f));
                AddTone(b, 0.06f, 0.15f, Sw(500f, 1750f, 0.10f), t => 0.22f * Mathf.Exp(-t * 21f));
                AddNoise(b, 0f, 0.035f, t => 0.16f * Mathf.Exp(-t * 85f), 2600f);
                return Make(name, b);

            case "acid_splash":                          // rain meeting the ruin
                b = Buf(0.36f);
                AddNoise(b, 0f, 0.3f, t => 0.5f * Mathf.Exp(-t * 13f), 3400f);
                AddTone(b, 0f, 0.26f, Sw(640f, 140f, 0.26f), t => 0.34f * Mathf.Exp(-t * 15f));
                AddTone(b, 0.02f, 0.2f, Sw(1500f, 430f, 0.2f), t => 0.15f * Mathf.Exp(-t * 21f));
                return Make(name, b);

            case "rust_beam":                            // thick, grinding, slow
                b = Buf(1.25f);
                AddTone(b, 0f, 1.2f, t => 60f + 9f * Mathf.Sin(2f * Mathf.PI * 5f * t),
                        t => (t < 0.05f ? t / 0.05f : Mathf.Max(0f, 1f - (t - 0.9f) / 0.3f)) * 0.4f, Saw);
                AddTone(b, 0f, 1.2f, Const(185f),
                        t => (t < 0.05f ? t / 0.05f : Mathf.Max(0f, 1f - t / 1.2f)) * 0.16f, Sqr);
                AddNoise(b, 0f, 1.2f,
                        t => 0.3f * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 31f * t)) *
                             (t < 0.05f ? t / 0.05f : Mathf.Max(0f, 1f - t / 1.2f)), 5200f);
                AddBrown(b, 0f, 1.2f, t => 0.32f * Mathf.Exp(-t * 1.6f), 250f);
                return Make(name, b);

            case "shatter":                              // a chunk deciding to be rubble
                b = Buf(0.5f);
                AddNoise(b, 0f, 0.35f, t => 0.55f * Mathf.Exp(-t * 17f), 7000f);
                AddNoise(b, 0f, 0.11f, t => 0.4f * Mathf.Exp(-t * 50f), 420f);
                AddTone(b, 0f, 0.3f, Sw(880f, 250f, 0.3f), t => 0.2f * Mathf.Exp(-t * 13f), Sqr);
                for (int i = 0; i < 7; i++)              // the metal rings after the break
                    AddTone(b, Random.Range(0f, 0.3f), 0.09f, Const(Random.Range(1400f, 3300f)),
                            t => 0.12f * Mathf.Exp(-t * 38f));
                return Make(name, b);

            case "turret_up":                            // the vessel roots itself
                b = Buf(1.15f);
                AddTone(b, 0f, 1.0f, Sw(70f, 430f, 1.0f), t => 0.3f * (t / 1.0f), Saw);
                AddNoise(b, 0f, 1.0f, t => 0.24f * (t / 1.0f), 2600f);
                AddTone(b, 0.9f, 0.22f, Sw(430f, 120f, 0.22f), t => 0.5f * Mathf.Exp(-t * 17f), Saw);
                AddNoise(b, 0.9f, 0.12f, t => 0.45f * Mathf.Exp(-t * 38f), 900f);
                return Make(name, b);

            case "collapse":                             // the floor giving way to the void
                b = Buf(1.7f);
                AddTone(b, 0f, 1.5f, Sw(230f, 32f, 1.5f), t => 0.45f * Mathf.Exp(-t * 2f), Saw);
                AddBrown(b, 0f, 1.6f, t => 0.62f * Mathf.Exp(-t * 2.2f), 230f);
                AddNoise(b, 0f, 1.5f, t => 0.34f * Mathf.Exp(-t * 2.4f), 1700f);
                AddNoise(b, 0f, 1.3f, t => 0.2f * Mathf.Exp(-t * 2.8f), 5200f);
                return Make(name, b);

            // ---- SHEPARD and the fourth reality
            case "shepard_rifle":                        // your rifle, in another hand
                b = Buf(0.14f);
                AddNoise(b, 0f, 0.09f, t => 0.55f * Mathf.Exp(-t * 60f), 5200f);
                AddTone(b, 0f, 0.07f, Sw(1500f, 320f, 0.07f), t => 0.4f * Mathf.Exp(-t * 70f), Sqr);
                AddTone(b, 0f, 0.11f, Const(150f), t => 0.25f * Mathf.Exp(-t * 30f), Saw);
                return Make(name, b);

            case "shepard_sniper":                       // one shot, no apology
                b = Buf(0.75f);
                AddNoise(b, 0f, 0.05f, t => 0.8f * Mathf.Exp(-t * 140f), 12000f);
                AddTone(b, 0f, 0.16f, Sw(2400f, 180f, 0.16f), t => 0.6f * Mathf.Exp(-t * 26f), Sqr);
                AddTone(b, 0f, 0.55f, Sw(140f, 44f, 0.55f), t => 0.5f * Mathf.Exp(-t * 6f), Saw);
                AddBrown(b, 0f, 0.7f, t => 0.35f * Mathf.Exp(-t * 4.5f), 260f);
                return Make(name, b);

            case "blade_flash":                         // bright blue, then nothing kind
                b = Buf(0.42f);
                AddTone(b, 0f, 0.34f, Sw(240f, 2100f, 0.34f), t => 0.3f * (t / 0.34f), Sqr);
                AddNoise(b, 0f, 0.3f, t => 0.14f * (t / 0.3f), 9500f);
                AddTone(b, 0.3f, 0.1f, Const(2600f), t => 0.2f * Mathf.Exp(-t * 40f));
                return Make(name, b);

            case "blade_slash":                         // the swing after the flash
                b = Buf(0.4f);
                AddNoise(b, 0f, 0.2f, t => 0.6f * Mathf.Exp(-t * 16f), 6500f);
                AddTone(b, 0f, 0.24f, Sw(1900f, 260f, 0.24f), t => 0.4f * Mathf.Exp(-t * 18f), Saw);
                AddTone(b, 0.03f, 0.3f, Sw(190f, 62f, 0.3f), t => 0.35f * Mathf.Exp(-t * 9f));
                return Make(name, b);

            case "clunk":                               // stasis landing: the world holds its breath
                b = Buf(0.9f);
                AddTone(b, 0f, 0.35f, Sw(190f, 62f, 0.35f), t => 0.65f * Mathf.Exp(-t * 12f), Saw);
                AddBrown(b, 0f, 0.6f, t => 0.4f * Mathf.Exp(-t * 7f), 220f);
                AddNoise(b, 0f, 0.06f, t => 0.5f * Mathf.Exp(-t * 90f), 3200f);
                AddTone(b, 0.01f, 0.8f, Const(92f), t => 0.16f * Bell(0.25f, 0.4f)(t));   // the ring after
                return Make(name, b);

            case "stasis_fire":                         // the orb leaving the barrel
                b = Buf(0.4f);
                AddTone(b, 0f, 0.36f, Sw(760f, 310f, 0.36f), t => 0.3f * Bell(0.1f, 0.28f)(t));
                AddTone(b, 0f, 0.34f, Const(1520f), t => 0.1f * Bell(0.08f, 0.3f)(t));
                AddNoise(b, 0f, 0.2f, t => 0.12f * Mathf.Exp(-t * 9f), 4200f);
                return Make(name, b);

            case "ghost":                               // three dead bosses answer at once
                b = Buf(1.9f);
                AddTone(b, 0f, 1.7f, t => 110f + 5f * Mathf.Sin(2f * Mathf.PI * 3.5f * t),
                        t => 0.24f * Bell(0.4f, 1.2f)(t), Saw);
                AddTone(b, 0.1f, 1.6f, Const(440f), t => 0.1f * Bell(0.5f, 1.1f)(t));
                AddTone(b, 0.2f, 1.5f, Const(660.5f), t => 0.07f * Bell(0.6f, 1.0f)(t));
                AddTone(b, 0.3f, 1.4f, Const(880f), t => 0.05f * Bell(0.7f, 0.9f)(t));
                AddNoise(b, 0f, 1.6f, t => 0.12f * Mathf.Exp(-t * 1.4f), 7000f);
                return Make(name, b);

            case "vhs_beep":                            // 1... 2... 3...
                b = Buf(0.14f);
                AddTone(b, 0f, 0.11f, Const(1240f), t => 0.3f * (t < 0.02f ? t / 0.02f : Mathf.Exp(-(t - 0.02f) * 26f)), Sqr);
                AddTone(b, 0f, 0.1f, Const(2480f), t => 0.08f * Mathf.Exp(-t * 30f));
                return Make(name, b);

            case "vhs_stop":                            // the tape giving the picture back
                b = Buf(0.55f);
                AddNoise(b, 0f, 0.12f, t => 0.5f * Mathf.Exp(-t * 30f), 5200f);
                AddTone(b, 0f, 0.4f, Sw(520f, 55f, 0.4f), t => 0.4f * Mathf.Exp(-t * 8f), Saw);
                AddTone(b, 0.1f, 0.25f, Const(70f), t => 0.5f * Mathf.Exp(-t * 12f));
                return Make(name, b);

            case "melt":                                // the horde turning into sludge
                b = Buf(1.2f);
                AddNoise(b, 0f, 1.1f, t => 0.45f * Mathf.Exp(-t * 2.6f), 800f);
                AddTone(b, 0f, 1.05f, Sw(300f, 60f, 1.05f), t => 0.32f * Mathf.Exp(-t * 3f), Saw);
                for (int i = 0; i < 14; i++)             // everything coming apart at once
                    AddTone(b, Random.Range(0f, 0.9f), 0.08f,
                            Sw(Random.Range(420f, 900f), Random.Range(80f, 150f), 0.08f),
                            t => 0.22f * Mathf.Exp(-t * 24f));
                return Make(name, b);

            case "butcher_gun":                         // meat coughing pellets
                b = Buf(0.42f);
                AddNoise(b, 0f, 0.28f, t => 0.65f * Mathf.Exp(-t * 13f), 3400f);
                AddTone(b, 0f, 0.26f, Sw(420f, 90f, 0.26f), t => 0.45f * Mathf.Exp(-t * 13f), Saw);
                AddNoise(b, 0.02f, 0.3f, t => 0.2f * Mathf.Exp(-t * 9f), 900f);
                return Make(name, b);

            case "shepard_leap":                         // his tape taking him back
                b = Buf(1.5f);
                AddTone(b, 0f, 1.1f, Sw(150f, 1700f, 1.1f),        // the rewind climbing
                        t => 0.3f * Mathf.Pow(t / 1.1f, 1.7f));
                AddNoise(b, 0f, 1.2f, t => 0.2f * Mathf.Pow(t / 1.2f, 2f), 6500f);
                for (int i = 0; i < 5; i++)                         // the clock rushing with it
                    AddTone(b, 0.1f + i * 0.19f, 0.05f, Const(1500f + i * 260f),
                            t => 0.16f * Mathf.Exp(-t * 90f), Sqr);
                AddTone(b, 1.08f, 0.4f, Sw(760f, 58f, 0.4f),        // and the landing
                        t => 0.5f * Mathf.Exp(-t * 8f), Saw);
                AddTone(b, 1.08f, 0.34f, Const(62f), t => 0.55f * Mathf.Exp(-t * 6.5f));
                AddBrown(b, 1.08f, 0.4f, t => 0.4f * Mathf.Exp(-t * 5.5f), 220f);
                return Make(name, b);

            case "tick":                                   // the clock, wherever it still runs
                b = Buf(0.1f);
                AddTone(b, 0f, 0.08f, Const(1850f), t => 0.3f * Mathf.Exp(-t * 58f), Sqr);
                AddTone(b, 0f, 0.05f, Const(920f), t => 0.16f * Mathf.Exp(-t * 75f));
                AddNoise(b, 0f, 0.025f, t => 0.16f * Mathf.Exp(-t * 170f), 7500f);
                return Make(name, b);

            // ---- the cutscenes: two voices, one letter at a time
            case "blip":                                   // YOU: bright, young, tense
                return MakeVoice(name, 425f, 0.25f, false, 90f, 0.002f, 0f, 0.06f, 0f, 0f);

            case "blip_shep":                              // SHEPARD: low, cold, tired
                return MakeVoice(name, 185f, 0.40f, false, 45f, 0.003f, 7f, 0.08f, 0f, 0f);

            case "scream":                                // YOU, shouting
                return MakeVoice(name, 500f, 0.30f, false, 55f, 0.001f, 0f, 0.09f, 0.30f, 2.4f);

            case "scream_shep":                           // SHEPARD, if he ever does
                return MakeVoice(name, 150f, 0.45f, false, 34f, 0.002f, 9f, 0.11f, 0.22f, 2.2f);

            case "tape_tick":                             // a faint tick, like a tape rewinding
                b = Buf(0.14f);
                AddNoise(b, 0f, 0.03f, t => 0.22f * Mathf.Exp(-t * 160f), 2600f);
                AddTone(b, 0f, 0.09f, Sw(1150f, 700f, 0.09f),
                        t => 0.16f * Mathf.Exp(-t * 46f), Sqr);
                AddTone(b, 0.005f, 0.07f, Const(340f), t => 0.10f * Mathf.Exp(-t * 60f));
                return Make(name, b);

            case "rage":                                  // the loudest the tape ever gets
                b = Buf(1.0f);
                AddBrown(b, 0f, 0.9f, t => 0.75f * Mathf.Exp(-t * 3.4f), 380f);
                AddTone(b, 0f, 0.85f, t => 132f + 40f * Mathf.Sin(2f * Mathf.PI * 6.5f * t),
                        t => (t < 0.02f ? t / 0.02f : Mathf.Exp(-(t - 0.02f) * 4.4f)) * 0.75f, Saw);
                AddTone(b, 0f, 0.8f, t => 198f + 60f * Mathf.Sin(2f * Mathf.PI * 9.5f * t),
                        t => 0.42f * Mathf.Exp(-t * 5f), Saw);
                AddNoise(b, 0f, 0.8f,
                        t => 0.55f * (t < 0.015f ? t / 0.015f : Mathf.Exp(-(t - 0.015f) * 5f)), 2400f);
                AddNoise(b, 0f, 0.35f, t => 0.3f * Mathf.Exp(-t * 9f), 7000f);      // the voice tearing
                for (int i = 0; i < 6; i++)
                    AddNoise(b, Random.Range(0.02f, 0.6f), 0.02f, t => 0.3f * Mathf.Exp(-t * 160f), 5200f);
                return Make(name, b);

            case "sigh":                                  // the breath after the joke
                b = Buf(0.7f);
                AddNoise(b, 0f, 0.6f, t => 0.30f * Bell(0.22f, 0.26f)(t), 1500f);
                AddTone(b, 0f, 0.6f, Sw(300f, 150f, 0.6f), t => 0.09f * Bell(0.24f, 0.24f)(t));
                return Make(name, b);
        }
        return null;
    }
}

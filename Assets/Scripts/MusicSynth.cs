using UnityEngine;

// ------------------------------------------------------------------- engine
// Every sample of the soundtrack is computed here, on the audio thread, out
// of nothing but math: two pulse channels (12.5/25/50 percent duty), a
// triangle bass, an LFSR noise kit for the drums, a palm-muted power-chord
// voice for the heavy layer and a couple of slow pads. There is no clip
// anywhere - the music is a sequencer driving oscillators in real time.
//
// The game thread never touches this state directly. It hands over requests
// (switch track, intensity, duck, mute) through the volatile slots below and
// the next render call picks them up, so nothing the audio thread reads can
// tear and there is never a lock on the audio thread.
public static class MusicSynth
{
    // ---- requests: written on the game thread, drained at the top of Render
    static volatile Player reqPlayer;
    static volatile float reqFade;
    static volatile bool reqArm;
    static volatile bool reqClear;
    static volatile int reqLevel = -1;
    static volatile float reqDuck = -1f, reqDuckTau;
    static volatile bool reqDuckArm;
    static volatile bool reqMute, reqMuteArm;
    static volatile bool reqRewind, reqRewindArm;
    static volatile float reqRewindSec;

    public const float Trim = 0.60f;          // the music sits under the sfx
    const float LayerTau = 0.30f;             // one intensity layer fades in this fast

    static bool ready;
    static int sr = 48000;
    static Player cur, nxt;
    static float xfLeft, xfDur;
    static int level;                         // 0..3: which layers are awake
    static float duck = 1f, duckTo = 1f, duckTau = 0.25f;
    static bool muted;

    // ---- the tape: the last few seconds of the mix, kept so the music can
    // be wound backwards (Shepard's freeze). While it rewinds the players
    // stand still, and the reversed audio is read straight out of this ring.
    const float TapeSec = 8f;
    static float[] tapeL, tapeR;
    static int tapeLen, tapeW;
    static bool rew;
    static float rewT, rewDur, rewBack;
    static int rampLeft, rampN;            // the little fade-up after the tape lets go

    // drum masks a pattern can put on a step
    public const int Kick = 1, Snare = 2, Hat = 4, Open = 8, Clank = 16,
                     Tom = 32, Ride = 64, Crash = 128;

    // drum kinds
    const int KKick = 0, KSnare = 1, KHat = 2, KOpen = 3, KRide = 4, KCrash = 5,
              KClank = 6, KTom = 7, KBeep = 8, KSweep = 9;
    const int KindCount = 10;
    static readonly int[] KindSize = { 4, 4, 6, 3, 3, 2, 3, 3, 3, 2 };

    public static int Level { get { return level; } }
    public static bool Muted { get { return muted; } }
    public static string Current
    {
        get { return cur != null ? cur.T.id : (nxt != null ? nxt.T.id : null); }
    }

    // ------------------------------------------------------- game-thread API
    public static void Request(MusicTrack t, float fade)
    {
        if (t == null) return;
        UseOutputRate();                        // the voices need the real rate
        MusicLib.Warm(t);                    // echo memory, built off the audio thread
        reqPlayer = new Player(t, fade <= 0f);
        reqFade = fade < 0f ? 0f : fade;
        reqArm = true;
    }

    public static void SetLevel(int l)
    {
        if (l < 0) l = 0; else if (l > 3) l = 3;
        reqLevel = l;
    }

    public static void SetDuck(float amount, float sec)
    {
        reqDuck = amount < 0f ? 0f : (amount > 1f ? 1f : amount);
        reqDuckTau = sec < 0f ? 0f : sec;
        reqDuckArm = true;
    }

    public static void SetMute(bool on) { reqMute = on; reqMuteArm = true; }

    // wind the music backwards for `sec` seconds (on = false lets go early)
    public static void SetRewind(bool on, float sec)
    {
        reqRewindSec = sec; reqRewind = on; reqRewindArm = true;
    }

    // a fresh run: whatever was playing dies where it stands
    public static void Clear()
    {
        reqClear = true;
        reqLevel = 0;
        reqDuck = 1f; reqDuckTau = 0.25f; reqDuckArm = true;
        reqMute = false; reqMuteArm = true;
    }

    static void UseOutputRate()
    {
        if (ready) return;
        int r = AudioSettings.outputSampleRate;
        sr = r >= 8000 ? r : 48000;
        tapeLen = (int)(TapeSec * sr);
        tapeL = new float[tapeLen];
        tapeR = new float[tapeLen];
        ready = true;
    }

    // the game thread pins the rate before the first render call can ask for
    // it: touching AudioSettings from the audio thread is not something to
    // rely on, and every voice sizes its envelope and its crush from this
    public static void Prepare() { UseOutputRate(); }

    static void Drain()
    {
        if (reqClear)
        {
            reqClear = false;
            cur = null; nxt = null; xfDur = 0f; xfLeft = 0f;
            rew = false; rampLeft = 0; tapeW = 0;
            if (tapeL != null) { System.Array.Clear(tapeL, 0, tapeLen); System.Array.Clear(tapeR, 0, tapeLen); }
        }
        if (reqRewindArm)
        {
            bool on = reqRewind; float sec = reqRewindSec;
            reqRewindArm = false;
            if (on && !rew && tapeL != null && (cur != null || nxt != null))
            {
                rew = true; rewT = 0f; rewBack = 1f;
                rewDur = sec > 0.3f ? sec : 3f;
            }
            else if (!on && rew)
                rewT = System.Math.Max(rewT, rewDur - 0.035f);   // into the fade-out tail
        }
        if (reqArm)
        {
            var p = reqPlayer; float f = reqFade;
            reqArm = false;
            if (p == null) { cur = null; nxt = null; xfDur = 0f; xfLeft = 0f; }
            else if (cur == null)
            {
                // the first track of a run: fade it up from silence, with no
                // outgoing side to the crossfade
                nxt = p; xfDur = f > 0f ? f : 0f; xfLeft = xfDur;
                if (f <= 0f) { cur = p; nxt = null; xfDur = 0f; }
            }
            else if (f <= 0f)                    // hard cut: the old track is gone
            {
                cur = p; nxt = null; xfDur = 0f; xfLeft = 0f;
            }
            else { nxt = p; xfDur = f; xfLeft = f; }
        }
        if (reqLevel >= 0) { level = reqLevel; reqLevel = -1; }
        if (reqDuckArm) { duckTo = reqDuck; duckTau = reqDuckTau; reqDuckArm = false; }
        if (reqMuteArm) { muted = reqMute; reqMuteArm = false; }
    }

    // -------------------------------------------------------- audio thread
    public static void Render(float[] data, int channels)
    {
        UseOutputRate();
        Drain();

        if (channels < 1) channels = 1;
        int n = data.Length;
        int frames = n / channels;
        float dt = frames / (float)sr;

        // the duck rides everything the game asks of the music
        if (duck != duckTo)
        {
            if (duckTau <= 0.015f) duck = duckTo;
            else
            {
                duck += (duckTo - duck) * (1f - Mathf.Exp(-dt / duckTau));
                float d = duckTo - duck;
                if (d < 0.002f && d > -0.002f) duck = duckTo;
            }
        }

        for (int i = 0; i < n; i++) data[i] = 0f;

        bool wasTape = rew && tapeL != null;
        if (wasTape) TapeRender(data, channels, frames, dt);
        else if (cur != null && nxt != null && xfDur > 0f)
        {
            float k = 1f - xfLeft / xfDur;
            if (k < 0f) k = 0f; else if (k > 1f) k = 1f;
            float wa = Mathf.Cos(k * 1.5707963f);      // equal power: no dip mid-fade
            float wb = Mathf.Sin(k * 1.5707963f);
            cur.Render(data, channels, wa, dt);
            nxt.Render(data, channels, wb, dt);
            xfLeft -= dt;
            if (xfLeft <= 0f) { cur = nxt; nxt = null; xfDur = 0f; }
        }
        else if (nxt != null)
        {
            float k = xfDur > 0f ? 1f - xfLeft / xfDur : 1f;
            if (k < 0f) k = 0f; else if (k > 1f) k = 1f;
            nxt.Render(data, channels, Mathf.Sin(k * 1.5707963f), dt);
            if (xfDur > 0f) xfLeft -= dt;
            if (xfLeft <= 0f || xfDur <= 0f) { cur = nxt; nxt = null; xfDur = 0f; }
        }
        else if (cur != null) cur.Render(data, channels, 1f, dt);
        else return;                                    // nothing scheduled: silence

        if (!wasTape && tapeL != null)
        {
            Record(data, channels, frames);
            if (rampLeft > 0) Ramp(data, channels, frames);
        }

        float g = Trim * duck * (muted ? 0f : 1f) * (Sfx.Volume / Sfx.Nominal);
        if (g <= 0f)
        {
            for (int i = 0; i < n; i++) data[i] = 0f;
            return;
        }
        for (int i = 0; i < n; i++) data[i] = (float)System.Math.Tanh(data[i] * g);
    }

    // ------------------------------------------------------------- the tape
    // every live block goes into the ring (before the duck, so a rewind plays
    // what the music was, not what the game had turned it down to)
    static void Record(float[] data, int channels, int frames)
    {
        for (int f = 0; f < frames; f++)
        {
            int o = f * channels;
            float l = data[o];
            float r = channels > 1 ? data[o + 1] : l;
            tapeL[tapeW] = l; tapeR[tapeW] = r;
            if (++tapeW >= tapeLen) tapeW = 0;
        }
    }

    // after the tape lets go the live music comes back up over a few ms:
    // the read head is somewhere in the past, so a straight cut would click
    static void Ramp(float[] data, int channels, int frames)
    {
        for (int f = 0; f < frames && rampLeft > 0; f++, rampLeft--)
        {
            float k = 1f - rampLeft / (float)rampN;
            int o = f * channels;
            for (int c = 0; c < channels; c++) data[o + c] *= k;
        }
    }

    // read the ring backwards at a speed that whirrs up and eases off: the
    // resampling is what lifts the pitch, exactly like a tape winding back.
    // It starts at normal speed in reverse, so it joins the live sound with
    // no jump, and fades out over its last few ms.
    static void TapeRender(float[] data, int channels, int frames, float dt)
    {
        float inv = 1f / sr;
        for (int f = 0; f < frames; f++)
        {
            float t = rewT + f * inv;
            float u = t / rewDur; if (u > 1f) u = 1f;
            float sp = 1f + 2.4f * Mathf.Sin(3.1415927f * u);
            rewBack += sp;
            if (rewBack > tapeLen - 4) rewBack = tapeLen - 4;

            float rp = tapeW - rewBack;
            while (rp < 0f) rp += tapeLen;
            int i0 = (int)rp; float fr = rp - i0;
            int i1 = i0 + 1; if (i1 >= tapeLen) i1 = 0;
            float l = tapeL[i0] + (tapeL[i1] - tapeL[i0]) * fr;
            float r = tapeR[i0] + (tapeR[i1] - tapeR[i0]) * fr;

            float left = rewDur - t;
            float gg = left >= 0.035f ? 1f : (left <= 0f ? 0f : left / 0.035f);
            int o = f * channels;
            if (channels == 1) data[o] += (l + r) * 0.5f * gg;
            else { data[o] += l * gg; data[o + 1] += r * gg; }
        }
        rewT += dt;
        if (rewT >= rewDur)
        {
            rew = false;
            rampN = rampLeft = System.Math.Max(1, (int)(0.03f * sr));
        }
    }

    // ------------------------------------------------------------- a player
    // One running copy of a track: its own position in the pattern, its own
    // oscillators and its own layer gains, so two of them can be rendered at
    // once while a crossfade runs.
    sealed class Player
    {
        public readonly MusicTrack T;
        readonly int[] barLen;

        float bpm, sLen, sAcc;               // sLen = samples in one 16th note
        int step, bar, inBar;
        long absBar;
        bool inGap, snap;

        public readonly float[] lg = new float[4];   // eased layer gains

        Tone lead, arp, bass, sus1, sus2;
        Heavy heavy;
        Drum[] all;
        int[] kindOff, kindNext;

        float gLead, gArp, gBass, gHeavy, gDrums, gPad;
        bool crushed;
        uint rng;

        float[] echo; int echoPos, echoLen, echoDelay;

        public Player(MusicTrack track, bool snapLayers)
        {
            T = track;
            snap = snapLayers;
            barLen = track.barLen;
            bpm = track.bpm;
            sLen = StepSamples(bpm);
            inGap = track.Gap(0);
            rng = (uint)System.Environment.TickCount * 2654435761u + 1u;
            if (rng == 0) rng = 1u;

            lead = new Tone();
            arp = new Tone();
            bass = new Tone();
            sus1 = new Tone();
            sus2 = new Tone();
            heavy = new Heavy();

            int leadWave = track.boxLead ? Tone.Sine : Tone.Pulse;
            lead.Set(leadWave, track.leadDuty, track.panLead, track.leadVib,
                     track.leadVibRate, track.wobble);
            lead.Env(track.leadAtk, track.leadDec, track.leadSus, track.leadRel,
                     track.leadGlide);
            if (track.crushAll) lead.Crush(track.crush);

            arp.Set(Tone.Pulse, track.arpDuty, track.panArp, 0f, 0f, track.wobble);
            arp.Env(0.002f, track.arpDec, track.arpSus, 0.04f, 0f);
            if (track.crushAll) arp.Crush(track.crush);
            crushed = track.crushAll;          // crushAtMax flips this at level 3

            bass.Set(Tone.Tri, 0f, 0f, 0f, 0f, track.wobble * 0.5f);
            bass.Env(0.002f, 0.12f, track.bassSus, 0.04f, 0f);

            float wob = track.wobble * 0.5f;
            sus1.Set(Tone.Pulse, 0.5f, -0.18f, 0f, 0f, wob);
            sus2.Set(Tone.Pulse, 0.5f, 0.18f, 0f, 0f, wob);
            sus1.Env(0.35f, 0.6f, 0.8f, 0.7f, 0f);
            sus2.Env(0.40f, 0.6f, 0.8f, 0.7f, 0f);

            heavy.Setup(track.heavyDrive, track.crush, track.heavySus, track.panHeavy);

            kindOff = new int[KindCount + 1];
            int total = 0;
            for (int k = 0; k < KindCount; k++) { kindOff[k] = total; total += KindSize[k]; }
            kindOff[KindCount] = total;
            all = new Drum[total];
            kindNext = new int[KindCount];
            for (int i = 0; i < total; i++) all[i] = new Drum();

            if (track.echo && track.echoBuf != null)
            {
                echo = track.echoBuf;
                echoLen = echo.Length;
                echoDelay = (int)(track.echoTime * sr);
                if (echoDelay < 1) echoDelay = 1;
                if (echoDelay >= echoLen) echoDelay = echoLen - 1;
            }
        }

        float StepSamples(float beatsPerMinute)
        {
            // one step = one 16th note
            return sr * 60f / Mathf.Max(20f, beatsPerMinute) / 4f;
        }

        float Rand()
        {
            uint x = rng;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            rng = x;
            return (x & 0xFFFFFF) / 16777216f;
        }

        float At(int layer) { return layer >= 0 && layer < 4 ? lg[layer] : 0f; }

        void EaseLayers(float dt)
        {
            float k = snap ? 1f : 1f - Mathf.Exp(-dt / LayerTau);
            for (int i = 0; i < 4; i++)
            {
                float want = level >= i ? 1f : 0f;
                lg[i] += (want - lg[i]) * k;
                if (snap) lg[i] = want;
            }
            snap = false;

            bool top = level >= 3;
            if (T.crushAtMax && top != crushed)        // phase two ground into bits
            {
                crushed = top;
                lead.Crush(top ? T.crush : 0f);
                arp.Crush(top ? T.crush : 0f);
            }
            gBass = T.gBass * At(T.layBass);
            gDrums = T.gDrums * At(T.layDrums);
            gArp = T.gArp * At(T.layArp);
            gLead = T.gLead * At(T.layLead) * (top ? T.topLeadTrim : 1f);
            gHeavy = T.gHeavy * At(T.layHeavy);
            gPad = (T.padGain + T.droneGain) * At(T.layPad);
        }

        public void Render(float[] data, int channels, float w, float dt)
        {
            EaseLayers(dt);

            int frames = channels == 1 ? data.Length : data.Length / channels;
            float inv = 1f / sr;

            for (int i = 0; i < frames; i++)
            {
                sAcc += 1f;
                if (sAcc >= sLen) { sAcc -= sLen; Advance(); }

                float L = 0f, R = 0f, s;

                if (gLead > 0.0001f && lead.Active)
                {
                    s = lead.Tick(inv);
                    L += s * lead.pl * gLead; R += s * lead.pr * gLead;
                }
                if (gArp > 0.0001f && arp.Active)
                {
                    s = arp.Tick(inv);
                    L += s * arp.pl * gArp; R += s * arp.pr * gArp;
                }
                if (gBass > 0.0001f && bass.Active)
                {
                    s = bass.Tick(inv);
                    L += s * bass.pl * gBass; R += s * bass.pr * gBass;
                }
                if (gHeavy > 0.0001f && heavy.Active)
                {
                    s = heavy.Tick(inv);
                    L += s * heavy.pl * gHeavy; R += s * heavy.pr * gHeavy;
                }
                if (gPad > 0.0001f)
                {
                    if (sus1.Active)
                    {
                        s = sus1.Tick(inv);
                        L += s * sus1.pl * gPad; R += s * sus1.pr * gPad;
                    }
                    if (sus2.Active)
                    {
                        s = sus2.Tick(inv);
                        L += s * sus2.pl * gPad; R += s * sus2.pr * gPad;
                    }
                }
                if (gDrums > 0.0001f) DrumMix(ref L, ref R, inv);

                if (echo != null)
                {
                    float dry = (L + R) * 0.5f;
                    int dp = echoPos - echoDelay;
                    if (dp < 0) dp += echoLen;
                    float d = echo[dp];
                    L += d * T.echoMix;
                    R += d * T.echoMix * 0.92f;
                    echo[echoPos] = dry * 0.6f + d * 0.42f;
                    echoPos++;
                    if (echoPos >= echoLen) echoPos = 0;
                }

                if (channels == 1) data[i] += (L + R) * 0.5f * w;
                else
                {
                    int o = i << 1;
                    data[o] += L * w;
                    data[o + 1] += R * w;
                }
            }
        }

        void DrumMix(ref float L, ref float R, float inv)
        {
            for (int i = 0; i < all.Length; i++)
            {
                var d = all[i];
                if (!d.Active) continue;
                float s = d.Tick(inv);
                if (s == 0f) continue;
                L += s * d.pl * gDrums;
                R += s * d.pr * gDrums;
            }
        }

        // ---------------------------------------------------- the sequencer
        void Advance()
        {
            // phase two rides the top intensity: some bosses speed up there
            float want = (level >= 3 && T.bpm2 > 0f) ? T.bpm2 : T.bpm;
            if (want != bpm) { bpm = want; sLen = StepSamples(bpm); }

            int i = step;
            bool gap = inGap;
            bool cut = T.glitch > 0f && Rand() < T.glitch;   // the void eats a note
            bool top = level >= 3;
            int oct = top ? T.octaveAtMax : 0;

            // ---- the bar's sustains (pads, the drone) come in on its downbeat
            if (gPad > 0.005f && inBar == 0 && !gap)
            {
                int root = T.root + T.chordSemis[bar];
                int g = barLen[bar];                 // a whole bar, counted in 16ths
                if (T.drone) { sus1.On(root - 12, g, sLen); sus2.On(root - 5, g, sLen); }
                else if (T.padCluster) { sus1.On(root, g, sLen); sus2.On(root + 1, g, sLen); }
                else { sus1.On(root, g, sLen); sus2.On(root + 7, g, sLen); }
            }

            // ---- drums
            if (!gap && !cut && gDrums > 0.01f)
            {
                int m = T.drums[i];
                if (m != 0) Hits(m);

                byte f = T.fx[i];
                if ((f & 1) != 0) Hit(KSweep, 1f, 0f);      // a rewind tears past
                if ((f & 2) != 0) Hit(KBeep, 1f, 0f);
            }
            if (!gap && gHeavy > 0.02f)                      // the heavy layer's kit
            {
                if (T.doubleKick && inBar % 2 == 0) Hit(KKick, 0.9f, 0f);
                if (T.blast && inBar % 4 == 2) Hit(KSnare, 0.55f, 0.12f);
            }

            // ---- bass: only once the track has reached its entry bar, and
            // never over a bar the void has eaten - a gap has to be a gap
            if (gBass > 0.01f && !cut && absBar >= T.bassFromBar && !gap)
            {
                short bn = T.bass[i];
                if (bn >= 0) bass.On(bn, T.bassGate[i], sLen);
            }

            // ---- arpeggio
            if (gArp > 0.01f && !cut && !gap)
            {
                short an = T.arp[i];
                if (an >= 0) arp.On(an + oct, T.arpGate[i], sLen);
            }

            // ---- lead
            if (gLead > 0.01f && !cut && !gap)
            {
                short ln = T.lead[i];
                if (ln >= 0) lead.On(ln + oct, T.leadGate[i], sLen);
            }

            // ---- the heavy layer: palm-muted power chords
            if (gHeavy > 0.01f && !gap && !cut)
            {
                if (T.gallopAtMax && top)
                {
                    int inBeat = inBar % 4;               // the gallop: x-x x per beat
                    if (inBeat == 0 || inBeat == 2 || inBeat == 3)
                        heavy.On(T.root + T.chordSemis[bar] - 12, 1.2f, sLen);
                }
                else
                {
                    short hn = T.heavy[i];
                    if (hn >= 0) heavy.On(hn, T.heavyGate[i], sLen);
                }
            }

            // ---- step bookkeeping: bars, gaps, the loop
            step++;
            if (step >= T.total) step = 0;
            inBar++;
            if (inBar >= barLen[bar])
            {
                inBar = 0;
                bar++;
                if (bar >= T.bars) bar = 0;
                absBar++;
                bool wasGap = inGap;
                inGap = T.Gap(bar);
                if (inGap && !wasGap)                     // the music gets eaten here
                {
                    lead.Off(); arp.Off(); bass.Off();
                    sus1.Off(); sus2.Off(); heavy.Off();
                }
            }
        }

        void Hits(int mask)
        {
            if ((mask & Kick) != 0) Hit(KKick, 1f, 0f);
            if ((mask & Snare) != 0) Hit(KSnare, 1f, 0f);
            if ((mask & Hat) != 0) Hit(KHat, 0.9f, -0.25f);
            if ((mask & Open) != 0) Hit(KOpen, 1f, -0.3f);
            if ((mask & Clank) != 0) Hit(KClank, 1f, 0.35f);   // the factory's iron
            if ((mask & Tom) != 0) Hit(KTom, 1f, -0.15f);
            if ((mask & Ride) != 0) Hit(KRide, 1f, 0.3f);
            if ((mask & Crash) != 0) Hit(KCrash, 1f, 0.1f);
        }

        void Hit(int kind, float gain, float pan)
        {
            int from = kindOff[kind], size = KindSize[kind];
            int j = kindNext[kind]++;
            if (j >= size) { j = 0; kindNext[kind] = 1; }
            all[from + j].Trig(kind, gain, pan, rng);
        }
    }

    // ------------------------------------------------------- a pulse channel
    // One oscillator with an envelope: attack, decay toward sustain while the
    // gate is open, release when it closes. Vibrato and tape wow ride the
    // frequency, and a little sample-and-hold is available for the bitcrush.
    sealed class Tone
    {
        public const int Pulse = 0, Tri = 1, Sine = 2;

        public float pl = 0.5f, pr = 0.5f;      // pan
        int wave, state;                        // 0 off, 1 attack, 2 decay, 3 release
        float ph, f, target, duty, env, gateS;
        float atkStep, decK, sus, relK, glideK;
        float vibA, vibR, vibPh, wobA, wobPh;
        int holdN = 1, holdC; float held, crushAmt;

        public bool Active { get { return state != 0; } }

        public void Set(int kind, float dutyCycle, float pan, float vib, float vibRate,
                        float wobble)
        {
            wave = kind;
            duty = dutyCycle <= 0f ? 0.5f : dutyCycle;
            pl = (1f - pan) * 0.5f;
            pr = (1f + pan) * 0.5f;
            vibA = vib; vibR = vibRate;
            wobA = wobble;
        }

        // times in seconds: attack, decay time constant, sustain level, release
        public void Env(float atk, float dec, float sustain, float release, float glide)
        {
            atkStep = 1f / Mathf.Max(1f, atk * sr);
            decK = 1f - Mathf.Exp(-1f / Mathf.Max(1f, dec * sr));
            sus = sustain;
            relK = Mathf.Exp(-1f / Mathf.Max(1f, release * sr));
            glideK = glide <= 0f ? 0f : 1f - Mathf.Exp(-1f / (glide * sr));
        }

        public void Crush(float amount)
        {
            crushAmt = amount;
            holdN = Mathf.Max(1, sr / 7000);
        }

        public void On(int midi, float gateSteps, float stepSamples)
        {
            target = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
            if (state == 0) { f = target; env = 0f; state = 1; }
            // retrigger mid-release: go back to attack from where the envelope
            // already is. Ramping up never clicks, and it is what gives a
            // percussive voice (the music box has no sustain to fall back on)
            // its strike back - decaying instead would make every note after
            // the first quieter than the one before it.
            else if (state == 3) state = 1;
            gateS = gateSteps * stepSamples;
        }

        public void Off() { if (state != 0 && state != 3) state = 3; }

        public float Tick(float inv)
        {
            if (state == 0) return 0f;

            if (state == 1)
            {
                env += atkStep;
                if (env >= 1f) { env = 1f; state = 2; }
            }
            if (state == 2)
            {
                gateS -= 1f;
                env += (sus - env) * decK;
                if (gateS <= 0f) state = 3;
                else if (env < 1e-4f && sus <= 1e-4f) { env = 0f; state = 0; return 0f; }
            }
            if (state == 3)
            {
                env *= relK;
                if (env < 1e-4f) { env = 0f; state = 0; return 0f; }
            }

            if (glideK > 0f && f != target)
            {
                f += (target - f) * glideK;
                if (target - f < 0.03f && f - target < 0.03f) f = target;
            }
            else f = target;

            float ff = f;
            if (vibA > 0f)
            {
                vibPh += 6.2831853f * vibR * inv;
                if (vibPh > 6.2831853f) vibPh -= 6.2831853f;
                ff *= 1f + vibA * Mathf.Sin(vibPh);
            }
            if (wobA > 0f)
            {
                wobPh += 6.2831853f * 0.73f * inv;
                if (wobPh > 6.2831853f) wobPh -= 6.2831853f;
                ff *= 1f + wobA * (Mathf.Sin(wobPh) + 0.45f * Mathf.Sin(wobPh * 4.7f));
            }

            ph += ff * inv;
            ph -= Mathf.Floor(ph);

            float s;
            if (wave == Tri)
            {
                s = 1f - 4f * Mathf.Abs(ph - 0.5f);
                s = Mathf.Floor(s * 8f + 0.5f) * 0.125f;      // the NES's stepped ramp
            }
            else if (wave == Sine) s = Mathf.Sin(ph * 6.2831853f);
            else s = ph < duty ? 1f : -1f;

            if (crushAmt > 0f)
            {
                if (--holdC <= 0) { holdC = holdN; held = s; }
                float q = Mathf.Round(held * 48f) * 0.020833334f;
                s += (q - s) * crushAmt;
            }
            return s * env;
        }
    }

    // --------------------------------------------- the heavy layer's chugs
    // root + fifth + octave, palm-muted, driven into a soft clip and, where
    // the track asks for it, sample-and-held into a light bitcrush.
    sealed class Heavy
    {
        public float pl = 0.5f, pr = 0.5f;
        int state;
        float ph1, ph2, ph3, f, env, gateS;
        float atkStep, decK, sus, relK;
        float drive = 3f, norm = 1f, crushAmt;
        int holdN = 6, holdC; float held;

        public bool Active { get { return state != 0; } }

        public void Setup(float driveAmount, float crush, float sustain, float pan)
        {
            drive = Mathf.Max(1f, driveAmount);
            norm = 1f / (float)System.Math.Tanh(drive);
            crushAmt = crush;
            sus = sustain;
            pl = (1f - pan) * 0.5f;
            pr = (1f + pan) * 0.5f;
            atkStep = 1f / (0.0025f * sr);
            decK = 1f - Mathf.Exp(-1f / (0.055f * sr));
            relK = Mathf.Exp(-1f / (0.018f * sr));
            holdN = Mathf.Max(1, sr / 7000);
        }

        public void On(int rootMidi, float gateSteps, float stepSamplesNow)
        {
            f = 440f * Mathf.Pow(2f, (rootMidi - 69) / 12f);
            gateS = gateSteps * stepSamplesNow;
            state = 1; env = 0f;
        }

        public void Off() { if (state != 0) state = 3; }

        public float Tick(float inv)
        {
            if (state == 0) return 0f;

            if (state == 1)
            {
                env += atkStep;
                if (env >= 1f) { env = 1f; state = 2; }
            }
            if (state == 2)
            {
                gateS -= 1f;
                env += (sus - env) * decK;
                if (gateS <= 0f) state = 3;
            }
            if (state == 3)
            {
                env *= relK;
                if (env < 1e-4f) { env = 0f; state = 0; return 0f; }
            }

            ph1 += f * inv; if (ph1 >= 1f) ph1 -= 1f;
            ph2 += f * 1.4983071f * inv; if (ph2 >= 1f) ph2 -= 1f;
            ph3 += f * 2f * inv; if (ph3 >= 1f) ph3 -= 1f;

            float s = ((ph1 < 0.5f ? 1f : -1f) + (ph2 < 0.5f ? 1f : -1f) +
                       (ph3 < 0.5f ? 1f : -1f)) * 0.3333333f;
            s = (float)System.Math.Tanh(s * drive) * norm;

            if (crushAmt > 0f)
            {
                if (--holdC <= 0) { holdC = holdN; held = s; }
                float q = Mathf.Round(held * 48f) * 0.020833334f;
                s += (q - s) * crushAmt;
            }
            return s * env;
        }
    }

    // ------------------------------------------------------------ the kit
    // One voice per drum slot, driven by the NES's 15-bit LFSR noise (7-bit
    // short mode for the metallic hits) plus a pitched body for the kick and
    // the toms. Slots are round-robined per kind, so a crash is never stolen
    // by the hat that follows it.
    sealed class Drum
    {
        public float pl = 0.5f, pr = 0.5f;
        int kind, state;
        float env, envK, atkStep, namp;
        int clk, clkN;
        ushort lfsr = 0x7FFF;
        bool shortMode;
        float bph, bf, bfStep, bfEnd, bamp, bodySq;
        int age;

        public bool Active { get { return state != 0; } }

        public void Trig(int k, float gain, float pan, uint seed)
        {
            kind = k;
            state = 1;
            env = 0f;
            age = 0;
            atkStep = 1f / (0.0006f * sr);
            pl = (1f - pan) * 0.5f;
            pr = (1f + pan) * 0.5f;
            lfsr = (ushort)(0x7FFF ^ (seed & 0x7FFF));
            if (lfsr == 0) lfsr = 0x7FFF;
            clk = 0;
            bph = 0f; bf = 0f; bfStep = 0f; bfEnd = 0f; bamp = 0f; bodySq = 0f;
            namp = 0f; shortMode = false; clkN = 0;

            switch (k)
            {
                case KKick:                                    // a thump with a click
                    envK = Dec(0.24f);
                    namp = 0.40f * gain; clkN = Div(3000f);
                    bamp = 1.0f * gain; bf = 150f; bfEnd = 46f;
                    bfStep = (46f - 150f) / (0.075f * sr);
                    break;

                case KSnare:
                    envK = Dec(0.14f);
                    namp = 0.95f * gain; clkN = Div(4300f);
                    bamp = 0.30f * gain; bf = 196f;
                    break;

                case KHat:
                    envK = Dec(0.035f);
                    namp = 0.55f * gain; clkN = Div(7200f); shortMode = true;
                    break;

                case KOpen:
                    envK = Dec(0.16f);
                    namp = 0.45f * gain; clkN = Div(6800f); shortMode = true;
                    break;

                case KRide:
                    envK = Dec(0.30f);
                    namp = 0.34f * gain; clkN = Div(6000f);
                    break;

                case KCrash:
                    envK = Dec(0.75f);
                    namp = 0.50f * gain; clkN = Div(5600f);
                    break;

                case KClank:                                   // iron being hit
                    envK = Dec(0.24f);
                    namp = 0.60f * gain; clkN = Div(2500f); shortMode = true;
                    bamp = 0.35f * gain; bf = 640f;
                    break;

                case KTom:
                    envK = Dec(0.20f);
                    namp = 0.30f * gain; clkN = Div(3000f);
                    bamp = 0.85f * gain; bf = 250f; bfEnd = 130f;
                    bfStep = (130f - 250f) / (0.14f * sr);
                    break;

                case KBeep:                                    // a sequencer blip
                    envK = Dec(0.10f);
                    namp = 0f;
                    bamp = 0.45f * gain; bf = 1760f; bodySq = 1f;
                    break;

                case KSweep:                                   // the tape rewinding
                    envK = Dec(0.55f);
                    namp = 0.55f * gain; clkN = Div(700f);
                    bamp = 0.35f * gain; bf = 320f; bfEnd = 3400f;
                    bfStep = (3400f - 320f) / (0.5f * sr);
                    break;
            }
        }

        float Dec(float seconds) { return Mathf.Exp(-1f / Mathf.Max(1f, seconds * sr)); }
        int Div(float hz) { int n = (int)(sr / Mathf.Max(50f, hz)); return n < 1 ? 1 : n; }

        public float Tick(float inv)
        {
            if (state == 0) return 0f;

            if (state == 1)
            {
                env += atkStep;
                if (env >= 1f) { env = 1f; state = 2; }
            }
            else
            {
                env *= envK;
                if (env < 1e-4f) { env = 0f; state = 0; return 0f; }
            }
            age++;

            // the noise clock: how fast the LFSR steps sets the brightness
            if (clkN > 0)
            {
                if (++clk >= clkN) { clk = 0; Step(); }
            }
            float n = (lfsr & 1) != 0 ? 1f : -1f;

            float body = 0f;
            if (bamp != 0f)
            {
                if (bfStep != 0f)
                {
                    bf += bfStep;
                    if (bfStep < 0f && bf <= bfEnd) { bf = bfEnd; bfStep = 0f; }
                    else if (bfStep > 0f && bf >= bfEnd) { bf = bfEnd; bfStep = 0f; }
                }
                bph += bf * inv;
                bph -= Mathf.Floor(bph);
                body = bodySq != 0f ? (bph < 0.5f ? 1f : -1f)
                                    : Mathf.Sin(bph * 6.2831853f);
            }

            if (kind == KSweep && (age & 511) == 0)
                clkN = Div(700f * (1f + 9f * age * inv / 0.5f));   // it climbs as it goes

            return (body * bamp + n * namp) * env;
        }

        void Step()
        {
            // 15-bit LFSR, taps at bit 0 and bit 2 (bit 6 in short/metallic mode)
            int fb = (lfsr ^ (lfsr >> (shortMode ? 6 : 2))) & 1;
            lfsr = (ushort)((lfsr >> 1) | (fb << 14));
            if (lfsr == 0) lfsr = 1;
        }
    }
}
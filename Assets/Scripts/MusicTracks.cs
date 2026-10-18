using System.Collections.Generic;
using UnityEngine;

// --------------------------------------------------------------- the record
// A track is data, not behaviour: tempo, meter, the scale it draws from, the
// chords it walks, the shared four-note leitmotif, and four lanes of pattern
// (bass, drums, arpeggio, lead) plus the heavy lane the top intensity layer
// opens up. The synth reads these arrays and never asks questions.
//
// Lanes are switched by intensity: layer 0 (SetIntensity(0)) is bass and
// drums, layer 1 adds the arpeggio, layer 2 the lead, layer 3 the heavy
// layer - palm-muted power chords, gallops, double kick and bitcrush.
public class MusicTrack
{
    public string id;
    public float bpm, bpm2;                  // bpm2: the tempo at top intensity
    public int bars, stepsPerBar, total;
    public int[] barSteps;                   // per-bar length, for ragged meters
    public bool[] barGap;                    // bars the void eats
    public int root;                         // the tonic, octave three
    public int[] scale;
    public int[] motif;                      // scale degrees: A-C-E-D, everywhere
    public bool reverseMotif;
    public int[] chordSemis, chordQual;      // one pair per bar
    public short[] lead, arp, bass, heavy;   // MIDI note per step, -1 = rest
    public byte[] leadGate, arpGate, bassGate, heavyGate;
    public byte[] drums, fx;                 // hit mask and fx mask per step
    public int[] barStart, barLen;           // filled by the pattern builder

    // which intensity layer wakes each lane up (4+ = never)
    public int layBass = 0, layDrums = 0, layArp = 1, layLead = 2, layHeavy = 3,
                layPad = 4;

    // mix and voice
    public float gBass = 0.55f, gDrums = 0.55f, gArp = 0.30f, gLead = 0.40f,
                 gHeavy = 0.42f;
    public float panLead = 0f, panArp = 0f, panHeavy = 0f;
    public float leadDuty = 0.5f, arpDuty = 0.25f;   // 0.125 / 0.25 / 0.5
    public float leadAtk = 0.004f, leadDec = 0.25f, leadSus = 0.55f, leadRel = 0.06f,
                 leadGlide = 0f;
    public float leadVib = 0.005f, leadVibRate = 5.4f;
    public float arpSus = 0.40f, arpDec = 0.15f, bassSus = 0.90f;
    public float wobble = 0f;                // tape wow
    public float glitch = 0f;                // per-step chance a note is eaten
    public float crush = 0f;                 // bitcrush depth
    public bool crushAll = false;            // crush the whole band, not just heavy
    public bool crushAtMax = false;          // ...and only on the top layer
    public float heavyDrive = 3.2f, heavySus = 0.35f;
    public bool gallopAtMax = false;         // top intensity: gallop off the chords
    public bool doubleKick = false, blast = false;
    public bool boxLead = false;             // music box: a sine with a bell envelope
    public bool echo = false;
    public float echoMix = 0.35f, echoTime = 0.35f;
    public float[] echoBuf;                  // built on the game thread
    public int bassFromBar = 0;              // absolute bar the bass enters
    public int octaveAtMax = 0;              // top intensity transposition
    public float topLeadTrim = 1f;
    public float padGain = 0f;               // slow pad (cluster if padCluster)
    public bool padCluster = false;          // root against its own semitone
    public float droneGain = 0f;             // the heavy drone, on the pad layer
    public bool drone = false;               // root + fifth below, held

    public int Steps(int bar) { return barSteps != null ? barSteps[bar] : stepsPerBar; }
    public bool Gap(int bar) { return barGap != null && barGap[bar]; }
}

// ---------------------------------------------------------------- the library
// Ten tracks: four maps that climb from arcade to doom metal, five bosses and
// the ending. Every one of them carries the same four notes.
public static class MusicLib
{
    // A-C-E-D, as scale degrees off the tonic: root, third, fifth, fourth.
    // Moved to each track's tonic and squeezed through each track's scale, it
    // reads as the same idea in every mood - that is the whole point.
    public static readonly int[] Motif = { 0, 2, 4, 3 };
    static readonly int[] Fall = { 4, 3, 1, 0 };          // the motif's shadow

    static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
    static readonly int[] Phrygian = { 0, 1, 3, 5, 7, 8, 10 };
    static readonly int[] HarmMinor = { 0, 2, 3, 5, 7, 8, 11 };

    // chord qualities a bar can carry
    const int QMinor = 0, QMajor = 1, QTritone = 2, QDim = 3, QUnresolved = 4;
    static readonly int[][] Qual =
    {
        new[] { 0, 3, 7 },        // minor
        new[] { 0, 4, 7 },        // major
        new[] { 0, 6, 12 },       // root against its own tritone
        new[] { 0, 3, 6 },        // diminished
        new[] { 0, 1, 3, 7 },     // minor with a flat second: no way home
    };

    static readonly Dictionary<string, MusicTrack> cache = new Dictionary<string, MusicTrack>();

    public static MusicTrack Get(string id)
    {
        MusicTrack t;
        if (cache.TryGetValue(id, out t) && t != null) return t;
        t = Build(id);
        if (t != null) cache[id] = t;
        return t;
    }

    // echo memory, sized for the device, made on the game thread
    public static void Warm(MusicTrack t)
    {
        if (t == null || !t.echo || t.echoBuf != null) return;
        int r = AudioSettings.outputSampleRate;
        if (r < 8000) r = 48000;
        t.echoBuf = new float[Mathf.CeilToInt((t.echoTime + 0.06f) * r)];
    }

    public static MusicTrack Build(string id)
    {
        switch (id)
        {
            case "factory": return Factory();
            case "lab": return Lab();
            case "void": return Void();
            case "cosmic": return Cosmic();
            case "smasher": return Smasher();
            case "herald": return Herald();
            case "vessel": return Vessel();
            case "shepard": return Shepard();
            case "shepard1v1": return Duel();
            case "ending": return Ending();
            default: return Factory();
        }
    }

    // --------------------------------------------------------- note helpers
    // A scale degree in the track's own scale, lifted by whole octaves
    public static int Deg(MusicTrack t, int degree, int oct)
    {
        var sc = t.scale;
        int n = sc.Length, o = 0;
        while (degree < 0) { degree += n; o--; }
        while (degree >= n) { degree -= n; o++; }
        return t.root + 12 * (oct + o) + sc[degree];
    }

    // the root of a bar's chord, in the tonic's octave
    static int ChordRoot(MusicTrack t, int bar) { return t.root + t.chordSemis[bar]; }

    // one tone of a bar's chord: idx walks the triad, and keeps climbing
    static int ChordNote(MusicTrack t, int bar, int idx, int oct)
    {
        var q = Qual[t.chordQual[bar]];
        int i = idx;
        int o = oct;
        while (i >= q.Length) { i -= q.Length; o++; }
        while (i < 0) { i += q.Length; o--; }
        return ChordRoot(t, bar) + 12 * o + q[i];
    }

    // -------------------------------------------------------- the leitmotif
    // The four notes, laid on a grid from `at` every `gap` steps of one bar.
    static void MotifAt(MusicTrack t, Pat p, int bar, int at, int gap, int oct, int gate)
    {
        for (int i = 0; i < 4; i++)
        {
            int s = at + i * gap;
            if (s < 0 || s >= p.N(bar)) break;
            int d = t.reverseMotif ? t.motif[3 - i] : t.motif[i];
            p.L(bar, s, Deg(t, d, oct), gate);
        }
    }

    // the same four notes, backwards, whoever is asking
    static void ReverseMotifAt(MusicTrack t, Pat p, int bar, int at, int gap, int oct, int gate)
    {
        for (int i = 0; i < 4; i++)
        {
            int s = at + i * gap;
            if (s < 0 || s >= p.N(bar)) break;
            p.L(bar, s, Deg(t, t.motif[3 - i], oct), gate);
        }
    }

    // -------------------------------------------------------- the riff box
    // The low root a bar's heavy riff is played from: the chord root two
    // octaves under the tonic's, dropped another octave for the high chords
    // so the riff never jumps up the neck.
    static int HRoot(MusicTrack t, int bar)
    {
        int r = ChordRoot(t, bar) - 12;
        if (t.chordSemis[bar] >= 7) r -= 12;
        return r;
    }

    // A bar of guitar written as text, one character per step, spaces ignored:
    //   .      rest
    //   0-9    palm-muted chug, that many semitones over the bar's root
    //   a-z    the same from ten up (a = 10, c = 12: the octave)
    //   O      the root, left ringing; A-Z are the long-ringing versions
    // The bass doubles the riff note for note, so the whole thing is one
    // unison chug.
    static void Riff(MusicTrack t, Pat p, int bar, string pat, bool bassDoubles)
    {
        int r = HRoot(t, bar);
        int n = p.N(bar), s = 0;
        for (int k = 0; k < pat.Length && s < n; k++)
        {
            char c = pat[k];
            if (c == ' ') continue;
            if (c != '.')
            {
                int off, gate = 1;
                if (c == 'O') { off = 0; gate = 4; }
                else if (c >= '0' && c <= '9') off = c - '0';
                else if (c >= 'a' && c <= 'z') off = 10 + (c - 'a');
                else { off = 10 + (c - 'A'); gate = 4; }
                p.H(bar, s, r + off, gate);
                if (bassDoubles) p.Bs(bar, s, r + off, gate);
            }
            s++;
        }
    }

    static MusicTrack New(string id, float bpm, int root, int[] scale = null)
    {
        var t = new MusicTrack();
        t.id = id;
        t.bpm = bpm;
        t.root = root;
        t.scale = scale != null ? scale : Minor;
        t.motif = Motif;                       // everyone gets the same four notes
        t.stepsPerBar = 16;
        return t;
    }

    // ============================================================ MAP ONE
    // SCI-FI FACTORY: A minor, 150. Bouncy and bright, industrial clanks on
    // the offbeats, and a heavy layer so light it barely counts as metal.
    static MusicTrack Factory()
    {
        var t = New("factory", 150f, 57);
        t.chordSemis = new[] { 0, 10, 8, 10 };            // i - VII - VI - VII
        t.chordQual = new[] { QMinor, QMajor, QMajor, QMajor };
        t.leadDuty = 0.5f; t.arpDuty = 0.25f;
        t.gHeavy = 0.30f; t.heavySus = 0.25f;             // almost no metal
        t.heavyDrive = 2.4f;
        var p = new Pat(t, 4);

        for (int b = 0; b < 4; b++)
        {
            int r = ChordRoot(t, b) - 12;

            for (int s = 0; s < 16; s += 2)               // bouncy octave eighths
                p.Bs(b, s, s % 4 == 0 ? r : r + 12, s == 14 ? 1 : 2);

            p.D(b, 0, K | H); p.D(b, 2, H); p.D(b, 4, S | H); p.D(b, 6, K | H);
            p.D(b, 8, K | H); p.D(b, 10, H); p.D(b, 12, S | H); p.D(b, 14, H);
            p.D(b, 3, C); p.D(b, 7, C);                    // the line's iron
            if (b == 0) p.D(b, 0, X);
            if (b == 3) { p.D(b, 13, TM); p.D(b, 15, C); }

            int[] up = { 0, 1, 2, 1 };
            for (int s = 0, i = 0; s < 16; s += 2, i++)    // eighth-note arpeggio
                p.A(b, s, ChordNote(t, b, up[i % 4], 1), 2);

            for (int s = 0; s < 16; s += 4)                // quarter palm mutes
                p.H(b, s, r, 2);
        }

        MotifAt(t, p, 0, 0, 4, 1, 3);                      // clean, the way you learn it
        MotifAt(t, p, 2, 0, 4, 1, 3);
        int[] lift = { 2, 3, 4, 5 };
        for (int i = 0; i < 4; i++) p.L(1, i * 4, Deg(t, lift[i], 1), 3);
        for (int i = 0; i < 4; i++) p.L(3, i * 4, Deg(t, Fall[i], 1), 3);
        p.L(1, 14, Deg(t, 1, 1), 2);
        p.L(3, 14, Deg(t, 3, 1), 2);
        return t;
    }

    // ============================================================ MAP TWO
    // LABORATORY: C# minor, 138. Cold 16th sequencer arps, beeps, a chug so
    // tight it sounds machined.
    static MusicTrack Lab()
    {
        var t = New("lab", 138f, 49);
        t.chordSemis = new[] { 0, 8, 4, 11 };              // i - bVI - bIII - bVII
        t.chordQual = new[] { QMinor, QMajor, QMajor, QMajor };
        t.leadDuty = 0.5f; t.arpDuty = 0.25f;
        t.gArp = 0.36f;
        t.heavyDrive = 3.6f; t.heavySus = 0.30f; t.crush = 0.15f;
        t.leadSus = 0.45f; t.leadDec = 0.12f;
        var p = new Pat(t, 4);

        for (int b = 0; b < 4; b++)
        {
            int r = ChordRoot(t, b) - 12;

            for (int s = 0; s < 16; s += 2) p.Bs(b, s, r, 1);   // cold eighths

            p.D(b, 0, K); p.D(b, 8, K);
            p.D(b, 4, S); p.D(b, 12, S);
            for (int s = 0; s < 16; s++) p.D(b, s, H);          // 16th hats
            p.D(b, 14, O);
            if (b == 0) p.D(b, 0, X);
            if (b == 3) { p.D(b, 15, O); p.D(b, 13, C); }

            int[] seq = { 0, 1, 2, 3, 4, 3, 2, 1 };             // up two octaves, back
            for (int s = 0; s < 16; s++)
                p.A(b, s, ChordNote(t, b, seq[s % 8], 1), 1);

            for (int s = 0; s < 16; s++)                        // the tight chug
                if (s % 4 != 1) p.H(b, s, r, 1);
        }

        int[] drop = { 4, 3, 2, 0 };
        int[] climb = { 5, 4, 3, 2 };
        for (int i = 0; i < 4; i++) p.L(0, i * 2, Deg(t, Motif[i], 1), 2);
        for (int i = 0; i < 4; i++) p.L(1, 8 + i * 2, Deg(t, drop[i], 1), 2);
        for (int i = 0; i < 4; i++) p.L(2, i * 2, Deg(t, Motif[i], 1), 2);
        for (int i = 0; i < 4; i++) p.L(3, i * 2, Deg(t, climb[i], 1), 2);
        p.Fx(3, 14, 2);                                          // a sequencer blip
        return t;
    }

    // ============================================================ MAP THREE
    // REALITY CONSUMED BY VOID: D Phrygian, 136 (148 at full intensity).
    // Doom territory: a swampy, down-tuned gallop on the low D, the flat
    // second creeping in underneath it, tritone stings at the end of the
    // phrase. The wrong-way motif and a thin screaming lead sit on top, and
    // the heavy riff is there from the very first layer.
    static MusicTrack Void()
    {
        var t = New("void", 136f, 50, Phrygian);
        t.bpm2 = 148f;
        t.chordSemis = new[] { 0, 0, 3, 0, 0, 0, 1, 0 };
        t.chordQual = new[] { QMinor, QMinor, QMajor, QMinor,
                              QMinor, QMinor, QMajor, QMinor };
        t.layHeavy = 0; t.layPad = 3;                          // metal from layer one
        t.gHeavy = 0.46f; t.heavyDrive = 4.0f; t.heavySus = 0.30f;
        t.gBass = 0.50f; t.gDrums = 0.55f;
        t.crush = 0.25f;
        t.arpDuty = 0.125f; t.gArp = 0.26f;
        t.leadDuty = 0.25f; t.gLead = 0.38f;
        t.leadVib = 0.010f; t.leadVibRate = 5.2f;
        t.leadSus = 0.55f; t.leadDec = 0.30f;
        t.padGain = 0.22f;                                     // dread, at the top layer
        var p = new Pat(t, 8);

        string[] riff =
        {
            "0.00 0.00 0.00 0.00",
            "0.00 0.00 0.00 11.1",
            "0.00 0.00 0.00 0.00",
            "0.00 0.00 0.00 65.0",
            "0.00 0.00 0.0c 0.0a",
            "0.00 0.00 0.00 cc.a",
            "0.00 0.00 0.00 0.00",
            "0.00 0.00 0.00 66..",
        };

        for (int b = 0; b < 8; b++)
        {
            Riff(t, p, b, riff[b], true);

            p.D(b, 0, K); p.D(b, 3, K); p.D(b, 8, K); p.D(b, 10, K);
            p.D(b, 4, S); p.D(b, 12, S);
            for (int s = 0; s < 16; s += 2) p.D(b, s, H);
            if (b == 0 || b == 4) p.D(b, 0, X);
            if (b == 3 || b == 7) { p.D(b, 12, TM); p.D(b, 13, TM); p.D(b, 14, TM); p.D(b, 15, C); }

            int[] seq = { 0, 1, 2, 1 };
            for (int s = 0; s < 16; s += 2)
                p.A(b, s, ChordNote(t, b, seq[(s / 2) % 4], 1), 2);
        }

        MotifAt(t, p, 0, 0, 4, 2, 3);
        p.L(1, 0, Deg(t, 1, 2), 8); p.L(1, 8, Deg(t, 0, 2), 8);
        MotifAt(t, p, 2, 0, 4, 2, 3);
        p.L(3, 0, Deg(t, 4, 1), 6); p.L(3, 8, Deg(t, 1, 1), 8);
        MotifAt(t, p, 4, 0, 4, 2, 3);
        ReverseMotifAt(t, p, 5, 0, 4, 2, 3);                   // the tape remembering it backwards
        p.L(6, 0, Deg(t, 2, 2), 8); p.L(6, 8, Deg(t, 1, 2), 8);
        p.L(7, 0, Deg(t, 0, 2), 14);
        p.L(7, 14, Deg(t, 0, 2) + 6, 2);                       // a wrong note at the edge
        return t;
    }

    // ============================================================ MAP FOUR
    // COSMIC HORROR: E Phrygian, 152 (168 at full intensity). The hardest
    // doom the game has: wall-to-wall chugging on the low E with octave
    // leaps, a tritone crawl at the end of each phrase, eighth-note double
    // kick, a screeching lead sawing at the tritone and, on the top layer,
    // a dissonant pad cluster underneath it all.
    static MusicTrack Cosmic()
    {
        var t = New("cosmic", 152f, 52, Phrygian);
        t.bpm2 = 168f;
        t.chordSemis = new[] { 0, 0, 0, 0, 0, 1, 6, 0 };
        t.chordQual = new[] { QMinor, QMinor, QMinor, QTritone,
                              QMinor, QMajor, QTritone, QTritone };
        t.layHeavy = 0; t.layPad = 3;
        t.gHeavy = 0.46f; t.heavyDrive = 4.4f; t.heavySus = 0.30f;
        t.doubleKick = true;                                   // eighth-note kick under everything
        t.leadDuty = 0.125f; t.arpDuty = 0.125f;
        t.leadVib = 0.014f; t.leadVibRate = 6.6f;
        t.leadSus = 0.5f; t.leadAtk = 0.002f;
        t.gLead = 0.32f; t.gArp = 0.26f;
        t.crush = 0.35f; t.crushAll = true;
        t.padGain = 0.34f; t.padCluster = true;                // dissonant pads
        var p = new Pat(t, 8);

        string[] riff =
        {
            "000. 00c. 000. 00a.",
            "000. 00c. 000. 0011",
            "000. 00c. 000. 00a.",
            "0.00 0.00 0.00 6650",
            "0000 0000 0000 0.00",
            "0.00 0.00 0.00 0.00",
            "0.00 0.00 0.00 0.00",
            "0000 0000 0000 66..",
        };

        for (int b = 0; b < 8; b++)
        {
            Riff(t, p, b, riff[b], true);

            p.D(b, 0, K); p.D(b, 8, K); p.D(b, 4, S); p.D(b, 12, S);
            for (int s = 0; s < 16; s += 2) p.D(b, s, H);
            if (b == 0 || b == 4) p.D(b, 0, X);
            if (b == 3 || b == 7) { p.D(b, 12, TM); p.D(b, 13, TM); p.D(b, 14, C); p.D(b, 15, C); }

            int[] seq = { 0, 1, 2, 3, 2, 1 };
            for (int s = 0; s < 16; s++)
                p.A(b, s, ChordNote(t, b, seq[s % 6], 1), 1);
        }

        // the motif with its fifth bent up to the tritone
        for (int i = 0; i < 4; i++)
        {
            int note = Deg(t, Motif[i], 2) + (i == 2 ? 6 : 0);
            p.L(0, i * 4, note, 3);
            p.L(2, i * 4, note, 3);
        }
        for (int i = 0; i < 4; i++)                            // bar two: the tritone sawing
            p.L(1, i * 4, Deg(t, 0, 2) + (i % 2 == 1 ? 6 : 0), 3);
        for (int i = 0; i < 4; i++)                            // and the motif, inverted
            p.L(3, i * 4, Deg(t, Fall[i], 2) + (i == 1 ? 6 : 0), 3);
        p.L(4, 0, Deg(t, 4, 2), 12);                           // second half: long screams
        p.L(5, 0, Deg(t, 1, 2), 14);
        p.L(6, 0, Deg(t, 3, 2), 6); p.L(6, 8, Deg(t, 2, 2), 6);
        p.L(7, 0, Deg(t, 1, 2), 6); p.L(7, 8, Deg(t, 0, 2), 6);
        return t;
    }

    // ================================================================ BOSS
    // THE SMASHER: E minor, 120. Slow, stomping, a hulking beast - and at
    // phase two it lunges to 160 and starts galloping.
    static MusicTrack Smasher()
    {
        var t = New("smasher", 120f, 52);
        t.bpm2 = 160f;
        t.chordSemis = new[] { 0, 0, 8, 10 };
        t.chordQual = new[] { QMinor, QMinor, QMajor, QMajor };
        t.gallopAtMax = true;
        t.doubleKick = true;
        t.crush = 0.25f;
        t.leadDec = 0.35f; t.leadSus = 0.6f;
        t.leadVib = 0.004f; t.leadVibRate = 4.2f;
        t.gLead = 0.46f; t.gArp = 0.24f; t.gBass = 0.60f;
        var p = new Pat(t, 4);

        for (int b = 0; b < 4; b++)
        {
            int n = p.N(b);
            int r = ChordRoot(t, b) - 12;

            p.Bs(b, 0, r, 7); p.Bs(b, 8, r, 5); p.Bs(b, 14, r + 12, 2);

            p.D(b, 0, K); p.D(b, 14, K); p.D(b, 8, S);
            p.D(b, 0, H); p.D(b, 4, H); p.D(b, 8, H); p.D(b, 12, H);
            if (b == 0 || b == 2) p.D(b, 0, X);
            if (b == 3) { p.D(b, 12, TM); p.D(b, 13, TM); p.D(b, 15, C); }

            for (int s = 0; s < n; s += 4)
            {
                p.A(b, s, ChordNote(t, b, (s / 4) % 3, 1), 3);
                p.H(b, s, r, 2);                              // stomping mutes
            }
        }

        p.L(0, 0, Deg(t, 0, 1), 8); p.L(0, 8, Deg(t, 2, 1), 8);
        p.L(1, 0, Deg(t, 4, 1), 8); p.L(1, 8, Deg(t, 3, 1), 8);
        p.L(2, 0, Deg(t, 0, 1), 8); p.L(2, 8, Deg(t, 2, 1), 8);
        p.L(3, 0, Deg(t, 4, 1), 6); p.L(3, 6, Deg(t, 5, 1), 4); p.L(3, 10, Deg(t, 3, 1), 6);
        return t;
    }

    // ================================================================ BOSS
    // MACHINE HERALD: F# minor, 150, in 7/8. The arp and the leitmotif both
    // run on a 16-step cycle against 14-step bars - the polyrhythm is the
    // point. Phase two goes up an octave and gets ground into bits.
    static MusicTrack Herald()
    {
        var t = New("herald", 150f, 54);
        t.stepsPerBar = 14;                                   // 7/8
        t.chordSemis = new[] { 0, 8, 3, 10, 0, 8, 3, 10 };
        t.chordQual = new[] { QMinor, QMajor, QMajor, QMajor,
                              QMinor, QMajor, QMajor, QMajor };
        t.octaveAtMax = 12;
        t.crush = 0.6f; t.crushAtMax = true;          // phase two: into the bits
        t.doubleKick = true;
        t.leadDuty = 0.25f; t.arpDuty = 0.125f;
        t.gArp = 0.34f; t.gLead = 0.38f;
        t.leadSus = 0.45f; t.leadDec = 0.14f;
        var p = new Pat(t, 8);                                // 8 x 14 = 112 steps

        for (int b = 0; b < 8; b++)
        {
            int n = p.N(b);
            int r = ChordRoot(t, b) - 12;

            for (int s = 0; s < n; s++) p.Bs(b, s, s % 7 == 6 ? r + 12 : r, 1);

            p.D(b, 0, K); p.D(b, 6, K); p.D(b, 8, K);
            p.D(b, 4, S); p.D(b, 12, S);
            for (int s = 0; s < n; s++) p.D(b, s, H);

            for (int s = 0; s < n; s++)
                if (s % 4 == 0 || s % 4 == 2 || s % 4 == 3) p.H(b, s, r, 1);
        }

        // the sequencer: a 16-step cycle that never lines up with the bars
        int[] seq = { 0, 1, 2, 3, 4, 3, 2, 1 };
        for (int i = 0; i < p.Total; i++)
            p.AAt(i, ChordNote(t, p.BarOf(i), seq[i % 8], 1), 1);

        // and the leitmotif, four-on-the-floor, crossing those same bars
        for (int ph = 0; ph * 16 + 12 < p.Total; ph++)
        {
            var line = (ph & 1) == 0 ? Motif : Fall;
            for (int i = 0; i < 4; i++)
                p.LAt(ph * 16 + i * 4, Deg(t, line[i], 1), 3);
        }
        p.FxAt(108, 2);
        return t;
    }

    // ================================================================ BOSS
    // BROKEN VESSEL: B harmonic minor, 100 (112 in phase two), in 5/4.
    // Halloween metal: a theremin wail sliding over a bouncing gallop, a
    // harpsichord arpeggio crawling up diminished chords, the snake-charmer
    // leap from the sixth to the leading tone, and a tolling iron bell. Phase
    // two stops singing, speeds up and a drone settles in underneath.
    static MusicTrack Vessel()
    {
        var t = New("vessel", 100f, 59, HarmMinor);
        t.bpm2 = 112f;
        t.stepsPerBar = 20;                                   // 5/4
        t.chordSemis = new[] { 0, 0, 8, 7, 0, 0, 8, 11 };      // i i VI V i i VI vii
        t.chordQual = new[] { QMinor, QMinor, QMajor, QMajor,
                              QMinor, QMinor, QMajor, QDim };
        t.layHeavy = 0; t.layPad = 3;
        t.gHeavy = 0.42f; t.heavyDrive = 4.2f; t.heavySus = 0.30f;
        t.crush = 0.30f;
        t.gBass = 0.55f;

        // the theremin: slow attack, a wide wobble, a slide between notes
        t.leadVib = 0.020f; t.leadVibRate = 5.6f;
        t.leadGlide = 0.065f;
        t.leadAtk = 0.025f; t.leadDec = 0.9f; t.leadSus = 0.75f; t.leadRel = 0.16f;
        t.leadDuty = 0.5f; t.gLead = 0.44f;
        t.topLeadTrim = 0.55f;

        // the harpsichord: thin, plucked, gone almost at once
        t.arpDuty = 0.125f; t.arpDec = 0.09f; t.arpSus = 0f;
        t.gArp = 0.30f;

        t.drone = true; t.droneGain = 0.34f;                  // the phase-two drone
        var p = new Pat(t, 8);

        string[] riff =
        {
            "0.00 0.00 0.00 0.00 1.1.",
            "0.00 0.00 0.00 0.00 6..6",
            "0.00 0.00 0.00 0.00 0...",
            "0.00 0.00 0.00 0.00 1..0",
            "0.00 0.00 0.00 0.00 1.1.",
            "0.00 0.00 0.00 0.0c c.a.",
            "0.00 0.00 0.0c 0.00 a.a.",
            "0.00 0.00 0.00 0.00 6.6.",
        };

        int[] creep = { 0, 1, 2, 1, 2, 3, 2, 1, 0, 1 };        // eighths, up and back

        for (int b = 0; b < 8; b++)
        {
            Riff(t, p, b, riff[b], true);

            p.D(b, 0, K); p.D(b, 6, K); p.D(b, 12, K); p.D(b, 16, K);
            p.D(b, 8, S); p.D(b, 18, S);
            for (int s = 0; s < 20; s += 2) p.D(b, s, H);
            if (b == 0 || b == 4) p.D(b, 0, X);
            if (b == 3 || b == 7) { p.D(b, 16, C); p.D(b, 18, TM); p.D(b, 19, TM); }  // the bell tolls

            for (int s = 0; s < 20; s += 2)
                p.A(b, s, ChordNote(t, b, creep[s / 2], 1), 1);
        }

        p.L(0, 0, Deg(t, 0, 1), 9); p.L(0, 10, Deg(t, 2, 1), 9);
        p.L(1, 0, Deg(t, 4, 1), 10); p.L(1, 10, Deg(t, 3, 1), 9);
        p.L(2, 0, Deg(t, 5, 1), 10); p.L(2, 10, Deg(t, 3, 1), 9);
        p.L(3, 0, Deg(t, 6, 1), 9); p.L(3, 10, Deg(t, 7, 1), 9);   // leading tone, home
        MotifAt(t, p, 4, 0, 6, 1, 5);                          // the motif, sung slowly
        ReverseMotifAt(t, p, 5, 0, 6, 1, 5);                   // ...and the way it comes apart
        p.L(6, 0, Deg(t, 5, 1), 10); p.L(6, 10, Deg(t, 6, 1), 9);   // the snake-charmer leap
        p.L(7, 0, Deg(t, 6, 1), 12); p.L(7, 12, Deg(t, 5, 1), 8);   // hanging on the diminished
        p.Fx(7, 16, 2);                                        // a ghost pings the loop shut
        return t;
    }

    // ================================================================ BOSS
    // SHEPARD, PHASE ONE: G Phrygian, 140 (156 in phase two). Doom through a
    // dying VHS deck: low-G chugging with octave and minor-seventh leaps, the
    // leitmotif played backwards over it, tape wobble through the melodic
    // voices and a rewind sweep tearing down the end of every phrase. At the
    // top layer the riff gives way to a straight gallop.
    // (On the VHS freeze the brain calls Music.Rewind: the mix winds backwards)
    static MusicTrack Shepard()
    {
        var t = New("shepard", 140f, 55, Phrygian);
        t.bpm2 = 156f;
        t.chordSemis = new[] { 0, 0, 1, 0, 0, 6, 5, 6 };      // G G Ab G G Db C Db
        t.chordQual = new[] { QMinor, QMinor, QMajor, QMinor,
                              QMinor, QTritone, QMinor, QTritone };
        t.reverseMotif = true;
        t.wobble = 0.014f;
        t.crush = 0.22f;
        t.gLead = 0.42f; t.gDrums = 0.50f;
        t.layHeavy = 0;
        t.gHeavy = 0.48f; t.heavyDrive = 4.6f; t.heavySus = 0.30f;
        t.gallopAtMax = true; t.doubleKick = true;
        t.leadDec = 0.30f;
        var p = new Pat(t, 8);

        string[] riff =
        {
            "00c0 00a0 0080 0060",
            "00c0 00a0 0080 0010",
            "0.00 0.00 0.00 0.00",
            "0.00 0.00 0.0c c.a.",
            "00c0 00a0 0080 0060",
            "0.00 0.00 0.00 0.00",
            "0.00 0.00 0.00 0.00",
            "0000 0000 0000 0.00",
        };

        for (int b = 0; b < 8; b++)
        {
            Riff(t, p, b, riff[b], true);

            p.D(b, 0, K); p.D(b, 8, K); p.D(b, 4, S); p.D(b, 12, S);
            for (int s = 0; s < 16; s += 2) p.D(b, s, H);
            p.D(b, 15, C);
            if (b == 0 || b == 4) p.D(b, 0, X);

            int[] seq = { 0, 1, 2, 1 };
            for (int s = 0; s < 16; s += 2)
                p.A(b, s, ChordNote(t, b, seq[(s / 2) % 4], 1), 2);
        }

        MotifAt(t, p, 0, 0, 4, 1, 3);
        MotifAt(t, p, 1, 0, 4, 2, 3);
        MotifAt(t, p, 2, 0, 4, 1, 3);
        MotifAt(t, p, 3, 0, 4, 2, 3);
        MotifAt(t, p, 4, 0, 4, 2, 3);
        MotifAt(t, p, 5, 0, 4, 1, 3);
        p.L(6, 0, Deg(t, 3, 1), 8); p.L(6, 8, Deg(t, 4, 1), 8);
        MotifAt(t, p, 7, 0, 4, 2, 3);
        p.Fx(1, 14, 1);
        p.Fx(3, 12, 1);                                        // the rewind
        p.Fx(5, 14, 1);
        p.Fx(7, 12, 1);
        return t;
    }

    // ================================================================ BOSS
    // SHEPARD 1V1: A minor, 180. Two pulses trading the leitmotif half a bar
    // at a time, the bass not arriving until bar 8, and everything below half
    // health going fully heavy (the fight's own hook calls SetIntensity(3)).
    static MusicTrack Duel()
    {
        var t = New("shepard1v1", 180f, 57);
        t.chordSemis = new[] { 0, 5, 8, 7 };
        t.chordQual = new[] { QMinor, QMinor, QMajor, QMajor };
        t.bassFromBar = 8;
        t.leadDuty = 0.5f; t.arpDuty = 0.125f;                 // two different pulses
        t.panLead = -0.55f; t.panArp = 0.55f;                  // trading across the room
        t.gLead = 0.40f; t.gArp = 0.34f;
        t.doubleKick = true; t.blast = true; t.crush = 0.3f;
        t.heavyDrive = 4.2f; t.gHeavy = 0.44f;
        t.leadSus = 0.5f;
        var p = new Pat(t, 4);

        for (int b = 0; b < 4; b++)
        {
            int n = p.N(b);
            int r = ChordRoot(t, b) - 12;

            for (int s = 0; s < n; s += 2) p.Bs(b, s, r, 1);

            p.D(b, 0, K); p.D(b, 6, K); p.D(b, 8, K); p.D(b, 14, K);
            p.D(b, 4, S); p.D(b, 12, S);
            for (int s = 0; s < n; s += 2) p.D(b, s, H);
            if (b == 0) p.D(b, 0, X);

            for (int s = 0; s < n; s++)
                if (s % 4 == 0 || s % 4 == 2 || s % 4 == 3) p.H(b, s, r, 1);
        }

        for (int b = 0; b < 4; b++)
        {
            int[] line = (b & 1) == 1 ? Fall : Motif;
            bool swap = (b & 1) == 1;                          // who leads changes each bar
            for (int i = 0; i < 4; i++)
            {
                int note = Deg(t, line[i], 1);
                if (swap) p.A(b, i * 2, note, 2); else p.L(b, i * 2, note, 2);
                if (swap) p.L(b, 8 + i * 2, note, 2); else p.A(b, 8 + i * 2, note, 2);
            }
        }
        for (int i = 0; i < 4; i++)                            // the last bar: both at once
        {
            p.L(3, i * 4, Deg(t, Motif[i], 1), 3);
            p.A(3, i * 4, Deg(t, Motif[i], 2), 3);
        }
        return t;
    }

    // ============================================================== ENDING
    // A minor, 56. No drums: a music box over a slow dotted echo, sixteen
    // bars long so it actually goes somewhere. It opens bare (low root, pad,
    // the four notes), a soft arpeggio rocks in under the second pass, the
    // melody climbs an octave in the middle and nearly breaks on a leading
    // tone, then it thins back out to the motif one last time. The final bar
    // holds A against the B-flat above it and never comes home.
    static MusicTrack Ending()
    {
        var t = New("ending", 56f, 57);
        t.chordSemis = new[] { 0, 8, 3, 10,   0, 8, 5, 7,
                               5, 8, 3, 7,    8, 5, 0, 0 };
        t.chordQual = new[] { QMinor, QMajor, QMajor, QMajor,
                              QMinor, QMajor, QMinor, QMajor,
                              QMinor, QMajor, QMajor, QMajor,
                              QMajor, QMinor, QMinor, QUnresolved };
        t.layLead = 0; t.layBass = 0; t.layArp = 0; t.layDrums = 4; t.layPad = 0;
        // every lane this track has is on the first layer: the brain pins the
        // ending to intensity 0
        t.leadDuty = 0.5f;
        t.boxLead = true;
        t.leadAtk = 0.002f; t.leadDec = 0.60f; t.leadSus = 0f; t.leadRel = 0.6f;
        t.leadVib = 0f;
        t.arpDuty = 0.5f; t.arpDec = 0.45f; t.arpSus = 0f;
        t.echo = true; t.echoMix = 0.50f; t.echoTime = 0.75f;
        t.gLead = 0.36f; t.gBass = 0.30f; t.gArp = 0.15f;
        t.padGain = 0.20f;
        var p = new Pat(t, 16);

        // a low root under every bar
        int[] low = { 45, 41, 36, 43, 45, 41, 38, 40, 38, 41, 36, 40, 41, 38, 45, 45 };
        for (int b = 0; b < 16; b++) p.Bs(b, 0, low[b], 16);

        // the rocking arpeggio: whole bars 4-11, half-time while it thins out
        int[] rock = { 0, 1, 2, 1 };
        for (int b = 4; b < 15; b++)
        {
            int ao = t.chordSemis[b] >= 5 ? -1 : 0;
            if (b < 12)
                for (int i = 0; i < 4; i++) p.A(b, i * 4, ChordNote(t, b, rock[i], ao), 4);
            else
            {
                p.A(b, 0, ChordNote(t, b, 0, ao), 6);
                p.A(b, 8, ChordNote(t, b, 2, ao), 6);
            }
        }

        // ---- first pass: the motif, and its answers
        MotifAt(t, p, 0, 0, 4, 1, 3);
        p.L(1, 0, Deg(t, 2, 1), 6); p.L(1, 8, Deg(t, 0, 1), 6);
        p.L(2, 0, Deg(t, 4, 1), 3); p.L(2, 4, Deg(t, 3, 1), 3); p.L(2, 8, Deg(t, 2, 1), 6);
        p.L(3, 0, Deg(t, 1, 1), 6); p.L(3, 8, Deg(t, 3, 1), 6);

        // ---- second pass: a little further
        MotifAt(t, p, 4, 0, 4, 1, 3);
        int[] reach = { 0, 2, 5, 4 };
        for (int i = 0; i < 4; i++) p.L(5, i * 4, Deg(t, reach[i], 1), 3);
        int[] down = { 5, 4, 3, 2 };
        for (int i = 0; i < 4; i++) p.L(6, i * 4, Deg(t, down[i], 1), 3);
        p.L(7, 0, Deg(t, 1, 1), 8);
        p.L(7, 8, t.root + 11, 8);                             // the leading tone, G-sharp

        // ---- the high verse: the same shape, an octave over
        int[] hiA = { 3, 5, 7, 6 };
        for (int i = 0; i < 4; i++) p.L(8, i * 4, Deg(t, hiA[i], 1), 3);
        int[] hiB = { 5, 7, 9, 7 };
        for (int i = 0; i < 4; i++) p.L(9, i * 4, Deg(t, hiB[i], 1), 3);
        p.L(10, 0, Deg(t, 9, 1), 6); p.L(10, 8, Deg(t, 6, 1), 4); p.L(10, 12, Deg(t, 4, 1), 4);
        p.L(11, 0, Deg(t, 8, 1), 8);
        p.L(11, 8, t.root + 23, 8);                            // G-sharp again, higher, aching

        // ---- thinning out
        p.L(12, 0, Deg(t, 5, 1), 10);
        p.L(13, 0, Deg(t, 3, 1), 6); p.L(13, 8, Deg(t, 2, 1), 6);
        MotifAt(t, p, 14, 0, 4, 1, 3);                         // the motif, one last time

        // ---- the last bar: A held, B-flat rubbing against it, no way home
        p.L(15, 0, Deg(t, 0, 1), 16);
        p.A(15, 0, Deg(t, 0, 1) + 1, 6);
        p.A(15, 8, Deg(t, 2, 1), 8);
        return t;
    }

    // ------------------------------------------------------ the drum masks
    const int K = MusicSynth.Kick, S = MusicSynth.Snare, H = MusicSynth.Hat,
              C = MusicSynth.Clank, TM = MusicSynth.Tom, X = MusicSynth.Crash,
              O = MusicSynth.Open;

    // ------------------------------------------------------- the pattern box
    // Writes into a track's lanes. Bars can be different lengths, so every
    // call goes through the bar's own start index.
    sealed class Pat
    {
        readonly MusicTrack t;
        public readonly int Total;

        public Pat(MusicTrack track, int bars)
        {
            t = track;
            t.bars = bars;
            t.barStart = new int[bars];
            t.barLen = new int[bars];
            int n = 0;
            for (int b = 0; b < bars; b++)
            {
                t.barStart[b] = n;
                t.barLen[b] = t.Steps(b);
                n += t.barLen[b];
            }
            Total = t.total = n;
            t.lead = new short[n]; t.leadGate = new byte[n];
            t.arp = new short[n]; t.arpGate = new byte[n];
            t.bass = new short[n]; t.bassGate = new byte[n];
            t.heavy = new short[n]; t.heavyGate = new byte[n];
            t.drums = new byte[n]; t.fx = new byte[n];
            for (int i = 0; i < n; i++)
            {
                t.lead[i] = -1; t.arp[i] = -1; t.bass[i] = -1; t.heavy[i] = -1;
            }
        }

        public int B(int bar) { return t.barStart[bar]; }
        public int N(int bar) { return t.barLen[bar]; }

        public int BarOf(int abs)
        {
            for (int b = t.bars - 1; b >= 0; b--)
                if (abs >= t.barStart[b]) return b;
            return 0;
        }

        int At(int bar, int s) { return t.barStart[bar] + s; }

        public void L(int bar, int s, int note, int gate) { LAt(At(bar, s), note, gate); }
        public void A(int bar, int s, int note, int gate) { AAt(At(bar, s), note, gate); }
        public void Bs(int bar, int s, int note, int gate) { BsAt(At(bar, s), note, gate); }
        public void H(int bar, int s, int note, int gate) { HAt(At(bar, s), note, gate); }
        public void D(int bar, int s, int mask) { DAt(At(bar, s), mask); }
        public void Fx(int bar, int s, int mask) { FxAt(At(bar, s), mask); }

        public void LAt(int i, int note, int gate)
        {
            if (i < 0 || i >= Total) return;
            t.lead[i] = (short)note;
            t.leadGate[i] = (byte)Mathf.Clamp(gate, 1, 64);
        }

        public void AAt(int i, int note, int gate)
        {
            if (i < 0 || i >= Total) return;
            t.arp[i] = (short)note;
            t.arpGate[i] = (byte)Mathf.Clamp(gate, 1, 64);
        }

        public void BsAt(int i, int note, int gate)
        {
            if (i < 0 || i >= Total) return;
            t.bass[i] = (short)note;
            t.bassGate[i] = (byte)Mathf.Clamp(gate, 1, 64);
        }

        public void HAt(int i, int note, int gate)
        {
            if (i < 0 || i >= Total) return;
            t.heavy[i] = (short)note;
            t.heavyGate[i] = (byte)Mathf.Clamp(gate, 1, 64);
        }

        public void DAt(int i, int mask)
        {
            if (i < 0 || i >= Total) return;
            t.drums[i] = (byte)(t.drums[i] | mask);
        }

        public void FxAt(int i, int mask)
        {
            if (i < 0 || i >= Total) return;
            t.fx[i] = (byte)(t.fx[i] | mask);
        }
    }
}
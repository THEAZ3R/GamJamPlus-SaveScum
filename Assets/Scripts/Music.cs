using UnityEngine;

// --------------------------------------------------------------------- music
// The game's whole soundtrack, made out of nothing: four NES channels and a
// data file per track, rendered sample by sample inside OnAudioFilterRead.
// This file is the surface the rest of the game talks to.
//
//     Music.CrossfadeTo("lab", 1.5f);   // maps crossfade, never cut
//     Music.CutTo("smasher");           // boss entrances cut
//     Music.SetIntensity(3);            // 0..3 = the four layers
//     Music.Duck(0.3f, 0.15f);          // dialogue gets the room
//     Music.Rewind(3f);                 // the tape winds back (Shepard's freeze)
//     Music.ToggleMute();
//
// MusicBrain below is the driver: it watches the run and does all of that by
// itself, so most of the game never has to know the music exists.
public static class Music
{
    static MusicRig rig;

    public static bool Muted { get { return MusicSynth.Muted; } }
    public static string Current { get { return MusicSynth.Current; } }
    public static int Intensity { get { return MusicSynth.Level; } }

    // the rig lives beside the sfx one: a silent looping source, so the
    // filter chain has something to hang off, parked out of the shot the same
    // way (a fresh rig means a fresh run, so the tape starts over)
    public static void Ensure()
    {
        if (rig != null) return;
        MusicSynth.Prepare();                // sample rate, pinned before the
        // first render call can go asking the audio thread for it
        var g = new GameObject("Music");
        g.transform.position = new Vector3(0f, -600f, 0f);
        rig = g.AddComponent<MusicRig>();
        MusicSynth.Clear();
        MusicBrain.Reset();
    }

    public static void CrossfadeTo(string id, float sec)
    {
        Ensure();
        MusicSynth.Request(MusicLib.Get(id), sec);
    }

    // an entrance, not a transition
    public static void CutTo(string id) { CrossfadeTo(id, 0f); }

    // 0 = bass and drums, 1 = + arpeggio, 2 = + lead, 3 = the heavy layer
    public static void SetIntensity(int level) { MusicSynth.SetLevel(level); }

    // amount 1 = full, 0 = silent; sec is how long the move takes
    public static void Duck(float amount, float sec) { MusicSynth.SetDuck(amount, sec); }
    public static void Unduck(float sec) { MusicSynth.SetDuck(1f, sec); }

    // wind the music backwards, pitch climbing, for sec seconds: the players
    // stand still while it plays and carry on where they left off afterwards
    public static void Rewind(float sec) { Ensure(); MusicSynth.SetRewind(true, sec); }
    public static void StopRewind() { MusicSynth.SetRewind(false, 0f); }

    public static void Mute(bool on) { MusicSynth.SetMute(on); }
    public static void ToggleMute() { MusicSynth.SetMute(!MusicSynth.Muted); }
}

// the host: one AudioSource playing nothing at all, which is what pulls the
// filter call, plus the driver's Update
public class MusicRig : MonoBehaviour
{
    AudioSource src;

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 0f;
        src.dopplerLevel = 0f;
        src.reverbZoneMix = 0f;

        int rate = AudioSettings.outputSampleRate;
        if (rate < 8000) rate = 48000;
        int n = Mathf.Max(256, (int)(rate * 0.1f));
        var silence = new float[n];
        var clip = AudioClip.Create("music_silence", n, 1, rate, false);
        clip.SetData(silence, 0);
        src.clip = clip;
        src.Play();
    }

    void Update()
    {
        if (src != null && !src.isPlaying) src.Play();
        MusicBrain.Tick();
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        MusicSynth.Render(data, channels);
    }
}

// --------------------------------------------------------------- the driver
// Everything the design asks for, read straight off the run:
//   - four map tracks, chosen by era; map to map the old song fades to
//     silence, a beat of nothing, then the new one fades up from zero
//   - boss tracks, cut in hard, starting on intensity layer 3
//   - intensity rising with kills on maps, jumping to layer 4 at phase two
//   - 30 percent duck under cutscene dialogue; on the VHS freeze the music
//     itself rewinds with the picture
//   - the ending track once the mirror falls
static class MusicBrain
{
    const int KillsPerLevel = 14;              // kills between intensity layers

    // the map-to-map change: out, a held breath, in
    const float OutSec = 1.4f;                 // the old song fades to nothing
    const float GapSec = 0.35f;                // dead air
    const float OutTau = 0.35f;                // duck time constant on the way out
    const float InTau = 0.55f;                 // ...and on the way back up

    static string curId;
    static int curLevel = -1;
    static int killsAtStart;
    static float duckAt = -1f;
    static bool started;
    static string pendId;                      // the map we are fading toward
    static float pendAt;                       // when the silence is complete
    static bool slowUp;                        // the next rise is the new song's fade-in
    static bool tapeOn;                        // Shepard's freeze is running
    const float TapeSec = 2.95f;               // the freeze is three seconds long

    public static void Reset()
    {
        curId = null; curLevel = -1; duckAt = -1f;
        started = false; killsAtStart = 0;
        pendId = null; slowUp = false; tapeOn = false;
    }

    public static void Tick()
    {
        if (GameManager.Instance == null) return;

        // nothing plays under the menu or the opening tape
        if (!started)
        {
            if (GameMenu.Open || Cutscenes.Running || GameManager.IntroT > 0f) return;
            started = true;
        }

        string id = Want();
        if (id == null) return;

        bool boss = IsBoss(id);
        if (pendId != null)
        {
            // mid-fade: a boss entrance or the ending doesn't wait for the
            // silence, and walking back into the old map just fades back up
            if (id == curId || boss || id == "ending") pendId = null;
            else
            {
                pendId = id;
                if (Time.unscaledTime >= pendAt)
                {
                    Switch(id, false, true);   // under the silence: a hard cut
                    pendId = null;
                    slowUp = true;             // ...and the duck opens slowly
                }
            }
        }
        else if (id != curId && curId != null && IsMap(curId) && IsMap(id))
        {
            pendId = id;                       // map to map: fade to silence first
            pendAt = Time.unscaledTime + OutSec + GapSec;
        }
        else if (id != curId) Switch(id, boss, false);

        // ---- intensity
        if (boss)
        {
            // a boss opens on layer 3 (bass, drums, arps, lead). Phase two
            // calls Music.SetIntensity(3) itself, so hands off here.
            if (curLevel < 0) { Music.SetIntensity(2); curLevel = 2; }
        }
        else
        {
            // a fresh map starts calm and climbs with the kills; a new leap
            // therefore always feels lighter than the end of the last map
            int lvl = id == "ending"
                    ? 0
                    : Mathf.Clamp((GameManager.Kills - killsAtStart) / KillsPerLevel, 0, 3);
            if (lvl != curLevel) { Music.SetIntensity(lvl); curLevel = lvl; }
        }

        // ---- the freeze: the music winds back with the picture
        bool vhs = Shepard.VhsActive;
        if (vhs != tapeOn)
        {
            tapeOn = vhs;
            if (vhs) Music.Rewind(TapeSec); else Music.StopRewind();
        }

        // ---- ducking
        float want = 1f;
        if (Cutscenes.Running) want = 0.30f;                        // text blips first
        if (PauseMenu.Opened) want = 0.30f;                         // the tape keeps playing, quietly
        if (GameManager.IntroT > 0f) want = 0f;                      // the opening tape
        if (GameManager.IsOver && !GameManager.Won) want = 0f;       // the horn takes the room
        if (pendId != null) want = 0f;                               // between maps: nothing
        if (want != duckAt)
        {
            float sec;
            if (want <= 0f) sec = pendId != null ? OutTau : 0.03f;
            else if (want < duckAt) sec = 0.12f;
            else sec = slowUp ? InTau : 0.45f;
            if (want > duckAt) slowUp = false;
            Music.Duck(want, sec);
            duckAt = want;
        }
    }

    static void Switch(string id, bool boss, bool underSilence)
    {
        bool first = curId == null;
        float fade;
        if (underSilence) fade = 0f;                         // nobody can hear the cut
        else if (boss) fade = 0f;                            // entrances cut
        else if (id == "ending" || curId == "ending") fade = 2f;
        else if (first) fade = 1.6f;
        else fade = 1.4f;                                    // maps crossfade

        Music.CrossfadeTo(id, fade);
        curId = id;
        killsAtStart = GameManager.Kills;
        curLevel = -1;                                       // reapplied next frame
    }

    static bool IsMap(string id)
    {
        return id == "factory" || id == "lab" || id == "void" || id == "cosmic";
    }

    static bool IsBoss(string id)
    {
        return id == "smasher" || id == "herald" || id == "vessel" ||
               id == "shepard" || id == "shepard1v1";
    }

    static string Want()
    {
        if (GameManager.Won) return "ending";
        if (MirrorFighter.Active != null) return "shepard1v1";
        if (Smasher.Active != null) return "smasher";
        if (Herald.Active != null) return "herald";
        if (BrokenVessel.Active != null) return "vessel";
        if (Shepard.Active != null) return "shepard";
        if (GameManager.Flesh) return "cosmic";
        if (GameManager.Crumble) return "void";
        if (GameManager.Lab) return "lab";
        return "factory";
    }
}
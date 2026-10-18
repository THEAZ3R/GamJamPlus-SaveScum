# SAVE_SCUM

> A fast, vicious top-down roguelite shooter about rewinding time, dying loudly, and killing the version of yourself that got away.

**SAVE_SCUM** is a single-player arena shooter built around a 120-second clock and a **REWIND** button. Fight a horde that sharpens every time you leap. Then leap again. Every TIMELEAP makes the enemies faster, meaner and smarter — but it also refills your clock, so the only way forward is *through* the escalation. Every leap, a little more of the timeline comes undone.

- 🎮 **Top-down twin-stick combat** with 17 stacking mods
- ⏪ **TIMELEAP** — rewind 3 seconds to undo your mistakes (and resurrect what you killed)
- 🧠 **Enemies that learn** — dodge your shots, hold shields, hide behind walls
- 👹 **Five boss fights**, including one that wears your face
- 🎵 **100% procedural chiptune soundtrack** — no audio files, every note synthesized live in code

---

## 📥 Download

Grab the latest build from the [**Releases**](../../releases) page. Screenshots and full patch notes are included with each release.

| Platform | Build |
|---|---|
| Windows | `SAVE_SCUM-win.zip` |

> Replace the table above with whatever you actually ship — if it's Windows-only, just say "Windows build, extract and run `SAVE_SCUM.exe`."

---

## 🕹️ Controls

| Input | Action |
|---|---|
| **WASD / Arrows** | Move |
| **Mouse** | Aim |
| **Left Mouse** | Shoot |
| **Space / Left Shift** | Dash (0.75s cooldown, brief invulnerability) |
| **T** | TIMELEAP — rewind 3 seconds (locked during bosses) |
| **1–3** | Pick an upgrade on level-up |
| **ENTER / Space** | Skip dialogue (hold to skip the page) |
| **R** | Restart after a run ends |
| **ESC** | Pause |
| **M** | Mute |

Every action is **rebindable** from the CONTROLS menu, along with mouse sensitivity.

---

## ⏱️ How it works

A 120-second bar runs down in the corner. When it hits zero, the run ends. Press **T** and you **leap**: the clock refills, but every enemy already on the field gets **15% faster and 15% harder-hitting**, and the horde gets a little smarter at dodging your shots.

The tension is the whole game: you *can* keep leaping, but each one makes the room worse. Spawns accelerate, elites unlock, and eventually a boss falls out of the sky, freezes the clock, and locks the rewind until it's dead.

### TIMELEAP, briefly

- **T** rewinds 3 seconds — a tape of the last 30 frames restores you *and* every enemy (the dead come back).
- Rewind at **zero clock** to refill to 120s.
- Every leap: **PowerMul** +15%, **XP gain** drops, **enemy skill** rises.
- Bosses scale with difficulty but **stop growing past ×3 HP** — a slow run with a weak build can still bring them down.

---

## 🧩 The 17 mods

Every level-up offers a choice of three. Builds stack into wildly different runs — a bouncing shotgun wall, a lifestealing dog pack, an orbital laser, or a black-hole portal that detonates on death.

Highlights: **BLOOD PUMP** (HP & size), **CELERITY** (speed, locks Blood Pump), **QUEN MATRIX** (recharging shield), **GUTS** (shotgun fan), **DEAD EYE / FUS RO DAH** (bouncing vs. piercing), **PORTAL GUN**, **MAZIODYNE** (chain lightning), **DOGMEAT** (up to 5 dogs), **HELIOS' JUDGMENT** (orbital strike), **ASTARION** (lifesteal), **225** (execute low-HP enemies). Some mods lock others out — choose your path.

---

## 🌍 The four realities

Each map is a different reality with its own enemies, hazards and music, and each leap makes the map itself more dangerous.

| # | Map | The vibe |
|---|---|---|
| 1 | **The Arena** | Steel and navy. Walls, pillars, boosters, void pits. Where the run starts. |
| 2 | **The Lab** | Violet circuits and pylons. Blood conducts electricity; oil burns. Everyone in herald-purple. |
| 3 | **The Ruin** | The floor is giving way. Void bites, crumbling tiles, **acid rain** that hurts you *and* the horde. |
| 4 | **The Flesh** | Meat, veins, cosmic eyes. Slime pools, no traps — and **Shepard** waiting. |

---

## 👑 The bosses

- **THE SMASHER** — a falling steel giant. Leap-slams your shadow, cracks the floor, and re-armours in phase two.
- **THE MACHINE HERALD** — a hovering blade of neon and a cyan eye. Zeus bolts, a flame sweep, and a breeding ward that spawns laser drones.
- **THE BROKEN VESSEL** — two dead realities in one lopsided body. Acid vomit, thrown scrap, and a rust beam that *eats walls*.
- **SHEPARD** — your exact model, red, 1.55× scale. He time-leaps, ghosts out, resurrects his old bosses as ghosts — and at 30% he **wipes every mod you own** and sends THE MIRROR to finish you with nothing but a base rifle.

Boss HP is capped at ×3 difficulty so a bad build can always claw its way through. No new enemies spawn while a boss lives, and TIMELEAP is locked until it's dead.

---

## 🎼 The soundtrack

There are **no audio files**. All 10 tracks are synthesized live in code from two pulse channels, a triangle bass and a noise drum kit. One four-note leitmotif — **A–C–E–D** — is rewritten into every track's key and scale, so the whole game is the same idea in a different mood: bright industrial on the Arena, cold sequencers in the Lab, swampy doom in the Ruin, wall-to-wall chug in the Flesh. The bosses get odd time signatures (**7/8**, **5/4**) and phase-two tempo kicks. And yes — Shepard's VHS freeze **winds the entire mix backwards in real time**.

Four live layers (bass+drums → arpeggio → lead → heavy) climb one level per 14 kills on each map; bosses cut in on layer three and slam layer four at phase two.

---

## 🏆 Winning

Kill **THE MIRROR** and the run is yours. A gold **YOU WIN** card shows your kills, level and time (only time actually spent in the ring counts — menus, cutscenes and tape-freezes don't tick it). From there you can **rewind the whole tape** back to the first cutscene with everything reset, or watch the epilogue type itself out over the cliff.

Lose and you get **YOU DIED** — or **THE COMMANDER ESCAPED** if the clock runs out. Press **R** to jump straight back into the ring.

---

## 🛠️ Built with

- **Unity** (C#)
- Everything in-game is **procedural** — the soundtrack, the menu sounds, the tapes
- No external audio assets

---

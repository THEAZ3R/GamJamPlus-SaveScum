# SAVE_SCUM — PLAYER DOSSIER

> Every number read directly from `Assets/Scripts/`. Difficulty `D` = `PowerMul × min(2.6, 1 + run time/150s)`.
> HP scales ×`D` · all damage ×`√D` · speed ×`(1 + 0.2×(D−1))`.

---

## ENEMIES

- Each TIMELEAP: everything already on the field gets ×1.15 speed **and** ×1.15 damage.
- "Big" = Bulwark / Artillery / Grabber / Butcher → orange HP bar (the rest red).
- Timeleap dodge: `Skill = min(0.7, 0.3×Leaps)` — enemies sidestep rounds aimed at them (dot ≥ 0.88, 0.38s step) with a 1.5s cooldown between steps; Shooter/Trapper/Stabber only at 0.55× chance; spiders never dodge.

| Kind | HP | Speed | Contact | Ability | XP |
|---|---|---|---|---|---|
| **Stabber** | 30 | 3.3 | 12 (lunge only) | Melee: 2.4u reach, stab 26×√d, windup 0.30s / lunge 0.22s / recover 0.55s, cd 0.4s. Small, hunched, leans into the stab | 12 |
| **Dasher** | 34 | 2.9 | 14 (dash only) | Dashes 0.30s from 1.7–6.5u when it has LOS, cd 1.15s. Lean, coiled, vibrate pose | 18 |
| **Spider** | 12 | 5.6 | 0 | Rushes in (never dodges), **0.8s fuse** under 1.6u → explodes for 45×√d in r2.3. Tiny scuttler, black oil blood, escalating tick before blowing | 20 |
| **Shooter** | 28 | 2.4 | 5 | Baseline trooper: pink bullet 8×√d every 1.5s, holds 7u ring, strafes, tucks behind live Bulwark shields | 16 |
| **Bulwark** | 120 | 1.5 | 15 | Walking shield (140hp front plate that tracks you). While up it soaks; once broken it shoots back (9×√d / 1.5s). Huge, slow, oil-blood tank | 45 |
| **Trapper** | 34 | 2.5 | 5 | Hides behind walls (probes 8 spots), throws a trap every 3.2s in a ring around you (1.1–2.2u), cycling Slow / Boom / Stun. Sways while aiming | 20 |
| **Piercer** | 30 | 2.2 | 5 | Heavy rigid frame, big cannon: piercing cyan round 12×√d every 2.0s (speed 6.5), holds 8u | 22 |
| **Bouncer** | 32 | 2.5 | 5 | Squat springy one: bouncing yellow bullet 10×√d / 1.7s, and *hops* around the arena 5.5–7.5u every 1.5–2.5s to hold its 7u ring | 22 |
| **Artillery** | 90 | 1.6 | 12 | Biggest turret on legs: deploys 1.1s → lobs 3 arcing shells 0.5s apart, 18×√d each (aim r1.2), every 4.6s at 11u. Fires over cover | 50 |
| **Grabber** | 210 | 3.0 | 0 | Hovering claw bot (no legs): grabs you → HUD `"TIED UP - DASH 5 MORE!"`, shooting blocked, it drags you; dash 5× to break free. Map 4 = flesh variant | 60 |
| **Butcher** | 150 | 2.15 | 18 | The flesh's melee tank: closes the gap, rears back 0.38s (flashing) then **coughs a 6-pellet shotgun** (13u/s, 5×√d each, 6.5° spread) under 5.4u, every 2.7s | 55 |

### Who spawns where
Cap **8 alive** · no spawns during bosses · interval `max(0.85, 1.6 − 0.1×Leaps)` · spawns ≥7u from you:

- **Map 1, leaps ≤ 2**: Shooter 40% · Trapper 30% (rationed: 2 alive, 3 on the leap before the boss) · Stabber
- **Map 1, leaps ≥ 3**: full 10-kind roster — unlocks at {0, 4, 6, 9, 12, 15, 18, 20, 26, 32}s → Stabber, Shooter, Spider, Dasher, Trapper, Piercer, Bouncer, Bulwark, Artillery, Grabber (weights 5 / 4 / 3.5 / 3 / 3 / 2.5 / 2.5 / 2 / 1.8 / 1.2)
- **Map 2 (Lab)**: Bouncer 40% · Dasher 32% · Spider 28% — all in herald-purple livery
- **Map 3 (Ruin)**: Piercer 45% · Bulwark 30% · Dasher 25%
- **Map 4 (Flesh)**: Butcher 40% · Artillery 30% · Grabber 30% — all meat-red variants

---

## BOSSES

The boss bar rides the **bottom** of the screen. The clock freezes while a boss lives (`"TIMELINE HIJACKED"`), TIMELEAP is locked, and no new enemies spawn. Boss HP scales with difficulty but **stops growing past ×3** — a slow run with a weak build still brings them down (damage keeps scaling the whole way).

### THE SMASHER — Map 1 (Arena), first leap after Leaps ≥ 3
**950 × D HP** (capped at ×3) · 220 XP.
Announce: `"!! SOMETHING HAS HIJACKED THE TIMELINE !!"` → `"!! SOMETHING IS FALLING FROM THE SKY !!"` → `"THE SMASHER ENTERS THE RING"` · death `"THE SMASHER IS DOWN"`.

- **Appearance**: steel-blue armour plate `#4d5261`, dark red body `#66211a`, furnace core `#ff731f` between two forward smash-fists.
- **Entrance**: drops from the sky — an expanding shock ring (30×√d, r26) crawls across the whole field and hits the horde too.
- **P1 attacks**: leap-slam onto your shadow (34×√d) · quake ring (24×√d, cracks the floor) · telegraphed dash (0.5s warn lane, 13u/s, 22×√d, `MOVE! THE SHADOW IS HUNTING`) · scattergun (9 pellets × 6.5×√d, `"SCATTERGUN!"`) · front armour shield (380hp) — broken once in P1, stays broken.
- **50% → P2** `"THE SMASHER SEES RED"`: rage flames, ×1.12 speed (×1.2 with no shield), meteor landing, red spike ring (20×√d), and the shield **regrows every 16s** (`"IT RE-ARMOURS"`).

### THE MACHINE HERALD — Map 2 (Lab), every 3rd lab leap after the 2nd
**435 × D HP** (capped at ×3) · 300 XP.
Announce: `"!! PREPARE FOR THE GLORIOUS EVOLUTION !!"` → `"THE MACHINE HERALD DESCENDS"` · death `"THE HERALD IS DOWN"`.

- **Appearance**: thin hovering blade-figure, neon purple over a cyan eye `#59f2ff` / `#b359ff`; hex ring spins around the core, eye pulses.
- **Attacks**: purge wave on spawn · oil volley (7 globs) · 180° flame sweep (1.1s, 5 dmg ticks) · tele-stab behind you (26×√d) · Zeus bolt strikes where you stand (26×√d) · **breeding ward** (90hp shield) spawning 6 LaserBots (22hp) that paint you with 10-dmg beams — `"SHOOT THE SHIELD - STOP THE BREEDING"`.
- **50% → P2** `"THE HERALD'S CORE IGNITES"`: eye goes white-hot, permanent 130hp ward (regen 8s), dash-laser combo (10/16 dmg).

### THE BROKEN VESSEL — Map 3 (Ruin), every 3rd ruin leap after the 2nd
**700 × D HP** (capped at ×3) · 340 XP.
Announce: `"!! SOMETHING IS CHEWING THROUGH REALITY !!"` → `"REALITY IS CRUMBLING"` (fires a CollapseEvent — 7 void bites chew the arena) · death `"THE VESSEL IS EMPTY"`.

- **Appearance**: a lopsided heap of two dead realities — **skull bolted to the crown, pale human arm with fingers, drag-leg (human leg), turning gear in the shoulder**, engine/mech plates, acid tank, one glowing green maw `#80f233`. Roars constantly (vol ~0.6, every ~3–4.5s), every step scrapes.
- **P1** (drags itself around): acid vomit cone (13×√d / 0.3s + acid: −50% speed & damage 4s, pools on the floor) · rips real cover out of the floor and throws 4 scrap chunks (15×√d) · **thick rust beam** sweeping 95–140° over ~3s (11×√d, *eats destructible walls* as it passes).
- **50% → P2** `"THE VESSEL ROOTS ITSELF"`: never moves again — a turret of 4 acid globs (9×√d), 5×3 bullet bursts (8×√d), and wider beams.

### SHEPARD — Map 4 (Flesh), after your FIRST map-4 leap
The horde melts into sludge first, then he spawns. **550 × D HP** (capped at ×3) · `dmgScale = √D`.
Announce: `"!! SOMETHING WEARS YOUR FACE !!"` → `"THE HORDE MELTS INTO SLUDGE"` → `"SHEPARD"`.

- **Appearance**: **your exact model at 1.55× scale, painted red** `#d11a1a` — same suit, helmet, rifle. He moves like you: always dashing toward you.
- **P1**: predicted 3-round bursts that lead your velocity (9×√d) · omni-blade (blue flash inside 2.6u → 30×√d + `Shove(9)`) · combat slide into sniper shot (22×√d).
- **70%**: **self-timeleap full heal** + the arena shrinks (`ShrinkArena(2.4)`) → `"SHEPARD TIME-LEAPS - THE ROOM CLOSES IN"`. Gains a slow-homing **stasis orb** (stun 1.5s + gray desat + 10-round mag dump of 7 dmg) and **Ghost Protocol**.
- **Ghost Protocol**: he floats off-screen, **invulnerable — you cannot hit him**, fires bullets down into the arena every 0.5s while gone (spawned inside the walls so they reach you), and his old bosses appear as ghosts that *physically perform their signature attacks*:
  - **Smasher ghost** — rears up over a growing floor mark, then drops and slams it (28×√d).
  - **Herald ghost** — the laser grows out of its body over a 0.35s charge, and the hex turns in sync as it sweeps (10×√d).
  - **Vessel ghost** — leans into its arc and shudders while the rust beam pours from its body (12×√d).
  He returns when the sequence ends and vulnerability comes back.
- **50%**: one **VHS dodge** — world freezes (`Time.timeScale=0`), pause glyph, static, 1-2-3 beeps → warn mark on you, 0.35s later a 36×√d strike in 2.4u (only dash i-frames survive).
- **30%**: second timeleap → HUD glitches → **`PlayerUpgrades.ResetAll()`** (every mod wiped, `"SHEPARD REWINDS - YOUR MODS ARE GONE"`) → he rewinds out (no xp) and spawns **THE MIRROR** — you, un-upgraded: **240 × D HP**, your base gun (10 dmg), base dash, strafes and kites in 3-round bursts. `"1V1 - THE MIRROR"`.
- **Win**: kill the mirror → gold `"YOU WIN"` screen.

**Leap-lockout lines** (during any boss): `THE VESSEL HOLDS REALITY` / `THE HERALD HOLDS THE LINE` / `THE SMASHER HOLDS THE RING` / `THE MIRROR HOLDS THE GUN` / `SHEPARD HOLDS THE TAPE`.

---

## MAPS & HAZARDS

| # | Map | Enter line | Look | What's in it |
|---|---|---|---|---|
| 1 | **Arena** | — (start) | steel + navy | Bunker: 6 walls, 2 solid pillars, 4 boosters (15u/s), 4 void pits. Leaps 1–2 kill the boosters; leap ≥3 adds paired walls + void pits |
| 2 | **Lab** | `SYSTEM RECALIBRATED // THE LAB` | violet floor, circuit grid | Hex platform, pylons, vents, glass/vials/terminals; blood pools conduct, oil burns; hazard machines at lab-leaps 2 & 4; everyone wears herald-purple |
| 3 | **Ruin** | `THE THIRD REALITY // THE FLOOR GIVES WAY` | dirt + violet | ~6% tiles missing + **cluster void bites** (7 irregular empty areas per collapse) + **acid rain**: bounded ~2.3–3.1u AcidField zones that rain inside and hurt both you and enemies (3 dmg/0.3s + burn), cd `max(2.8, 6.8 − 0.5×leaps)`; each leap adds wall + pit + heavier rain |
| 4 | **Flesh** | `THE FOURTH REALITY // THE FLESH REMEMBERS` | meat tiles, veins, cosmic eyes | Gore floor, veins, flesh walls, bone/meat cover, void pits — **no traps**; each flesh leap adds 2 slime pools (red-purple, burns, wets you for dashing) |

**CollapseEvent**: 7 void bites, each 2.8–4.2 × 2.4–3.6u, swallowing cover/enemies/fluids — they become pits.

**Hazard set across the run**: fire & electric pools, steam vents, Slow/Boom/Stun traps, crumble tiles, bottomless pits (instant kill), directional boosters, acid rain.

---

## TIMELEAP & SYSTEMS

- **Clock**: 120s bar (top-right): `TIMELEAP   Ns   |   LEAPS n   |   [T] REWIND 3s` (`[T] LOCKED` / `TIMELINE HIJACKED` variants). Hits 0 → run ends `"THE COMMANDER ESCAPED"`. Flashes red under 15s.
- **T** rewinds **3 seconds**: a tape of 30 frames @0.1s of you and every enemy (the dead come back, the off-tape despawn with no XP). Cooldown 2s.
- **Refill**: at 0 with clock ≥ 60 → full 120; otherwise `min(120, 60 + clock)` — full before the last minute, exactly half at the last second.
- **Each leap**: `"TIMELEAP n - THE HORDE SHARPENS"` · PowerMul = 1 + 0.15×Leaps · XpMul = `max(0.25, 2 − 0.1×Leaps)` · enemy skill = `min(1, 0.3×Leaps)`.
- **XP curve**: `200 + 60×(L−1) + 8×(L−1)²` → 200, 268, 344, 428…

### The 17 mods (max levels & numbers)

| Mod | Max | Effect |
|---|---|---|
| BLOOD PUMP | 99 | +25 HP & +5% size/lvl (locks CELERITY) |
| SANDEVISTAN OVERCLOCK | 99 | +20% damage/lvl |
| CELERITY | 99 | +12% speed, −5% size/lvl (locks BLOOD PUMP) |
| QUEN MATRIX | 5 | shield 40×lvl, recharge `max(4, 6.5−0.5×lvl)`s |
| GUTS | 10 | fan of `min(5, lvl+1)` |
| BLOODY MESS | 5 | `22+14×lvl` dmg, r `1.7+0.35×lvl` |
| BLOODTINGE.EXE | 5 | `10+6×lvl` dps + `14+10×lvl` splash on red blood |
| DEAD EYE | 3 | `3+lvl` bounces (locks FUS RO DAH) |
| FUS RO DAH | 3 | pierce + shove, +25%/lvl (locks DEAD EYE) |
| PORTAL GUN | 3 | portal lasts `6+3×lvl`s; lvl2+ kills burst 70+60×(lvl−1) r `2.2+0.3×(lvl−2)` |
| THE INCREDIBLE GASSY | 5 | `5+4×lvl` dps |
| MAZIODYNE | 8 | `min(lvl+2, 6)` chains, +15% from lvl 5 |
| DOGMEAT | 10 | up to 5 dogs, bite `10+6×(lvl−5)` |
| HELIOS' JUDGMENT | 10 | cd `max(7.5, 14−1.5×lvl)`s, `60+40×max(0, lvl−5)` dmg |
| PUSH PUSH PUSH | 5 | regen `clamp(lvl, 0, 5)` HP/s |
| 225 | 6 | execute under `min(30%, 5%×lvl)` |
| ASTARION | 5 | lifesteal `min(25%, 5%×lvl)` |

### Player base stats
**100 HP** · 0.7s i-frames · speed **6** · fire rate **0.15s** · bullet **10 @ 16u/s** · hitbox r 0.32 · dash **22u/s × 0.17s (3.7u)**, cd 0.75s, i-frames, breaks grabs (5 dashes to free).

---

## CONTROLS

| Input | Action |
|---|---|
| WASD / Arrows | Move |
| Mouse | Aim |
| Left mouse | Shoot (blocked while stunned/grabbed/dashing) |
| Space / Left Shift | Dash (0.75s cd, i-frames) |
| **T** | TIMELEAP — rewind 3s (locked during bosses) |
| **1–3** | Pick an upgrade on level-up |
| **R** | Restart (after run end) |
| **M** | Mute |

**Run-end strings**: `YOU DIED` · `THE COMMANDER ESCAPED` · `YOU WIN`

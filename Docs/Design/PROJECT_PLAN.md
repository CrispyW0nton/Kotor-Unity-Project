# MRL_GameForge v2 — Full Project Design Plan
## KotOR-Unity Hybrid Command System

**Version**: 2.0  
**Date**: 2026-03-01  
**Status**: Active Development  

---

## Table of Contents
1. [Design Intent](#1-design-intent)
2. [Target Player Psychology](#2-target-player-psychology)
3. [Core Game Equation](#3-core-game-equation)
4. [Primary Loop](#4-primary-loop)
5. [Secondary / Meta Loop](#5-secondary--meta-loop)
6. [System Variables](#6-system-variables)
7. [Mechanics Derivation](#7-mechanics-derivation)
8. [Decision Density Analysis](#8-decision-density-analysis)
9. [Failure Modes & Dominant Strategies](#9-failure-modes--dominant-strategies)
10. [Balance Model](#10-balance-model)
11. [Progression Scaling Model](#11-progression-scaling-model)
12. [Scope & Technical Requirements](#12-scope--technical-requirements)
13. [MVP Cutline](#13-mvp-cutline)
14. [Validation Classification](#14-validation-classification)
15. [Critical Design Challenges](#15-critical-design-challenges)
16. [Implementation Roadmap](#16-implementation-roadmap)

---

## 1. Design Intent

Create a modal gameplay system where a single player can dynamically toggle between:

- **RTS Commander Mode**: Isometric/elevated camera with pausable tactical planning (Dragon Age Origins, KOTOR)
- **Action Mode**: First/third-person direct control with real-time gunplay and melee (Mass Effect 2, Call of Duty, Jedi Academy)

The core challenge: maintaining mechanical coherence across two drastically different input/feedback paradigms without one mode becoming a "tourist mode."

---

## 2. Target Player Psychology

### Primary Motivational Profiles

| Profile | Target % | Behavior |
|---|---|---|
| **Switchers** | 60% | Value both tactical depth and visceral action. Switch based on encounter type, fatigue, or desired intensity. |
| **RTS Mains** | 20% | Prefer tactical control but tolerate action mode for story/exploration. |
| **Action Mains** | 20% | Prefer direct control but use RTS for overwhelming encounters or inventory management. |

### Engagement Drivers
- **Mastery Bifurcation**: Skill expression in both modes (positioning/timing in RTS, aim/reflexes in Action)
- **Agency Flexibility**: Player controls cognitive load dynamically
- **Risk/Reward Asymmetry**: Action mode offers higher damage potential; RTS offers survivability and multi-unit coordination

### Dropout Risks
- RTS mode feels like "cheating" (trivializes action content)
- Action mode feels mandatory despite marketing RTS as viable
- Mode-switching mid-combat causes disorientation or exploits

---

## 3. Core Game Equation

### Variables
```
M  = Active mode {RTS, Action}
S  = Game state (unit positions, health, cooldowns, enemy AI state)
A_RTS    = {Move orders, ability queue, formation, pause}
A_Action = {Aim, shoot, melee, dodge, ability hotkey}
P  = Player skill vector (tactical_iq, reaction_time, aim_precision)
E  = Encounter difficulty scalar
```

### Core Utility Function
```
U(M, S, P, E) = DamageOutput(M, P) × Survivability(M, S) - CognitiveCost(M, E)
```

### Mode Switch Cost
```
SwitchPenalty = CameraTransitionTime + MentalReorientationDelay + VulnerabilityWindow
```

### Optimal Play Condition
```
Player switches when: U(M_new) - SwitchPenalty > U(M_current)
```

**Critical Balance Requirement**: Neither mode should have >70% dominance in any encounter type.

---

## 4. Primary Loop

### Action Mode (Direct Control)
```
Engage → Aim/Shoot or Melee → Use Abilities (cooldown-gated) → Dodge/Cover → Assess Threat
→ [Optional: Switch to RTS] → Repeat
```
- Cycle Time: 3–8 seconds per decision
- Decision Density: 15–25 decisions/minute (high intensity)

### RTS Mode (Tactical Command)
```
Pause/Slow-time → Survey Battlefield → Issue Movement Orders → Queue Abilities
→ Unpause → Monitor Execution → [Optional: Switch to Action for priority target] → Repeat
```
- Cycle Time: 10–30 seconds per pause cycle
- Decision Density: 6–12 decisions/minute (lower intensity, higher strategic weight)

---

## 5. Secondary / Meta Loop

### Session-to-Session Progression

**Character Build System**: Talent trees must synergize with BOTH modes.
- Example: "Marksman" tree benefits ADS accuracy in Action AND squad rifle DPS in RTS

**Squad Composition**: Recruit AI companions with distinct roles.
- RTS: Benefits from diverse squad
- Action: Benefits from complementary abilities (Mass Effect 2 style)

**Equipment Progression**: Weapons/armor scale numerically AND unlock mode-specific perks.
- Example: "Tactical Scope: +20% damage in RTS, +zoom stability in Action"

### Long-Term Mastery

**Mode Affinity Tracking**:
- Game tracks which mode player uses per encounter type
- Subtle buffs encourage trying the under-used mode (+5% XP if under-used mode in last 3 encounters)

**Combo Mastery**:
- Unlock hybrid tactics (e.g., "Overwatch Protocol: Mark target in RTS, switch to Action for crit bonus on first shot")

---

## 6. System Variables

### Player State (Per Character)
```
Health:    [0, MaxHealth]
Shield:    [0, MaxShield]           // Regenerates out of combat
Stamina:   [0, 100]                 // Melee/dodge cost in Action; instant in RTS
Position:  (x, y, z)               // World coordinates
Facing:    yaw/pitch                // Matters in Action, abstracted in RTS
Abilities[]: {Cooldown, Range, Damage, Cost}
Weapons[]:   {Type, Ammo, Damage, Accuracy_Action, DPS_RTS}
```

### Squad State
```
AICompanions[]: Up to 3 units with own stats
Formation:      {Spread, Line, Wedge}   // RTS only
OrderQueue[]:   Pending commands in RTS
```

### Encounter State
```
Enemies[]:      {Type, Health, Armor, AI_Aggro_Target, Threat_Level}
Cover_Nodes[]:  {Position, Height, Destructibility}
Objective:      {Type: Kill_All | Defend | Extract, Timer}
```

### Mode State
```
ActiveMode:      {RTS, Action}
PauseAvailable:  Boolean     // RTS pause on cooldown after unpause
SwitchCooldown:  [0, 2.0s]  // Prevent mode-flicker exploits
```

---

## 7. Mechanics Derivation

### Solution 1: Dual-Stat Weapons

Each weapon has two stats:

| Weapon | DPS_RTS | Damage_Action | Notes |
|---|---|---|---|
| Sniper Rifle | Low | Very High | Rewards headshots in Action |
| Assault Rifle | Medium | Medium | Versatile |
| Shotgun | Low | Very High | AI poor at range; devastating up-close |
| Blaster Pistol | Medium-High | Medium | Balanced across modes |
| Vibroblade | High | High (timing) | Melee; Action requires parry timing |

### Solution 2: Time Dilation in RTS

RTS mode slows to **10% speed** (does NOT fully pause):
- Player still feels live action
- Enemy projectiles visible for planning
- Prevents full pause-scum exploitation

### Solution 3: Mode-Specific Ability Variants

| Ability | Action Mode | RTS Mode |
|---|---|---|
| Blade Rush | Directional input + timing → 100% damage if perfect | Auto-executes on target → 80% damage, guaranteed hit |
| Force Push | Aim + timing-based interrupt | Auto-executed, AOE, lower KB distance |
| Sniper Shot | Manual aim → crit on weak point | Stat-based → normal damage, no crit available |

---

## 8. Decision Density Analysis

### Action Mode Target: 18–28 meaningful decisions/minute
- Aiming at priority target: every 2–3s
- Ability usage: every 8–12s
- Cover positioning: every 5–10s
- Mode switch consideration: every 15–30s

### RTS Mode Target: 8–12 meaningful decisions/minute
- Unit positioning per pause cycle: every 15s
- Ability queue prioritization: every 10s
- Formation adjustment: every 20–30s
- Mode switch for critical execution: situational

### Critical Balance Rule
If Action mode achieves 30% higher damage than RTS, the 2x decision density cost is justified.
If RTS achieves similar damage with half the effort, Action becomes obsolete.

---

## 9. Failure Modes & Dominant Strategies

### Exploit 1: Pause-Scum Immortality
- **Behavior**: Stay in RTS, pause before every attack, reposition perfectly
- **Counter**: 5-second pause cooldown after unpause. Enemies gain "Adaptation" stacks (+10% damage per pause cycle, resets after 30s real-time)

### Exploit 2: Action-Only Deathball
- **Behavior**: Ignore squad AI, solo everything in Action mode
- **Counter**:
  - Encounters scale to assume 4-unit squad DPS
  - Solo player takes +50% incoming damage ("Exposed Target" debuff)
  - Certain enemies require simultaneous multi-angle attacks

### Exploit 3: Mode-Flicker Invincibility
- **Behavior**: Rapidly switch modes to exploit camera transition invincibility
- **Counter**: 2-second switch cooldown. Switching mid-combat incurs 1s vulnerability (+30% damage taken)

### Exploit 4: RTS Kiting Trivialization
- **Behavior**: Endlessly kite melee enemies in slow-motion
- **Counter**:
  - Enemy AI has "Sprint" ability on 20s cooldown (ignores slow-motion)
  - Encounter timers (defend objectives, environmental hazards)

### Degenerate Strategy Risk Assessment

| Risk | Detection Metric | Fix |
|---|---|---|
| Action too strong | <15% combat time in RTS | Buff AI companion RTS damage by 15-20%; add Tactical Advantage bonus |
| RTS too strong | >70% combat time in RTS | Add Adrenaline System; boss weak points require Action precision |

---

## 10. Balance Model

### Damage Output Parity Formula
For a 60-second encounter:
```
DPS_Action_Solo ≈ DPS_RTS_Squad × 0.7
```

Action mode trades squad power for:
- Direct control over positioning
- Precision damage (headshots, weak points)
- Interrupt potential (stagger enemies mid-attack)

### Companion AI Scaling
When player is in Action mode, AI companions operate at:
- **60% of RTS-commanded efficiency** (pathfinding less optimal, ability usage reactive not predictive)

### Difficulty Modifiers

| Difficulty | Action Enemy HP | RTS Enemy HP | Notes |
|---|---|---|---|
| Easy | 100% | 100% | Both modes viable |
| Normal | 120% | 100% | Slight RTS advantage (intended) |
| Hard | 150% | 120% | Action requires high skill |
| Nightmare | 150% | 150% | Both modes mandatory |

**Nightmare Encounter Design**:
- Opening 30s → RTS positioning benefits
- Middle 60s → Action mode DPS checks
- Final 20s → RTS survival coordination

---

## 11. Progression Scaling Model

### Character Power Growth

**Health**:
```
HP(level) = 100 + 25 × level
```
Linear growth, capped at level 30 (850 HP).

**Ability Damage**:
```
Damage(level) = BaseDamage × (1 + 0.1 × level) × (1 + TalentMultiplier)
```
Soft exponential via talents (max +50% from tree), hard cap prevents one-shot issues.

**Weapon Damage**:
```
DPS_RTS(tier)    = 10 × tier^1.15
Damage_Action(tier) = 15 × tier^1.15
```
Slightly super-linear (1.15 exponent) to make upgrades feel meaningful, capped at Tier 10.

### Enemy Scaling

**Enemy HP**:
```
HP(level) = 80 + 30 × level × (1 + 0.05 × EncounterIndex)
```

**Enemy Count by Level Range**:
| Level Range | Enemy Count | Special |
|---|---|---|
| 1–10 | 3–6 | Basic enemies |
| 11–20 | 6–10 | + 1 elite |
| 21–30 | 8–12 | + 2 elites OR 1 boss |

**Critical Rule**: Total encounter DPS budget increases linearly, but health pools increase faster than player solo damage. This mathematically enforces squad usage.

---

## 12. Scope & Technical Requirements

### Engine: Unity 2022.3 LTS / 2023.x

**Rationale**:
- Direct compatibility with existing KotOR-Unity codebase
- Robust NavMesh AI system for companion pathfinding
- Timeline/Cinemachine for seamless camera transitions
- Universal Render Pipeline (URP) for modern shader support
- Strong asset pipeline for KotOR format integration

### Core Systems Required

**AI Architecture**:
```
Companion AI: 3-tier behavior trees
  Tier 1 (RTS):    Execute queued orders with pathfinding
  Tier 2 (Action): Autonomous cover-seeking, target prioritization
  Tier 3 (Hybrid): Interrupt system for player "manual override"

Enemy AI: Competent in slow-motion (RTS) AND real-time (Action)
  State machine: Patrol → Detect → Engage → Flank/Suppress → Execute
  Difficulty modifiers scale reaction time (not just stats)
```

**Camera System**:
```
Action Camera:  Third-person with ADS zoom (Mass Effect 2 style)
RTS Camera:     Orthographic/high-angle, smooth 1.5s spline transition
Hybrid "Tactical Aim": Slow-motion third-person for precision shots
```

**Combat Resolution**:
```
Action Mode: Hitscan + projectile VFX OR true projectile physics (by weapon type)
RTS Mode:    Stat-based dice rolls with hit chance visualization (XCOM-lite)
             No misses below 95% — removes frustrating whiff on high-probability shots
```

### Team Size Estimates

| Team Size | Timeline |
|---|---|
| Solo Dev (experienced) | 18–24 months |
| Small Indie (3–5 people) | 12–18 months |
| Mid Studio (10–15 people) | 9–12 months |
| Community (open source) | Ongoing / milestone-based |

---

## 13. MVP Cutline

### Must-Have (Core Loop Functional)

**Playable Content**:
- 1 player character with 2 companions
- 3 weapon types (Blaster Rifle, Shotgun, Blaster Pistol)
- 3 abilities per character
- 5 enemy types (Melee, Ranged, Shielded, Elite, Boss)
- 3 mission types (Combat, Defense, Assassination)
- 1 hub area + 3 combat zones (KotOR modules)

**Mode Functionality**:
- RTS mode with pause/slow-motion (10% time scale)
- Action mode with functional gunplay (ADS, recoil, hit detection)
- Smooth camera transition (max 2s, no disorientation)
- Mode switch with 2s cooldown

**Progression**:
- 10 character levels
- 1 talent tree per character (8 nodes)
- 3 weapon tiers

**Failure Condition**:
- Party wipe = mission restart

**Mastery Tracking**:
- Action mode: headshot accuracy tracking
- RTS mode: ability combo efficiency (multi-target overlap)

### Post-MVP (Expansion)
- Destructible cover
- Stealth mechanics
- Crafting system
- Branching story choices (KotOR dialogue trees)
- Advanced formation commands (wedge, overwatch, breach)
- Enemy weak-point system requiring Action mode precision
- "Tactical Cam" hybrid mode
- Co-op mode (Player 2 controls companion)
- Full NWScript scripting support
- VR mode for both cameras

---

## 14. Validation Classification

### Proven Mechanics (P)
- ✅ Pausable tactical combat with real-time unpause (Dragon Age Origins)
- ✅ Third-person shooting with cover (Mass Effect 2)
- ✅ Companion AI with command queue (KOTOR, Dragon Age)
- ✅ Dual-stat weapons (hero units in Total War: Warhammer)

### Empirical Mechanics (E) — Requires Testing
- ⚠️ Dynamic mode switching mid-combat (Natural Selection 2, Battlefield Commander)
- ⚠️ Time-dilation (10% speed) instead of full pause (Superhot, Fallout 4 VATS)
- ⚠️ Mode-specific ability variants (MOBA Quick Cast analog)

### Speculative Mechanics (S) — Requires Playtesting
- 🔬 Adrenaline/Tactical Advantage asymmetry to prevent mode dominance
- 🔬 Enemy AI adaptation to pause frequency
- 🔬 Telemetry-driven mode balancing (buff underused mode)

---

## 15. Critical Design Challenges

### Challenge 1: The "Why Switch?" Problem
**Solution** — Encounter Archetypes:

| Type | Description | Optimal Mode |
|---|---|---|
| A: Swarm | 8+ weak enemies | RTS (multi-target ability queues) |
| B: Sniper Duel | Few high-HP enemies at range | Action (precision damage) |
| C: Siege | Defend objective from waves | RTS (positioning, cooldown management) |
| D: Assassination | One boss with weak points | Action (react to telegraphs, crit zones) |

**Rule**: Using wrong mode = 40% longer clear time OR risk of failure.

### Challenge 2: Camera Disorientation
- 1.5s smooth spline transition (no instant cut)
- Brief "orientation pulse" highlight on player character after switch
- Optional: "Tactical Aim" hybrid mode (zoomed third-person + slow-motion)

### Challenge 3: Companion AI Competence
**Tuning Target**:
- Action Mode AI: 60% as effective as RTS-commanded (contributes but doesn't trivialize)
- RTS Commanded AI: 100% efficiency (player earns this via skill and planning)

---

## 16. Implementation Roadmap

### Phase 1: Foundation (Weeks 1–4)
- [x] Project structure and documentation
- [x] Core GameManager with mode-awareness
- [x] Mode Switch System
- [x] Basic player controller (action)
- [ ] Action camera (third-person)
- [ ] Basic combat hit detection
- [ ] 1 enemy type, 1 weapon

### Phase 2: RTS Layer (Weeks 5–7)
- [ ] RTS camera
- [ ] Camera transition system (spline interpolation)
- [ ] Time dilation system (10% speed)
- [ ] Click-to-move command system
- [ ] Ability queue
- [ ] Mode switch cooldown + vulnerability window

### Phase 3: AI (Weeks 8–10)
- [ ] Companion AI behavior trees (both modes)
- [ ] Enemy AI state machine
- [ ] Formation system
- [ ] Cover-seeking AI

### Phase 4: Progression (Weeks 11–12)
- [ ] Level system
- [ ] Talent trees
- [ ] Weapon tiers and dual-stat system
- [ ] Mode affinity tracker

### Phase 5: KotOR Integration (Weeks 13–16)
- [ ] Full module loading with new systems
- [ ] Character data from KotOR files (UTC format)
- [ ] Item data from KotOR files (UTI format)
- [ ] Dialogue system (DLG parser + UI)
- [ ] NWScript interpreter (basic conditionals)

### Phase 6: Balance & Polish (Weeks 17–20)
- [ ] Encounter archetype testing
- [ ] Mode balance tuning
- [ ] UI polish (HUD, mode indicator, RTS command UI)
- [ ] Save/load system
- [ ] Performance optimization

### Phase 7: MVP Release (Week 21+)
- [ ] 3 playable KotOR modules
- [ ] Complete progression loop
- [ ] Community feedback integration
- [ ] Documentation and contributing guides

# Technical Architecture — KotOR-Unity MRL_GameForge v2

## System Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        GameManager                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │ ModeSwitchSys│  │  EventBus    │  │ KotOR ModuleLoader   │  │
│  └──────┬───────┘  └──────┬───────┘  └──────────────────────┘  │
│         │                  │                                      │
│  ┌──────▼───────┐  ┌──────▼───────┐  ┌──────────────────────┐  │
│  │ CameraSystem │  │ CombatSystem │  │ ProgressionSystem    │  │
│  │  ┌─────────┐ │  │ ┌──────────┐ │  │ ┌──────────────────┐ │  │
│  │  │ActionCam│ │  │ │DamageSys │ │  │ │  LevelSystem     │ │  │
│  │  │RTSCam   │ │  │ │CoverSys  │ │  │ │  TalentTree      │ │  │
│  │  │Transition│ │  │ │AbilitySys│ │  │ │  ModeAffinity    │ │  │
│  │  └─────────┘ │  │ └──────────┘ │  │ └──────────────────┘ │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
│                                                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │
│  │  PlayerCtrl  │  │  CompanionAI │  │  EnemyAI             │  │
│  │  ┌─────────┐ │  │ ┌──────────┐ │  │ ┌──────────────────┐ │  │
│  │  │ActionCtl│ │  │ │RTSBehav. │ │  │ │  StateMachine    │ │  │
│  │  │RTSCtrl  │ │  │ │ActionBeh.│ │  │ │  ThreatSystem    │ │  │
│  │  │InputHndl│ │  │ │OrderQueue│ │  │ │  AdaptationSys   │ │  │
│  │  └─────────┘ │  │ └──────────┘ │  │ └──────────────────┘ │  │
│  └──────────────┘  └──────────────┘  └──────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Script Dependency Map

### Core Layer (No dependencies on other game scripts)
```
EventBus.cs             → Static event system
GameConstants.cs        → All numeric constants
GameEnums.cs            → All enumerations
```

### Data Layer
```
PlayerStats.cs          → depends on: GameEnums, GameConstants
WeaponData.cs           → depends on: GameEnums
AbilityData.cs          → depends on: GameEnums
CompanionData.cs        → depends on: PlayerStats
```

### System Layer
```
GameManager.cs          → depends on: EventBus, GameEnums, KotOR.ModuleLoader
ModeSwitchSystem.cs     → depends on: GameManager, EventBus, CameraTransitionController
CombatResolver.cs       → depends on: WeaponData, AbilityData, PlayerStats
DamageSystem.cs         → depends on: CombatResolver, PlayerStats
```

### Controller Layer
```
PlayerController.cs         → depends on: ModeSwitchSystem, PlayerStats
ActionPlayerController.cs   → depends on: PlayerController, DamageSystem
RTSPlayerController.cs      → depends on: PlayerController, CompanionAI
CameraTransitionController.cs → depends on: ModeSwitchSystem
ActionCamera.cs             → depends on: CameraTransitionController
RTSCamera.cs                → depends on: CameraTransitionController
```

### AI Layer
```
CompanionAI.cs          → depends on: PlayerStats, CombatResolver, ModeSwitchSystem
CompanionBehaviorTree.cs → depends on: CompanionAI
OrderQueue.cs           → depends on: CompanionAI
EnemyAI.cs              → depends on: PlayerStats, CombatResolver
EnemyStateMachine.cs    → depends on: EnemyAI
ThreatSystem.cs         → depends on: EnemyAI, PlayerStats
```

### KotOR Integration Layer
```
BifReader.cs            → No game deps (pure file IO)
ErfReader.cs            → No game deps
RimReader.cs            → No game deps  
GffReader.cs            → No game deps
MdlReader.cs            → No game deps
TpcReader.cs            → No game deps
AudioLoader.cs          → depends on: NAudio
ModuleLoader.cs         → depends on: BifReader, ErfReader, RimReader, GffReader
EntitySpawner.cs        → depends on: ModuleLoader, CompanionAI, EnemyAI
```

## Mode Switch State Machine

```
         [IDLE - ACTION]
               │
       Tab pressed + cooldown=0
               │
               ▼
    [TRANSITIONING: ACTION→RTS]
    • Camera spline starts (1.5s)
    • Player +30% damage taken
    • Input locked
    • Time scale → 0.1 over 0.5s
               │
         1.5s elapsed
               │
               ▼
         [IDLE - RTS]
               │
       Tab pressed + cooldown=0
               │
               ▼
    [TRANSITIONING: RTS→ACTION]
    • Camera spline reverses (1.5s)
    • Player +30% damage taken
    • Input locked
    • Time scale → 1.0 over 0.5s
               │
         1.5s elapsed
               │
               ▼
         [IDLE - ACTION]
```

## Combat Resolution Pipeline

### Action Mode Pipeline
```
Player Input (Fire)
    → RaycastHit / Projectile
    → HitDetection.cs
    → DamageSystem.ApplyDamage(target, Damage_Action × skill_multiplier)
    → Enemy.TakeDamage()
    → VFX spawn at hit point
    → UI update (damage number float)
```

### RTS Mode Pipeline
```
Player orders ability on target
    → AbilityQueue.Enqueue(ability, target, caster)
    → Unpause / execute at 10% speed
    → CombatResolver.RollHit(caster, target)
        → hitChance = BaseAccuracy + AttackBonus - DefenseRating
        → if hitChance >= 0.95 → guaranteed hit
        → else → random(0,1) < hitChance
    → if hit → DamageSystem.ApplyDamage(target, DPS_RTS × timeDelta)
    → HitChanceUI.ShowVisualization(hitChance, target)
```

## Camera Spline Transition

```
Action Camera Position:  player.position + offset(1.5, 2.0, -3.0)  // over-shoulder
RTS Camera Position:     encounter.center + offset(0, 25, -15)       // elevated

Transition:
  t = 0 → 1 over 1.5 seconds (Mathf.SmoothStep)
  currentPos = Vector3.Lerp(actionPos, rtsPos, EaseInOut(t))
  currentRot = Quaternion.Slerp(actionRot, rtsRot, EaseInOut(t))
  
  At t=0.5:
    → Flash player character with "orientation pulse" highlight (0.3s)
    → Play mode-switch audio cue
```

## Save Data Schema

```json
{
  "version": "2.0",
  "timestamp": "2026-03-01T00:00:00Z",
  "player": {
    "level": 10,
    "health": 350,
    "shield": 100,
    "position": [12.5, 0.0, -8.3],
    "facing": [0.0, 180.0],
    "abilities": [
      { "id": "blade_rush", "cooldown_remaining": 0.0 },
      { "id": "force_push", "cooldown_remaining": 3.2 }
    ],
    "equipped_weapon": "blaster_rifle_t2",
    "talent_nodes": ["marksman_1", "marksman_2", "tactical_eye_1"]
  },
  "companions": [
    {
      "id": "companion_0",
      "health": 280,
      "position": [11.0, 0.0, -7.5],
      "order_queue": []
    }
  ],
  "mode_state": {
    "active_mode": "Action",
    "switch_cooldown_remaining": 0.0,
    "pause_cooldown_remaining": 0.0,
    "time_scale": 1.0
  },
  "mode_preference_history": {
    "encounters_in_rts": 12,
    "encounters_in_action": 8,
    "rts_affinity_bonus_active": false,
    "action_affinity_bonus_active": true
  },
  "progression": {
    "experience": 4250,
    "xp_to_next_level": 5000
  },
  "module": {
    "name": "danm14aa",
    "kotor_dir": "C:/KotOR",
    "target_game": "KotOR"
  }
}
```

## Encounter Archetype Definitions

### Type A: Swarm
```
EnemyCount:     8–12 (weak, low HP)
OptimalMode:    RTS
ActionPenalty:  Solo player cannot interrupt all enemies; 50% risk of party wipe
RTSBonus:       Multi-target abilities chain-hit 3+ enemies per cast
```

### Type B: Sniper Duel
```
EnemyCount:     2–4 (high HP, high defense)
OptimalMode:    Action
RTSPenalty:     No crit zone access; hit chance reduced by armor
ActionBonus:    Headshot multiplier 2.5x; weak point crit 3.0x
```

### Type C: Siege
```
EnemyCount:     Waves (6–8 per wave, 3 waves)
OptimalMode:    RTS
ActionPenalty:  Cannot cover all approach vectors simultaneously
RTSBonus:       Formation commands block chokepoints; cooldown management between waves
```

### Type D: Assassination
```
EnemyCount:     1 Boss + 2–4 adds
OptimalMode:    Hybrid (RTS for adds, Action for boss weak points)
RTSPenalty:     No weak point crits; boss enters rage if adds survive >20s
ActionBonus:    Precision weak point shots during telegraphed windows
```

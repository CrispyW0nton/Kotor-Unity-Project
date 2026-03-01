# KotOR-Unity Project

A community-driven Unity port of **Star Wars: Knights of the Old Republic (2003)**, built on top of the original [KotOR-Unity](https://github.com/reubenduncan/KotOR-Unity) conversion layer by reubenduncan.

---

## Project Vision

This project extends the KotOR-Unity engine conversion layer into a **fully playable Unity-native experience**, introducing a revolutionary **Hybrid Command System** that blends:

- **RTS Commander Mode** — Pausable tactical combat, isometric camera, squad-level ability queuing (Dragon Age: Origins style)
- **Action Mode** — Third-person direct control with real-time gunplay, melee, and precision aiming (Mass Effect 2 style)

The player can **dynamically switch between both modes mid-encounter**, with each mode offering distinct strategic advantages.

---

## Current Status

### Inherited from KotOR-Unity (reubenduncan)
- [x] Extract files from BIF, ERF, and RIM archives
- [x] Parse Aurora/Odyssey file formats into memory
- [x] Binary models (MDL/MDX) loaded and viewable with limited animation
- [x] Materials from TPC and TGA textures with lightmap support
- [x] GFF files loaded and exportable to JSON
- [x] Module layouts loaded
- [x] Audio files loaded
- [x] Full modules with room placement, ambient music, characters, doors, placeables
- [x] Module traversal with standard player controllers

### New MRL_GameForge v2 Additions (This Fork)
- [x] Project architecture and design documentation
- [x] Hybrid Mode Switch System (RTS <-> Action)
- [x] Dual-camera system (Tactical RTS cam + Action third-person cam)
- [x] Extended GameManager with mode-awareness
- [x] Companion AI foundation with dual behavior trees
- [x] Combat system with dual-stat weapons
- [x] Ability system with mode-variant execution
- [x] Progression system (level scaling, talent trees)
- [x] Save/Load system with mode preference tracking
- [ ] Full KotOR module loading integration with new systems
- [ ] VR mode integration with new cameras
- [ ] Scripting (NWScript) support
- [ ] Full combat resolution engine
- [ ] Dialogue system integration

---

## Architecture Overview

```
Assets/Scripts/
├── Core/               # GameManager, ModeSwitchSystem, EventBus
├── Player/             # PlayerController, PlayerStats, InputHandler
├── Camera/             # ActionCamera, RTSCamera, CameraTransitionController
├── Combat/             # CombatResolver, DamageSystem, CoverSystem
├── Weapons/            # WeaponBase, WeaponDatabase, dual-stat weapon data
├── Abilities/          # AbilityBase, AbilityDatabase, mode-variant abilities
├── AI/
│   ├── Companion/      # CompanionAI, CompanionBehaviorTree, OrderQueue
│   └── Enemy/          # EnemyAI, EnemyBehaviorTree, ThreatSystem
├── Progression/        # LevelSystem, TalentTree, ExperienceManager
├── UI/                 # HUDController, RTSCommandUI, ActionUI, ModeIndicator
├── SaveSystem/         # SaveManager, GameState, ModePreferenceTracker
└── KotOR/              # All KotOR file format readers and parsers
    ├── FileReaders/    # BIF, ERF, RIM, MOD archive readers
    ├── Parsers/        # GFF, MDL/MDX, TPC/TGA, audio parsers
    ├── Models/         # Data model classes for KotOR formats
    ├── Audio/          # KotOR audio integration
    └── Modules/        # Module loader, room placement, entity spawning
```

---

## Getting Started

### Prerequisites
- Unity 2022.3 LTS or later (2023.x recommended)
- A legitimate copy of **Star Wars: Knights of the Old Republic (2003)**
- NAudio (for audio playback)
- fastJSON (for JSON serialization)
- Git

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/CrispyW0nton/Kotor-Unity-Project.git
   cd Kotor-Unity-Project
   ```

2. **Install dependencies**
   - Clone [NAudio](https://github.com/naudio/NAudio) and import into `Assets/Plugins/NAudio`
   - Clone [fastJSON](https://github.com/mgholam/fastJSON) and import into `Assets/Plugins/fastJSON`

3. **Open in Unity**
   - Open the `KotOR-Unity-Project` directory as a Unity project
   - Wait for asset compilation

4. **Configure GameManager**
   - Load scene: `Assets/Scenes/SampleScene`
   - Select the `GameManager` object in the hierarchy
   - Set `Kotor Dir` to your KotOR installation root
   - Set `Target Game` to `KotOR` or `TSL`
   - Enter the `Entry Module` name (module names in `{KotOR Dir}/Modules`)

5. **Configure Mode Settings** (new)
   - Select `ModeSwitchSystem` in the hierarchy
   - Set `Default Mode` to `Action` or `RTS`
   - Configure `Switch Cooldown` (default: 2.0s)
   - Enable/disable `Debug Mode Overlay`

6. **Hit Play**

---

## Hybrid Combat System — Quick Reference

| Feature | RTS Mode | Action Mode |
|---|---|---|
| Camera | Isometric / High-angle | Third-person over-shoulder |
| Time | 10% speed (time-dilation) | Real-time |
| Control | Click-to-command + ability queue | Direct WASD + aim |
| Damage | Stat-based (dice + AI DPS) | Hitscan + skill-based |
| Pause | 5s cooldown after unpause | N/A |
| Companion AI | Full command control | 60% autonomous |
| Best for | Swarm, Siege, Coordination | Sniper Duel, Boss Weak Points |

### Mode Switch
- **Default Keybind**: `Tab` (configurable)
- **Cooldown**: 2.0 seconds
- **Transition Time**: 1.5 seconds (smooth spline camera interpolation)
- **Vulnerability Window**: +30% damage taken during transition

---

## VR Support

VR is working with Oculus Rift. Drag `OVRPlayerController` from `Assets/Resources/PlayerControllers` into the scene.

> Note: VR mode currently only supports Action Mode. RTS VR mode is on the roadmap.

---

## Contributing

Everyone is welcome to contribute! If you have something to add:

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature-name`
3. Commit your changes: `git commit -m "feat: add your feature"`
4. Push and open a pull request

See [CONTRIBUTING.md](Docs/CONTRIBUTING.md) for full guidelines.

### Priority Contribution Areas
- NWScript scripting engine integration
- Full combat resolution (THAC0 → D20 conversion for Unity)
- Dialogue system (DLG file parsing + conversation UI)
- Specular and bump map shader support
- MOD archive and override folder support
- Animation improvement (LIP sync, attack animations)
- Additional KotOR module testing and bug reports

---

## Licensing

This project is released under the **GNU GPLv3**. You are free to copy, distribute, and modify the source however you like, including for commercial use, so long as any derivative work is also released under GPLv3 terms.

**IMPORTANT**: This project requires a legitimate copy of Star Wars: Knights of the Old Republic. No game assets are included or distributed with this project.

See [LICENSE.md](LICENSE.md) for details.

---

## Credits

- **Original KotOR-Unity**: [reubenduncan](https://github.com/reubenduncan/KotOR-Unity)
- **MRL_GameForge v2 Design System**: Community contributors
- **BioWare / Obsidian**: Original game developers (Star Wars: KotOR)
- **NAudio**: Mark Heath
- **fastJSON**: Mehdi Gholam

---

## Links

- [Original KotOR-Unity Repository](https://github.com/reubenduncan/KotOR-Unity)
- [Project Design Document](Docs/Design/PROJECT_PLAN.md)
- [Technical Architecture](Docs/Technical/ARCHITECTURE.md)
- [Contributing Guidelines](Docs/CONTRIBUTING.md)

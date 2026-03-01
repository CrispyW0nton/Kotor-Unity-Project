# Contributing to KotOR-Unity MRL_GameForge v2

Thank you for contributing to this community-driven KotOR port! This document outlines the contribution process.

---

## Code of Conduct

- Be respectful and inclusive
- Keep discussions focused on technical merit
- No redistribution of KotOR game assets

---

## How to Contribute

### 1. Fork and Branch
```bash
git fork https://github.com/CrispyW0nton/Kotor-Unity-Project
git checkout -b feature/your-feature-name
```

### 2. Coding Standards

**C# Style**:
- Follow Microsoft C# conventions
- Use `PascalCase` for classes, `camelCase` for variables
- XML doc comments on all public methods
- No magic numbers — use `GameConstants.cs`
- All scripts in appropriate `Assets/Scripts/` subfolder

**Unity Conventions**:
- Use `[SerializeField]` for inspector-exposed private fields
- Use events via `EventBus` for cross-system communication
- No direct `Find()` or `GetComponent()` in `Update()` — cache references in `Awake()`
- Coroutines for timed sequences, not `Update()` timers

### 3. Testing
- Add `EditMode` tests in `Tests/EditMode/` for logic
- Add `PlayMode` tests in `Tests/PlayMode/` for runtime behavior
- All PRs must pass existing tests

### 4. Submit PR
- Target the `main` branch
- Use the PR template
- Include before/after screenshots for visual changes
- Reference any related issues

---

## Priority Areas

| Area | Difficulty | Impact |
|---|---|---|
| NWScript interpreter | Hard | Critical |
| DLG dialogue system | Medium | Critical |
| MOD archive support | Medium | High |
| Animation improvements | Medium | High |
| Override folder support | Easy | Medium |
| Specular/bump maps | Medium | Medium |
| Bug reports & testing | Easy | High |

---

## Licensing Note

By contributing, you agree your contributions are licensed under GNU GPLv3.

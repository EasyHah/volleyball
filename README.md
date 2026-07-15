# Volleyball Match

The standalone Unity client for third-person volleyball matches. It owns rally
state, rules, ball and player simulation, AI, player controls and match facts.
It does not own player saves, progression, contracts, scouting or calendar state.

## First playable target

The first milestone is an indoor 3v3 rally that starts from an AI serve and ends
only after a point. The created outside hitter remains under player control; the
other five athletes are AI-controlled. Simplified rotation occurs after a side-out.

The player moves and aims. In valid positions the game automatically selects the
volleyball action: receive/dig, spike or block jump. There are no timing-button
minigames.

## Project setup

Open this repository in Unity Hub with Unity `6000.0.43f1`. Unity will create
local `Library/` files; they are intentionally ignored.

The initial manifest contains only basic physics, UI and Test Framework modules.
Add the approved URP, Input System and NavMesh components through Unity Package
Manager after Unity has created `Packages/packages-lock.json`, then commit that
lock file before any feature work begins.

## Repository layout

```text
Assets/VolleyballMatch/Runtime/  Match-domain and Unity adapter code
Assets/VolleyballMatch/Tests/    EditMode and PlayMode test assemblies
Assets/VolleyballMatch/Scenes/   3v3 prototype scenes
Assets/VolleyballMatch/Fixtures/ Read-only MatchContext and MatchResult fixtures
docs/                            Architecture, test and contract-consumption notes
ProjectSettings/                 Version-pinned Unity project metadata
Packages/                        Versioned Unity package manifest and lock file
.github/workflows/               Disabled Windows CI enablement checklist
```

## Contracts

This repository consumes released `volleyball-contracts` artifacts only. During the
initial local phase, copy a released fixture set into
`Assets/VolleyballMatch/Fixtures/` and record the exact contract release in
`docs/contract-consumption.md`. Never change a fixture or Schema here to make the
client compile; propose that change in `volleyball-contracts` instead.

## Verification

After Unity is installed, run EditMode tests before every pull request. The Windows
CI workflow remains disabled until a Windows x64 machine has generated the complete
Unity project files and package lock. Then configure Unity activation and enable a
Windows x64 IL2CPP build with the same EditMode tests. Validate every playable
candidate manually on a physical Windows x64 PC.

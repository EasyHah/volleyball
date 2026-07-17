# Volleyball

A unified Unity game repository for real-time volleyball matches and the career
experience around them. Match and career code share one Unity project and build,
while folders and Assembly Definitions keep their responsibilities isolated.

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
Assets/Volleyball/Match/     Match runtime, scenes and tests
Assets/Volleyball/Career/    Career domain, application and presentation modules
Assets/Volleyball/Shared/    Versioned Match/Career contracts and boundary tests
Assets/Volleyball/Bootstrap/ Cross-module composition and future entry scenes
Assets/Volleyball/Shared/Tests/Fixtures/ Read-only MatchContext and MatchResult fixtures
docs/                            Architecture, test and contract-consumption notes
docs/changes/                    Match/Career change records and handoff highlights
ProjectSettings/                 Version-pinned Unity project metadata
Packages/                        Versioned Unity package manifest and lock file
.github/workflows/               Disabled Windows CI enablement checklist
```

The module layout is documented in `docs/changes/unified-unity-modules-plan.md`. Match
code now lives under `Match/`; Career and Bootstrap have explicit assembly
boundaries ready for the first playable career loop.

All implementation changes are recorded in `docs/changes/`. Changes that affect
the other module are marked as cross-module highlights with explicit owner,
consumer, compatibility and required follow-up.

The first Shared boundary is now available as the Unity-free
`Volleyball.Shared` assembly. It defines stable career `PlayerId`/`TeamId`
values, immutable ability snapshots, and hash-validated `MatchContextV1` and
`MatchResultV1` payloads. The existing prototype `PlayerId` remains a temporary
court slot and must not be persisted as career identity.

## Contracts

Match and Career communicate only through the local, Unity-free `Shared`
contracts. Record any fixture revision in `docs/contract-consumption.md`; do not
hand-edit copied payloads.

## Verification

After Unity is installed, run EditMode tests before every pull request. The Windows
CI workflow remains disabled until a Windows x64 machine has generated the complete
Unity project files and package lock. Then configure Unity activation and enable a
Windows x64 IL2CPP build with the same EditMode tests. Validate every playable
candidate manually on a physical Windows x64 PC.

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
Assets/VolleyballMatch/Runtime/  Match-domain and Unity adapter code
Assets/VolleyballMatch/Tests/    EditMode and PlayMode test assemblies
Assets/VolleyballMatch/Scenes/   3v3 prototype scenes
Assets/VolleyballMatch/Fixtures/ Read-only MatchContext and MatchResult fixtures
docs/                            Architecture, test and contract-consumption notes
ProjectSettings/                 Version-pinned Unity project metadata
Packages/                        Versioned Unity package manifest and lock file
.github/workflows/               Disabled Windows CI enablement checklist
```

The target module layout is documented in
`docs/unified-unity-modules-plan.md`. Existing match code remains in place until
the shared contracts and module-boundary tests are ready for a mechanical move.

## Contracts

The current Match module consumes released `volleyball-contracts` fixtures. The
unified-game migration will promote the source contract types into the local
`Shared` module while keeping the payload versioned and immutable. Until that
migration is complete, record every imported fixture release in
`docs/contract-consumption.md` and do not hand-edit copied payloads.

## Verification

After Unity is installed, run EditMode tests before every pull request. The Windows
CI workflow remains disabled until a Windows x64 machine has generated the complete
Unity project files and package lock. Then configure Unity activation and enable a
Windows x64 IL2CPP build with the same EditMode tests. Validate every playable
candidate manually on a physical Windows x64 PC.

# Volleyball

A unified Unity game repository for real-time volleyball matches and the career
experience around them. Match and career code share one Unity project and build,
while folders and Assembly Definitions keep their responsibilities isolated.

## Match prototype status

The repository contains automated indoor 3v3 and 6v6 physical-match sandboxes.
Both currently run all athletes through AI; direct control of the created outside
hitter is a future integration target, not an implemented feature. Simplified
rotation occurs after a side-out.

The intended direct-control design lets the player move and aim while valid
positions automatically select receive/dig, spike or block jump. There are no
timing-button minigames, but this input loop has not been connected yet.

The repository now also includes the formal physical match target at
`Assets/Volleyball/Match/Scenes/FormalIndoor6v6.unity`: twelve players, six-position
rotation, 25-point win-by-two scoring and physical blocks. The result currently
contains one coarse V1 statistics record for each of the
twelve players. The original 3v3 scene remains an automated compatibility baseline.

## Project setup

Open this repository in Unity Hub with Unity `6000.3.20f1`. Unity will create
local `Library/` files; they are intentionally ignored.

The committed manifest and lock file are authoritative. They currently contain
the Unity 6.3 built-in physics/UI modules, Test Framework, the official Input
System used by Career menus, and the Newtonsoft package used by the Editor-side
MenShen benchmark. The MenShen assembly and credentials stay Editor-only, although
the package's global `Newtonsoft.Json.dll` currently remains in Player builds.
URP and NavMesh remain separate reviewed changes and must not be added as part of
unrelated feature work.

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

Run EditMode tests before every pull request. The dedicated
`Volleyball.Bootstrap.Editor.CareerWindowsDevelopmentBuild.Build` entry point now
builds only the Career vertical-slice scene as Windows x64 IL2CPP with Development
and AllowDebugging, checks `BuildReport`, and publishes a local manifest under
ignored `Builds/Windows/`. The Unity CI workflow remains disabled until a matching
runner and activation are configured. Enable the same EditMode and Career PlayMode
tests there, and still validate every playable candidate manually with keyboard and
an XInput controller on a physical Windows x64 PC.

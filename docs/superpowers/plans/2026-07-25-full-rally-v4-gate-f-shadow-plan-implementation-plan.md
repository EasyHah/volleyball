# Full Rally V4 Gate F Shadow Plan Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record deterministic, command-free, twelve-player responsibility-plan shadow revisions in Replay V4 without changing formal-rally behavior.

**Architecture:** Add pure immutable shadow-plan values and a deterministic constrained composer under `FullRallyV3`, retained only as rule/world-fact authority. Extend Replay V4, then have the director build one world snapshot and one shared artifact per accepted contact, compose two team plans, and publish only to the replay recorder.

**Tech Stack:** Unity 6000.0.43f1, C#, NUnit EditMode/PlayMode, `ContractJson`, native `MatchReplayV4`.

---

## File Structure

- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/PlayerResponsibilityAssignmentV3.cs`: task, condition, spatial claim, branch, value, rank.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/TeamRallyPlanV3.cs`: six-player team plan and candidate evidence.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/RallyPlanV3.cs`: revision with source snapshot, both plans, artifact identity, coverage.
- Create `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/DeterministicRallyPlanComposerV3.cs`: pure candidate filter, stable beam, coverage evaluator.
- Modify `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs` and `Assets/Volleyball/Shared/Runtime/ContractJson.cs`: V4 shadow record and canonical JSON.
- Modify `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs` and `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs`: replay-only producer and recorder.
- Create `Assets/Volleyball/Match/Tests/EditMode/DeterministicRallyPlanComposerV3Tests.cs`; modify `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs` and `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs`.

### Task 1: Add Canonical Replay V4 Shadow Records

**Files:** `Assets/Volleyball/Shared/Runtime/MatchReplayV4.cs`, `Assets/Volleyball/Shared/Runtime/ContractJson.cs`, `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs`.

- [ ] Write failing `ReplayV4_ShadowRecords_RoundTripInCanonicalHash` test using revision 0, source event 3, one Home and one Away plan, six sorted unique assignments per plan, and a common 64-character artifact hash. Assert JSON round-trip and stable replay hash.
- [ ] Add failure cases for missing Home/Away, duplicate primary players/ranks, mismatched artifact identity, unsupported task/claim/coverage values, and non-finite values.
- [ ] Run the filtered EditMode test. Expected red state: `ReplayShadowRecordV4` missing.
- [ ] Add `ReplayShadowAssignmentRecordV4`, `ReplayTeamRallyPlanRecordV4`, `ReplayCoverageDecisionRecordV4`, and `ReplayShadowRecordV4`. Require finite values, fixed allowed strings, exact Home/Away pair, six distinct rank-ordered assignments, and common SHA-256 identity.
- [ ] Add nullable `Shadow` to `MatchReplayEventV4`, retaining its existing constructor as an overload that passes null.
- [ ] In `ContractJson`, write optional `shadow` after `ruleDecision`; serialize revision, source sequence, identity, Home/Away order, rank order, and coverage. Parse absence as null and do not reorder existing V4 fields.
- [ ] Run all `MatchReplayV4Tests`; commit `feat: add replay v4 shadow plan records`.

### Task 2: Add Command-Free Shadow Domain Values

**Files:** the three new Shadow value files and `Assets/Volleyball/Match/Tests/EditMode/DeterministicRallyPlanComposerV3Tests.cs`.

- [ ] Write tests that a team plan accepts exactly six eligible matching-side players, rejects duplicate player IDs/ranks/claims, and defensively copies collections. Test `RallyPlanV3` requires Home/Away plans, non-negative revision/source sequence, and common artifact identity.
- [ ] Run the fixture. Expected red state: missing shadow-type compilation failure.
- [ ] Define task, condition, claim, and branch enums. Implement `PlayerResponsibilityAssignmentV3` with player, enum values, finite value and rank. Implement `TeamRallyPlanV3` validation/copies. Implement `RallyPlanV3` with immutable `RallyWorldSnapshotV3`, both plans, identity, and `PlanCoverageDecision`.
- [ ] Assert at compile-time and review that no shadow file references player agents, director, Unity components, schedulers, or contact APIs.
- [ ] Run fixture; commit `feat: add immutable rally plan shadow domain`.

### Task 3: Compose Deterministic Legal Plans and Coverage

**Files:** `Assets/Volleyball/Match/Runtime/Domain/FullRallyV3/Shadow/DeterministicRallyPlanComposerV3.cs` and its EditMode fixture.

- [ ] Write failing tests for six current team players, off-court exclusion, libero/back-row restrictions, duplicate and exclusive spatial-claim rejection, deterministic ties under reversed input, and common artifact identity for Home/Away plans.
- [ ] Add declared-branch coverage and local/scoped/global/terminal out-of-envelope reason tests; each must prove source plan immutability.
- [ ] Run fixture. Expected red state: composer missing.
- [ ] Implement pure `Compose(RallyWorldSnapshotV3 snapshot, TeamSide side, string trajectoryIdentity)`. Derive candidates only from snapshot/eligibility; filter hard-illegal candidates before scoring; sort descending value then ordinal player ID/task/claim; build a fixed beam only when player and exclusive claim remain unused; return exactly six assignments.
- [ ] Implement pure `EvaluateCoverage(RallyPlanV3 plan, AcceptedRuleEventV3 acceptedEvent)` as value computation only; it cannot replan or command.
- [ ] Run fixture; commit `feat: compose deterministic shadow rally plans`.

### Task 4: Publish Replay-Only Plans from the Formal Director

**Files:** `Assets/Volleyball/Match/Runtime/Presentation/PhysicalMatchRallyDirector.cs` and composer tests.

- [ ] Write an integration test that subscribes to `ReplayShadowPlanRecorded`, executes one accepted contact, receives one revision with two six-player plans/common identity, and observes unchanged scheduled decisions, positions, score, and accepted-contact count.
- [ ] Run it. Expected red state: event missing.
- [ ] Add `public event Action<RallyPlanV3> ReplayShadowPlanRecorded;`. Immediately after successful `ObserveAcceptedContactV3` and before `ReplayContactAccepted`, build `RallyWorldSnapshotV3` from ball, all players, `CreateV3Eligibility(_matchContext)`, V3 touch/rule/court facts, and sequence.
- [ ] Reuse `acceptedTrajectoryArtifact` once for both composition calls; publish the resulting revision only through the new event. A missing artifact fails recording and must not cause another prediction or tactical change.
- [ ] Run focused test, `FullRallyV3RuntimeAdapterTests`, and `FullRallyV3WorldSnapshotTests`; commit `feat: record formal rally shadow plan revisions`.

### Task 5: Attach Revisions to Replay V4 Contacts

**Files:** `Assets/Volleyball/Match/Runtime/Presentation/MatchReplayRecorder.cs` and `Assets/Volleyball/Match/Tests/EditMode/MatchReplayV4Tests.cs`.

- [ ] Write failing mapping tests for all assignment/claim/condition/branch/value/rank fields, common identity, and coverage on `MatchReplayEventV4.Shadow`; reject unmatched shadow callback.
- [ ] Run it. Expected red state: recorder has no shadow subscription.
- [ ] Subscribe/unsubscribe with existing contact events. Hold revision by source sequence because it is emitted before the matching contact notification, consume it during contact record construction, and use private V3-to-V4 mapping methods.
- [ ] Reject duplicate or unresolved pending revision at rally resolution.
- [ ] Run all replay tests; commit `feat: capture shadow plans in replay v4`.

### Task 6: Prove Live-Rally Invariance and Document Evidence

**Files:** `Assets/Volleyball/Match/Tests/PlayMode/FormalSixVsSixReplayPlayModeTests.cs` and new `docs/changes/2026-07-25-full-rally-v4-gate-f-shadow-plans.md`.

- [ ] Write a PlayMode test running the same fixed seed with/without recorder. Assert equal score, accepted-contact count/sequence, V3 transition count, and two plans/common identity per captured contact. Repeat capture and assert byte-identical V4 JSON/hash.
- [ ] Run it. Correct only ordering, identity, or recorder mapping; never modify planners, player agents, scoring, or V3 rules.
- [ ] Run full EditMode and PlayMode suites using Unity batch mode, then run `git diff --check`. Expected: zero failures and no diff output.
- [ ] Record exact suite counts/result paths and byte/hash evidence; state no HTML overlay or tactical authority was added. Commit `test: verify gate f shadow plan invariance`.

## Final Review Checklist

- [ ] Shadow code has no player-agent, director command, scheduler, or Unity-component reference.
- [ ] Every revision is Home plus Away from one artifact identity.
- [ ] Shadow enters canonical Replay V4 but older V4 records without it parse.
- [ ] Formal score, contacts, and V3 transitions are invariant.
- [ ] Full EditMode/PlayMode pass and `git diff --check` is clean.

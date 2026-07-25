# Full Rally V4 Gate F: Twelve-Player Responsibility Plan Shadow

**Date:** 2026-07-25

**Status:** Approved design; implementation planning pending

**Scope:** Add a deterministic, replay-only twelve-player responsibility-plan
shadow to the native V4 match runtime. This is Gate F from the consolidated V4
roadmap.

## Goal

For every relevant accepted-contact revision, construct one immutable plan for
each team from the same observed physical state. The plans explain the six
current players' proposed responsibilities, spatial claims, conditions, and
coverage interpretation. They are diagnostics only: they must never schedule
movement, select a contact, or change the director's score, rule transitions,
or accepted contacts.

## Architecture

### Inputs

The director constructs a single immutable shadow input at a plan trigger:

- current twelve-player physical world facts, eligibility, rotation, and event
  sequence;
- one V4 trajectory artifact identity shared by both team compositions;
- the current V4 execution-envelope and accepted-contact facts where present.

The input is a snapshot. A plan composer may not read mutable player-agent
state, opponent internal intent, future execution samples, or any scheduling
API while composing.

### Domain model

New immutable shadow-domain types provide:

- `RallyPlan`: the revision identity, source snapshot identity, shared physical
  artifact references, both `TeamRallyPlan` values, and coverage result;
- `TeamRallyPlan`: a team-scoped ordered composition, candidate evidence, and
  exactly six primary assignments;
- `PlayerResponsibilityAssignment`: player, task, condition, spatial claim,
  declared branch, value, and deterministic rank;
- explicit spatial-claim, conditional-task, candidate, beam, and
  `PlanCoverageDecision` values.

The composer rejects ineligible players and hard conflicts before scoring:
off-court players, rotation/position restrictions, libero restrictions,
back-row restrictions, duplicate primary ownership, and incompatible spatial
claims cannot be rescued by a high score. Stable total ordering resolves every
tie.

### Orchestration and isolation

At a relevant accepted contact, the director creates the snapshot and acquires
the shared trajectory artifact once. It invokes the pure composer separately
for each team, then joins both results into one revision. The resulting plan is
sent only to replay recording.

No shadow type receives a player agent, director command surface, scheduler,
or contact API. The runtime integration contains no path from a plan or
coverage decision to movement/contact commands. Existing tactical ownership
continues unchanged until later gates deliberately consume this data.

### Replay V4

Replay V4 gains an additive canonical shadow record for each plan revision.
It contains ordered team plans, assignments, claims, conditions, candidate and
beam evidence needed for diagnosis, shared artifact identity, and coverage
decision/reason codes. The canonical JSON serializer and replay hash include
the record. Existing replay consumers remain valid when no shadow records are
present.

## Coverage semantics

Coverage evaluates an accepted contact against declared branches and claim
bounds. A covered contact activates only its declared branch. An
out-of-envelope input produces a bounded local, scoped, global, or terminal
reason code; it does not modify the live rally. The coverage decision is
diagnostic data rather than a replan command.

## Verification

EditMode tests prove:

- each plan has exactly six distinct eligible current players and one primary
  assignment per player;
- all lineup, libero, back-row, and spatial conflicts reject before scoring;
- equal inputs produce byte-stable ordering, plan revisions, and canonical
  replay hashes;
- both teams reference the same trajectory-artifact identity while retaining
  separate team plans;
- coverage uses declared branches and returns bounded reason codes; and
- all shadow APIs are command-free.

PlayMode tests run complete formal rallies and prove every relevant revision
records both plans, while score, accepted-contact count, and V3 rule-transition
count exactly match the non-shadow baseline. Repeated fixed-seed runs produce
the same Replay V4 bytes and hash.

## Non-goals

- No HTML overlay or new replay UI; Gate K owns the dual-view diagnostic UI.
- No replacement of receive, organization, attack, defense, or director
  tactical ownership.
- No perception uncertainty or `CourtAwareness` behavior; that is Gate J.
- No access to hidden opponent final routes or future samples.

## Compatibility and rollout

Gate F requires the native V4 contracts and Replay V4 from Gates A-E. It adds
only optional replay data and keeps its producer isolated behind a feature
boundary so formal gameplay has identical behavior with shadow generation on
or off. Gates G through K consume this frozen diagnostic contract incrementally.

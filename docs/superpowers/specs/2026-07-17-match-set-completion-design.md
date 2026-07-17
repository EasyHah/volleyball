# Match Set Completion Design

**Goal:** Make `Physical3v3Rally` play and finish one 15-point, win-by-two set,
then expose a validated `MatchResultV1` for a future Career consumer.

## Scope

This is Match-only work. It adds no Career scene, Bootstrap flow, persistence,
multi-set match format, substitutions, timeouts, or Shared contract fields.

The scene creates one deterministic six-player sandbox `MatchContextV1`. Future
Bootstrap code may replace that source without changing set rules or result
generation.

## Rules

- Every completed rally awards exactly one point.
- The first team to 15 points wins only when it leads by at least two points.
- A team that wins while already serving keeps service and does not rotate.
- A receiving team that wins takes service and rotates its three court slots by
  one position before the next rally.
- A missed expected contact, contact timeout, ball-ground collision, net
  collision, or other terminal environment collision awards the opponent a point.
- When the set is complete, no new rally is scheduled, the ball is stopped, and
  the scene shows the final score and that a result is ready.

## Architecture

`Volleyball.Match.Domain` owns a Unity-free `MatchSet` aggregate. It receives a
rally winner plus optional point and error attribution; updates score, serving
team, per-team rotation offset, player statistics, and completion state; and can
create `MatchResultV1` from its immutable `MatchContextV1`.

`ThreeVsThreeRallyDirector` remains a Unity adapter. It translates physical ball
events into one terminal rally outcome, identifies the final successful contact
and responsible error player, calls `MatchSet`, updates player court positions
when rotation changes, and stops after completion. It never implements scoring,
rotation, or result validation itself.

## Data And Statistics

The sandbox context contains stable IDs for the blue and orange setter, attacker,
and defender and fixed ability snapshots. `MatchSet` initializes one stats entry
for every context player and returns all six entries in `MatchResultV1`.

- `contacts` increments for each accepted player-ball contact.
- `points` increments for the final successful contact of a winning rally. A
  rally won solely because of an opponent error has no attributed point.
- `errors` increments on the player who misses the expected contact. For a
  terminal environment collision, it increments on the final successful contact
  player on the losing team; a rally with no successful contact has no player
  error attribution.
- `workload` is the sum of accepted contacts plus assigned movement distance in
  metres. It remains finite and non-negative.

## Rally Flow

```text
Start rally with current serving team and rotated court slots
  -> accepted contacts update MatchSet contacts/workload
  -> physical terminal event determines rally winner and attribution
  -> MatchSet awards point, service and any receiving-team rotation
  -> if incomplete, director applies new positions and starts next rally
  -> if complete, director stores MatchResultV1, stops the ball and renders final state
```

## Tests

EditMode tests cover initial service, ordinary rally scoring, receiving-team
service transfer and one-step rotation, serving-team non-rotation, 15-point
completion, deuce continuation, all-six-player result statistics, and result
validation against the original context.

The existing `Physical3v3Rally` PlayMode test becomes a set-completion smoke test:
it waits for a completed result, confirms a valid 15-point win-by-two score,
verifies all six stats entries, and confirms the director stops scheduling
rallies after completion.

## Risks And Boundaries

The physical prototype currently follows a fixed six-contact loop rather than a
full referee model. This milestone treats its existing timeout and terminal
environment events as rally-ending faults. Out-of-bounds, blocks, service faults
and detailed volleyball stat categories are deferred until the physical rules
model owns those distinctions.

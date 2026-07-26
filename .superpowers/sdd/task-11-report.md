# Task 11 Report: Formal Diagnostic Invariance and End-to-End V4

## Outcome

Added formal PlayMode gates that prove optional native V4 diagnostic recording
does not change the fixed-seed authority path. No production isolation change
was necessary: the native recorder records accepted V4 evidence without
changing authoritative RNG, ball state, or candidate selection.

The paired fixture recreates the same V4 formal context with a one-point,
fixed-seed set twice:

- baseline: no diagnostic recorder;
- recorded: only `MatchReplayRecorder` attached and capturing.

It compares winner, score, accepted contacts, V3 transition count, ordered V3
reason codes, and every accepted contact's V4 trajectory cache-key
`BallStateVersion`. The recorded run must also have exactly one diagnostic
replay event for each accepted contact. The assertion log prints the authority
summary for direct diagnosis.

Observed paired summary:

```text
winner=formal-away; score=0-1; contacts=3; v3Transitions=3;
reasons=None,None,None; ballVersions=0,1063815296,1063815296
```

## End-to-end V4 boundaries

- Formal calibration setup now verifies the initialized `MatchContextV4`, each
  actual player-to-snapshot `MatchPlayerBinding`, and the player's V4 derived
  input/result fingerprints and derivation versions.
- The formal set result must carry V4 contract/context/result identities and
  agree with the authority accepted-contact and V3 transition counts.
- Native replay validation now proves V4 context/replay, execution-envelope,
  derived, trajectory-artifact, and trajectory-cache-key identities. Every
  replay rule decision independently asserts V3 rules identity.

## Verification

- Focused diagnostic-invariance PlayMode: `1/1` passed,
  `/tmp/volleyball-v4-invariance-final.xml`.
- Formal replay PlayMode fixture: `4/4` passed,
  `/tmp/volleyball-v4-replay.xml`.
- Required combined PlayMode command: `12/12` passed in `413.46s`,
  `/tmp/volleyball-v4-playmode-final.xml`.
  - Attack-chain calibration: `3/3` passed.
  - Formal rally: `5/5` passed.
  - Formal replay: `4/4` passed.
- `git diff --check`: clean.

## External-review correction: accepted authority fingerprints

The original paired run compared the authority summary, V3 reasons, and
trajectory cache-key ball-state versions, but did not prove that the recorder
could not alter the selected accepted candidate's full execution evidence.

`ReplayContactAccepted` now records an ordered, baseline-visible fingerprint
for every accepted contact before any optional recorder is attached. Each
fingerprint includes the actor ID, action, V4 classification kind, tested and
executable envelope identities, executable sample envelope identity and
sampling key, sample candidate category, source-intent/candidate identities,
and exact IEEE-754 bits for target, velocity, and effort. The recorder-off and
recorder-on sequences must compare exactly in order.

Focused verification after the correction: `1/1` passed,
`/tmp/volleyball-v4-invariance-fingerprint.xml`.

Required combined PlayMode verification after the correction: `12/12` passed
in `413.41s`, `/tmp/volleyball-v4-playmode-fingerprint.xml`.

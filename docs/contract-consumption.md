# Contract Consumption

## Authority

The match client consumes a frozen `MatchContextV1` and emits a `MatchResultV1`.
The Career module is authoritative for identity, session state, attributes,
fatigue, injury consequences, coach trust, scouting and contracts.

The in-repository source of truth for the V1 boundary is now
`Assets/VolleyballMatch/Shared/Runtime/`. Stable career `PlayerId` and `TeamId`
values are strings; the prototype's blue/orange role identifier is only a court
slot and is never a persisted identity.

## Required checks

Before loading a direct or simulated fixture, the client must reject contexts that
have an unsupported `contractVersion`, a malformed payload, an expired session or
an invalid `contextHash`. It must send the unmodified `sessionId`,
`contractVersion` and `contextHash` with the result.

## Local fixture policy

Fixtures copied under `Assets/VolleyballMatch/Shared/Tests/Fixtures/` are read-only consumer
test data. Their source release, source commit and contract version must be noted in
the importing pull request. Updating a fixture requires a corresponding immutable
contracts release; do not hand-edit copied data.

## Result responsibility

The result reports scores, player statistics, performance signals, workload,
injury observations and structured key events. The client does not calculate
progression, fatigue persistence, injury duration, trust, offers or transfers.

`MatchContextV1` is hash-validated when deserialized. `MatchResultV1` must be
validated against the exact context before Career applies it; its version,
session ID and context hash must all match.

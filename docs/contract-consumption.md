# Contract Consumption

## Authority

The match client consumes a frozen `MatchContextV1` and emits a `MatchResultV1`.
The Career module is authoritative for identity, session state, attributes,
fatigue, injury consequences, coach trust, scouting and contracts.

## Required checks

Before loading a direct or simulated fixture, the client must reject contexts that
have an unsupported `contractVersion`, a malformed payload, an expired session or
an invalid `contextHash`. It must send the unmodified `sessionId`,
`contractVersion` and `contextHash` with the result.

## Local fixture policy

Fixtures copied under `Assets/VolleyballMatch/Fixtures/` are read-only consumer
test data. Their source release, source commit and contract version must be noted in
the importing pull request. Updating a fixture requires a corresponding immutable
contracts release; do not hand-edit copied data.

## Result responsibility

The result reports scores, player statistics, performance signals, workload,
injury observations and structured key events. The client does not calculate
progression, fatigue persistence, injury duration, trust, offers or transfers.

# Contract Consumption

## Authority

The match client consumes a frozen `MatchContextV1` and emits a `MatchResultV1`.
The Career module is authoritative for identity, session state, attributes,
fatigue, injury consequences, coach trust, scouting and contracts.

The in-repository source of truth for the V1 boundary is now
`Assets/Volleyball/Shared/Runtime/`. Stable career `PlayerId` and `TeamId`
values are strings; the prototype's blue/orange role identifier is only a court
slot and is never a persisted identity.

For the first Career milestone this legacy V1 runtime is frozen at tree
`61c7a928f2bf4740defea34c67e5cb108f6dfe76`. V1 remains the only contract consumed
by the frozen physical Match and its existing validators. Detailed Career settlement
facts are not inferred from V1's coarse `points/contacts/errors/workload` values.
They will be added in the sibling `Volleyball.Shared.MatchV2` assembly and consumed
only by `Career.MatchIntegration` and FakeMatch during this milestone. In particular,
`ContractVersions.SupportsMatch(2)` remains false; V2 owns a separate validator.

## Required checks

Before loading a direct or simulated fixture, the client must reject contexts that
have an unsupported `contractVersion`, a malformed payload, an expired session or
an invalid `contextHash`. It must send the unmodified `sessionId`,
`contractVersion` and `contextHash` with the result.

## Local fixture policy

Fixtures copied under `Assets/Volleyball/Shared/Tests/Fixtures/` are read-only consumer
test data. Their source release, source commit and contract version must be noted in
the importing pull request. Updating a fixture requires a corresponding immutable
contracts release; do not hand-edit copied data.

## Result responsibility

V1 reports scores and coarse per-player statistics only. V2 will report the detailed
performance, workload, injury-observation and structured event facts required by
Career. No match result calculates progression, fatigue persistence, injury duration,
trust, offers or transfers.

`MatchContextV1` is hash-validated when deserialized. `MatchResultV1` must be
validated against the exact context before Career applies it; its version,
session ID and context hash must all match.

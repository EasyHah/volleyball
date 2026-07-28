# Contract Consumption

## Authority

`Assets/Volleyball/Shared/Runtime/` is the single source of truth for cross-module
match contracts. New Career matches use concrete `MatchContextV4` and
`MatchResultV4`. V1/V2/V3 production readers, adapters and fallback constructors
are not supported. Career must not create a parallel Shared assembly or expose
Shared match DTOs from its Domain or Application public API.

Career owns identity, attributes, fatigue, mindset, coach trust, progression and
settlement consequences. Match consumes a frozen context and reports facts.
`Career.MatchIntegration` is the only Career assembly that maps between those two
models, and it never references Match implementation assemblies.

## Required checks

Before execution or recovery, Career must reject a context/result pair whose
contract version, session ID, context hash, result hash, team IDs or player IDs do
not match the persisted `PendingMatch`. Persisted UTF-8 payloads must round-trip to
the byte-exact canonical V3 serialization.

The Career save schema version is independent from the match contract version.
The current save schema is V2 and its new match payloads are V3.

## Current fixture policy

The first offline milestone uses `DeterministicFixtureMatchRunnerV3` as a
development producer. It creates one deterministic 25–21 set and facts for all
twelve frozen players. It does not mutate Career state and is replaceable by a
physical or quick-simulation V3 producer through the Career-owned asynchronous
port.

Legacy developer saves that contain the removed parallel Match V2 payload are not
silently reinterpreted. They follow the existing quarantine and backup-recovery
path. Saves without match payload remain readable under save schema V2.

## Result responsibility

V3 currently reports the final score and coarse per-player
`points/contacts/errors/workload`. The integration adapter produces explicit,
deterministic compatibility estimates for the first milestone. Injury observations
and richer technical facts require a later Shared contract decision with a concrete
Match producer. Match results never calculate progression, persisted fatigue,
mindset, trust, offers or transfers.

# V3 Ability Semantics Correction and V4 Layered Attributes Design

> [!IMPORTANT]
> 本文档中的 V3/V4 兼容迁移与并行运行路线已被
> `docs/superpowers/specs/2026-07-24-full-rally-v4-consolidated-design-and-roadmap.md`
> 取代。V4 双端基础属性、确定性派生和 C 型权衡原则继续有效；新的权威决策是不迁移旧
> 存档/回放，正式 Career/Match 直接硬切 V4，并继续复用 V3 规则引擎。

**Date:** 2026-07-24
**Status:** User-approved design
**Scope:** Player attribute contracts, runtime consumption, deterministic derivation, replay diagnostics, and benchmark boundaries

## 1. Decision

V3 remains a versioned, backward-compatible **mixed input contract**. Its serialized
fields are not renamed, removed, or given a different persisted meaning. The V3
runtime must accurately state which fields it consumes and which fields are reserved
for the full V3 planner.

V4 replaces that mixed model with an explicit layered contract:

```text
PhysicalBaseAttributesV4 + TechnicalBaseAttributesV4
       -- deterministic, versioned derivation -->
MatchAttributesV4
       -- authoritative runtime consumption -->
match result, replay, statistics
```

The match engine reads derived match attributes only. Raw physical and technical
attributes are inputs to player authoring, roster generation, growth, and the
derivation explanation recorded for replay and benchmarks.

## 2. Current V3 Correction

### 2.1 Contract semantics

`PlayerAbilitySnapshotV3` has the following intended semantic groups:

| Group | V3 fields | Meaning |
|---|---|---|
| Physical/general input | `Mobility`, `Reaction`, `Jump`, `MaxAttackReach` | Movement, decision time, airborne formation, and maximum attack contact reach. `MaxAttackReach` is not a height field. |
| Technical input | `ReceiveTechnique`, `SetTechnique`, `AttackControl`, `AttackPower`, `SoftTouch`, `BlockTechnique`, `CourtAwareness` | Action-specific control, output, touch, block mechanics, and perception input. |
| Compatibility provenance | `SourceVersion`, `MigrationVersion`, `IsCompatibilityEstimate`, `CompatibilityCollapsedAxes` | Explicit record that V2 data was estimated rather than authored as native V3. |

V3 must not describe `Jump` as a complete hang-time model, or
`MaxAttackReach` as height, standing reach, or block reach. They are partial inputs
with the exact effects documented by the runtime at that version.

### 2.2 Runtime truth at the V3 authority stage

The current formal 6v6 runtime still builds and consumes the V2-shaped
`PlayerAbilityProfile`. The confirmed effects are:

| Field | Current effect |
|---|---|
| `Mobility` | Movement speed, reachable distance, and approach feasibility. |
| `Reaction` | Time available for decisions, movement, and execution delay. |
| `Jump` | Attack/block pose height and block-candidate evaluation. |
| `ReceiveTechnique` | Deterministic reception, digging, and recovery control. |
| `SetTechnique` | Set and emergency-organization control. |
| `AttackTechnique` (V2 profile) | Attack execution control; it remains the live compatibility input. |
| `AttackPower` | Attack velocity scale, effort pressure, and attack-plan scoring. |
| `MaxAttackReach` | Attack contact height and attack-plan evaluation. It does not yet increase block reach. |

`AttackControl`, `SoftTouch`, `BlockTechnique`, and `CourtAwareness` exist in the
V3 shared contract, but they do not yet independently drive the formal runtime.
They must be labelled **reserved / not yet independently consumed**, rather than
presented as active balance axes. A V2-to-V3 migration remains a deterministic
compatibility estimate, not an authoritative roster-balance conversion.

### 2.3 V3 implementation constraints

- Preserve the existing V3 serialized schema and explicit V1/V2/V3 parsing
  boundaries.
- Add field-level documentation and diagnostics that identify `Active`,
  `CompatibilityMapped`, or `Reserved` consumption status.
- Make the V2 compatibility mapping explicit wherever a formal match constructs
  `PlayerAbilityProfile`; do not silently treat the V3 contract as fully consumed.
- Do not claim that height trade-offs, independent hang time, or full attack/block/
  defense/set attributes are in V3.

## 3. V4 Attribute Contract

### 3.1 Physical base inputs

`PhysicalBaseAttributesV4` is the authored physiological input. All values are
bounded and use documented normalizations or physical units; no field is duplicated
as a separate canonical source elsewhere.

| Field | Purpose |
|---|---|
| `Height` | Body-height contribution to attack and block geometry. |
| `StandingReach` | Arm-length and standing-reach contribution, separate from height. |
| `Jump` | Vertical impulse and airborne contact window contribution. |
| `Mobility` | Acceleration, transition speed, and court coverage. |
| `Reaction` | Recognition and response time. |
| `Coordination` | Body control, balance, and stable control under movement. |

`Endurance` is deliberately out of scope until match fatigue is authoritative. It
must not be added as a decorative field before it changes a defined simulation path.

### 3.2 Technical base inputs

`TechnicalBaseAttributesV4` holds learnable, authored skills. It is separate from
physical ability, but together with it forms the input to the derived profile.

| Field | Purpose |
|---|---|
| `AttackTechnique` | Swing mechanics, aim control, and adjustment attack control. |
| `AttackPower` | Force-production skill/output used with physical contact height. |
| `BlockTechnique` | Hand shape, penetration, sealing, and controlled deflection. |
| `DefenseTechnique` | Digging platform/control and emergency ball handling. |
| `ReceiveTechnique` | Serve-receive control and target stability. |
| `SetTechnique` | Set precision across normal and emergency organization. |
| `ServeTechnique` | Serve control and tactical placement. |
| `SoftTouch` | Tip, roll, push, and controlled rebound quality. |
| `CourtAwareness` | Space reading, support selection, and visible-action interpretation. |

These values are canonical authored skills. They are not duplicated in the derived
profile as separately authored values.

### 3.3 Derived match attributes

`MatchAttributesV4` is immutable for a match and contains the values actually read
by tactics, execution, rules-adjacent feasibility, statistics, and replay:

| Primary attribute | Required explanatory sub-attributes |
|---|---|
| `Attack` | `AttackContactHeight`, `AttackWindow`, `AttackControl`, `AttackPower` |
| `Block` | `BlockContactHeight`, `BlockCoverage`, `BlockControl` |
| `Defense` | `DefenseCoverage`, `DigControl`, `RecoveryControl` |
| `Receive` | `ReceiveCoverage`, `ReceiveControl`, `ReceiveStability` |
| `Set` | `SetReachability`, `SetControl`, `EmergencySetControl` |
| `Serve` | `ServeControl`, `ServePressure` |

Primary values provide compact gameplay decisions and player cards. Sub-attributes
provide an audit trail for tuning, replay overlays, and tests. The runtime must not
re-read `Height`, `Jump`, or technical inputs after derivation to create hidden
second calculations.

## 4. Deterministic Derivation and C Trade-off Policy

`MatchAttributeDerivationV4` is a pure function:

```text
(PhysicalBaseAttributesV4, TechnicalBaseAttributesV4,
 DerivationFormulaVersion, match configuration)
    -> DerivedMatchAttributesV4
```

`DerivedMatchAttributesV4` contains the immutable `MatchAttributesV4`, formula
version, normalized input fingerprint, and a deterministic explanation payload.
The formula version and result fingerprint are included in the match context and
replay, so fixed-seed results can be reproduced after formulas evolve.

The approved **C policy** uses both a bounded biomechanical modifier and a larger
roster-generation/growth budget trade-off:

- Height and standing reach directly improve attack and block contact geometry.
- Jump improves attack/block contact geometry and `AttackWindow`; it is not an
  automatic attack-power bonus.
- Larger bodies apply only a small, capped modifier against receive/defense coverage
  and coordination-derived control. It cannot make a player categorically poor at
  first contact.
- `Coordination`, `Mobility`, `Reaction`, and corresponding technique can overcome
  that small modifier. Exceptional tall defenders therefore remain possible.
- Player generation and growth budgets provide the major archetype trade-off:
  spending heavily on height/reach/power leaves fewer budget opportunities for
  mobility, coordination, or specialist technique unless a deliberate exceptional
  roster rule supplies more total talent.

The coefficient table is versioned data, not hard-coded scattered constants. Every
formula is bounded, monotonic where expected, and tested at minimum, maximum, and
representative archetype values.

## 5. Required Relationships

The first V4 formula set must express these relationships, without making the
primary values opaque:

- **Attack:** attack technique and power are the main skill inputs; standing reach,
  height, jump, reaction, and coordination determine usable contact geometry and
  adjustment capacity.
- **Block:** standing reach, height, jump, block technique, mobility, and reaction
  determine contact geometry, lateral coverage, timing, and controlled rebounds.
- **Defense and receive:** mobility, reaction, coordination, and specialist
  technique are dominant; the C-policy body modifier is intentionally small and
  capped.
- **Set:** set technique, court awareness, coordination, mobility, and reaction
  determine normal and emergency organization quality.
- **Serve:** serve technique, attack power, and coordination determine pressure and
  control. Its detailed physical model remains narrower until serve execution is
  fully unified with the V4 execution envelope.

## 6. Migration and Compatibility

There is no silent V3-to-V4 reinterpretation. A named migration produces a V4
profile with provenance:

```text
LegacyV3ToPlayerAttributesV4(...)
```

Because V3 lacks true `Height`, `StandingReach`, `Coordination`, `DefenseTechnique`,
and `ServeTechnique`, migration must:

- use deterministic bounded estimates only where a documented proxy exists;
- record each estimated or collapsed V4 axis in provenance;
- never present a migrated estimate as a native authored physical measurement;
- permit roster tools to replace estimates with authored V4 values; and
- keep V3 matches on V3 formula/runtime behavior unless a match is explicitly
  created using V4 context and hash rules.

## 7. Replay, Benchmarks, and Acceptance

Replay V4 diagnostics record, for each participant and relevant contact:

```text
base attributes -> formula version -> derived match attributes
-> selected action/route -> execution envelope -> observed result
```

The post-match interface may show this as a tactical/attribute overlay, including
attack route selection, available attack window, block contact/coverage, defense
coverage, receive target stability, and setter organization quality. Raw values,
derived values, and action result must remain distinguishable.

Fixed deterministic benchmarks use identical teams, rotation, tactics, seed, and
opponents, varying one authored profile at a time. Required comparison fixtures:

1. Technical outside hitter versus power outside hitter with equal overall budget.
2. High-jump/high-attack-window player versus baseline, holding power and technique
   fixed.
3. Tall/reach-focused player versus baseline, measuring attack and block gains plus
   the small capped first-contact/coordination modifier.
4. Tall player with high coordination/receive technique, proving the C policy permits
   an exceptional first-contact player.
5. Specialist setter, middle blocker, libero, and opposite profiles to verify that
   each primary match attribute affects its intended live decision/execution path.

Each fixture must assert both derived values and aggregate, fixed-seed match
outcomes. The replay assertion must identify the formula version and at least one
contact-level explanation of the changed result.

## 8. Delivery Sequence

1. Correct V3 field documentation, consumption status, and explicit compatibility
   mapping without changing the V3 schema.
2. Introduce V4 base, technical, derived-match, provenance, formula-version, and
   canonical-hash contracts with unit tests.
3. Implement the pure derivation with versioned coefficient data and archetype tests.
4. Migrate the formal runtime to read `MatchAttributesV4` only, preserving a named
   V3 compatibility runtime path.
5. Add fixed benchmark fixtures and replay diagnostics/overlay data.
6. Calibrate coefficients only after the benchmarks demonstrate the intended
   differences and deterministic replay stability.

## 9. Non-goals

- Retrofitting V4 behavior into already-recorded V3 replays.
- Claiming a height, hang-time, fatigue, or body-mass simulation before its inputs
  and runtime effects are implemented.
- Balancing every position in the first derivation pass; the initial goal is correct
  information flow, explainability, and deterministic comparison.
- Removing the existing V3 contract or compatibility path during V4 introduction.

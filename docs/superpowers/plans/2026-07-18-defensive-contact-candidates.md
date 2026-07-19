# Defensive Contact Candidates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let any receiving-side player physically dig an opponent attack when the ball actually intersects them, instead of only accepting the scripted defender.

**Architecture:** Keep the current six-touch rally loop and add a narrow “emergency receive window” to `PrototypePlayerAgent`. `ThreeVsThreeRallyDirector` opens that window for all three defenders after an attack contact, and converts a non-scripted receive into the next scripted set phase.

**Tech Stack:** Unity 6 C#, NUnit EditMode tests, Unity PlayMode scene regression.

---

### Task 1: Agent-level emergency receive candidate

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/PrototypePlayerAgent.cs`
- Test: `Assets/Volleyball/Match/Tests/EditMode/PrototypePlayerContactSourceTests.cs`

- [ ] **Step 1: Write the failing tests**

Add tests proving an unscheduled player can emit a receive candidate during an explicit emergency window, and that the window expires without replacing normal scheduled contacts.

- [ ] **Step 2: Run EditMode test to verify red**

Run:

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles -runTests -testPlatform EditMode -testResults /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/EditMode-defensive-contact-red.xml -logFile /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/EditMode-defensive-contact-red.log
```

Expected: compile/test failure because `EnableEmergencyReceiveWindow` is not implemented yet.

- [ ] **Step 3: Implement minimal agent behavior**

Add `EnableEmergencyReceiveWindow(...)` / `DisableEmergencyReceiveWindow()` to `PrototypePlayerAgent`. When no scheduled contact is active and simulation time is inside the window, pose as Receive, capture active Receive surfaces, and add candidates using the configured target velocity and technique. Support actions remain visual and do not create contacts unless the emergency window is active.

- [ ] **Step 4: Run targeted EditMode verification**

Run the same EditMode command and expect all tests to pass.

### Task 2: Director transition for unscripted digs

**Files:**
- Modify: `Assets/Volleyball/Match/Runtime/Presentation/ThreeVsThreeRallyDirector.cs`
- Test: `Assets/Volleyball/Match/Tests/PlayMode/ThreeVsThreeRallyPlayModeTests.cs`

- [ ] **Step 1: Write the failing PlayMode assertion**

Add diagnostics for `EmergencyReceiveWindowAssignments` and `EmergencyReceiveContacts`, then assert the physical loop records at least one emergency receive contact.

- [ ] **Step 2: Run PlayMode test to verify red**

Run:

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles -runTests -testPlatform PlayMode -testResults /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/PlayMode-defensive-contact-red.xml -logFile /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/PlayMode-defensive-contact-red.log
```

Expected: compile/test failure before the new diagnostics exist, or assertion failure before director wiring is complete.

- [ ] **Step 3: Implement director wiring**

When a scripted attack is contacted, open emergency receive windows for every player on the defending team until the attack should land. If the ball reports a Receive contact while waiting for landing, accept it even if the contact actor is not the scripted defender, record that actor as the receive touch, clear emergency windows, advance `_expectedIndex` to the receiving team’s setter, and schedule the set from the current ball trajectory.

- [ ] **Step 4: Run PlayMode verification**

Run the PlayMode command and expect the full scene regression to pass.

### Task 3: Documentation and final verification

**Files:**
- Create: `docs/changes/2026-07-18-008-defensive-contact-candidates.md`
- Modify: `docs/changes/README.md`

- [ ] **Step 1: Document the Match-only behavior change**

Add CHG-20260718-008 with summary, touched files, no cross-module interaction, verification commands, and rollback notes.

- [ ] **Step 2: Run final checks**

Run:

```bash
git diff --check
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles -runTests -testPlatform EditMode -testResults /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/EditMode-defensive-contact-final.xml -logFile /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/EditMode-defensive-contact-final.log
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles -runTests -testPlatform PlayMode -testResults /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/PlayMode-defensive-contact-final.xml -logFile /Users/wys/Documents/program/volleyball-match/.worktrees/blocking-roles/TestResults/PlayMode-defensive-contact-final.log
```

Expected: no whitespace errors; EditMode and PlayMode pass.

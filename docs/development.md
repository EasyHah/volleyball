# Development Rules

## Git workflow

`main` is the only long-lived branch. Create short-lived
`feature/<module>-<description>`, `fix/<module>-<description>`,
`chore/<description>` or `docs/<description>` branches from the latest `main`.
Merge through pull requests only, then delete the branch.

The future remote `main` branch must require CI, up-to-date branches and at least
one review; direct push, force push and branch deletion must be disabled.

## Testing

Write pure rule, scoring, rotation and statistics tests as EditMode tests. Use
PlayMode tests for one full 3v3 rally with Unity scene integration. Use a fixed
random seed for deterministic simulation tests. Record the Unity editor and package
lock versions used to reproduce each test run.

## Windows delivery

Windows x64 is the release platform. The committed workflow is intentionally
disabled until Unity `6000.0.43f1` has generated complete `ProjectSettings` and
`Packages/packages-lock.json` files on a Windows x64 development machine. Then pin
a matching Unity Windows runner or self-hosted runner, configure Unity activation
as a repository secret, add the verified batch-mode build command, and enable the job.
CI artifacts do not replace regular testing on real Windows x64 hardware for
keyboard, controller, graphics and performance checks.

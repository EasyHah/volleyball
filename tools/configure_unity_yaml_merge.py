#!/usr/bin/env python3
"""Configure repository-local UnityYAMLMerge and Git LFS settings."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path


def run(root: Path, *arguments: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        list(arguments),
        cwd=root,
        check=check,
        capture_output=True,
        text=True,
    )


def project_version(root: Path) -> str:
    for line in (root / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8").splitlines():
        if line.startswith("m_EditorVersion:"):
            return line.split(":", 1)[1].strip()
    raise RuntimeError("ProjectVersion.txt does not contain m_EditorVersion")


def editor_candidates(version: str) -> list[Path]:
    candidates: list[Path] = []
    explicit = os.environ.get("UNITY_EDITOR_PATH")
    if explicit:
        candidates.append(Path(explicit))

    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA")
        if appdata:
            secondary = Path(appdata) / "UnityHub" / "secondaryInstallPath.json"
            if secondary.is_file():
                try:
                    install_root = Path(json.loads(secondary.read_text(encoding="utf-8")))
                    candidates.append(install_root / version / "Editor" / "Unity.exe")
                except (json.JSONDecodeError, OSError, TypeError):
                    pass
        program_files = Path(os.environ.get("ProgramFiles", "C:/Program Files"))
        candidates.append(program_files / "Unity" / "Hub" / "Editor" / version / "Editor" / "Unity.exe")
    elif sys.platform == "darwin":
        candidates.append(
            Path("/Applications/Unity/Hub/Editor")
            / version
            / "Unity.app"
            / "Contents"
            / "MacOS"
            / "Unity"
        )
    return candidates


def yaml_merge_path(editor: Path) -> Path:
    if sys.platform == "darwin":
        return editor.parents[1] / "Tools" / "UnityYAMLMerge"
    return editor.parent / "Data" / "Tools" / "UnityYAMLMerge.exe"


def driver_value(tool: Path) -> str:
    normalized = tool.resolve().as_posix()
    return f'"{normalized}" merge -p %O %B %A %A'


def find_editor(root: Path, override: str | None) -> Path:
    version = project_version(root)
    candidates = [Path(override)] if override else editor_candidates(version)
    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()
    formatted = "\n".join(f"- {candidate}" for candidate in candidates)
    raise RuntimeError(
        f"Unity {version} editor was not found. Set UNITY_EDITOR_PATH or pass --unity.\n{formatted}"
    )


def check_configuration(root: Path, tool: Path) -> list[str]:
    errors: list[str] = []
    expected = {
        "merge.unityyamlmerge.name": "Unity SmartMerge",
        "merge.unityyamlmerge.driver": driver_value(tool),
        "merge.unityyamlmerge.recursive": "binary",
    }
    for key, expected_value in expected.items():
        result = run(root, "git", "config", "--local", "--get", key, check=False)
        actual = result.stdout.strip() if result.returncode == 0 else None
        if actual != expected_value:
            errors.append(f"{key}: {actual!r} != {expected_value!r}")
    lfs_version = run(root, "git", "lfs", "version", check=False)
    if lfs_version.returncode != 0 or not lfs_version.stdout.startswith("git-lfs/"):
        errors.append("Git LFS executable is unavailable or did not report a valid version")
    lfs_expected = {
        "filter.lfs.clean": "git-lfs clean -- %f",
        "filter.lfs.smudge": "git-lfs smudge -- %f",
        "filter.lfs.process": "git-lfs filter-process",
        "filter.lfs.required": "true",
    }
    for key, expected_value in lfs_expected.items():
        result = run(root, "git", "config", "--local", "--get", key, check=False)
        actual = result.stdout.strip() if result.returncode == 0 else None
        if actual != expected_value:
            errors.append(f"{key}: {actual!r} != {expected_value!r}")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", help="Path to the exact Unity editor executable")
    parser.add_argument("--check", action="store_true", help="Only verify current local settings")
    arguments = parser.parse_args()

    root = Path(__file__).resolve().parents[1]
    try:
        editor = find_editor(root, arguments.unity)
        tool = yaml_merge_path(editor)
        if not tool.is_file():
            raise RuntimeError(f"UnityYAMLMerge was not found: {tool}")
        if not arguments.check:
            run(root, "git", "config", "--local", "merge.unityyamlmerge.name", "Unity SmartMerge")
            run(root, "git", "config", "--local", "merge.unityyamlmerge.driver", driver_value(tool))
            run(root, "git", "config", "--local", "merge.unityyamlmerge.recursive", "binary")
            run(root, "git", "lfs", "install", "--local")
        errors = check_configuration(root, tool)
    except (OSError, RuntimeError, subprocess.CalledProcessError) as exc:
        print(f"Configuration failed: {exc}", file=sys.stderr)
        return 1

    if errors:
        print("Local Git configuration is incomplete:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1
    print(f"UnityYAMLMerge and Git LFS are configured for {editor}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

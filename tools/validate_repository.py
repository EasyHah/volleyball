#!/usr/bin/env python3
"""Validate repository invariants without launching Unity or requiring a license."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from collections import Counter
from pathlib import Path
from typing import Any, Iterable


CHANGE_FILE_RE = re.compile(
    r"^(?P<year>\d{4})-(?P<month>\d{2})-(?P<day>\d{2})-(?P<number>\d{3})-.+\.md$"
)
CHANGE_HEADER_RE = re.compile(r"^#\s+(?P<id>CHG-\d{8}-\d{3})[：:]", re.MULTILINE)
INDEX_ROW_RE = re.compile(
    r"^\|\s*(?P<id>CHG-\d{8}-\d{3})\s*\|.*?\]\((?P<path>[^)]+\.md)\)\s*\|\s*$",
    re.MULTILINE,
)
META_GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)
LFS_POINTER_RE = re.compile(
    rb"\Aversion https://git-lfs\.github\.com/spec/v1\r?\n"
    rb"oid sha256:[0-9a-f]{64}\r?\n"
    rb"size [0-9]+\r?\n\Z"
)
MATERIAL_CHANGE_PREFIXES = (
    ".github/",
    "Assets/",
    "Packages/",
    "ProjectSettings/",
    "tools/",
)
MATERIAL_CHANGE_FILES = {".gitattributes", ".gitignore"}


def read_json(path: Path, errors: list[str]) -> dict[str, Any] | None:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        errors.append(f"Invalid JSON {path.as_posix()}: {exc}")
        return None
    if not isinstance(value, dict):
        errors.append(f"JSON root must be an object: {path.as_posix()}")
        return None
    return value


def validate_meta_pairs(root: Path) -> list[str]:
    errors: list[str] = []
    assets = root / "Assets"
    if not assets.is_dir():
        return ["Missing Assets directory"]

    guids: dict[str, str] = {}
    for path in sorted(assets.rglob("*")):
        relative = path.relative_to(assets)
        if any(part.startswith(".") for part in relative.parts):
            continue
        if path.name.endswith("~") or ".tmp" in path.name:
            continue
        if path.name.endswith(".meta"):
            target = Path(str(path)[:-5])
            if not target.exists():
                errors.append(f"Orphan Unity meta file: {path.relative_to(root).as_posix()}")
            try:
                meta_text = path.read_text(encoding="utf-8")
            except (OSError, UnicodeError) as exc:
                errors.append(f"Cannot read Unity meta file {path.relative_to(root).as_posix()}: {exc}")
                continue
            guid_match = META_GUID_RE.search(meta_text)
            if guid_match is None:
                errors.append(f"Missing or malformed GUID: {path.relative_to(root).as_posix()}")
            else:
                guid = guid_match.group(1)
                previous = guids.get(guid)
                if previous is not None:
                    errors.append(
                        f"Duplicate Unity GUID {guid}: {previous} and "
                        f"{path.relative_to(root).as_posix()}"
                    )
                else:
                    guids[guid] = path.relative_to(root).as_posix()
            continue
        meta = Path(f"{path}.meta")
        if not meta.is_file():
            kind = "directory" if path.is_dir() else "asset"
            errors.append(
                f"Missing .meta for {kind}: {path.relative_to(root).as_posix()}"
            )
    return errors


def _normalized_values(document: dict[str, Any], key: str) -> set[str]:
    value = document.get(key, [])
    if value is None:
        return set()
    if not isinstance(value, list) or not all(isinstance(item, str) for item in value):
        return {"<invalid>"}
    return set(value)


def _find_cycles(graph: dict[str, set[str]]) -> list[list[str]]:
    cycles: list[list[str]] = []
    state: dict[str, int] = {}
    stack: list[str] = []

    def visit(node: str) -> None:
        state[node] = 1
        stack.append(node)
        for target in sorted(graph.get(node, set())):
            if state.get(target, 0) == 0:
                visit(target)
            elif state.get(target) == 1:
                start = stack.index(target)
                cycle = stack[start:] + [target]
                if cycle not in cycles:
                    cycles.append(cycle)
        stack.pop()
        state[node] = 2

    for node in sorted(graph):
        if state.get(node, 0) == 0:
            visit(node)
    return cycles


def validate_asmdefs(root: Path, policy: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    assets = root / "Assets"
    configured = policy.get("assemblies", {})
    if not isinstance(configured, dict):
        return ["repository_policy.json assemblies must be an object"]

    documents: dict[str, tuple[Path, dict[str, Any]]] = {}
    for path in sorted(assets.rglob("*.asmdef")):
        document = read_json(path, errors)
        if document is None:
            continue
        name = document.get("name")
        if not isinstance(name, str) or not name:
            errors.append(f"Assembly has no valid name: {path.relative_to(root).as_posix()}")
            continue
        if name in documents:
            errors.append(f"Duplicate assembly name: {name}")
            continue
        documents[name] = (path, document)

    missing_policy = sorted(set(documents) - set(configured))
    missing_assembly = sorted(set(configured) - set(documents))
    for name in missing_policy:
        errors.append(f"Assembly is not registered in repository policy: {name}")
    for name in missing_assembly:
        errors.append(f"Configured assembly is missing: {name}")

    for name in sorted(set(documents) & set(configured)):
        path, document = documents[name]
        expected = configured[name]
        if not isinstance(expected, dict):
            errors.append(f"Assembly policy must be an object: {name}")
            continue
        actual_path = path.relative_to(root).as_posix()
        if expected.get("path") != actual_path:
            errors.append(
                f"Assembly path mismatch for {name}: {actual_path} != {expected.get('path')}"
            )
        for key in (
            "references",
            "precompiledReferences",
            "includePlatforms",
            "optionalUnityReferences",
        ):
            actual_values = _normalized_values(document, key)
            expected_values = set(expected.get(key, []))
            if actual_values != expected_values:
                errors.append(
                    f"{name} {key} mismatch: {sorted(actual_values)} != {sorted(expected_values)}"
                )
        actual_no_engine = document.get("noEngineReferences", False) is True
        expected_no_engine = expected.get("noEngineReferences", False) is True
        if actual_no_engine != expected_no_engine:
            errors.append(
                f"{name} noEngineReferences mismatch: {actual_no_engine} != {expected_no_engine}"
            )

    graph: dict[str, set[str]] = {}
    internal_names = set(documents)
    for name, (_, document) in documents.items():
        graph[name] = _normalized_values(document, "references") & internal_names
    for cycle in _find_cycles(graph):
        errors.append(f"Assembly dependency cycle: {' -> '.join(cycle)}")

    for name, (_, document) in documents.items():
        references = _normalized_values(document, "references")
        internal_references = references & internal_names
        if name.startswith("Volleyball.Shared") and any(
            reference.startswith(("Volleyball.Match", "Volleyball.Career", "Volleyball.Bootstrap"))
            for reference in internal_references
        ):
            errors.append(f"Shared assembly depends on a business module: {name}")
        if name.startswith("Volleyball.Match") and any(
            reference.startswith(("Volleyball.Career", "Volleyball.Bootstrap"))
            for reference in internal_references
        ):
            errors.append(f"Match assembly depends on Career/Bootstrap: {name}")
        is_career_test = name.endswith(("EditModeTests", "PlayModeTests"))
        if name.startswith("Volleyball.Career") and not is_career_test and any(
            reference.startswith(("Volleyball.Match", "Volleyball.Bootstrap"))
            for reference in internal_references
        ):
            errors.append(f"Career runtime assembly depends on Match/Bootstrap: {name}")
        uses_newtonsoft = bool(
            references & {"Unity.Newtonsoft.Json", "Newtonsoft.Json"}
            or _normalized_values(document, "precompiledReferences") & {"Newtonsoft.Json.dll"}
        )
        editor_only = _normalized_values(document, "includePlatforms") == {"Editor"}
        if uses_newtonsoft and not editor_only:
            errors.append(f"Newtonsoft reference is not restricted to Editor: {name}")

    for script in sorted(assets.rglob("*.cs")):
        parent = script.parent
        covered = False
        while parent != assets.parent:
            if any(parent.glob("*.asmdef")):
                covered = True
                break
            if parent == assets:
                break
            parent = parent.parent
        if not covered:
            errors.append(
                "C# source would compile into Assembly-CSharp: "
                f"{script.relative_to(root).as_posix()}"
            )
    return errors


def validate_change_records(root: Path) -> list[str]:
    errors: list[str] = []
    changes = root / "docs" / "changes"
    index_path = changes / "README.md"
    if not index_path.is_file():
        return ["Missing docs/changes/README.md"]

    excluded = {"README.md", "TEMPLATE.md", "unified-unity-modules-plan.md"}
    records: dict[str, str] = {}
    ids: list[str] = []
    for path in sorted(changes.glob("*.md")):
        if path.name in excluded:
            continue
        file_match = CHANGE_FILE_RE.match(path.name)
        if file_match is None:
            errors.append(f"Invalid change record filename: {path.name}")
            continue
        expected_id = (
            f"CHG-{file_match.group('year')}{file_match.group('month')}"
            f"{file_match.group('day')}-{file_match.group('number')}"
        )
        text = path.read_text(encoding="utf-8")
        header_match = CHANGE_HEADER_RE.search(text)
        if header_match is None:
            errors.append(f"Missing CHG header: {path.name}")
            continue
        actual_id = header_match.group("id")
        if actual_id != expected_id:
            errors.append(f"Change ID mismatch in {path.name}: {actual_id} != {expected_id}")
        ids.append(actual_id)
        records[actual_id] = path.name

    for identifier, count in Counter(ids).items():
        if count > 1:
            errors.append(f"Duplicate change ID: {identifier}")

    index = index_path.read_text(encoding="utf-8")
    rows = [(match.group("id"), match.group("path")) for match in INDEX_ROW_RE.finditer(index)]
    row_ids = [identifier for identifier, _ in rows]
    for identifier, count in Counter(row_ids).items():
        if count > 1:
            errors.append(f"Duplicate change index row: {identifier}")
    indexed = dict(rows)
    for identifier, filename in sorted(records.items()):
        if indexed.get(identifier) != filename:
            errors.append(
                f"Change index mismatch for {identifier}: {indexed.get(identifier)} != {filename}"
            )
    for identifier, filename in rows:
        if records.get(identifier) != filename:
            errors.append(f"Index points to missing or mismatched change record: {identifier} -> {filename}")
    return errors


def validate_changed_paths(
    changed_paths: Iterable[str],
    eligible_change_records: Iterable[str] | None = None,
    index_updated: bool | None = None,
) -> list[str]:
    normalized = {path.replace("\\", "/") for path in changed_paths if path}
    material = {
        path
        for path in normalized
        if path in MATERIAL_CHANGE_FILES
        or path.startswith(MATERIAL_CHANGE_PREFIXES)
    }
    if not material:
        return []
    if eligible_change_records is None:
        formal_records = {
            path
            for path in normalized
            if path.startswith("docs/changes/")
            and CHANGE_FILE_RE.match(Path(path).name)
        }
    else:
        formal_records = {
            path.replace("\\", "/") for path in eligible_change_records
        }
    if index_updated is None:
        index_updated = "docs/changes/README.md" in normalized
    errors: list[str] = []
    if not formal_records:
        errors.append("Material changes require a new or updated formal CHG record")
    if not index_updated:
        errors.append("Material changes require an update to docs/changes/README.md")
    return errors


def _parse_name_status(output: str) -> list[tuple[str, str]]:
    tokens = output.split("\0")
    if tokens and tokens[-1] == "":
        tokens.pop()
    if len(tokens) % 2 != 0:
        raise ValueError("git name-status output did not contain status/path pairs")
    return [(tokens[index], tokens[index + 1]) for index in range(0, len(tokens), 2)]


def validate_diff_change_record(root: Path, base_revision: str | None) -> list[str]:
    if not base_revision:
        return []
    commands = (
        (["git", "diff", "--name-status", "--no-renames", "-z", base_revision, "HEAD"], "committed diff"),
        (["git", "diff", "--name-status", "--no-renames", "-z", "--cached"], "staged diff"),
        (["git", "diff", "--name-status", "--no-renames", "-z"], "working-tree diff"),
    )
    changed: dict[str, set[str]] = {}
    errors: list[str] = []
    for arguments, label in commands:
        result = subprocess.run(
            arguments,
            cwd=root,
            check=False,
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            errors.append(
                f"Cannot inspect {label} against base revision: {base_revision}"
            )
            continue
        try:
            entries = _parse_name_status(result.stdout)
        except ValueError as exc:
            errors.append(f"Cannot parse {label}: {exc}")
            continue
        for status, path in entries:
            changed.setdefault(path.replace("\\", "/"), set()).add(status[:1])

    untracked = subprocess.run(
        ["git", "ls-files", "--others", "--exclude-standard", "-z"],
        cwd=root,
        check=False,
        capture_output=True,
        text=True,
    )
    if untracked.returncode != 0:
        errors.append("Cannot inspect untracked files")
    else:
        for path in untracked.stdout.split("\0"):
            if path:
                changed.setdefault(path.replace("\\", "/"), set()).add("A")

    index_path = root / "docs" / "changes" / "README.md"
    indexed_records: set[str] = set()
    if index_path.is_file():
        index_text = index_path.read_text(encoding="utf-8")
        indexed_records = {
            f"docs/changes/{match.group('path')}"
            for match in INDEX_ROW_RE.finditer(index_text)
        }
    eligible_records = {
        path
        for path, statuses in changed.items()
        if "D" not in statuses
        and path.startswith("docs/changes/")
        and CHANGE_FILE_RE.match(Path(path).name)
        and (root / path).is_file()
        and path in indexed_records
    }
    index_statuses = changed.get("docs/changes/README.md", set())
    valid_index_update = bool(index_statuses - {"D"}) and index_path.is_file()
    errors.extend(
        validate_changed_paths(
            changed,
            eligible_change_records=eligible_records,
            index_updated=valid_index_update,
        )
    )
    return errors


def _paths_with_lfs_filter(root: Path, paths: list[str]) -> tuple[list[str], list[str]]:
    if not paths:
        return [], []
    result = subprocess.run(
        [
            "git",
            "-c",
            "core.ignorecase=false",
            "check-attr",
            "-z",
            "filter",
            "--stdin",
        ],
        cwd=root,
        check=False,
        capture_output=True,
        input=("\0".join(paths) + "\0").encode("utf-8"),
    )
    if result.returncode != 0:
        return [], ["git check-attr failed while locating LFS-managed files"]
    lfs_paths: list[str] = []
    tokens = result.stdout.split(b"\0")
    if tokens and tokens[-1] == b"":
        tokens.pop()
    if len(tokens) % 3 != 0:
        return [], ["git check-attr returned malformed NUL-delimited output"]
    for index in range(0, len(tokens), 3):
        path, attribute, value = tokens[index : index + 3]
        if attribute == b"filter" and value == b"lfs":
            lfs_paths.append(path.decode("utf-8", errors="surrogateescape"))
    return lfs_paths, []


def validate_lfs_pointers(root: Path) -> list[str]:
    errors: list[str] = []
    sources = (
        ("HEAD", ["git", "ls-tree", "-r", "--name-only", "HEAD"]),
        ("index", ["git", "ls-files"]),
    )
    for label, list_command in sources:
        listed = subprocess.run(
            list_command,
            cwd=root,
            check=False,
            capture_output=True,
            text=True,
        )
        if listed.returncode != 0:
            errors.append(f"Cannot list {label} files for LFS validation")
            continue
        paths = [path for path in listed.stdout.splitlines() if path]
        lfs_paths, attribute_errors = _paths_with_lfs_filter(root, paths)
        errors.extend(attribute_errors)
        for path in lfs_paths:
            object_name = f"HEAD:{path}" if label == "HEAD" else f":{path}"
            blob = subprocess.run(
                ["git", "show", object_name],
                cwd=root,
                check=False,
                capture_output=True,
            )
            if blob.returncode != 0:
                errors.append(f"Cannot read {label} blob for LFS-managed file: {path}")
            elif LFS_POINTER_RE.fullmatch(blob.stdout) is None:
                errors.append(f"{label} blob is not a strict Git LFS pointer: {path}")
    return errors


def validate_project_baseline(
    root: Path, policy: dict[str, Any], base_revision: str | None = None
) -> list[str]:
    errors: list[str] = []
    version_path = root / "ProjectSettings" / "ProjectVersion.txt"
    try:
        version_text = version_path.read_text(encoding="utf-8")
    except OSError as exc:
        return [f"Cannot read ProjectVersion.txt: {exc}"]
    match = re.search(r"^m_EditorVersion:\s*(\S+)\s*$", version_text, re.MULTILINE)
    actual_version = match.group(1) if match else None
    expected_version = policy.get("unityEditorVersion")
    if actual_version != expected_version:
        errors.append(f"Unity version mismatch: {actual_version} != {expected_version}")

    manifest = read_json(root / "Packages" / "manifest.json", errors)
    lock = read_json(root / "Packages" / "packages-lock.json", errors)
    if manifest is not None and lock is not None:
        direct = manifest.get("dependencies", {})
        locked = lock.get("dependencies", {})
        if not isinstance(direct, dict) or not isinstance(locked, dict):
            errors.append("Package dependencies must be JSON objects")
        else:
            direct_depth_zero = {
                name
                for name, entry in locked.items()
                if isinstance(entry, dict) and entry.get("depth") == 0
            }
            if set(direct) != direct_depth_zero:
                errors.append(
                    "Direct package set differs from lock depth-0 set: "
                    f"{sorted(direct)} != {sorted(direct_depth_zero)}"
                )
            for name, version in direct.items():
                entry = locked.get(name)
                locked_version = entry.get("version") if isinstance(entry, dict) else None
                if locked_version != version:
                    errors.append(
                        f"Package version mismatch for {name}: {version} != {locked_version}"
                    )

    attributes_path = root / ".gitattributes"
    attributes = attributes_path.read_text(encoding="utf-8").splitlines()
    normalized = {line.strip() for line in attributes if line.strip() and not line.lstrip().startswith("#")}
    for required in policy.get("requiredGitattributes", []):
        if required not in normalized:
            errors.append(f"Missing required .gitattributes rule: {required}")
    for line in normalized:
        if line.startswith(("*.unity ", "*.prefab ", "*.asset ", "*.meta ")) and "filter=lfs" in line:
            errors.append(f"Unity YAML must not use Git LFS: {line}")

    attribute_expectations = {
        "Assets/__Policy__/sample.unity": {"merge": "unityyamlmerge", "filter": "unspecified", "lockable": "unspecified"},
        "Assets/__Policy__/sample.prefab": {"merge": "unityyamlmerge", "filter": "unspecified", "lockable": "unspecified"},
        "Assets/__Policy__/sample.asset": {"merge": "unityyamlmerge", "filter": "unspecified", "lockable": "unspecified"},
        "Assets/__Policy__/sample.meta": {"merge": "unspecified", "filter": "unspecified", "lockable": "unspecified"},
        "Assets/__Policy__/source.psd": {"merge": "lfs", "filter": "lfs", "lockable": "set"},
        "Assets/__Policy__/SOURCE.PSD": {"merge": "lfs", "filter": "lfs", "lockable": "set"},
        "Assets/__Policy__/MiXeD.FbX": {"merge": "lfs", "filter": "lfs", "lockable": "set"},
        "Assets/__Policy__/export.png": {"merge": "lfs", "filter": "lfs", "lockable": "unspecified"},
        "Assets/__Policy__/EXPORT.PNG": {"merge": "lfs", "filter": "lfs", "lockable": "unspecified"},
    }
    for sample_path, expected_attributes in attribute_expectations.items():
        result = subprocess.run(
            [
                "git",
                "-c",
                "core.ignorecase=false",
                "check-attr",
                "merge",
                "filter",
                "lockable",
                "--",
                sample_path,
            ],
            cwd=root,
            check=False,
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            errors.append(f"git check-attr failed for {sample_path}")
            continue
        actual_attributes: dict[str, str] = {}
        for line in result.stdout.splitlines():
            parts = line.split(": ", 2)
            if len(parts) == 3:
                actual_attributes[parts[1]] = parts[2]
        for attribute, expected_value in expected_attributes.items():
            actual_value = actual_attributes.get(attribute)
            if actual_value != expected_value:
                errors.append(
                    f"Git attribute mismatch for {sample_path} {attribute}: "
                    f"{actual_value} != {expected_value}"
                )

    codeowners_path = root / ".github" / "CODEOWNERS"
    if not codeowners_path.is_file():
        errors.append("Missing .github/CODEOWNERS")
    else:
        codeowner_lines = {
            line.strip()
            for line in codeowners_path.read_text(encoding="utf-8").splitlines()
            if line.strip() and not line.lstrip().startswith("#")
        }
        for required in policy.get("requiredCodeowners", []):
            if required not in codeowner_lines:
                errors.append(f"Missing required CODEOWNERS rule: {required}")

    frozen_trees = policy.get("frozenTrees", {})
    if not isinstance(frozen_trees, dict):
        errors.append("repository_policy.json frozenTrees must be an object")
    else:
        for relative_path, expected_hash in sorted(frozen_trees.items()):
            status = subprocess.run(
                ["git", "status", "--porcelain", "--", relative_path],
                cwd=root,
                check=False,
                capture_output=True,
                text=True,
            )
            if status.returncode != 0:
                errors.append(f"Cannot inspect frozen tree working state: {relative_path}")
                continue
            if status.stdout.strip():
                errors.append(f"Frozen tree has uncommitted changes: {relative_path}")
                continue
            revision = subprocess.run(
                ["git", "rev-parse", f"HEAD:{relative_path}"],
                cwd=root,
                check=False,
                capture_output=True,
                text=True,
            )
            actual_hash = revision.stdout.strip() if revision.returncode == 0 else None
            if actual_hash != expected_hash:
                errors.append(
                    f"Frozen tree hash mismatch for {relative_path}: "
                    f"{actual_hash} != {expected_hash}"
                )
            if base_revision:
                base = subprocess.run(
                    ["git", "rev-parse", f"{base_revision}:{relative_path}"],
                    cwd=root,
                    check=False,
                    capture_output=True,
                    text=True,
                )
                base_hash = base.stdout.strip() if base.returncode == 0 else None
                if base_hash is None:
                    errors.append(
                        f"Frozen path is missing from comparison base {base_revision}: "
                        f"{relative_path}"
                    )
                elif actual_hash != base_hash:
                    errors.append(
                        f"Frozen path changed relative to {base_revision}: {relative_path} "
                        f"({base_hash} -> {actual_hash})"
                    )
    return errors


def run_validators(root: Path, base_revision: str | None = None) -> list[str]:
    policy_errors: list[str] = []
    policy = read_json(root / "tools" / "repository_policy.json", policy_errors)
    if policy is None:
        return policy_errors
    errors = list(policy_errors)
    validators: Iterable[tuple[str, list[str]]] = (
        ("Unity meta pairs", validate_meta_pairs(root)),
        ("Assembly definitions", validate_asmdefs(root, policy)),
        ("Change records", validate_change_records(root)),
        ("Changed-path record", validate_diff_change_record(root, base_revision)),
        ("Project baseline", validate_project_baseline(root, policy, base_revision)),
        ("Git LFS pointers", validate_lfs_pointers(root)),
    )
    for label, failures in validators:
        for failure in failures:
            errors.append(f"[{label}] {failure}")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--base",
        help="Git base revision used to require a CHG record for material changes.",
    )
    arguments = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    errors = run_validators(root, arguments.base)
    if errors:
        print("Repository validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1
    print("Repository validation passed (meta, asmdef, change records, Unity/packages, Git policy).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

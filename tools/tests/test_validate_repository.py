from __future__ import annotations

import json
import subprocess
import tempfile
import unittest
from pathlib import Path
import sys


TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

from validate_repository import (  # noqa: E402
    validate_asmdefs,
    validate_change_records,
    validate_changed_paths,
    validate_diff_change_record,
    validate_lfs_pointers,
    validate_meta_pairs,
)


class RepositoryValidatorTests(unittest.TestCase):
    @staticmethod
    def initialize_repository(root: Path) -> None:
        subprocess.run(["git", "init"], cwd=root, check=True, capture_output=True)
        subprocess.run(
            ["git", "config", "user.email", "validator@example.invalid"],
            cwd=root,
            check=True,
        )
        subprocess.run(
            ["git", "config", "user.name", "Repository Validator"],
            cwd=root,
            check=True,
        )
        subprocess.run(
            ["git", "config", "commit.gpgsign", "false"], cwd=root, check=True
        )

    def test_meta_validation_ignores_hidden_placeholder_and_reports_missing_asset_meta(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            assets = root / "Assets"
            folder = assets / "Feature"
            folder.mkdir(parents=True)
            (assets / "Feature.meta").write_text(
                "folderFormatVersion: 2\n"
                "guid: 0123456789abcdef0123456789abcdef\n",
                encoding="utf-8",
            )
            (folder / ".gitkeep").write_text("", encoding="utf-8")
            script = folder / "Rule.cs"
            script.write_text("class Rule {}\n", encoding="utf-8")

            errors = validate_meta_pairs(root)

            self.assertEqual(1, len(errors))
            self.assertIn("Assets/Feature/Rule.cs", errors[0])

    def test_meta_validation_reports_duplicate_guid(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            assets = root / "Assets"
            assets.mkdir()
            first = assets / "First.txt"
            second = assets / "Second.txt"
            first.write_text("first\n", encoding="utf-8")
            second.write_text("second\n", encoding="utf-8")
            guid = "0123456789abcdef0123456789abcdef"
            (assets / "First.txt.meta").write_text(f"guid: {guid}\n", encoding="utf-8")
            (assets / "Second.txt.meta").write_text(f"guid: {guid}\n", encoding="utf-8")

            errors = validate_meta_pairs(root)

            self.assertTrue(any("Duplicate Unity GUID" in error for error in errors))

    def test_asmdef_validation_detects_internal_cycle(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            a_path = root / "Assets" / "A" / "A.asmdef"
            b_path = root / "Assets" / "B" / "B.asmdef"
            a_path.parent.mkdir(parents=True)
            b_path.parent.mkdir(parents=True)
            a_path.write_text(json.dumps({"name": "A", "references": ["B"]}), encoding="utf-8")
            b_path.write_text(json.dumps({"name": "B", "references": ["A"]}), encoding="utf-8")
            policy = {
                "assemblies": {
                    "A": {"path": "Assets/A/A.asmdef", "references": ["B"]},
                    "B": {"path": "Assets/B/B.asmdef", "references": ["A"]},
                }
            }

            errors = validate_asmdefs(root, policy)

            self.assertTrue(any("Assembly dependency cycle" in error for error in errors))

    def test_change_record_must_be_indexed_with_matching_id(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            changes = root / "docs" / "changes"
            changes.mkdir(parents=True)
            (changes / "README.md").write_text("# Index\n", encoding="utf-8")
            (changes / "2026-07-21-001-example.md").write_text(
                "# CHG-20260721-001：Example\n", encoding="utf-8"
            )

            errors = validate_change_records(root)

            self.assertEqual(
                ["Change index mismatch for CHG-20260721-001: None != 2026-07-21-001-example.md"],
                errors,
            )

    def test_material_diff_requires_record_and_index_update(self) -> None:
        errors = validate_changed_paths(["Assets/Feature/Rule.cs"])
        self.assertEqual(
            [
                "Material changes require a new or updated formal CHG record",
                "Material changes require an update to docs/changes/README.md",
            ],
            errors,
        )
        self.assertEqual(
            [],
            validate_changed_paths(
                [
                    "Assets/Feature/Rule.cs",
                    "docs/changes/2026-07-21-001-feature.md",
                    "docs/changes/README.md",
                ]
            ),
        )

    def test_diff_validation_includes_untracked_precommit_files(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.initialize_repository(root)
            seed = root / "README.md"
            seed.write_text("seed\n", encoding="utf-8")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True)
            subprocess.run(
                ["git", "commit", "-m", "seed"],
                cwd=root,
                check=True,
                capture_output=True,
            )
            rule = root / "Assets" / "Feature" / "Rule.cs"
            rule.parent.mkdir(parents=True)
            rule.write_text("class Rule {}\n", encoding="utf-8")

            errors = validate_diff_change_record(root, "HEAD")

            self.assertIn(
                "Material changes require a new or updated formal CHG record", errors
            )
            self.assertIn(
                "Material changes require an update to docs/changes/README.md", errors
            )

    def test_deleted_old_change_record_does_not_satisfy_current_change(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.initialize_repository(root)
            changes = root / "docs" / "changes"
            changes.mkdir(parents=True)
            old_name = "2026-07-20-001-old.md"
            old_id = "CHG-20260720-001"
            (changes / old_name).write_text(f"# {old_id}：Old\n", encoding="utf-8")
            (changes / "README.md").write_text(
                f"| {old_id} | [Old]({old_name}) |\n", encoding="utf-8"
            )
            asset = root / "Assets" / "Rule.cs"
            asset.parent.mkdir()
            asset.write_text("class Rule {}\n", encoding="utf-8")
            subprocess.run(["git", "add", "."], cwd=root, check=True)
            subprocess.run(
                ["git", "commit", "-m", "baseline"],
                cwd=root,
                check=True,
                capture_output=True,
            )

            (changes / old_name).unlink()
            (changes / "README.md").write_text("# Index\n", encoding="utf-8")
            asset.write_text("class Rule { int Value; }\n", encoding="utf-8")

            errors = validate_diff_change_record(root, "HEAD")

            self.assertIn(
                "Material changes require a new or updated formal CHG record", errors
            )
            self.assertNotIn(
                "Material changes require an update to docs/changes/README.md", errors
            )

    def test_lfs_validation_rejects_raw_blob_after_attribute_is_added(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.initialize_repository(root)
            asset = root / "Assets" / "IMAGE.PNG"
            asset.parent.mkdir()
            asset.write_bytes(b"not-an-lfs-pointer")
            subprocess.run(["git", "add", "."], cwd=root, check=True)
            subprocess.run(
                ["git", "commit", "-m", "raw asset"],
                cwd=root,
                check=True,
                capture_output=True,
            )
            (root / ".gitattributes").write_text(
                "*.[pP][nN][gG] filter=lfs diff=lfs merge=lfs -text\n",
                encoding="utf-8",
            )

            errors = validate_lfs_pointers(root)

            self.assertTrue(
                any("not a strict Git LFS pointer: Assets/IMAGE.PNG" in error for error in errors)
            )


if __name__ == "__main__":
    unittest.main()

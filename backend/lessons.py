"""
Lesson Plan Management for CAT-Pharmacy.
Handles retrieval and organization of study modules for the UI.
"""

from __future__ import annotations

import argparse
import json
import os
from datetime import datetime
from pathlib import Path
from typing import Any, List, Optional


DEFAULT_LESSON_FILE = "lesson-plans.json"


def _default_data_dir() -> Optional[Path]:
    if "CAT_DATA_DIR" in os.environ:
        return Path(os.environ["CAT_DATA_DIR"])

    if "LOCALAPPDATA" in os.environ:
        return Path(os.environ["LOCALAPPDATA"]) / "CatAdaptive" / "data"

    home_dir = Path.home()
    if home_dir.exists():
        return home_dir / ".local" / "share" / "CatAdaptive" / "data"

    return None


def _resolve_lessons_path(data_dir: Optional[Path]) -> Optional[Path]:
    target_dir = data_dir or _default_data_dir()
    if target_dir is None or not target_dir.exists():
        return None

    lesson_path = target_dir / DEFAULT_LESSON_FILE
    if lesson_path.exists():
        return lesson_path

    return None


def _extract_lessons(raw: Any) -> List[dict]:
    if raw is None:
        return []
    if isinstance(raw, list):
        return [item for item in raw if isinstance(item, dict)]
    if isinstance(raw, dict):
        if isinstance(raw.get("items"), list):
            return [item for item in raw["items"] if isinstance(item, dict)]
        if isinstance(raw.get("lessons"), list):
            return [item for item in raw["lessons"] if isinstance(item, dict)]
        values = [value for value in raw.values() if isinstance(value, dict)]
        if values:
            return values
    return []


def load_lessons(data_dir: Optional[Path]) -> tuple[List[dict], Optional[str], Optional[str]]:
    lesson_path = _resolve_lessons_path(data_dir)
    if lesson_path is None:
        return [], None, None

    payload = json.loads(lesson_path.read_text(encoding="utf-8"))
    lessons = _extract_lessons(payload)
    updated_at = datetime.utcfromtimestamp(lesson_path.stat().st_mtime).isoformat() + "Z"
    return lessons, str(lesson_path), updated_at


def main() -> int:
    parser = argparse.ArgumentParser(description="Lesson plan utilities")
    parser.add_argument("--data-dir", help="Override lesson plan data directory")
    args = parser.parse_args()

    data_dir = Path(args.data_dir) if args.data_dir else None
    lessons, source, last_updated = load_lessons(data_dir)

    print(
        json.dumps(
            {
                "lessons": lessons,
                "source": source,
                "lastUpdated": last_updated,
            }
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

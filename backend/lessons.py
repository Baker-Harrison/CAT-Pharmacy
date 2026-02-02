"""
Lesson generation and retrieval for CAT-Pharmacy.
Generates Brilliant-style interactive lessons from Knowledge Units.
"""
from __future__ import annotations

import argparse
import json
import os
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional
from uuid import uuid4

from backend.models import write_json_atomic

LESSON_STORE_FILE = "lesson-plans.json"
UNIT_STORE_FILE = "knowledge-units.json"
DEFAULT_MODEL = os.environ.get("GEMINI_MODEL", "gemini-1.5-pro")


@dataclass(frozen=True)
class LessonPayload:
    lessons: List[dict]
    source: Optional[str]
    last_updated: Optional[str]


def _default_data_dir() -> Optional[Path]:
    if "CAT_DATA_DIR" in os.environ:
        return Path(os.environ["CAT_DATA_DIR"])

    if "LOCALAPPDATA" in os.environ:
        return Path(os.environ["LOCALAPPDATA"]) / "CatAdaptive" / "data"

    home_dir = Path.home()
    if home_dir.exists():
        return home_dir / ".local" / "share" / "CatAdaptive" / "data"

    return None


def _resolve_data_dir(data_dir: Optional[Path]) -> Optional[Path]:
    target_dir = data_dir or _default_data_dir()
    if target_dir is None:
        return None
    target_dir.mkdir(parents=True, exist_ok=True)
    return target_dir


def _resolve_units_path(data_dir: Path) -> Path:
    return data_dir / UNIT_STORE_FILE


def _resolve_lessons_path(data_dir: Path) -> Path:
    return data_dir / LESSON_STORE_FILE


def _load_units(data_dir: Path) -> List[dict]:
    path = _resolve_units_path(data_dir)
    if not path.exists():
        return []
    payload = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(payload, dict):
        units = payload.get("units", [])
        return [unit for unit in units if isinstance(unit, dict)]
    if isinstance(payload, list):
        return [unit for unit in payload if isinstance(unit, dict)]
    return []


def _load_lessons(data_dir: Path) -> LessonPayload:
    lesson_path = _resolve_lessons_path(data_dir)
    if not lesson_path.exists():
        return LessonPayload([], None, None)

    payload = json.loads(lesson_path.read_text(encoding="utf-8"))
    lessons = payload.get("lessons") if isinstance(payload, dict) else []
    if not isinstance(lessons, list):
        lessons = []

    updated_at = datetime.fromtimestamp(lesson_path.stat().st_mtime, tz=timezone.utc).isoformat()
    return LessonPayload(
        lessons=[lesson for lesson in lessons if isinstance(lesson, dict)],
        source=str(lesson_path),
        last_updated=updated_at,
    )


def _save_lessons(data_dir: Path, lessons: List[dict]) -> None:
    payload = {
        "lessons": lessons,
        "updatedAt": datetime.now(tz=timezone.utc).isoformat(),
    }
    write_json_atomic(_resolve_lessons_path(data_dir), payload)


def _extract_json_from_text(text: str) -> Optional[dict]:
    if not text:
        return None
    text = text.strip()
    if text.startswith("{") and text.endswith("}"):
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            return None

    end_index = text.rfind("}")
    start_index = text.find("{")
    if start_index == -1 or end_index == -1 or end_index <= start_index:
        return None
    try:
        return json.loads(text[start_index : end_index + 1])
    except json.JSONDecodeError:
        return None


def _lesson_prompt(unit: dict) -> str:
    objectives = unit.get("learning_objectives") or []
    key_points = unit.get("key_points") or []
    summary = unit.get("summary") or ""

    return (
        "You are generating a Brilliant-style interactive lesson for pharmacy learners.\n"
        "Return JSON only with this schema:\n"
        "{\n"
        "  \"title\": string,\n"
        "  \"summary\": string,\n"
        "  \"objectives\": [string],\n"
        "  \"estimatedReadMinutes\": number,\n"
        "  \"sections\": [\n"
        "     {\"heading\": string, \"body\": string, \"checkpoint\": {\"prompt\": string, \"hint\": string}}\n"
        "  ],\n"
        "  \"preQuiz\": {\"items\": [{\"type\": string, \"prompt\": string, \"choices\": [string], \"answer\": string}]},\n"
        "  \"postQuiz\": {\"items\": [{\"type\": string, \"prompt\": string, \"choices\": [string], \"answer\": string}]}\n"
        "}\n"
        "Rules:\n"
        "- Make it interactive, concise, and Socratic.\n"
        "- Ensure 3-5 sections.\n"
        "- Pre-quiz is diagnostic (easier). Post-quiz is mastery (harder).\n"
        "- Pedagogical constraints for quiz items:\n"
        "  * Open Response and Fill In The Blank: Must be fundamental knowledge questions strictly aligned to the provided learning objectives.\n"
        "  * Case-Based MCQ: Must use a clinical scenario and require the learner to transfer knowledge from the objectives to a new context (application/analysis).\n"
        "- Use open response, fill-in, and case-based MCQ across quizzes.\n\n"
        f"Topic: {unit.get('topic', 'General')}\n"
        f"Summary: {summary}\n"
        f"Learning Objectives: {objectives}\n"
        f"Key Points: {key_points}\n"
    )


def _gemini_generate(unit: dict) -> Optional[dict]:
    api_key = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY")
    if not api_key:
        return None

    try:
        import google.generativeai as genai
    except ImportError:
        return None

    genai.configure(api_key=api_key)
    model = genai.GenerativeModel(DEFAULT_MODEL)
    response = model.generate_content(_lesson_prompt(unit))
    return _extract_json_from_text(getattr(response, "text", ""))


def _fallback_quiz_items(unit: dict, difficulty: str) -> List[dict]:
    topic = unit.get("topic", "the concept")
    key_points = unit.get("key_points") or []
    anchor_point = key_points[0] if key_points else topic
    return [
        {
            "type": "open_response",
            "prompt": f"Explain {topic} in one paragraph and connect it to {anchor_point}.",
            "answer": "Learner response",
        },
        {
            "type": "fill_in",
            "prompt": f"{topic} is best summarized as _______.",
            "answer": topic,
        },
        {
            "type": "case_mcq",
            "prompt": f"A patient scenario highlights {topic}. Which action aligns with best practice?",
            "choices": [
                "Apply the primary mechanism directly",
                "Ignore contraindications",
                "Delay therapy unnecessarily",
                "Choose an unrelated pathway",
            ],
            "answer": "Apply the primary mechanism directly",
        },
    ]


def _fallback_lesson(unit: dict) -> dict:
    title = unit.get("topic") or "Knowledge Unit"
    summary = unit.get("summary") or "Focused lesson summary generated from the knowledge unit."
    objectives = unit.get("learning_objectives") or []
    key_points = unit.get("key_points") or []

    sections = []
    for idx, point in enumerate(key_points[:4], start=1):
        sections.append(
            {
                "heading": f"Step {idx}: {point[:48]}",
                "body": f"Connect {point} to the broader mechanism and clinical application.",
                "checkpoint": {
                    "prompt": f"Why does {point} matter in patient care?",
                    "hint": "Link the mechanism to an outcome.",
                },
            }
        )

    if not sections:
        sections.append(
            {
                "heading": "Core idea",
                "body": summary,
                "checkpoint": {
                    "prompt": "Describe the most critical clinical takeaway.",
                    "hint": "Aim for one sentence.",
                },
            }
        )

    estimated_read = max(6, 4 + len(key_points))
    return {
        "title": title,
        "summary": summary,
        "objectives": objectives,
        "estimatedReadMinutes": estimated_read,
        "sections": sections,
        "preQuiz": {"items": _fallback_quiz_items(unit, "pre")},
        "postQuiz": {"items": _fallback_quiz_items(unit, "post")},
    }


def _build_lesson(unit: dict) -> dict:
    content = _gemini_generate(unit) or _fallback_lesson(unit)
    lesson_id = content.get("id") if isinstance(content, dict) else None
    payload = {
        "id": lesson_id or str(uuid4()),
        "title": content.get("title") if isinstance(content, dict) else unit.get("topic", "Lesson"),
        "summary": content.get("summary") if isinstance(content, dict) else unit.get("summary", ""),
        "objectives": content.get("objectives") if isinstance(content, dict) else unit.get("learning_objectives", []),
        "estimatedReadMinutes": content.get("estimatedReadMinutes", 0) if isinstance(content, dict) else 0,
        "sections": content.get("sections", []) if isinstance(content, dict) else [],
        "preQuiz": content.get("preQuiz") if isinstance(content, dict) else {},
        "postQuiz": content.get("postQuiz") if isinstance(content, dict) else {},
        "sourceUnitId": unit.get("id"),
        "createdAt": datetime.now(tz=timezone.utc).isoformat(),
        "flow": ["preQuiz", "lesson", "postQuiz"],
    }
    return payload


def generate_lessons(data_dir: Path, limit: Optional[int] = None) -> LessonPayload:
    units = _load_units(data_dir)
    if not units:
        return LessonPayload([], None, None)

    existing_payload = _load_lessons(data_dir)
    lessons = existing_payload.lessons
    existing_unit_ids = {lesson.get("sourceUnitId") for lesson in lessons if lesson.get("sourceUnitId")}

    generated = 0
    for unit in units:
        if unit.get("id") in existing_unit_ids:
            continue
        lessons.append(_build_lesson(unit))
        generated += 1
        if limit and generated >= limit:
            break

    _save_lessons(data_dir, lessons)
    return _load_lessons(data_dir)


def main() -> int:
    parser = argparse.ArgumentParser(description="Lesson generation utilities")
    parser.add_argument("--data-dir", help="Override lesson plan data directory")
    parser.add_argument("--generate", action="store_true", help="Generate lessons from knowledge units")
    parser.add_argument("--limit", type=int, default=1, help="Limit number of generated lessons")
    args = parser.parse_args()

    data_dir = _resolve_data_dir(Path(args.data_dir) if args.data_dir else None)
    if data_dir is None:
        raise FileNotFoundError("Unable to resolve data directory")

    if args.generate:
        payload = generate_lessons(data_dir, limit=args.limit)
    else:
        payload = _load_lessons(data_dir)

    print(
        json.dumps(
            {
                "lessons": payload.lessons,
                "source": payload.source,
                "lastUpdated": payload.last_updated,
            }
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

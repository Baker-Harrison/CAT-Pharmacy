"""
Practice exam generator for CAT-Pharmacy.
Creates mixed-format exams: open response, fill-in, and case-based MCQ.
"""
from __future__ import annotations

import argparse
import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import List, Optional
from uuid import uuid4

from backend.models import write_json_atomic

EXAM_STORE_FILE = "practice-exams.json"
UNIT_STORE_FILE = "knowledge-units.json"
DEFAULT_MODEL = os.environ.get("GEMINI_MODEL", "gemini-1.5-pro")


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


def _resolve_exam_path(data_dir: Path) -> Path:
    return data_dir / EXAM_STORE_FILE


def _resolve_units_path(data_dir: Path) -> Path:
    return data_dir / UNIT_STORE_FILE


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


def _load_exams(data_dir: Path) -> List[dict]:
    path = _resolve_exam_path(data_dir)
    if not path.exists():
        return []
    payload = json.loads(path.read_text(encoding="utf-8"))
    if isinstance(payload, dict):
        exams = payload.get("exams", [])
        return [exam for exam in exams if isinstance(exam, dict)]
    if isinstance(payload, list):
        return [exam for exam in payload if isinstance(exam, dict)]
    return []


def _save_exams(data_dir: Path, exams: List[dict]) -> None:
    payload = {
        "exams": exams,
        "updatedAt": datetime.now(tz=timezone.utc).isoformat(),
    }
    write_json_atomic(_resolve_exam_path(data_dir), payload)


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


def _exam_prompt(units: List[dict]) -> str:
    topics = [unit.get("topic") for unit in units if unit.get("topic")]
    key_points = [unit.get("key_points") for unit in units if unit.get("key_points")]
    learning_objectives = [unit.get("learning_objectives") for unit in units if unit.get("learning_objectives")]
    
    return (
        "Generate a practice exam in JSON only. Schema:\n"
        "{\n"
        "  \"title\": string,\n"
        "  \"summary\": string,\n"
        "  \"sections\": {\n"
        "    \"Open Response\": [{\"prompt\": string}],\n"
        "    \"Fill In The Blank\": [{\"prompt\": string, \"answer\": string}],\n"
        "    \"Case-Based MCQ\": [{\"prompt\": string, \"choices\": [string], \"answer\": string}]\n"
        "  }\n"
        "}\n"
        "Rules:\n"
        "- 3 questions per section.\n"
        "- Pedagogical constraints:\n"
        "  * Open Response and Fill In The Blank: Must be fundamental knowledge questions strictly aligned to the provided learning objectives.\n"
        "  * Case-Based MCQ: Must use a clinical scenario and require the learner to transfer knowledge from the objectives to a new context (application/analysis).\n"
        "- Keep it concise.\n\n"
        f"Topics: {topics}\n"
        f"Key points: {key_points}\n"
        f"Learning Objectives: {learning_objectives}\n"
    )


def _gemini_generate(units: List[dict]) -> Optional[dict]:
    api_key = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY")
    if not api_key:
        return None
    try:
        import google.generativeai as genai
    except ImportError:
        return None

    genai.configure(api_key=api_key)
    model = genai.GenerativeModel(DEFAULT_MODEL)
    response = model.generate_content(_exam_prompt(units))
    return _extract_json_from_text(getattr(response, "text", ""))


def _fallback_exam(units: List[dict]) -> dict:
    topics = [unit.get("topic") for unit in units if unit.get("topic")]
    topic = topics[0] if topics else "core pharmacotherapy"
    return {
        "title": "Practice Exam",
        "summary": "Mixed-format practice exam with recall, application, and clinical reasoning.",
        "sections": {
            "Open Response": [
                {"prompt": f"Summarize the key clinical considerations for {topic}."},
                {"prompt": "Describe a common contraindication and how to address it."},
                {"prompt": "Outline a monitoring plan for therapy safety."},
            ],
            "Fill In The Blank": [
                {"prompt": f"The primary mechanism of {topic} is _______.", "answer": ""},
                {"prompt": "The most important lab to monitor is _______.", "answer": ""},
                {"prompt": "A red-flag adverse effect is _______.", "answer": ""},
            ],
            "Case-Based MCQ": [
                {
                    "prompt": "A patient presents with symptoms consistent with the knowledge unit. What is the next best step?",
                    "choices": [
                        "Start first-line therapy",
                        "Delay treatment",
                        "Ignore patient history",
                        "Choose an unrelated option",
                    ],
                    "answer": "Start first-line therapy",
                },
                {
                    "prompt": "Which adjustment is most appropriate for renal impairment?",
                    "choices": ["Reduce dose", "Increase dose", "Stop monitoring", "Switch to placebo"],
                    "answer": "Reduce dose",
                },
                {
                    "prompt": "Which counseling point is most essential?",
                    "choices": ["Explain dosing schedule", "Avoid all exercise", "Skip follow-up", "Ignore side effects"],
                    "answer": "Explain dosing schedule",
                },
            ],
        },
    }


def _build_exam(units: List[dict]) -> dict:
    content = _gemini_generate(units) or _fallback_exam(units)
    exam_id = content.get("id") if isinstance(content, dict) else None
    return {
        "id": exam_id or str(uuid4()),
        "title": content.get("title", "Practice Exam"),
        "summary": content.get("summary", ""),
        "sections": content.get("sections", {}),
        "createdAt": datetime.now(tz=timezone.utc).isoformat(),
        "sourceUnitIds": [unit.get("id") for unit in units if unit.get("id")],
    }


def generate_exam(data_dir: Path) -> List[dict]:
    units = _load_units(data_dir)
    if not units:
        return []

    exams = _load_exams(data_dir)
    exams.insert(0, _build_exam(units))
    _save_exams(data_dir, exams)
    return exams


def main() -> int:
    parser = argparse.ArgumentParser(description="Practice exam utilities")
    parser.add_argument("--data-dir", help="Override exam data directory")
    parser.add_argument("--generate", action="store_true", help="Generate a new practice exam")
    args = parser.parse_args()

    data_dir = _resolve_data_dir(Path(args.data_dir) if args.data_dir else None)
    if data_dir is None:
        raise FileNotFoundError("Unable to resolve data directory")

    if args.generate:
        exams = generate_exam(data_dir)
    else:
        exams = _load_exams(data_dir)

    print(json.dumps({"exams": exams}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

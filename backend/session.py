"""
Adaptive Learning Session Engine for CAT-Pharmacy.
Handles mastery calculations, IRT-based student modeling, and session summary generation.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import sys
from dataclasses import dataclass
from datetime import datetime, timedelta
from enum import Enum
from pathlib import Path
from typing import Dict, Iterable, List, Optional
from uuid import UUID, uuid4

from backend.models import GraphSummary

SESSION_STATE_FILE = "adaptive-session.json"
KNOWLEDGE_UNITS_FILE = "knowledge-units.json"
STUDENT_STATE_PREFIX = "student-state"


@dataclass(frozen=True)
class AbilityEstimate:
    id: UUID
    theta: float
    standard_error: float
    method: str
    timestamp: datetime

    @property
    def variance(self) -> float:
        return self.standard_error * self.standard_error

    @property
    def information(self) -> float:
        if self.variance <= 0:
            return 0.0
        return 1.0 / self.variance

    @staticmethod
    def initial(theta: float = -1.5, standard_error: float = 1.0, method: str = "Prior") -> "AbilityEstimate":
        return AbilityEstimate(uuid4(), theta, standard_error, method, datetime.utcnow())


@dataclass(frozen=True)
class TerminationCriteria:
    target_standard_error: float
    max_items: int
    mastery_theta: Optional[float]
    max_stall_count: int

    @staticmethod
    def default() -> "TerminationCriteria":
        return TerminationCriteria(0.3, 25, 1.2, 3)


@dataclass(frozen=True)
class LearnerProfile:
    id: UUID
    name: str
    objectives: List[str]

    @staticmethod
    def create(name: str, objectives: Optional[Iterable[str]] = None) -> "LearnerProfile":
        if name is None or not name.strip():
            raise ValueError("Name is required")

        goals = [goal.strip() for goal in objectives or [] if goal and goal.strip()]
        return LearnerProfile(uuid4(), name.strip(), goals)


class ItemFormat(str, Enum):
    MULTIPLE_CHOICE = "MultipleChoice"
    SHORT_ANSWER = "ShortAnswer"
    CASE_SCENARIO = "CaseScenario"
    MECHANISTIC_EXPLANATION = "MechanisticExplanation"


@dataclass(frozen=True)
class ItemChoice:
    id: UUID
    text: str
    is_correct: bool

    @staticmethod
    def create(text: str, is_correct: bool) -> "ItemChoice":
        return ItemChoice(uuid4(), text, is_correct)


@dataclass(frozen=True)
class ItemParameter:
    difficulty: float
    discrimination: float = 1.0
    guessing: float = 0.2

    _D = 1.7
    _MAX_EXPONENT = 35.0
    _MIN_PROBABILITY = 1e-9

    def probability_correct(self, theta: float) -> float:
        exponent = -self._D * self.discrimination * (theta - self.difficulty)
        capped = max(-self._MAX_EXPONENT, min(self._MAX_EXPONENT, exponent))
        logistic = 1.0 / (1.0 + math.exp(capped))
        return self.guessing + (1 - self.guessing) * logistic

    def fisher_information(self, theta: float) -> float:
        p = self.probability_correct(theta)
        q = 1.0 - p
        one_minus_guessing = 1.0 - self.guessing
        if one_minus_guessing <= 0:
            return 0.0

        clamped_p = max(self._MIN_PROBABILITY, min(1.0 - self._MIN_PROBABILITY, p))
        clamped_q = 1.0 - clamped_p
        scaled_slope = self._D * self.discrimination
        normalized_p = (clamped_p - self.guessing) / one_minus_guessing
        return (scaled_slope**2) * (clamped_q / clamped_p) * (normalized_p**2)


@dataclass(frozen=True)
class ItemTemplate:
    id: UUID
    stem: str
    choices: List[ItemChoice]
    format: ItemFormat
    parameter: ItemParameter
    knowledge_unit_ids: List[UUID]
    topic: str
    subtopic: str
    explanation: str
    bloom_level: str
    learning_objective: str
    primary_concept_id: Optional[UUID]
    secondary_concept_ids: List[UUID]

    @staticmethod
    def create(
        stem: str,
        choices: Iterable[ItemChoice],
        format: ItemFormat,
        parameter: ItemParameter,
        knowledge_unit_ids: Iterable[UUID],
        topic: str,
        subtopic: str,
        explanation: str,
        bloom_level: str = "Apply",
        learning_objective: str = "",
        primary_concept_id: Optional[UUID] = None,
        secondary_concept_ids: Optional[Iterable[UUID]] = None,
    ) -> "ItemTemplate":
        if stem is None or not stem.strip():
            raise ValueError("Stem is required")

        choice_list = list(choices or [])
        if not choice_list and format == ItemFormat.MULTIPLE_CHOICE:
            raise ValueError("Multiple choice items require at least one choice")

        ku_list = list(knowledge_unit_ids or [])
        secondary_list = list(secondary_concept_ids or [])

        return ItemTemplate(
            id=uuid4(),
            stem=stem.strip(),
            choices=choice_list,
            format=format,
            parameter=parameter,
            knowledge_unit_ids=ku_list,
            topic=topic.strip() if topic else "",
            subtopic=subtopic.strip() if subtopic else "",
            explanation=explanation.strip() if explanation else "",
            bloom_level=bloom_level.strip() if bloom_level else "Apply",
            learning_objective=learning_objective.strip() if learning_objective else "",
            primary_concept_id=primary_concept_id,
            secondary_concept_ids=secondary_list,
        )


@dataclass(frozen=True)
class ItemResponse:
    item_id: UUID
    item_template_id: UUID
    is_correct: bool
    score: float
    response_time: timedelta
    raw_response: str
    ability_after: AbilityEstimate

    @staticmethod
    def create(
        item_id: UUID,
        template_id: UUID,
        is_correct: bool,
        score: float,
        response_time: timedelta,
        raw_response: str,
        ability_after: AbilityEstimate,
    ) -> "ItemResponse":
        return ItemResponse(
            item_id=item_id,
            item_template_id=template_id,
            is_correct=is_correct,
            score=score,
            response_time=response_time,
            raw_response=raw_response,
            ability_after=ability_after,
        )


class AdaptiveSession:
    def __init__(
        self,
        id: UUID,
        learner: LearnerProfile,
        item_pool: Iterable[ItemTemplate],
        criteria: TerminationCriteria,
        initial_ability: Optional[AbilityEstimate] = None,
    ) -> None:
        if learner is None:
            raise ValueError("learner cannot be None")
        if item_pool is None:
            raise ValueError("item_pool cannot be None")
        if criteria is None:
            raise ValueError("criteria cannot be None")

        self.id = id
        self.learner = learner
        self.criteria = criteria
        self.current_ability = initial_ability or AbilityEstimate.initial()
        self.active_item: Optional[ItemTemplate] = None
        self.is_complete = False

        self._remaining_items = list(item_pool)
        self._responses: List[ItemResponse] = []

    @property
    def responses(self) -> List[ItemResponse]:
        return list(self._responses)

    def advance_to_next_item(self) -> Optional[ItemTemplate]:
        if self.is_complete:
            self.active_item = None
            return None

        if not self._remaining_items:
            self.is_complete = True
            self.active_item = None
            return None

        self.active_item = max(
            self._remaining_items,
            key=lambda item: item.parameter.fisher_information(self.current_ability.theta),
        )
        self._remaining_items.remove(self.active_item)
        return self.active_item

    def record_response(self, is_correct: bool, response_time: timedelta, raw_response: str) -> ItemResponse:
        if self.active_item is None:
            raise RuntimeError("Cannot record a response without an active item.")

        score = 1.0 if is_correct else 0.0
        updated_ability = self._update_ability_estimate(self.active_item, is_correct)
        self.current_ability = updated_ability

        response = ItemResponse.create(
            uuid4(),
            self.active_item.id,
            is_correct,
            score,
            response_time,
            raw_response,
            updated_ability,
        )

        self._responses.append(response)
        self.active_item = None

        if self._should_terminate():
            self.is_complete = True

        return response

    def _update_ability_estimate(self, item: ItemTemplate, is_correct: bool) -> AbilityEstimate:
        theta = self.current_ability.theta
        probability = item.parameter.probability_correct(theta)
        score = 1.0 if is_correct else 0.0
        info = max(item.parameter.fisher_information(theta), 1e-3)
        gradient = score - probability
        step = gradient / info

        new_theta = max(-3.0, min(3.0, theta + step))
        standard_error = 1.0 / math.sqrt(info)
        return AbilityEstimate(uuid4(), new_theta, standard_error, "MLE", datetime.utcnow())

    def _should_terminate(self) -> bool:
        if len(self._responses) >= self.criteria.max_items:
            return True

        if self.current_ability.standard_error <= self.criteria.target_standard_error:
            return True

        if self.criteria.mastery_theta is not None and self.current_ability.theta >= self.criteria.mastery_theta:
            return True

        return False


def build_graph_summary(graph_path: Optional[Path], student_state_path: Optional[Path]) -> GraphSummary:
    if graph_path is None or not graph_path.exists():
        return GraphSummary.empty()

    data = json.loads(graph_path.read_text(encoding="utf-8"))
    student_state = None
    if student_state_path is not None and student_state_path.exists():
        try:
            student_state = json.loads(student_state_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            student_state = None

    last_updated = datetime.utcfromtimestamp(graph_path.stat().st_mtime).isoformat() + "Z"
    return GraphSummary.from_payload(
        payload=data,
        source=str(graph_path),
        last_updated=last_updated,
        student_state=student_state,
    )


def resolve_graph_path(data_dir: Optional[Path]) -> Optional[Path]:
    if data_dir is None:
        data_dir = _default_data_dir()

    if data_dir is None or not data_dir.exists():
        return None

    knowledge_graphs = sorted(data_dir.glob("knowledge-graph-*.json"), key=lambda p: p.stat().st_mtime)
    if knowledge_graphs:
        return knowledge_graphs[-1]

    domain_graph = data_dir / "domain-knowledge-graph.json"
    if domain_graph.exists():
        return domain_graph

    return None


def resolve_student_state_path(data_dir: Optional[Path]) -> Optional[Path]:
    if data_dir is None:
        data_dir = _default_data_dir()

    if data_dir is None or not data_dir.exists():
        return None

    states = sorted(data_dir.glob("student-state-*.json"), key=lambda p: p.stat().st_mtime)
    if states:
        return states[-1]

    return None


def _default_data_dir() -> Optional[Path]:
    env_dir = Path(str(Path.home()))
    if "CAT_DATA_DIR" in os.environ:
        return Path(os.environ["CAT_DATA_DIR"])

    if "LOCALAPPDATA" in os.environ:
        return Path(os.environ["LOCALAPPDATA"]) / "CatAdaptive" / "data"

    if env_dir.exists():
        return env_dir / ".local" / "share" / "CatAdaptive" / "data"

    return None


def _summary_to_payload(summary: GraphSummary) -> dict:
    return summary.to_payload()


def _resolve_units_path(data_dir: Optional[Path]) -> Optional[Path]:
    target_dir = data_dir or _default_data_dir()
    if target_dir is None:
        return None
    if not target_dir.exists():
        return None
    units_path = target_dir / KNOWLEDGE_UNITS_FILE
    if units_path.exists():
        return units_path
    return None


def _load_units(data_dir: Optional[Path]) -> List[dict]:
    units_path = _resolve_units_path(data_dir)
    if units_path is None:
        return []

    payload = json.loads(units_path.read_text(encoding="utf-8"))
    raw_units = payload.get("units") if isinstance(payload, dict) else payload
    if not isinstance(raw_units, list):
        return []

    normalized: List[dict] = []
    for index, raw in enumerate(raw_units):
        if not isinstance(raw, dict):
            continue
        unit_id = raw.get("id") or raw.get("Id") or f"unit-{index}"
        topic = raw.get("topic") or raw.get("Topic") or raw.get("summary") or raw.get("Summary") or "Untitled"
        summary = raw.get("summary") or raw.get("Summary") or ""
        key_points = raw.get("key_points") or raw.get("keyPoints") or raw.get("KeyPoints") or []
        if not isinstance(key_points, list):
            key_points = []
        normalized.append(
            {
                "id": str(unit_id),
                "topic": str(topic),
                "summary": str(summary),
                "keyPoints": [str(point) for point in key_points if point],
            }
        )

    return normalized


def _resolve_session_state_path(data_dir: Optional[Path]) -> Optional[Path]:
    target_dir = data_dir or _default_data_dir()
    if target_dir is None:
        return None
    if not target_dir.exists():
        return None
    return target_dir / SESSION_STATE_FILE


def _load_session_state(data_dir: Optional[Path]) -> Optional[dict]:
    state_path = _resolve_session_state_path(data_dir)
    if state_path is None or not state_path.exists():
        return None
    try:
        return json.loads(state_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None


def _save_session_state(data_dir: Optional[Path], state: dict) -> None:
    target_dir = data_dir or _default_data_dir()
    if target_dir is None:
        return
    target_dir.mkdir(parents=True, exist_ok=True)
    state_path = target_dir / SESSION_STATE_FILE
    state_path.write_text(json.dumps(state, indent=2), encoding="utf-8")


def _resolve_student_state_save_path(data_dir: Optional[Path]) -> Optional[Path]:
    target_dir = data_dir or _default_data_dir()
    if target_dir is None:
        return None
    target_dir.mkdir(parents=True, exist_ok=True)

    existing = resolve_student_state_path(target_dir)
    if existing is not None:
        return existing

    timestamp = datetime.utcnow().isoformat().replace(":", "-").replace(".", "-")
    return target_dir / f"{STUDENT_STATE_PREFIX}-{timestamp}.json"


def _initialize_session(units: List[dict]) -> dict:
    ability = AbilityEstimate.initial()
    unit_ids = [unit["id"] for unit in units]
    count = len(unit_ids) or 1
    unit_difficulties = {
        unit_id: ((index / (count - 1)) * 2.0 - 1.0) if count > 1 else 0.0
        for index, unit_id in enumerate(unit_ids)
    }
    mastery = {
        unit_id: {
            "score": 0.0,
            "level": "Unknown",
            "attempts": 0,
            "correct": 0,
            "lastAssessed": None,
        }
        for unit_id in unit_ids
    }
    return {
        "sessionId": str(uuid4()),
        "createdAt": datetime.utcnow().isoformat() + "Z",
        "updatedAt": datetime.utcnow().isoformat() + "Z",
        "ability": {
            "theta": ability.theta,
            "standardError": ability.standard_error,
        },
        "responses": [],
        "remainingUnitIds": unit_ids,
        "activeUnitId": None,
        "unitDifficulties": unit_difficulties,
        "mastery": mastery,
        "stallCount": 0,
    }


def _update_ability_estimate(theta: float, item_parameter: ItemParameter, is_correct: bool) -> AbilityEstimate:
    probability = item_parameter.probability_correct(theta)
    score = 1.0 if is_correct else 0.0
    info = max(item_parameter.fisher_information(theta), 1e-3)
    gradient = score - probability
    step = gradient / info

    new_theta = max(-3.0, min(3.0, theta + step))
    standard_error = 1.0 / math.sqrt(info)
    return AbilityEstimate(uuid4(), new_theta, standard_error, "MLE", datetime.utcnow())


def _normalize_level(score: float, attempts: int) -> str:
    if attempts <= 0:
        return "Unknown"
    if score >= 0.85:
        return "Advanced"
    if score >= 0.65:
        return "Proficient"
    if score >= 0.45:
        return "Developing"
    if score >= 0.2:
        return "Novice"
    return "Unknown"


def _update_mastery_entry(entry: dict, is_correct: bool, assessed_at: str) -> dict:
    attempts = int(entry.get("attempts") or 0) + 1
    correct = int(entry.get("correct") or 0) + (1 if is_correct else 0)
    score = float(entry.get("score") or 0.0)
    score = max(0.0, min(1.0, score + (0.2 if is_correct else -0.12)))
    level = _normalize_level(score, attempts)
    return {
        "score": score,
        "level": level,
        "attempts": attempts,
        "correct": correct,
        "lastAssessed": assessed_at,
    }


def _evaluate_response(unit: dict, payload: dict) -> bool:
    if isinstance(payload.get("isCorrect"), bool):
        return payload["isCorrect"]

    raw_answer = payload.get("answer") or payload.get("rawResponse") or ""
    if not isinstance(raw_answer, str) or not raw_answer.strip():
        return False

    answer = raw_answer.lower()
    key_points = unit.get("keyPoints") or []
    for key_point in key_points:
        if isinstance(key_point, str) and key_point.strip().lower() in answer:
            return True
    return False


def _select_next_unit(
    units_by_id: Dict[str, dict],
    remaining_unit_ids: List[str],
    mastery: Dict[str, dict],
    unit_difficulties: Dict[str, float],
    theta: float,
) -> Optional[dict]:
    if not remaining_unit_ids:
        return None

    candidates: List[dict] = []
    for unit_id in remaining_unit_ids:
        unit = units_by_id.get(unit_id)
        if unit is None:
            continue
        entry = mastery.get(unit_id, {})
        score = float(entry.get("score") or 0.0)
        last_assessed = entry.get("lastAssessed")
        difficulty = float(unit_difficulties.get(unit_id, 0.0))
        info = ItemParameter(difficulty=difficulty).fisher_information(theta)
        priority = (1.0 - score) * (1.0 + info)
        candidates.append(
            {
                "unit": unit,
                "priority": priority,
                "lastAssessed": last_assessed or "",
            }
        )

    if not candidates:
        return None

    candidates.sort(key=lambda item: (item["priority"], item["lastAssessed"]))
    return candidates[-1]["unit"]


def _build_mastery_levels(mastery: Dict[str, dict]) -> Dict[str, int]:
    levels = {"Advanced": 0, "Proficient": 0, "Developing": 0, "Novice": 0, "Unknown": 0}
    for entry in mastery.values():
        level = entry.get("level") or "Unknown"
        if level not in levels:
            level = "Unknown"
        levels[level] = levels.get(level, 0) + 1
    return levels


def _save_student_state(
    data_dir: Optional[Path],
    session_id: str,
    ability: dict,
    mastery: Dict[str, dict],
) -> None:
    save_path = _resolve_student_state_save_path(data_dir)
    if save_path is None:
        return

    mastery_list: List[dict] = []
    for unit_id, entry in mastery.items():
        mastery_list.append(
            {
                "domainNodeId": unit_id,
                "level": entry.get("level"),
                "lastAssessed": entry.get("lastAssessed"),
                "score": entry.get("score"),
                "attempts": entry.get("attempts"),
                "correct": entry.get("correct"),
            }
        )

    payload = {
        "sessionId": session_id,
        "updatedAt": datetime.utcnow().isoformat() + "Z",
        "ability": ability,
        "knowledgeMasteries": mastery_list,
    }
    save_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def process_response(payload: dict, data_dir: Optional[Path]) -> dict:
    units = _load_units(data_dir)
    if not units:
        raise RuntimeError("No knowledge units available. Ingest content before starting a session.")

    units_by_id = {unit["id"]: unit for unit in units}
    state = _load_session_state(data_dir) or _initialize_session(units)

    mastery = state.get("mastery") or {}
    remaining_unit_ids = list(state.get("remainingUnitIds") or [])
    unit_difficulties = state.get("unitDifficulties") or {}
    responses = list(state.get("responses") or [])
    ability = state.get("ability") or {}

    action = payload.get("action") or "response"
    assessed_at = datetime.utcnow().isoformat() + "Z"

    if payload.get("reset"):
        state = _initialize_session(units)
        mastery = state["mastery"]
        remaining_unit_ids = list(state["remainingUnitIds"])
        unit_difficulties = state["unitDifficulties"]
        responses = list(state["responses"])
        ability = state["ability"]

    if not remaining_unit_ids:
        remaining_unit_ids = [unit["id"] for unit in units]

    current_unit_id = payload.get("unitId") or state.get("activeUnitId")
    current_unit = units_by_id.get(str(current_unit_id)) if current_unit_id else None

    if action == "start" or current_unit is None:
        current_unit = _select_next_unit(
            units_by_id,
            remaining_unit_ids,
            mastery,
            unit_difficulties,
            float(ability.get("theta") or -1.5),
        )
        state["activeUnitId"] = current_unit["id"] if current_unit else None
        state["updatedAt"] = assessed_at
        _save_session_state(data_dir, state)
        return {
            "sessionId": state["sessionId"],
            "currentUnit": current_unit,
            "progress": {
                "completed": len(responses),
                "total": len(units),
                "percent": round((len(responses) / max(len(units), 1)) * 100, 1),
            },
            "ability": ability,
            "masteryLevels": _build_mastery_levels(mastery),
            "isComplete": False if current_unit else True,
        }

    is_correct = _evaluate_response(current_unit, payload)
    previous_theta = float(ability.get("theta") or -1.5)
    difficulty = float(unit_difficulties.get(current_unit["id"], 0.0))
    updated_ability = _update_ability_estimate(previous_theta, ItemParameter(difficulty=difficulty), is_correct)

    ability = {
        "theta": updated_ability.theta,
        "standardError": updated_ability.standard_error,
    }

    responses.append(
        {
            "unitId": current_unit["id"],
            "isCorrect": is_correct,
            "answer": payload.get("answer") or payload.get("rawResponse") or "",
            "timestamp": assessed_at,
            "abilityAfter": ability,
        }
    )

    mastery_entry = mastery.get(current_unit["id"]) or {
        "score": 0.0,
        "level": "Unknown",
        "attempts": 0,
        "correct": 0,
        "lastAssessed": None,
    }
    mastery[current_unit["id"]] = _update_mastery_entry(mastery_entry, is_correct, assessed_at)

    if mastery[current_unit["id"]]["score"] >= 0.85 and current_unit["id"] in remaining_unit_ids:
        remaining_unit_ids.remove(current_unit["id"])

    theta_shift = abs(updated_ability.theta - previous_theta)
    stall_count = int(state.get("stallCount") or 0)
    if theta_shift < 0.01:
        stall_count += 1
    else:
        stall_count = 0

    criteria = TerminationCriteria.default()
    is_complete = False
    if len(responses) >= criteria.max_items:
        is_complete = True
    if updated_ability.standard_error <= criteria.target_standard_error:
        is_complete = True
    if criteria.mastery_theta is not None and updated_ability.theta >= criteria.mastery_theta:
        is_complete = True
    if stall_count >= criteria.max_stall_count:
        is_complete = True
    if not remaining_unit_ids:
        is_complete = True

    next_unit = None
    if not is_complete:
        next_unit = _select_next_unit(
            units_by_id,
            remaining_unit_ids,
            mastery,
            unit_difficulties,
            updated_ability.theta,
        )

    state.update(
        {
            "updatedAt": assessed_at,
            "ability": ability,
            "responses": responses,
            "remainingUnitIds": remaining_unit_ids,
            "activeUnitId": next_unit["id"] if next_unit else None,
            "mastery": mastery,
            "stallCount": stall_count,
        }
    )

    _save_session_state(data_dir, state)
    _save_student_state(data_dir, state["sessionId"], ability, mastery)

    feedback = "Great job! Mastery is trending up." if is_correct else "Review the key points and try again."
    return {
        "sessionId": state["sessionId"],
        "currentUnit": current_unit,
        "nextUnit": next_unit,
        "result": {
            "isCorrect": is_correct,
            "feedback": feedback,
        },
        "progress": {
            "completed": len(responses),
            "total": len(units),
            "percent": round((len(responses) / max(len(units), 1)) * 100, 1),
        },
        "ability": ability,
        "masteryLevels": _build_mastery_levels(mastery),
        "isComplete": is_complete,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Adaptive session utilities")
    parser.add_argument("--summary", action="store_true", help="Output knowledge graph summary as JSON")
    parser.add_argument("--process-response", action="store_true", help="Process a learning response as JSON")
    parser.add_argument("--data-dir", help="Override knowledge graph data directory")
    args = parser.parse_args()

    if args.summary:
        data_dir = Path(args.data_dir) if args.data_dir else None
        graph_path = resolve_graph_path(data_dir)
        student_state_path = resolve_student_state_path(data_dir)
        summary = build_graph_summary(graph_path, student_state_path)
        print(json.dumps(_summary_to_payload(summary)))
        return 0

    if args.process_response:
        data_dir = Path(args.data_dir) if args.data_dir else None
        raw = sys.stdin.read()
        payload = json.loads(raw) if raw.strip() else {}
        result = process_response(payload, data_dir)
        print(json.dumps(result))
        return 0

    parser.print_help()
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

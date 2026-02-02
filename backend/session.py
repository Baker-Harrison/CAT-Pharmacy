from __future__ import annotations

import argparse
import json
import math
import os
from dataclasses import dataclass
from datetime import datetime, timedelta
from enum import Enum
from pathlib import Path
from typing import Iterable, List, Optional
from uuid import UUID, uuid4

from backend.models import GraphSummary


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


def main() -> int:
    parser = argparse.ArgumentParser(description="Adaptive session utilities")
    parser.add_argument("--summary", action="store_true", help="Output knowledge graph summary as JSON")
    parser.add_argument("--data-dir", help="Override knowledge graph data directory")
    args = parser.parse_args()

    if args.summary:
        data_dir = Path(args.data_dir) if args.data_dir else None
        graph_path = resolve_graph_path(data_dir)
        student_state_path = resolve_student_state_path(data_dir)
        summary = build_graph_summary(graph_path, student_state_path)
        print(json.dumps(_summary_to_payload(summary)))
        return 0

    parser.print_help()
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

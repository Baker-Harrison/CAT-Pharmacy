"""
Core domain models and Knowledge Graph logic for CAT-Pharmacy.
This module defines the data structures for Knowledge Units, Nodes, and Edges.
"""

from __future__ import annotations

import json
import os
import time
from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import Enum
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional
from uuid import UUID, uuid4


@dataclass(frozen=True)
class VisualRepresentation:
    type: str  # e.g., "diagram", "chart", "map"
    data: Any  # JSON-serializable structure for rendering
    caption: str
    function_logic: Optional[str] = None


@dataclass(frozen=True)
class KnowledgeUnit:
    id: UUID
    topic: str
    subtopic: str
    source_slide_id: str
    summary: str
    key_points: List[str]
    learning_objectives: List[str]
    visualizations: List[VisualRepresentation] = field(default_factory=list)

    @staticmethod
    def create(
        topic: str,
        subtopic: str,
        source_slide_id: str,
        summary: str,
        key_points: Iterable[str],
        learning_objectives: Optional[Iterable[str]] = None,
        visualizations: Optional[Iterable[VisualRepresentation]] = None,
    ) -> "KnowledgeUnit":
        topic_value = topic.strip() if topic and topic.strip() else "General"
        subtopic_value = subtopic.strip() if subtopic and subtopic.strip() else ""
        summary_value = summary.strip() if summary and summary.strip() else ""

        points = [point.strip() for point in key_points if point and point.strip()]
        objectives = (
            [objective.strip() for objective in learning_objectives if objective and objective.strip()]
            if learning_objectives
            else []
        )
        visuals = list(visualizations) if visualizations else []

        return KnowledgeUnit(
            id=uuid4(),
            topic=topic_value,
            subtopic=subtopic_value,
            source_slide_id=source_slide_id,
            summary=summary_value,
            key_points=points,
            learning_objectives=objectives,
            visualizations=visuals,
        )


class DomainNodeType(str, Enum):
    CONCEPT = "Concept"
    SKILL = "Skill"
    OBJECTIVE = "Objective"
    TOPIC = "Topic"
    SUBTOPIC = "Subtopic"


class DomainEdgeType(str, Enum):
    PREREQUISITE_OF = "PrerequisiteOf"
    PART_OF = "PartOf"
    RELATED_TO = "RelatedTo"
    CONTRASTS_WITH = "ContrastsWith"


@dataclass(frozen=True)
class DomainNode:
    id: UUID
    title: str
    description: str
    type: DomainNodeType
    blooms_level: str
    difficulty: float
    exam_relevance_weight: float
    tags: List[str]


@dataclass(frozen=True)
class DomainEdge:
    id: UUID
    from_node_id: UUID
    to_node_id: UUID
    type: DomainEdgeType
    strength: float = 1.0


class DomainKnowledgeGraph:
    def __init__(self) -> None:
        self._nodes: Dict[UUID, DomainNode] = {}
        self._edges: Dict[UUID, DomainEdge] = {}
        self._outgoing_edges: Dict[UUID, List[DomainEdge]] = {}
        self._incoming_edges: Dict[UUID, List[DomainEdge]] = {}

    @property
    def nodes(self) -> Dict[UUID, DomainNode]:
        return dict(self._nodes)

    @property
    def edges(self) -> Dict[UUID, DomainEdge]:
        return dict(self._edges)

    def add_node(self, node: DomainNode) -> None:
        if node is None:
            raise ValueError("node cannot be None")
        self._nodes[node.id] = node
        self._outgoing_edges.setdefault(node.id, [])
        self._incoming_edges.setdefault(node.id, [])

    def add_edge(self, edge: DomainEdge) -> None:
        if edge is None:
            raise ValueError("edge cannot be None")
        if edge.from_node_id not in self._nodes:
            raise ValueError(f"Source node {edge.from_node_id} does not exist.")
        if edge.to_node_id not in self._nodes:
            raise ValueError(f"Target node {edge.to_node_id} does not exist.")

        self._edges[edge.id] = edge
        self._outgoing_edges[edge.from_node_id].append(edge)
        self._incoming_edges[edge.to_node_id].append(edge)

    def get_node(self, node_id: UUID) -> Optional[DomainNode]:
        return self._nodes.get(node_id)

    def get_nodes_by_type(self, node_type: DomainNodeType) -> List[DomainNode]:
        return [node for node in self._nodes.values() if node.type == node_type]

    def get_prerequisites(self, node_id: UUID) -> List[DomainNode]:
        edges = self._outgoing_edges.get(node_id, [])
        prerequisites = [
            self.get_node(edge.to_node_id)
            for edge in edges
            if edge.type == DomainEdgeType.PREREQUISITE_OF
        ]
        return [node for node in prerequisites if node is not None]

    def get_related_nodes(self, node_id: UUID) -> List[DomainNode]:
        related: List[DomainNode] = []

        outgoing = self._outgoing_edges.get(node_id, [])
        related.extend(
            self.get_node(edge.to_node_id)
            for edge in outgoing
            if edge.type == DomainEdgeType.RELATED_TO
        )

        incoming = self._incoming_edges.get(node_id, [])
        related.extend(
            self.get_node(edge.from_node_id)
            for edge in incoming
            if edge.type == DomainEdgeType.RELATED_TO
        )

        unique = {node.id: node for node in related if node is not None}
        return list(unique.values())


DEFAULT_MASTERY_LEVELS = ["Advanced", "Proficient", "Developing", "Novice", "Unknown"]

_LOCK_TIMEOUT_SECONDS = 2.0
_LOCK_SLEEP_SECONDS = 0.05


def _acquire_file_lock(lock_path: Path, timeout: float = _LOCK_TIMEOUT_SECONDS) -> int:
    start = time.monotonic()
    while True:
        try:
            return os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_RDWR)
        except FileExistsError as exc:
            if time.monotonic() - start >= timeout:
                raise RuntimeError("Database locked") from exc
            time.sleep(_LOCK_SLEEP_SECONDS)


def _release_file_lock(lock_path: Path, fd: int) -> None:
    try:
        os.close(fd)
    finally:
        try:
            lock_path.unlink()
        except FileNotFoundError:
            pass


def write_json_atomic(target_path: Path, payload: Any, *, indent: int = 2) -> None:
    target_path.parent.mkdir(parents=True, exist_ok=True)
    lock_path = target_path.with_suffix(target_path.suffix + ".lock")
    temp_path = target_path.with_name(
        f".{target_path.name}.{os.getpid()}.{int(time.time() * 1000)}.tmp"
    )
    fd = _acquire_file_lock(lock_path)
    try:
        with temp_path.open("w", encoding="utf-8") as handle:
            json.dump(payload, handle, indent=indent)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temp_path, target_path)
    finally:
        if temp_path.exists():
            try:
                temp_path.unlink()
            except OSError:
                pass
        _release_file_lock(lock_path, fd)


@dataclass(frozen=True)
class GraphSummary:
    node_count: int
    edge_count: int
    node_types: Dict[str, int]
    mastery_levels: Dict[str, int]
    spaced_repetition: Dict[str, Optional[str]]
    source: str
    last_updated: Optional[str]
    recent_topics: List[Dict[str, str]]

    @classmethod
    def empty(cls) -> "GraphSummary":
        return cls(
            0,
            0,
            {},
            {level: 0 for level in DEFAULT_MASTERY_LEVELS},
            {"dueCount": 0, "nextReviewAt": None},
            "No graph data found",
            None,
            [],
        )

    @classmethod
    def from_payload(
        cls,
        payload: Optional[dict],
        source: str,
        last_updated: Optional[str],
        student_state: Optional[dict],
        recent_limit: int = 6,
    ) -> "GraphSummary":
        graph_payload = payload if isinstance(payload, dict) else {}
        nodes = _extract_items(graph_payload.get("Nodes") or graph_payload.get("nodes"))
        edges = _extract_items(graph_payload.get("Edges") or graph_payload.get("edges"))
        node_types = _build_node_type_counts(nodes)
        mastery_levels, recent_topics = _build_mastery_snapshot(student_state, nodes, recent_limit)
        spaced_repetition = _build_spaced_repetition_snapshot(student_state)
        return cls(
            len(nodes),
            len(edges),
            node_types,
            mastery_levels,
            spaced_repetition,
            source,
            last_updated,
            recent_topics,
        )

    def to_payload(self) -> dict:
        return {
            "nodeCount": self.node_count,
            "edgeCount": self.edge_count,
            "nodeTypes": self.node_types,
            "masteryLevels": self.mastery_levels,
            "spacedRepetition": self.spaced_repetition,
            "source": self.source,
            "lastUpdated": self.last_updated,
            "recentTopics": self.recent_topics,
        }


def _build_node_type_counts(nodes: List[dict]) -> Dict[str, int]:
    type_counts: Dict[str, int] = {}
    for node in nodes:
        node_type = _get_value(node, "Type", "type")
        if isinstance(node_type, dict):
            node_type = _get_value(node_type, "Value", "value")
        if node_type is None:
            node_type = DomainNodeType.CONCEPT.value
        type_name = str(node_type)
        type_counts[type_name] = type_counts.get(type_name, 0) + 1
    return type_counts


def _build_mastery_snapshot(
    student_state: Optional[dict],
    nodes: List[dict],
    recent_limit: int,
) -> tuple[Dict[str, int], List[Dict[str, str]]]:
    mastery_levels = {level: 0 for level in DEFAULT_MASTERY_LEVELS}
    if not isinstance(student_state, dict):
        return mastery_levels, []

    mastery_data = student_state.get("knowledgeMasteries") or student_state.get("KnowledgeMasteries")
    mastery_items = _extract_items(mastery_data)

    nodes_by_id: Dict[str, str] = {}
    for node in nodes:
        node_id = _get_value(node, "id", "Id")
        title = _get_value(node, "title", "Title") or "Untitled topic"
        if node_id:
            nodes_by_id[str(node_id)] = title

    recent_candidates: List[tuple[datetime, Dict[str, str]]] = []

    for mastery in mastery_items:
        level_value = _get_value(mastery, "level", "Level")
        level_name = _normalize_mastery_level(level_value)
        mastery_levels[level_name] = mastery_levels.get(level_name, 0) + 1

        last_assessed_value = _get_value(mastery, "lastAssessed", "LastAssessed")
        last_assessed = _parse_timestamp(last_assessed_value)
        if last_assessed is None:
            continue

        node_id = _get_value(mastery, "domainNodeId", "DomainNodeId", "nodeId", "NodeId")
        title = nodes_by_id.get(str(node_id)) if node_id else None
        if not title:
            title = "Untitled topic"

        recent_candidates.append(
            (
                last_assessed,
                {
                    "title": title,
                    "level": level_name,
                    "lastAssessed": _format_timestamp(last_assessed),
                },
            )
        )

    recent_candidates.sort(key=lambda item: item[0], reverse=True)
    recent_topics = [topic for _, topic in recent_candidates[:recent_limit]]
    return mastery_levels, recent_topics


def _build_spaced_repetition_snapshot(student_state: Optional[dict]) -> Dict[str, Optional[str]]:
    if not isinstance(student_state, dict):
        return {"dueCount": 0, "nextReviewAt": None}

    mastery_data = student_state.get("knowledgeMasteries") or student_state.get("KnowledgeMasteries")
    mastery_items = _extract_items(mastery_data)

    due_count = 0
    next_review: Optional[datetime] = None
    now = datetime.utcnow()

    for mastery in mastery_items:
        next_review_value = _get_value(mastery, "nextReviewAt", "NextReviewAt")
        review_time = _parse_timestamp(next_review_value)
        if review_time is None:
            continue
        if review_time <= now:
            due_count += 1
            continue
        if next_review is None or review_time < next_review:
            next_review = review_time

    return {
        "dueCount": due_count,
        "nextReviewAt": _format_timestamp(next_review) if next_review else None,
    }


def _extract_items(raw: Any) -> List[dict]:
    if raw is None:
        return []
    if isinstance(raw, dict):
        return [value for value in raw.values() if isinstance(value, dict)]
    if isinstance(raw, list):
        return [item for item in raw if isinstance(item, dict)]
    return []


def _get_value(source: dict, *keys: str):
    for key in keys:
        if key in source:
            return source[key]
    return None


def _normalize_mastery_level(level_value: Any) -> str:
    if isinstance(level_value, str):
        trimmed = level_value.strip()
        if trimmed.isdigit():
            level_value = int(trimmed)
        else:
            canonical = trimmed.capitalize()
            for level in DEFAULT_MASTERY_LEVELS:
                if level.lower() == trimmed.lower():
                    return level
            return canonical or "Unknown"

    if isinstance(level_value, int):
        mapping = {
            4: "Advanced",
            3: "Proficient",
            2: "Developing",
            1: "Novice",
            0: "Unknown",
        }
        return mapping.get(level_value, "Unknown")

    return "Unknown"


def _parse_timestamp(value: Any) -> Optional[datetime]:
    if value in (None, "", 0):
        return None
    if isinstance(value, (int, float)):
        try:
            return datetime.utcfromtimestamp(value)
        except (OSError, ValueError):
            return None
    if isinstance(value, str):
        cleaned = value.replace("Z", "+00:00")
        try:
            parsed = datetime.fromisoformat(cleaned)
            if parsed.year <= 1:
                return None
            if parsed.tzinfo is None:
                return parsed
            return parsed.astimezone(timezone.utc).replace(tzinfo=None)
        except ValueError:
            return None
    return None


def _format_timestamp(value: datetime) -> str:
    if value.tzinfo is None:
        return value.isoformat() + "Z"
    return value.astimezone().isoformat()

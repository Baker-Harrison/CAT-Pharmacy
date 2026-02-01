from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Dict, Iterable, List, Optional
from uuid import UUID, uuid4


@dataclass(frozen=True)
class KnowledgeUnit:
    id: UUID
    topic: str
    subtopic: str
    source_slide_id: str
    summary: str
    key_points: List[str]
    learning_objectives: List[str]

    @staticmethod
    def create(
        topic: str,
        subtopic: str,
        source_slide_id: str,
        summary: str,
        key_points: Iterable[str],
        learning_objectives: Optional[Iterable[str]] = None,
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

        return KnowledgeUnit(
            id=uuid4(),
            topic=topic_value,
            subtopic=subtopic_value,
            source_slide_id=source_slide_id,
            summary=summary_value,
            key_points=points,
            learning_objectives=objectives,
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

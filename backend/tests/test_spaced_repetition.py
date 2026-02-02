import unittest
from datetime import datetime, timedelta

from backend import models
from backend import session as session_module


class SpacedRepetitionTests(unittest.TestCase):
    def test_graph_summary_reports_due_and_next_review(self) -> None:
        now = datetime.utcnow()
        past = (now - timedelta(days=1)).isoformat() + "Z"
        future = (now + timedelta(days=2)).isoformat() + "Z"
        student_state = {
            "knowledgeMasteries": [
                {"nextReviewAt": past},
                {"nextReviewAt": future},
            ]
        }

        summary = models.GraphSummary.from_payload({"nodes": [], "edges": []}, "source", None, student_state)
        payload = summary.to_payload()

        spaced = payload["spacedRepetition"]
        self.assertEqual(spaced["dueCount"], 1)
        self.assertIsNotNone(spaced["nextReviewAt"])

        next_review = datetime.fromisoformat(spaced["nextReviewAt"].replace("Z", "+00:00"))
        future_dt = datetime.fromisoformat(future.replace("Z", "+00:00"))
        self.assertLess(abs((next_review - future_dt).total_seconds()), 2)

    def test_update_mastery_entry_schedules_next_review(self) -> None:
        entry = {
            "score": 0.4,
            "level": "Developing",
            "attempts": 2,
            "correct": 1,
            "intervalDays": 1.0,
            "lastAssessed": "2026-01-31T10:00:00Z",
        }
        assessed_at = "2026-02-02T12:00:00Z"
        updated = session_module._update_mastery_entry(entry, True, assessed_at)

        self.assertIn("intervalDays", updated)
        self.assertIn("nextReviewAt", updated)
        self.assertGreater(updated["intervalDays"], 0)

        next_review = datetime.fromisoformat(updated["nextReviewAt"].replace("Z", "+00:00"))
        assessed = datetime.fromisoformat(assessed_at.replace("Z", "+00:00"))
        self.assertGreater(next_review, assessed)

    def test_inject_due_reviews_restores_due_items(self) -> None:
        now = datetime.utcnow()
        past = (now - timedelta(days=1)).isoformat() + "Z"
        future = (now + timedelta(days=3)).isoformat() + "Z"

        units_by_id = {"unit-1": {"id": "unit-1"}, "unit-2": {"id": "unit-2"}}
        mastery = {
            "unit-1": {"nextReviewAt": past},
            "unit-2": {"nextReviewAt": future},
        }

        remaining = session_module._inject_due_reviews(units_by_id, ["unit-2"], mastery)
        self.assertIn("unit-1", remaining)
        self.assertIn("unit-2", remaining)


if __name__ == "__main__":
    unittest.main()

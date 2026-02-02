import unittest

from backend import session as session_module


class PredictivePlotTests(unittest.TestCase):
    def test_predictive_plot_builds_expected_horizon(self) -> None:
        ability = {"theta": -0.8, "standardError": 0.7}
        mastery = {
            "unit-1": {"score": 0.6},
            "unit-2": {"score": 0.2},
            "unit-3": {"score": 0.9},
        }
        unit_difficulties = {"unit-1": -0.2, "unit-2": 0.3}

        plot = session_module._build_predictive_plot(ability, mastery, unit_difficulties, horizon=5)

        self.assertEqual(plot["horizon"], 5)
        self.assertEqual(len(plot["points"]), 5)
        self.assertAlmostEqual(plot["baselineTheta"], -0.8, places=3)
        self.assertGreaterEqual(plot["probCorrect"], 0.15)
        self.assertLessEqual(plot["probCorrect"], 0.9)

        for point in plot["points"]:
            self.assertIn("expectedTheta", point)
            self.assertIn("lowerTheta", point)
            self.assertIn("upperTheta", point)
            self.assertLessEqual(point["lowerTheta"], point["expectedTheta"])
            self.assertGreaterEqual(point["upperTheta"], point["expectedTheta"])


if __name__ == "__main__":
    unittest.main()

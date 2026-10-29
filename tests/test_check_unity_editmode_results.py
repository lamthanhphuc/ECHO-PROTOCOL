import sys
import tempfile
import unittest
from pathlib import Path

# Add scripts directory to sys.path so check_unity_editmode_results can be imported
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))

from check_unity_editmode_results import verify_results


class NUnitGateTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.folder = Path(self.directory.name)

    def write_report(self, **changes):
        attributes = dict(
            total=2,
            passed=2,
            failed=0,
            skipped=0,
            inconclusive=0,
            result="Passed",
        )
        attributes.update(changes)
        content = "<test-run " + " ".join(
            f'{key}="{value}"'
            for key, value in attributes.items()
        ) + "/>"
        (self.folder / "editmode-results.xml").write_text(
            content, encoding="utf-8"
        )

    def test_pass(self):
        self.write_report()
        summary = verify_results(self.folder, "success")
        self.assertIn("2 passed", summary)
        self.assertIn("0 skipped", summary)
        self.assertIn("2 total", summary)

    def test_runner_outcome_not_success(self):
        self.write_report()
        with self.assertRaises(ValueError) as ctx:
            verify_results(self.folder, "failure")
        self.assertIn("outcome was failure", str(ctx.exception))

    def test_no_xml_found(self):
        with self.assertRaises(ValueError) as ctx:
            verify_results(self.folder, "success")
        self.assertIn("No NUnit XML result found", str(ctx.exception))

    def test_zero_tests(self):
        self.write_report(total=0, passed=0, failed=0, skipped=0, inconclusive=0)
        with self.assertRaises(ValueError) as ctx:
            verify_results(self.folder, "success")
        self.assertIn("Zero Unity EditMode tests discovered", str(ctx.exception))

    def test_failed_test(self):
        self.write_report(total=2, passed=1, failed=1, result="Failed")
        with self.assertRaises(ValueError) as ctx:
            verify_results(self.folder, "success")
        self.assertIn("Unity tests not passed", str(ctx.exception))

    def test_inconclusive_test(self):
        self.write_report(total=2, passed=1, inconclusive=1, result="Passed")
        with self.assertRaises(ValueError) as ctx:
            verify_results(self.folder, "success")
        self.assertIn("Unity tests not passed", str(ctx.exception))

    def test_inconsistent_counters(self):
        self.write_report(total=3, passed=2, failed=0, skipped=0, inconclusive=0)
        with self.assertRaises(ValueError) as ctx:
            verify_results(self.folder, "success")
        self.assertIn("Inconsistent NUnit counters", str(ctx.exception))

    def test_negative_counter(self):
        self.write_report(total=1, passed=2, failed=-1)
        with self.assertRaises(ValueError) as ctx:
            verify_results(self.folder, "success")
        self.assertIn("Negative NUnit counter", str(ctx.exception))


if __name__ == "__main__":
    unittest.main()

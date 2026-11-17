"""Negative checks for the CI report gate: false-green reports must be rejected."""
from pathlib import Path
import tempfile
import unittest
from verify import validate_report


class ReportGateTests(unittest.TestCase):
    def report(self, total=1, executed=1, passed=1, failed=0, skipped=0, outcome='Passed', result_count=1):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'tests.trx'
            results = ''.join(f'<UnitTestResult outcome="{outcome}" />' for _ in range(result_count))
            path.write_text(f'''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
<Results>{results}</Results><ResultSummary><Counters total="{total}" executed="{executed}"
passed="{passed}" failed="{failed}" notExecuted="{skipped}" /></ResultSummary></TestRun>''')
            return validate_report(path)

    def test_all_passed_is_accepted(self):
        self.assertEqual(1, self.report()['passed'])

    def test_zero_tests_is_rejected(self):
        with self.assertRaises(ValueError):
            self.report(total=0, executed=0, passed=0, result_count=0)

    def test_skipped_test_is_rejected(self):
        with self.assertRaises(ValueError):
            self.report(executed=0, passed=0, skipped=1, outcome='NotExecuted')

    def test_failed_assertion_is_rejected(self):
        with self.assertRaises(ValueError):
            self.report(passed=0, failed=1, outcome='Failed')

    def test_missing_results_is_rejected(self):
        with self.assertRaises(ValueError):
            self.report(result_count=0)

    def test_inconsistent_outcome_is_rejected(self):
        with self.assertRaises(ValueError):
            self.report(outcome='Failed')


if __name__ == '__main__':
    unittest.main()

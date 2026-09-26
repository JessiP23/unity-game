import contextlib
import io
from pathlib import Path
import tempfile
import unittest
from unity import check_results

class ResultValidationTests(unittest.TestCase):
    def check_xml(self, text):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'results.xml'
            path.write_text(text)
            with contextlib.redirect_stdout(io.StringIO()):
                check_results(path)

    def test_passed_suite(self):
        self.check_xml('<test-run><test-case result="Passed"/></test-run>')

    def test_failed_suite_rejected(self):
        with self.assertRaises(ValueError):
            self.check_xml('<test-run><test-case result="Failed"/></test-run>')

    def test_empty_suite_rejected(self):
        with self.assertRaises(ValueError):
            self.check_xml('<test-run/>')

    def test_wholly_skipped_suite_rejected(self):
        with self.assertRaises(ValueError):
            self.check_xml('<test-run><test-case result="Skipped"/></test-run>')

    def test_partial_failure_rejected(self):
        with self.assertRaises(ValueError):
            self.check_xml('<test-run><test-case result="Passed"/><test-case result="Failed"/></test-run>')

if __name__ == '__main__':
    unittest.main(verbosity=2)

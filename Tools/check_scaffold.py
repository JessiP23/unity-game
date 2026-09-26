"""Repository checks only; does not substitute for Unity compilation/tests."""
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]

class ScaffoldChecks(unittest.TestCase):
    def test_required_files(self):
        for path in ['Packages/manifest.json', 'ProjectSettings/ProjectVersion.txt',
                     'Assets/Editor/BootstrapSetup.cs', '.gitignore',
                     'Documentation/testing.md', 'Documentation/development.md']:
            self.assertTrue((ROOT / path).is_file(), path)

    def test_package_pins(self):
        dependencies = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']
        for version in dependencies.values():
            self.assertRegex(version, r'^\d+\.\d+\.\d+$')
        self.assertEqual(dependencies['com.unity.test-framework'], '1.4.6')

    def test_asset_metadata(self):
        guids = set()
        for path in (ROOT / 'Assets').rglob('*'):
            if path.suffix == '.meta':
                self.assertTrue(Path(str(path)[:-5]).exists(), str(path))
                continue
            meta = Path(str(path) + '.meta')
            self.assertTrue(meta.exists(), str(path))
            match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
            self.assertIsNotNone(match, str(meta))
            self.assertNotIn(match[1], guids)
            guids.add(match[1])

    def test_assembly_isolation(self):
        assemblies = [json.loads(p.read_text()) for p in (ROOT / 'Assets').rglob('*.asmdef')]
        self.assertEqual(len(assemblies), 3)
        names = {a['name'] for a in assemblies}
        names.update({'Unity.RenderPipelines.Universal.Runtime',
                      'Unity.RenderPipelines.Core.Runtime', 'Unity.RenderPipelines.Core.Editor'})
        for assembly in assemblies:
            for reference in assembly.get('references', []):
                self.assertIn(reference, names)
            if assembly['name'].endswith('Tests'):
                self.assertIn('TestAssemblies', assembly['optionalUnityReferences'])
            if assembly['name'].endswith('.Editor'):
                self.assertEqual(assembly['includePlatforms'], ['Editor'])

if __name__ == '__main__':
    unittest.main(verbosity=2)

#!/usr/bin/env python3
"""Run local Unity setup/tests/build with explicit failure reporting."""
import argparse
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
VERSION = (ROOT / 'ProjectSettings/ProjectVersion.txt').read_text().split(':', 1)[1].strip()

def editor_path():
    candidates = [os.environ.get('UNITY_EDITOR', ''),
                  str(ROOT / '.toolchain/Unity/Unity.app/Contents/MacOS/Unity'),
                  f'/Applications/Unity/Hub/Editor/{VERSION}/Unity.app/Contents/MacOS/Unity']
    return next((Path(p) for p in candidates if p and Path(p).is_file()), None)

def check_results(path):
    """Reject missing, empty, failed, or wholly skipped NUnit results."""
    root = ET.parse(path).getroot()
    tests = root.findall('.//test-case')
    if not tests or any(t.get('result') not in ('Passed', 'Skipped', 'Ignored') for t in tests):
        raise ValueError('Missing tests or failed Unity tests: ' + str(path))
    passed = sum(t.get('result') == 'Passed' for t in tests)
    if not passed:
        raise ValueError('No Unity tests passed: ' + str(path))
    print(f'{path.name}: {passed} passed, {len(tests) - passed} skipped')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['setup', 'edit', 'play', 'build', 'open'])
    args = parser.parse_args()
    editor = editor_path()
    if editor is None:
        parser.error('Unity Editor not found. Set UNITY_EDITOR to its executable.')
    if args.action == 'open':
        return subprocess.call([str(editor), '-projectPath', str(ROOT)], cwd=ROOT)
    output = ROOT / 'TestResults'
    output.mkdir(exist_ok=True)
    log = output / (args.action + '.log')
    command = [str(editor), '-batchmode', '-projectPath', str(ROOT), '-logFile', str(log)]
    if args.action in ('edit', 'play'):
        xml = output / (args.action + '.xml')
        if xml.exists():
            xml.unlink()  # A previous successful run must not mask a failed new run.
        command += ['-runTests', '-testPlatform', 'EditMode' if args.action == 'edit' else 'PlayMode',
                    '-testResults', str(xml)]
    else:
        method = 'Configure' if args.action == 'setup' else 'BuildWindows'
        command += ['-quit', '-executeMethod', 'NightSupermarket.Editor.ProjectSetup.' + method]
    print('Unity log:', log, flush=True)
    result = subprocess.run(command, cwd=ROOT)
    if result.returncode:
        print('Unity failed. Inspect ' + str(log), file=sys.stderr)
        return result.returncode
    if args.action in ('edit', 'play'):
        try:
            check_results(xml)
        except (OSError, ET.ParseError, ValueError) as error:
            print(error, file=sys.stderr)
            return 1
    return 0

if __name__ == '__main__':
    sys.exit(main())

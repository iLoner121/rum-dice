#!/usr/bin/env python3
"""Build and run deterministic tests. The same entry point runs locally and in CI."""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
RESULTS = ROOT / 'artifacts' / 'verification'
NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}


def validate_report(path):
    tree = ET.parse(path)
    counters = tree.find('t:ResultSummary/t:Counters', NS)
    results = tree.findall('t:Results/t:UnitTestResult', NS)
    if counters is None:
        raise ValueError('Missing test counters')
    counts = {key: int(counters.get(key, '0')) for key in ('total', 'executed', 'passed', 'failed', 'notExecuted')}
    if (counts['total'] <= 0 or counts['executed'] != counts['total']
            or counts['passed'] != counts['total'] or counts['failed'] != 0
            or counts['notExecuted'] != 0 or len(results) != counts['total']
            or any(r.get('outcome') != 'Passed' for r in results)):
        raise ValueError(f'Tests must all execute and pass: {counts}')
    return counts


def run(args, log_name):
    print('+ ' + ' '.join(args), flush=True)
    result = subprocess.run(args, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            text=True, encoding='utf-8', errors='replace', env={**os.environ, 'DOTNET_NOLOGO': '1'})
    (RESULTS / log_name).write_text(result.stdout, encoding='utf-8')
    print(result.stdout, end='', flush=True)
    if result.returncode:
        raise RuntimeError(f'{args[0]} failed with exit {result.returncode}; see {RESULTS / log_name}')
    return result.stdout


def main():
    # Never accept a stale TRX from an earlier successful run.
    if RESULTS.exists():
        shutil.rmtree(RESULTS)
    RESULTS.mkdir(parents=True)
    expected = json.loads((ROOT / 'global.json').read_text())['sdk']['version']
    actual = run(['dotnet', '--version'], 'sdk.log').strip()
    if actual != expected:
        raise RuntimeError(f'Expected SDK {expected}, found {actual}')
    run([sys.executable, '-m', 'unittest', 'discover', '-s', 'scripts', '-p', 'test_*.py', '-v'], 'gate-tests.log')
    run(['dotnet', 'restore', 'RumDice.sln', '--locked-mode', '--nologo'], 'restore.log')
    build = run(['dotnet', 'build', 'RumDice.sln', '-c', 'Release', '--no-restore', '--nologo', '-t:Rebuild'], 'build.log')
    run(['dotnet', 'test', 'tests/RumDice.Tests/RumDice.Tests.csproj', '-c', 'Release', '--no-build', '--no-restore',
         '--logger', 'trx;LogFileName=tests.trx', '--results-directory', str(RESULTS)], 'test.log')
    counts = validate_report(RESULTS / 'tests.trx')
    # De-duplicate warnings repeated in the MSBuild summary, preserving full text in build.log.
    warnings = sorted(set(line.strip() for line in build.splitlines() if re.search(r'\bwarning\s+[A-Z]+\d+\s*:', line)))
    summary = {'sdk': actual, 'tests': counts, 'distinct_warning_lines': len(warnings), 'warning_lines': warnings,
               'limits': ['Test host is net10.0; production remains net6.0.', 'No live platform calls or deployment validation.']}
    (RESULTS / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f"PASS: {counts['passed']} assertion tests; reports: {RESULTS}")


if __name__ == '__main__':
    try:
        main()
    except (OSError, RuntimeError, ValueError, ET.ParseError) as error:
        print(f'FAIL: {error}', file=sys.stderr)
        sys.exit(1)

"""Require the frozen original app-name multiset, for the probe or full suite.

The reference describes source98 inventory. Executed assembly/native identities
come from the fresh candidate build and must never be substituted with its old
producer hashes. This helper only reads files and writes a qualification record.
"""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

REFERENCE_SHA = '2c8d892a2f9e881b829c6f6e17df40d014879267252019cae1f1d7c3c3291974'
SOURCE = '98c73f45ac2b81518ce652a0f6612bd3ef83c5e1'
NS = '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
FOCUSED = {
    'SlateWindows.Tests.W1SidebarHardeningTests.FiveThousandItemRefreshReturnsWithinTheUiDispatchBudget',
    'SlateWindows.Tests.CitationAsyncInterleavingTests.ADeferredSummaryDoesNotAnswerForADifferentNote',
    'SlateWindows.Tests.CitationAsyncInterleavingTests.ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout',
}
ADVERSE = ('failed', 'error', 'timeout', 'aborted', 'inconclusive', 'passedButRunAborted',
           'notRunnable', 'notExecuted', 'disconnected', 'warning', 'completed', 'inProgress', 'pending')

def require(value, message):
    if not value:
        raise ValueError(message)

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def read_reference(path):
    path = Path(path)
    require(path.is_file() and not path.is_symlink() and sha(path) == REFERENCE_SHA,
            'Frozen reference bytes changed or are unavailable')
    value = json.loads(path.read_text(encoding='utf-8-sig'))
    expected = {'schemaVersion': 1, 'sourceRevision': SOURCE, 'producerRunId': 37138568771,
                'producerAttempt': 1, 'expectedTotal': 4806,
                'manifestSha256': '93a097242cb3dc0383c8db245411054189e9c7cc2571e88be8f7e420ad67b9dd',
                'nativeSha256': '8a0b43d62d760aabe6f37b216afb517b0f6c06b13ad047ac1e7a0be8f5ceb133',
                'originalTestAssemblySha256': '523ba3f79243456b40a796e1f946f15c1edca1abc3712330e5e0143b8ffd16b4',
                'filter': 'FullyQualifiedName!~ConnectionsLeafTests.TheModelOf'}
    require(all(value.get(key) == item for key, item in expected.items()), 'Wrong reference provenance')
    counts = value.get('expectedNameCounts')
    require(isinstance(counts, dict) and counts and all(isinstance(name, str) and name
            and type(count) is int and count > 0 for name, count in counts.items())
            and sum(counts.values()) == 4806 and len(counts) == 4789, 'Malformed reference multiset')
    return value

def expected_names(reference, scope):
    counts = reference['expectedNameCounts']
    require(scope in ('fast', 'full'), 'Unsupported reference scope')
    if scope == 'full':
        return collections.Counter(counts)
    selected = {name: count for name, count in counts.items()
                if name.startswith('SlateWindows.Tests.Censuses.') or name in FOCUSED}
    require(sum(selected.values()) == 693 and len(selected) == 685
            and all(selected.get(name) == 1 for name in FOCUSED), 'Frozen fast subset changed')
    return collections.Counter(selected)

def verify_trx(path, reference, scope):
    document = ET.parse(path).getroot()
    require(document.tag == NS + 'TestRun', 'Wrong TRX root or namespace')
    rows = list(document.iter(NS + 'UnitTestResult'))
    summaries = list(document.iter(NS + 'ResultSummary'))
    counters = list(document.iter(NS + 'Counters'))
    require(len(summaries) == len(counters) == 1
            and counters[0] in list(summaries[0]), 'Expected exactly one ResultSummary and its Counters')
    names = expected_names(reference, scope)
    count = sum(names.values())
    require(len(rows) == count, 'Missing, duplicate or incomplete result rows')
    require(collections.Counter(row.get('testName') for row in rows) == names, 'Reference name multiset changed')
    ids = [row.get('executionId') for row in rows]
    require(all(isinstance(value, str) and value and value == value.strip() for value in ids)
            and len({value.casefold() for value in ids}) == count, 'Missing or duplicate execution ID')
    counter = counters[0].attrib
    require(all(counter.get(key) == str(count) for key in ('total', 'executed', 'passed')), 'Pass/execution counters failed')
    for key in ADVERSE:
        require(counter.get(key) == '0', 'Missing/adverse TRX counter: ' + key)
    require(all(value == '0' for key, value in counter.items()
                if key not in ('total', 'executed', 'passed')), 'Unknown adverse TRX counter')
    require(all(row.get('outcome') == 'Passed' for row in rows), 'Failed, skipped or unclassified result row')
    require(summaries[0].get('outcome') == 'Completed', 'ResultSummary is not Completed')
    return {'status': 'passed', 'scope': scope, 'trxSha256': sha(path), 'counters': counter,
            'executionIdCount': count, 'uniqueNameCount': len(names), 'referenceNameMultisetMatches': True,
            'referenceSha256': REFERENCE_SHA, 'referenceOriginRevision': SOURCE,
            'referenceOriginTrxSha256': reference['referenceTrxSha256'],
            'qualification': 'Fresh candidate fixture qualification; reference inventory is historical source98, not old binary identity or provider comparison.'}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--trx', type=Path, required=True)
    parser.add_argument('--scope', choices=('fast', 'full'), required=True)
    parser.add_argument('--revision', required=True)
    parser.add_argument('--test-assembly', type=Path, required=True)
    parser.add_argument('--app-assembly', type=Path, required=True)
    parser.add_argument('--native', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--probe-record', type=Path)
    args = parser.parse_args()
    record = {'status': 'failed-or-incomplete', 'scope': args.scope,
              'executedSourceRevision': args.revision, 'referenceOriginRevision': SOURCE}
    try:
        require(re.fullmatch('[0-9a-f]{40}', args.revision), 'Candidate source revision is not exact')
        reference = read_reference(args.reference)
        identities = {'testAssemblySha256': sha(args.test_assembly), 'appAssemblySha256': sha(args.app_assembly),
                      'nativeSha256': sha(args.native)}
        record.update(identities)
        if args.scope == 'full':
            require(args.probe_record is not None, 'Full gate lacks preceding fast probe identity')
            probe = json.loads(args.probe_record.read_text(encoding='utf-8-sig'))
            require(probe.get('status') == 'passed' and probe.get('revision') == args.revision
                    and all(probe.get(key) == value for key, value in identities.items()),
                    'Full suite differs from its passed fast probe source/app/test/native identities')
        record.update(verify_trx(args.trx, reference, args.scope))
        record['executedSourceRevision'] = args.revision
    except (ValueError, OSError, ET.ParseError) as error:
        record.update({'status': 'failed-or-incomplete', 'error': str(error)})
        raise SystemExit(str(error))
    finally:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps(record, indent=2))

if __name__ == '__main__':
    main()

import io, subprocess, sys

ROOT = r'C:\dev\slate'
SURF = ROOT + r'\apps\slate-windows\src\SlateWindows\Canvas\CanvasSurfaceView.cs'
REG = ROOT + r'\apps\slate-windows\src\SlateWindows\Commands\SlateCommandRegistrar.cs'
CENSUS = ROOT + r'\apps\slate-windows\tests\SlateWindows.Tests\Censuses\ContractsCitationCensus.cs'

REBIND = """            if (wasTheAttachedPane || view.IsKeyboardFocusWithin)
            {
                model.Navigator.AttachPresenter(view);
            }
"""
SUBSCRIBE = "            _hostWindow.GotKeyboardFocus += OnHostFocusMoved;\n"
WITHDRAW = """        // The menu or overlay closed and the keys went ELSEWHERE. Routed
        // through the one classifier rather than withdrawing here, so the
        // mode stack hears the same thing the restoration does — it has
        // been holding a mode alive across a menu the reader has now
        // left, which is the same starvation one object over.
        Depart(CanvasFocusDeparture.PaneFocus);"""

MUTATIONS = [
    ('B1 rebind dropped', SURF, REBIND, '', 'ARetargetKeepsThePaneTheReaderIsWorkingIn'),
    ('B1 rebind on focus only', SURF,
     'if (wasTheAttachedPane || view.IsKeyboardFocusWithin)',
     'if (view.IsKeyboardFocusWithin && !view.IsKeyboardFocusWithin)',
     'ARetargetKeepsThePaneTheReaderIsWorkingIn'),
    ('M2 window watch dropped', SURF, SUBSCRIBE, '',
     'AHeldRestorationIsWithdrawnWhenTheReaderTurnsOutToHaveLeft'),
    ('M2 reclassify never withdraws', SURF, WITHDRAW, '        return;',
     'AHeldRestorationIsWithdrawnWhenTheReaderTurnsOutToHaveLeft'),
    ('M2 reclassify ignores the cause', SURF,
     '        if (ShellOverlayIsOpen() || FocusIsInAMenu(e.NewFocus))\n        {\n            return;\n        }\n',
     '', 'FullyQualifiedName~EachLevelOfTheRestorationHoldAnswersOnItsOwn'),
    ('Min4 staged claim reasserted', REG,
     '            // Canvas (W6-1 #745, contract A18). showVisual resolves to',
     '            // Canvas: disabled until PR B ships. showVisual resolves to',
     'NoStagedClaimOutlivesThePrItNames'),
    ('Min4 shipped set not derived', CENSUS,
     r'@"(?m)^## PR ([A-Z]) "', r'@"(?m)^## PR ([Z]) "',
     'NoStagedClaimOutlivesThePrItNames'),
]


def read(p):
    return io.open(p, encoding='utf-8', newline='').read()


def write(p, s):
    io.open(p, 'w', encoding='utf-8', newline='').write(s)


originals = {p: read(p) for p in {m[1] for m in MUTATIONS}}
results = []
for name, path, old, new, test in MUTATIONS:
    body = originals[path]
    if body.count(old) != 1:
        results.append((name, 'ANCHOR-MISS', 'appears %d times' % body.count(old)))
        print('%-34s %-16s %s' % results[-1], flush=True)
        continue
    write(path, body.replace(old, new))
    try:
        f = test if test.startswith('FullyQualifiedName') else 'FullyQualifiedName~' + test
        run = subprocess.run(
            ['dotnet', 'test',
             r'apps\slate-windows\tests\SlateWindows.Tests\SlateWindows.Tests.csproj',
             '-c', 'Release', '--filter', f],
            cwd=ROOT, capture_output=True, text=True, timeout=2400)
        o = run.stdout + run.stderr
        if 'error CS' in o:
            verdict = ('COMPILE WALL',
                       [l.strip() for l in o.splitlines() if 'error CS' in l][0][:90])
        elif 'Failed!' in o:
            verdict = ('CAUGHT',
                       [l for l in o.splitlines() if l.startswith('Failed!')][0]
                       .split(' - ')[1].strip()[:56])
        elif 'Passed!' in o:
            verdict = ('*** ESCAPED ***', 'green with the fix removed')
        else:
            verdict = ('UNKNOWN', o[-200:].replace('\n', ' '))
    finally:
        write(path, originals[path])
    results.append((name,) + verdict)
    print('%-34s %-16s %s' % results[-1], flush=True)

print('\n=== round 4 mutation battery ===')
for r in results:
    print('%-34s %-16s %s' % r)
bad = [r for r in results if 'ESCAPED' in r[1] or r[1] in ('UNKNOWN', 'ANCHOR-MISS')]
print('\nescaped: %d of %d' % (len(bad), len(results)))
sys.exit(1 if bad else 0)

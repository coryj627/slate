import io

p = r'C:\dev\slate\apps\slate-windows\tests\SlateWindows.Tests\Censuses\ContractsCitationCensus.cs'
s = io.open(p, encoding='utf-8', newline='').read()

old = '''    /// stays a word nobody retired; that class is caught by reading, and
    /// this table does not pretend otherwise.
    /// </para>'''
new = '''    /// stays a word nobody retired; that class is caught by reading, and
    /// this table does not pretend otherwise.
    /// </para>
    /// <para>
    /// A THIRD shape, found in codex round 5 and outside BOTH guards:
    /// C8 recorded that menu-then-elsewhere is not reclassified and
    /// deferred the repair to PR F — two waves after the window watcher
    /// had done it. The retired-vocabulary rows could not see it (no
    /// retired name), and the staged-claim rule could not either, because
    /// its question is "does this claim name a PR that has SHIPPED" and
    /// PR F has not. **A deferral to a future PR that the present already
    /// carried out is invisible to a shipped-set test by construction**,
    /// and no textual rule proposed so far would catch it. Named here so
    /// the next reviewer looks for it by hand rather than trusting the
    /// green.
    /// </para>'''
assert s.count(old) == 1, 'honesty'
io.open(p, 'w', encoding='utf-8', newline='').write(s.replace(old, new))
print('ok')

import io
p = r'C:\dev\slate\apps\slate-windows\src\SlateWindows\Canvas\CanvasModeController.cs'
s = io.open(p, encoding='utf-8', newline='').read()

def sub(old, new, count=1):
    global s
    assert s.count(old) == count, 'anchor x%d: %r' % (s.count(old), old[:70])
    s = s.replace(old, new)

# --- the ONE announce boundary -------------------------------------------
sub('''    /// <summary>
    /// Apply a departure held across a commit, if there is one. Drained''',
    '''    /// <summary>
    /// The ONE place this controller speaks (contract C7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A retired stack composes nothing. That was fixed once as a gate
    /// on `Commit`'s confirmation — the one site the failing test walked
    /// — and a per-verb gate is a list somebody has to keep complete:
    /// `Cancel` had the identical defect in a worse form, and any fifth
    /// site added later would have had it too. So the check lives where
    /// every sentence passes instead, which is the same move the
    /// document made for its requests and the ladder made for its rungs.
    /// </para>
    /// <para>
    /// EVALUATION ORDER is the whole reason this works, and it is worth
    /// stating because it looks like a detail. C# evaluates a call's
    /// ARGUMENTS before its body, so `Speak(new CanvasModeCancelled(…,
    /// spec.OnCancel()))` runs the cancel effect first and reads
    /// `_retired` afterwards. That is exactly right: the effect is
    /// arbitrary host code that can retire the document from inside
    /// itself, and a check at call ENTRY would have been made before the
    /// thing it needs to observe had happened. The boundary reads
    /// retirement at EMIT time.
    /// </para>
    /// <para>
    /// The announcer keeps its own `Debug.Fail` (A5). This is not a
    /// silent sink standing in front of it: it stops the SPEAKER from
    /// composing, and anything that still reaches the funnel after
    /// retirement is a real defect the guard should be loud about.
    /// </para>
    /// </remarks>
    private void Speak(CanvasA11yEvent @event)
    {
        if (_retired)
        {
            return;
        }
        _announce(@event);
    }

    /// <summary>
    /// Apply a departure held across a commit, if there is one. Drained''')

sub('            _announce(new CanvasA11yEvent.CanvasModeRejected(current.Mode));',
    '            Speak(new CanvasA11yEvent.CanvasModeRejected(current.Mode));')
sub('        _announce(new CanvasA11yEvent.CanvasModeEntered(spec.Mode, spec.Object));',
    '        Speak(new CanvasA11yEvent.CanvasModeEntered(spec.Mode, spec.Object));')
sub('''                // NOT on a retired stack. The effect can retire the
                // document from inside itself — the shell closes the tab
                // while a commit is running — and the confirmation is
                // composed after that. The announcer would DROP it, which
                // is why this looked correct for six rounds; but dropping
                // is A5's `Debug.Fail`, and a retired object composing a
                // sentence at all is the thing the guard is there to
                // catch. Terminality is the SPEAKER's question, not the
                // funnel's: `Shutdown` has already spoken the only line a
                // retirement owes.
                if (!_retired && result.Confirmation is { } confirmation)
                {
                    _announce(confirmation);''',
    '''                if (result.Confirmation is { } confirmation)
                {
                    // Through the boundary, like every other sentence:
                    // the effect can retire the document from inside
                    // itself — the shell closes the tab while a commit is
                    // running — and this is composed after that.
                    Speak(confirmation);''')
sub('        _announce(new CanvasA11yEvent.CanvasModeCancelled(spec.Mode, spec.OnCancel()));',
    '''        // `spec.OnCancel()` runs as this argument is built, so the
        // boundary inside `Speak` reads a retirement the RESTORATION
        // caused — which a check written here, before the call, could
        // not have seen.
        Speak(new CanvasA11yEvent.CanvasModeCancelled(spec.Mode, spec.OnCancel()));''')
io.open(p, 'w', encoding='utf-8', newline='').write(s)
print('ok')

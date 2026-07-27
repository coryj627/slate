// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using uniffi.slate_uniffi;

namespace SlateWindows.Reading;

/// <summary>
/// Run-kind activation (W3-1, `w3_inline_runs_spec.md` §10.3).
///
/// The three rules the §10.3 table encodes, each a real mac defect:
/// `Text` is a decision, not an absence — core stripped the affordance
/// and there is no destination left to re-examine here; a match is never
/// re-derived — <c>ReadingMatchLink</c> is the one ordered candidate-key
/// implementation; and `Resolved` styles while <c>ReadingMatchLink</c>
/// activates, neither derived from the other. Activation consumes the
/// run kind the builder stamped on the element — no URI parsing, no
/// string inspection, nothing for the §10.8 census to find.
/// </summary>
internal sealed class ReadingActivation
{
    private readonly WorkspaceTabViewModel _tab;
    private readonly Action<A11yEvent> _announce;
    private readonly Func<OutgoingLink[]> _records;
    private readonly Func<string, bool> _openExternal;

    public ReadingActivation(
        WorkspaceTabViewModel tab,
        Action<A11yEvent> announce,
        Func<OutgoingLink[]> records,
        Func<string, bool>? openExternalForTests = null)
    {
        _tab = tab;
        _announce = announce;
        _records = records;
        _openExternal = openExternalForTests ?? OpenWithShell;
    }

    public void Activate(ReadingInlineRunKind kind)
    {
        switch (kind)
        {
            case ReadingInlineRunKind.ExternalLink external:
                // Core's activation allowlist already decided this URL is
                // openable (anything else arrived as `Text`); the host's
                // whole job is handing it to the system opener.
                _announce(_openExternal(external.Url)
                    ? new A11yEvent.ExternalLinkOpened()
                    : new A11yEvent.ExternalLinkFailed(external.Url));
                break;

            case ReadingInlineRunKind.Wikilink wiki:
                ActivateRecord(
                    SlateUniffiMethods.ReadingMatchLink(
                        wiki.Target, wiki.Grammar, false, _records()),
                    wiki.BaseTarget);
                break;

            case ReadingInlineRunKind.Embed embed:
                // W3-1 opens the embed's source note; the in-place card
                // state machine is W3-5's.
                ActivateRecord(
                    SlateUniffiMethods.ReadingMatchLink(
                        embed.Key, ReadingWikiGrammar.Wikilink, true, _records()),
                    embed.Key);
                break;

            case ReadingInlineRunKind.Tag tag:
                _tab.ActivateTagFromReading(tag.Name);
                break;

            case ReadingInlineRunKind.Citation citation:
                // Interim until the citation popover slice: the speech
                // text is core's, posted verbatim. It is also already on
                // the run as HelpText, so activation and inspection agree.
                _announce(new A11yEvent.HostComposed(
                    citation.Speech, A11yPriority.Medium));
                break;
        }
    }

    private void ActivateRecord(uint? match, string spokenTarget)
    {
        OutgoingLink[] records = _records();
        if (match is not { } index
            || index >= records.Length
            || records[index] is not { IsUnresolved: false, TargetPath: { Length: > 0 } path })
        {
            _announce(new A11yEvent.LinkUnresolved(spokenTarget));
            return;
        }
        // New tab by default; the Editor-menu preference flips it to the
        // editor's in-place navigation (owner call 2026-07-25; gap G22 —
        // mac reading links stay current-tab with ⌘-click for new tab).
        _tab.NavigateFromReading(new EditorNavigationRequest(
            path,
            records[index].TargetAnchor,
            null,
            OpenInNewTab: _tab.EditorPreferences.OpenReadingLinksInNewTab));
    }

    private static bool OpenWithShell(string url)
    {
        try
        {
            using Process? process = Process.Start(
                new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception
                or InvalidOperationException
                or PlatformNotSupportedException)
        {
            return false;
        }
    }
}

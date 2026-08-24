// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W6-1 PR A (#745): the canvas document registry and the three surface
/// commands — the workspace half of the canvas surface, living under
/// <c>Canvas/</c> so the funnel census (contract A6) covers it, exactly
/// as mac keeps its <c>AppState+Canvas*.swift</c> extensions inside
/// <c>Sources/SlateMac/Canvas/</c> for the same reason.
///
/// One document per byte-exact vault-relative path (contract A1), the
/// W4-6 Bases registry pattern: <see cref="CanvasDocumentFor"/> is the
/// only construction site, the release sweep is the only shutdown site,
/// and a rename re-keys rather than mutating a document's path (CD-32).
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private readonly Dictionary<string, CanvasDocumentViewModel> _canvasDocuments =
        new(StringComparer.Ordinal);

    /// <summary>The active tab's canvas document, or null — every
    /// <c>slate.canvas.*</c> command gates on this (the Bases
    /// <c>ActiveBaseDocument</c> precedent). Keyed on the ATTACHED
    /// document, not the tab kind.</summary>
    internal CanvasDocumentViewModel? ActiveCanvasDocument =>
        ActiveGroup.ActiveTab?.Canvas;

    private const string CanvasKeyPrefix = "canvas:";

    private static string CanvasKey(string path) => CanvasKeyPrefix + path;

    /// <summary>The registry's 0→1 transition (contract A1): a miss
    /// constructs, installs the seams and loads — which is where the
    /// once-per-open degraded announcement lands (contract A4), so a
    /// second pane on the same path is a hit and hears nothing.</summary>
    internal CanvasDocumentViewModel CanvasDocumentFor(
        string path, CanvasSelection? seedSelection = null, string? retargetedFrom = null)
    {
        string key = CanvasKey(path);
        if (!_canvasDocuments.TryGetValue(key, out CanvasDocumentViewModel? document))
        {
            document = new CanvasDocumentViewModel(
                _session,
                path,
                new CanvasAnnouncer(_announceRendered),
                synchronousForTests: !_startInteractionBackgroundWork,
                retargetedFrom: retargetedFrom);
            if (seedSelection is not null)
            {
                document.Selection.SeedFrom(seedSelection);
            }
            _canvasDocuments[key] = document;
            InstallCanvasDocumentSeams(document);
            document.Load();
        }
        return document;
    }

    /// <summary>The document never opens tabs or launches the shell; it
    /// hands the workspace what it decided (contract A13), which owns
    /// the ONE navigation seam and the shared external-link policy's
    /// opener.</summary>
    private void InstallCanvasDocumentSeams(CanvasDocumentViewModel document)
    {
        document.OpenFileCardFromSurface = (path, anchor) =>
        {
            bool navigated = false;
            RunWorkspaceMutation(() =>
            {
                navigated = OpenPathCore(path, WorkspaceOpenTarget.CurrentTab);
                if (navigated && anchor is not null)
                {
                    WorkspaceGroupViewModel group = ActiveGroup;
                    WorkspaceTabViewModel? tab = group.ActiveTab;
                    _ = tab?.NavigateToAnchor(
                        anchor,
                        null,
                        _announce,
                        () => ReferenceEquals(ActiveGroup, group)
                            && ReferenceEquals(group.ActiveTab, tab));
                }
            });
            return navigated;
        };
        document.OpenExternalLinkFromSurface = target => _externalOpener(target);
        // The media hand-off (contract A13/CD-38): the document holds a
        // VAULT-RELATIVE target and the workspace owns the root, so the
        // absolute path is composed exactly here. Containment is checked
        // against the PHYSICAL identity and the whole thing fails
        // closed — see CanvasMediaPolicy.ResolveInsideVault.
        document.OpenMediaCardFromSurface = target =>
            CanvasMediaPolicy.ResolveInsideVault(_vaultRoot, target) is { } absolute
            && _externalOpener(absolute);
        // Contract A15: the persisted token follows the shared surface
        // for EVERY tab on this path, since they share the document.
        document.SurfaceChanged += (sender, surface) =>
        {
            string? token = surface switch
            {
                CanvasSurfaceKind.Table => "table",
                CanvasSurfaceKind.Visual => "visual",
                _ => null,
            };
            foreach (WorkspaceTabViewModel tab in Groups
                .SelectMany(group => group.Tabs)
                .Where(candidate => ReferenceEquals(candidate.Canvas, sender)))
            {
                tab.SetActiveCanvasSurface(token);
            }
            Persist();
        };
    }

    /// <summary>
    /// Force the open document at this path to re-read the file
    /// (W6-1 B2). The registry is keyed by path and a hit returns the
    /// document as it stands, so a disk change the shell itself made —
    /// a history restore is the reachable one today; PR E's funnel and
    /// the file watcher are next — leaves the surface contradicting the
    /// bytes. Selection survives wherever the node id still exists:
    /// <c>PublishReady</c> only re-seats when the selected node is gone.
    /// </summary>
    private void ReloadCanvasDocumentAt(string path)
    {
        if (_canvasDocuments.TryGetValue(
            CanvasKey(path), out CanvasDocumentViewModel? document))
        {
            document.Load();
        }
    }

    /// <summary>The Bases sweep, verbatim in shape (contract A1): the
    /// live key set comes from the open tabs, so no counter can
    /// disagree with what the user can see. A retired document closes
    /// its handle and takes its selection and marks with it.</summary>
    private void ReleaseUnreferencedCanvasDocuments()
    {
        if (_canvasDocuments.Count == 0)
        {
            return;
        }
        var live = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (tab.IsCanvas)
            {
                _ = live.Add(CanvasKey(tab.Path));
            }
        }
        foreach (string key in _canvasDocuments.Keys
            .Where(candidate => !live.Contains(candidate))
            .ToList())
        {
            CanvasDocumentViewModel retired = _canvasDocuments[key];
            retired.Shutdown();
            TrackRetiredBasesWork(retired.WhenHandleClosed());
            _ = _canvasDocuments.Remove(key);
        }
    }

    /// <summary>
    /// Rename/move (CD-32): the registry keys by path and a document's
    /// path is immutable, so a rename retires the old document and
    /// attaches a fresh one at the new spelling — the Bases
    /// <c>RetargetBaseDocuments</c> shape, whose round-2 blocker was
    /// exactly the alternative (a renamed tab keeping a document that
    /// reopens the OLD path forever). The previous selection and marks
    /// ride across: a rename is not a close.
    /// </summary>
    private void RetargetCanvasDocuments(string source, string destination)
    {
        foreach (string oldKey in _canvasDocuments.Keys
            .Where(key => TryRetargetPath(
                key[CanvasKeyPrefix.Length..], source, destination, out _))
            .ToList())
        {
            CanvasDocumentViewModel oldDocument = _canvasDocuments[oldKey];
            _ = _canvasDocuments.Remove(oldKey);
            oldDocument.Shutdown();
            TrackRetiredBasesWork(oldDocument.WhenHandleClosed());
            _ = TryRetargetPath(
                oldDocument.Path, source, destination, out string newPath);
            foreach (WorkspaceTabViewModel tab in Groups
                .SelectMany(group => group.Tabs)
                .Where(candidate => candidate.IsCanvas
                    && string.Equals(candidate.Path, newPath, StringComparison.Ordinal)))
            {
                tab.AttachCanvasDocument(CanvasDocumentFor(
                    newPath,
                    seedSelection: oldDocument.Selection,
                    retargetedFrom: oldDocument.Path));
            }
        }
        // A rename lands NEW BYTES at the DESTINATION, and a document
        // open there is now stale (W6-1 B3). Two shapes reach this:
        // an atomic save (write `x.tmp`, rename it onto the open
        // `board.canvas` — the source was never open, so the loop above
        // did nothing at all), and both-open, where the loop re-keyed
        // the source's tabs onto the destination's existing document
        // without re-reading it. Same answer for both, and it must come
        // AFTER the re-key so the surviving document is the one that
        // reloads.
        ReloadCanvasDocumentAt(destination);
    }

    /// <summary>Vault close: every document holds the shared session and
    /// a native handle. Shut down and drained with the Bases documents
    /// in the one bounded teardown drain (contract A1/A17).</summary>
    private void ShutdownCanvasDocuments(List<Task> drains)
    {
        foreach (CanvasDocumentViewModel document in _canvasDocuments.Values)
        {
            document.Shutdown();
            drains.Add(document.WhenHandleClosed());
        }
        _canvasDocuments.Clear();
    }

    // --- Surface commands (contract A18) --------------------------------

    private RelayCommand? _canvasShowOutlineCommand;
    private RelayCommand? _canvasShowTableCommand;
    private RelayCommand? _canvasShowVisualCommand;

    public System.Windows.Input.ICommand CanvasShowOutlineCommand =>
        _canvasShowOutlineCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.ShowSurface(CanvasSurfaceKind.Outline),
            _ => ActiveCanvasDocument is not null);

    /// <summary>Registered now, disabled until PR B ships the
    /// projection (contract A18): the palette lists it and answers with
    /// the registrar's canonical unavailable sentence, because a
    /// registered row carries no reason of its own and "ships in PR B"
    /// is not copy a user should hear.</summary>
    public System.Windows.Input.ICommand CanvasShowTableCommand =>
        _canvasShowTableCommand ??= new RelayCommand(_ => { }, _ => false);

    /// <summary>Registered now, disabled until PR D ships the
    /// projection (contract A18).</summary>
    public System.Windows.Input.ICommand CanvasShowVisualCommand =>
        _canvasShowVisualCommand ??= new RelayCommand(_ => { }, _ => false);
}

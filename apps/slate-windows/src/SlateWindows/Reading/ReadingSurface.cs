// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfList = System.Windows.Documents.List;

namespace SlateWindows.Reading;

/// <summary>
/// The reading view's document host (W3-1, #728): a read-only
/// <see cref="RichTextBox"/>.
///
/// The container spike measured the alternatives out:
/// <c>FlowDocumentScrollViewer</c> has no keyboard caret (arrows
/// re-announce one line forever), and an <c>ItemsControl</c> of blocks
/// exposes no Text pattern at all. The read-only RichTextBox keeps the
/// document Text pattern, adds a real caret and native table semantics,
/// and supplies <c>Hyperlink</c> peers natively — which is why the
/// structural peer walk below adds headings and lists but never links.
///
/// AutomationId <c>ReadingSurface</c> is the identity contract
/// (`w3_spec` §W3-1): externally-dependable API that the W-E7 AT-idiom
/// layers key on. Renaming it is a breaking change to shipped
/// assistive-technology configuration, not a refactor.
/// </summary>
internal sealed class ReadingSurface : RichTextBox
{
    public ReadingSurface()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = true;
        // A read-only viewer records no undo — and it MUST not: undo
        // preservation XamlWriter-serializes removed content, and the
        // first post-click merge died on the task checkbox's stamped
        // payload ("Cannot serialize a generic type" — field root
        // cause, 2026-07-26: every toggle became "could not load" and
        // the caret went dead).
        IsUndoEnabled = false;
        // Keeps hyperlinks live in a read-only document.
        IsDocumentEnabled = true;
        AcceptsReturn = false;
        AcceptsTab = false;
        // Tab walks the document's focusable children — links, Jump
        // links, math/diagram elements, checkboxes — then continues
        // out (field, 2026-07-30: without an explicit mode, Tab from
        // the caret exited straight to the pane thumb and the
        // embedded objects were unreachable by keyboard focus).
        System.Windows.Input.KeyboardNavigation.SetTabNavigation(
            this, System.Windows.Input.KeyboardNavigationMode.Continue);
        BorderThickness = new Thickness(0);
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        // The reading surface renders on the surface token per §10.4 —
        // the APCA pairs (#1050) are gated against it.
        this.SetResourceReference(BackgroundProperty, "Slate.SurfaceBrush");
        this.SetResourceReference(ForegroundProperty, "Slate.TextBrush");
        // #1755: a RichTextBox draws its selection on top of the text, so the
        // opaque text box pair would paint over the words. The theme's overlay
        // carries its own opacity, chosen so the text keeps its contrast.
        this.SetResourceReference(SelectionBrushProperty, TextBoxSelection.ReadingBrushKey);
        SelectionOpacity = 1;
        SpellCheck.IsEnabled = false;

        AutomationProperties.SetAutomationId(this, "ReadingSurface");
        AutomationProperties.SetName(this, "Reading view");
        AutomationProperties.SetHelpText(this, Commands.NavigationHelp.Reading);
        DataObject.AddCopyingHandler(this, OnCopying);
        DataObject.AddSettingDataHandler(this, OnSettingData);
        // #1708 (the merge judge's M1): the end of a press, however WPF's
        // own selection handling marks its events.
        AddHandler(
            System.Windows.Input.Mouse.MouseUpEvent,
            new System.Windows.Input.MouseButtonEventHandler((_, _) => ReleasePointer()),
            handledEventsToo: true);
        AddHandler(
            System.Windows.Input.Mouse.LostMouseCaptureEvent,
            new System.Windows.Input.MouseEventHandler((_, _) => ReleasePointer()),
            handledEventsToo: true);

        // ONE document for the surface's whole life. Replacing the
        // Document property on a live RichTextBox breaks the UIA
        // binding: the automation peer stays wired to the original text
        // container, so a screen reader sees "blank" forever and caret
        // moves go silent while the visual caret works — the 2026-07-27
        // manual-pass finding. Content arrives by MERGING blocks into
        // this document instead (ApplyBuiltDocument).
        Document = new FlowDocument
        {
            PagePadding = new Thickness(PagePaddingFloor),
            ColumnWidth = double.PositiveInfinity,
        };
        Document.SetResourceReference(
            FlowDocument.FontFamilyProperty, "Slate.ReadingFontFamily");
        ReadingDocumentBuilder.ApplyBodyType(Document);
        SetResourceReference(BodySizeProperty, TypeRamp.ReaderBody.SizeKey);
        SetResourceReference(BodyFamilyProperty, "Slate.ReadingFontFamily");
        SizeChanged += (_, _) => FitMeasure();

        // Activation: clicks and Enter on links/cards route by the run
        // kind the builder STAMPED on the element — never by parsing
        // anything back out of a URI (§10.3/§10.8). Handled at the
        // surface so the builder stays a pure projector.
        AddHandler(
            System.Windows.Documents.Hyperlink.ClickEvent,
            new System.Windows.RoutedEventHandler((_, args) =>
            {
                // Copy-code links FIRST (field, 2026-07-30: the Copy
                // affordance became a hyperlink for the same caret-
                // stream reason as the embed Jump link); the marker
                // tells them apart from every navigation link.
                if (args.Source is System.Windows.Documents.Hyperlink
                    {
                        Tag: string copySource
                    } copyLink
                    && ReadingSemantics.IsCodeCopy(copyLink)
                    && _model is { } copyLinkModel)
                {
                    copyLinkModel.CopyCode(copySource);
                    args.Handled = true;
                    return;
                }
                // Embed Jump links whose target core ALREADY RESOLVED
                // navigate directly (the W3-5 contract) — the run-kind
                // fallback below covers degraded cards only.
                if (args.Source is System.Windows.Documents.Hyperlink jumpLink
                    && ReadingSemantics.TryGetEmbedJump(
                        jumpLink, out string jumpPath, out var jumpAnchor)
                    && _model is { } jumpModel)
                {
                    jumpModel.ActivateResolvedEmbedSource(jumpPath, jumpAnchor);
                    args.Handled = true;
                    return;
                }
                if (args.Source is System.Windows.Documents.Hyperlink
                    {
                        Tag: uniffi.slate_uniffi.ReadingInlineRunKind kind
                    }
                    && _model is { } model)
                {
                    model.Activate(kind);
                    args.Handled = true;
                }
            }));
        AddHandler(
            System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
            new System.Windows.RoutedEventHandler((_, args) =>
            {
                // Copy buttons FIRST: both they and embed cards are
                // Buttons with string Tags, and only the marker tells
                // them apart (W3-4).
                if (args.Source is System.Windows.Controls.Button
                    {
                        Tag: string codeSource
                    } copyButton
                    && ReadingSemantics.IsCodeCopy(copyButton)
                    && _model is { } copyModel)
                {
                    copyModel.CopyCode(codeSource);
                    args.Handled = true;
                    return;
                }
                // Jump buttons whose target core ALREADY RESOLVED
                // navigate directly (W3-5 round 1: nested targets are
                // absent from the host's record snapshot, so a
                // re-match would dead-end on content the card shows).
                if (args.Source is System.Windows.Controls.Button jumpButton
                    && ReadingSemantics.TryGetEmbedJump(
                        jumpButton, out string jumpPath, out var jumpAnchor)
                    && _model is { } jumpModel)
                {
                    jumpModel.ActivateResolvedEmbedSource(jumpPath, jumpAnchor);
                    args.Handled = true;
                    return;
                }
                if (args.Source is System.Windows.Controls.Button
                    {
                        Tag: string embedKey
                    }
                    && _model is { } model)
                {
                    model.Activate(
                        new uniffi.slate_uniffi.ReadingInlineRunKind.Embed(embedKey));
                    args.Handled = true;
                    return;
                }

                // Task checkbox (mouse, Space, or a UIA TogglePattern
                // call — all raise Click). The checkbox never holds
                // state: revert WPF's optimistic flip and route the
                // stamped source range through the core task command;
                // the re-projection renders the real outcome, and the
                // canonical announcements ("Task completed." /
                // conflict / unsaved refusal) narrate it.
                if (args.Source is System.Windows.Controls.CheckBox box
                    && ReadingSemantics.TryDecodeTaskRange(
                        box.Tag, out ulong taskStart, out ulong taskEnd)
                    && _model is { } taskModel)
                {
                    box.IsChecked = box.IsChecked != true;
                    taskModel.ToggleTaskAt(taskStart, taskEnd);
                    // The click focused the checkbox — an element the
                    // re-projection is about to destroy. Return focus
                    // to the document so the reader stays in caret
                    // context and WPF never recovers focus from a
                    // removed element.
                    _ = Focus();
                    args.Handled = true;
                }
            }));

        // W7-7 PR 8 (R-10): the surface takes focus only through the
        // landing the shell asks for (RequestFocusLanding) — the mode toggle
        // and every tab switch ask. It used to focus itself whenever it was
        // shown over merged content, which also pulled focus off a tab header
        // the reader had clicked or arrowed to. Hidden (the tab left reading
        // mode, another view took the cell, the surface unloaded), a held
        // landing is withdrawn so a later merge seats nobody — one a rescan
        // reload carries too (WB-9, #1312): a hidden surface lets go of
        // everything it holds.
        // (IsVisibleChanged is an event, not a virtual, on UIElement.)
        IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is false)
            {
                WithdrawFocusLanding();
                _carried = null;
            }
        };
    }

    /// <summary>
    /// The view-model attach point: XAML binds <c>Model</c>
    /// (RichTextBox.Document is a plain CLR property, so it cannot be
    /// the binding target itself). The surface subscribes for document
    /// swaps, keeps its navigator's landmark index current, and owns
    /// the navigator lifetime.
    /// </summary>
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model), typeof(object), typeof(ReadingSurface),
            new PropertyMetadata(null, OnModelChanged));

    private ReadingNavigator? _navigator;
    private ReadingContentViewModel? _model;

    public object? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private static void OnModelChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var surface = (ReadingSurface)d;
        // A held landing (R-10) was asked of the OUTGOING projection; the
        // incoming one's route asks again for itself. A landing a rescan
        // reload of the tab carries (WB-9, #1312) is not pending here: the
        // reload's end decides it.
        surface.WithdrawFocusLanding();
        if (surface._model is { } previous)
        {
            // Remember where the reader WAS (field, 2026-07-30: the
            // unconditional caret park sent a returning reader back
            // to the top of the note). Captured on the outgoing model
            // so the same-projection fast path below can restore it.
            previous.SavedCaretOffsetForRebind =
                surface.Document.ContentStart.GetOffsetToPosition(
                    surface.CaretPosition);
            previous.PropertyChanged -= surface.Model_PropertyChanged;
            previous.BlocksAppended -= surface.Model_BlocksAppended;
            previous.TornDown -= surface.Model_TornDown;
            previous.DiagramsCompleted -= surface.Model_DiagramsCompleted;
            previous.Tab.ReadingRescanReloadBegan -= surface.OnReadingRescanReloadBegan;
            // Kill the outgoing model's stream BEFORE anything else:
            // its chunk continuations hold list objects that live in
            // THIS surface's document and would keep growing them
            // under whatever binds next.
            previous.OnSurfaceDetached();
        }
        surface._model = e.NewValue as ReadingContentViewModel;
        if (surface._model is { } model)
        {
            surface._navigator ??= new ReadingNavigator(surface, model.Announce);
            model.PropertyChanged += surface.Model_PropertyChanged;
            model.BlocksAppended += surface.Model_BlocksAppended;
            model.TornDown += surface.Model_TornDown;
            model.DiagramsCompleted += surface.Model_DiagramsCompleted;
            model.Tab.ReadingRescanReloadBegan += surface.OnReadingRescanReloadBegan;
            if (ReferenceEquals(surface._lastMerged, model.Document)
                && model.Document is not null
                && model.ProjectionComplete)
            {
                // The persistent document already shows this exact,
                // COMPLETE projection (reading tab → editor tab →
                // back): free. Completeness matters — a stream this
                // surface's own detach canceled leaves _lastMerged
                // pointing at a torso that must re-project instead.
                // Restore the reader's caret (field, 2026-07-30: the
                // old unconditional park sent them to the top), and
                // re-fetch if a dependency moved while hidden (same
                // field pass: a target saved in another tab stayed
                // stale forever — the current content remains visible
                // while the digest decides whether anything changed).
                surface.RestoreCaret(
                    surface.Document.ContentStart.GetPositionAtOffset(
                        Math.Max(0, model.SavedCaretOffsetForRebind))
                    ?? surface.Document.ContentEnd);
                if (model.HasPendingDependencyRefresh)
                {
                    model.RefreshForDependencies();
                }
                // WB-20: the detach retired this projection's diagram
                // stage; nothing re-projects here, so it resumes.
                model.ResumePendingDiagrams();
                surface.ModelChanged?.Invoke(surface, EventArgs.Empty);
                return;
            }
            // A model swap is a different projection (navigation, tab
            // switch): the merge's caret preservation is for SAME-note
            // re-projections only, so park the caret before applying
            // (ClearForModelSwitch parks again with the placeholder).
            surface.CaretPosition = surface.Document.ContentStart;
            // Any other rebind is a DIFFERENT note (or an untrusted
            // projection): the outgoing content leaves the tree NOW —
            // waiting for the incoming publish would let readers and
            // AT read the previous note under the new tab's identity.
            // An accessible placeholder stands in until the projection
            // (or its failure notice) arrives through PropertyChanged.
            surface.ClearForModelSwitch();
            model.EnsureProjected();
        }
        surface.ModelChanged?.Invoke(surface, EventArgs.Empty);
    }

    private void ClearForModelSwitch()
    {
        // The clear takes whatever in the document holds the keys — a link,
        // a task's check box — and a link taken out of its document raises no
        // re-evaluation: the keys would rest on it, out of every document,
        // until the incoming note's projection reclaimed them. Reclaim them
        // for the document first, as ApplyBuiltDocument does (W7-7 PR 4b
        // round 3, R-5 (h)).
        if (IsKeyboardFocusWithin && !IsKeyboardFocused)
        {
            _ = Focus();
        }

        // A formula's place is for this note's next projection, not the next note's.
        _formulaScrolls.Clear();
        Document.Blocks.Clear();
        var placeholder = new Paragraph(new Run("Loading reading view…"))
        {
            FontStyle = System.Windows.FontStyles.Italic,
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(
            placeholder, "ReadingLoadingNotice");
        Document.Blocks.Add(placeholder);
        // Park the caret at the placeholder's start: a leftover offset
        // would be "preserved" into the incoming note by the next
        // merge, skipping its focus-on-content path.
        CaretPosition = Document.ContentStart;
        _landmarks = Array.Empty<ReadingLandmark>();
        _navigator?.SetLandmarks(_landmarks);
        _lastMerged = null;
        // WB-11 (#1329): the cached children would otherwise serve the previous
        // note's lists and elements under the new tab until the incoming merge.
        InvalidateStructuralPeers();
        ShowsSearchableContent = false;
        RaiseContentChanged(ReadingContentChange.Replaced);
    }

    private void Model_PropertyChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReadingContentViewModel.Document))
        {
            ApplyModel();
        }
        else if (e.PropertyName is nameof(ReadingContentViewModel.RefreshInFlight)
            && _model is { RefreshInFlight: false })
        {
            // Every formula has arrived, so a place left over has lost its formula.
            _formulaScrolls.Clear();
            if (_focusLandingPending)
            {
                ScheduleSettledLanding();
            }
        }
    }

    /// <summary>
    /// Streamed chunk arrival (§W3-1 item 9): append the fragment's
    /// blocks to the persistent document and refresh the landmark
    /// index so chords cover everything rendered so far. The caret is
    /// untouched — appends only ever grow the tail.
    /// </summary>
    private void Model_BlocksAppended(FlowDocument fragment)
    {
        ResumeFormulaScrolls(fragment, _landmarks);
        while (fragment.Blocks.FirstBlock is { } block)
        {
            fragment.Blocks.Remove(block);
            Document.Blocks.Add(block);
        }
        _landmarks = ReadingDocumentBuilder.CollectLandmarks(Document);
        _navigator?.SetLandmarks(_landmarks);
        // A chunk can append items to a list opened in an earlier one.
        InvalidateStructuralPeers();
        RaiseContentChanged(ReadingContentChange.Appended);
    }

    private FlowDocument? _lastMerged;

    /// <summary>DS-3: where formulas had scrolled to when a merge replaced them. A long
    /// note's later formulas arrive after the merge, so their places wait here until the
    /// refresh settles or the note changes.</summary>
    private readonly Dictionary<(string Source, int Nth), double> _formulaScrolls = [];
    private IReadOnlyList<ReadingLandmark> _landmarks = Array.Empty<ReadingLandmark>();
    private bool _focusLandingPending;
    private object? _focusLandingToken;
    private Action? _focusLandingAnnouncement;
    private Action? _focusLandingRefusal;
    // WB-9 (#1312): the held landing while a rescan reload of its tab runs —
    // moved off the pending fields, so the reload's teardown and rebind
    // cannot end it, and still the window slot's under its original token.
    private CarriedLanding? _carried;

    /// <summary>A held landing on a rescan reload of <paramref name="Tab"/>,
    /// asked of the projection <paramref name="From"/>. The park is not
    /// carried: it ran when the landing was asked, and a second run would be
    /// a second focus move.</summary>
    private sealed record CarriedLanding(
        object Token,
        Action? Announce,
        Action? Refuse,
        WorkspaceTabViewModel Tab,
        ReadingContentViewModel From);

    internal IReadOnlyList<ReadingLandmark> LandmarksForTests => _landmarks;

    private void ApplyModel()
    {
        if (_model is not { } model)
        {
            return;
        }
        if (model.Document is { } built && !ReferenceEquals(_lastMerged, built))
        {
            ApplyBuiltDocument(built);
            _lastMerged = built;
            // No landing here: the apply path never claims focus. A landing
            // held for this projection is delivered by the settle of the
            // refresh that published it (ScheduleSettledLanding).
        }
    }

    /// <summary>WB-20 (#1317) test seam: invoked at the top of every
    /// <see cref="ApplyBuiltDocument"/> — the pin that a diagram's landing
    /// completes in place instead of re-applying the document.</summary>
    internal static Action? ApplyProbeForTests;

    private void Model_DiagramsCompleted(
        FlowDocument document, IReadOnlyList<(string Key, DiagramOutcome Outcome)> outcomes)
    {
        // Only the surface showing that projection completes it; any other
        // ignores it, and the model's next build uses what landed.
        if (ReferenceEquals(_lastMerged, document))
        {
            CompleteDiagrams(outcomes);
        }
    }

    /// <summary>Where a text pointer sat relative to a pending diagram
    /// paragraph, recorded before its completion edits it.</summary>
    private readonly record struct DiagramAnchor(DiagramAnchorKind Kind, int Offset, TextPointer? Pointer);

    private enum DiagramAnchorKind
    {
        /// <summary>Outside the paragraph: left alone — the edit is confined
        /// to it and WPF's pointers are live, so they stay on their
        /// character.</summary>
        Outside,
        InSource,

        /// <summary>At or before the placeholder's container: on its line,
        /// the pending name's run included (#1708).</summary>
        AtContainerStart,

        /// <summary>After the placeholder's container and before the
        /// source.</summary>
        AtContainerEnd,
    }

    /// <summary>The shape a completion leaves a pending diagram paragraph
    /// in.</summary>
    private enum DiagramShape
    {
        Rendered,
        Failed,

        /// <summary>No artifact: the source, and the placeholder only while
        /// the reader is on it.</summary>
        Source,
    }

    /// <summary>
    /// Completes this surface's pending diagram paragraphs in place (WB-20,
    /// #1317). Each paragraph's placeholder element and its container are
    /// kept — the reader may be on it, hold the caret beside it or anchor a
    /// selection at it — and the paragraph's text is edited around them in
    /// one change. The caret and the selection are remapped onto the same
    /// characters. A late diagram says nothing (WD11): the element's name
    /// change is a property change, not speech.
    /// </summary>
    internal void CompleteDiagrams(IReadOnlyList<(string Key, DiagramOutcome Outcome)> outcomes)
    {
        bool removedContainer = false;
        bool collapsed = Selection.IsEmpty;
        bool backward = !collapsed && CaretPosition.CompareTo(Selection.Start) == 0;
        BeginChange();
        try
        {
            foreach ((string key, DiagramOutcome outcome) in outcomes)
            {
                Paragraph? paragraph = Document.Blocks.OfType<Paragraph>().FirstOrDefault(
                    candidate => string.Equals(
                        ReadingSemantics.PendingDiagramKeyOf(candidate), key, StringComparison.Ordinal));
                // #1708 (CB-20; the owner's answer to Call 1, "Before the
                // source"): the pending shape is the name's hidden run, the
                // placeholder's container, a line break and the source run.
                if (paragraph?.Inlines.OfType<InlineUIContainer>().FirstOrDefault(
                        candidate => candidate.Child is ReadingDiagramElement) is not
                        {
                            Child: ReadingDiagramElement element,
                        } container)
                {
                    continue;
                }
                Run? source = paragraph.Inlines.OfType<Run>().FirstOrDefault(
                    run => !ReadingSemantics.IsHiddenSpeech(run));
                string sourceText = source?.Text ?? string.Empty;

                DiagramAnchor caret = RecordDiagramAnchor(CaretPosition, paragraph, source, container);
                DiagramAnchor start = RecordDiagramAnchor(Selection.Start, paragraph, source, container);
                DiagramAnchor end = RecordDiagramAnchor(Selection.End, paragraph, source, container);

                Run? remappedSource = null;
                bool keptContainer = true;
                DiagramShape shape = DiagramShape.Source;
                switch (outcome)
                {
                    case DiagramOutcome.Rendered done:
                        ReadingDocumentBuilder.ApplyRenderedShape(paragraph, element, done);
                        shape = DiagramShape.Rendered;
                        break;
                    case DiagramOutcome.Failed failed:
                        remappedSource = ReadingDocumentBuilder.ApplyFailureShape(
                            paragraph, element, failed, sourceText);
                        shape = DiagramShape.Failed;
                        break;
                    default:
                        // No artifact: main's unmatched shape — the source run
                        // alone — unless the reader is on the placeholder,
                        // which then keeps focus, named by the source until
                        // the next projection rebuilds the paragraph. #1708: the
                        // name's run and its line break go either way; a kept
                        // placeholder stays where it stood, first, since an
                        // element moved in the tree loses the keys.
                        foreach (Inline spare in paragraph.Inlines
                                     .Where(inline => inline is LineBreak || inline is Run run && ReadingSemantics.IsHiddenSpeech(run))
                                     .ToList())
                        {
                            paragraph.Inlines.Remove(spare);
                        }
                        if (element.IsKeyboardFocusWithin
                            || ReferenceEquals(
                                System.Windows.Input.FocusManager.GetFocusedElement(
                                    System.Windows.Input.FocusManager.GetFocusScope(element)),
                                element))
                        {
                            element.Complete(sourceText, null);
                        }
                        else
                        {
                            paragraph.Inlines.Remove(container);
                            keptContainer = false;
                            removedContainer = true;
                        }
                        ReadingSemantics.MarkDiagramBlock(paragraph, sourceText);
                        remappedSource = source;
                        break;
                }
                ReadingSemantics.ClearDiagramPending(paragraph);

                // Caret state is repairable UI state (ApplyBuiltDocument's rule):
                // a pointer that cannot be restored leaves WPF's own.
                try
                {
                    TextPointer? newCaret = RestoreDiagramAnchor(
                        caret, shape, paragraph, remappedSource, keptContainer ? container : null, atEnd: true);
                    TextPointer? newStart = RestoreDiagramAnchor(
                        start, shape, paragraph, remappedSource, keptContainer ? container : null, atEnd: false);
                    TextPointer? newEnd = RestoreDiagramAnchor(
                        end, shape, paragraph, remappedSource, keptContainer ? container : null, atEnd: true);
                    if (collapsed)
                    {
                        if (newCaret is not null)
                        {
                            CaretPosition = newCaret;
                        }
                    }
                    else if (newStart is not null || newEnd is not null)
                    {
                        TextPointer from = newStart ?? Selection.Start;
                        TextPointer to = newEnd ?? Selection.End;
                        // Select(anchor, moving): a backward selection keeps its
                        // moving end at its start.
                        if (backward)
                        {
                            Selection.Select(to, from);
                        }
                        else
                        {
                            Selection.Select(from, to);
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
        finally
        {
            EndChange();
        }

        // Each landmark copies its text when collected, and the landing
        // changed; the caret's Enter path reads the description live.
        _landmarks = ReadingDocumentBuilder.CollectLandmarks(Document);
        _navigator?.SetLandmarks(_landmarks);
        // A removed container leaves the surface's children: drop WB-11's
        // cached list and WPF's own (the stale-child problem). A text-only
        // completion keeps every peer and invalidates nothing.
        if (removedContainer)
        {
            InvalidateStructuralPeers();
        }
        RaiseContentChanged(ReadingContentChange.CompletedInPlace);
    }

    private static DiagramAnchor RecordDiagramAnchor(
        TextPointer pointer, Paragraph paragraph, Run? source, InlineUIContainer container)
    {
        if (pointer.CompareTo(paragraph.ContentStart) < 0 || pointer.CompareTo(paragraph.ContentEnd) > 0)
        {
            return new DiagramAnchor(DiagramAnchorKind.Outside, 0, pointer);
        }
        // #1708: the source follows the placeholder's line, so a pointer is
        // placed against the source first, then against the container.
        if (source is not null
            && pointer.CompareTo(source.ContentStart) >= 0
            && pointer.CompareTo(source.ContentEnd) <= 0)
        {
            int offset = Math.Clamp(
                source.ContentStart.GetOffsetToPosition(pointer), 0, source.Text.Length);
            return new DiagramAnchor(DiagramAnchorKind.InSource, offset, null);
        }
        return pointer.CompareTo(container.ElementStart) <= 0
            ? new DiagramAnchor(DiagramAnchorKind.AtContainerStart, 0, null)
            : new DiagramAnchor(DiagramAnchorKind.AtContainerEnd, 0, null);
    }

    private static TextPointer? RestoreDiagramAnchor(
        DiagramAnchor anchor,
        DiagramShape shape,
        Paragraph paragraph,
        Run? source,
        InlineUIContainer? container,
        bool atEnd)
    {
        if (anchor.Kind == DiagramAnchorKind.Outside)
        {
            return null;
        }
        TextPointer InSource(Run run) =>
            run.ContentStart.GetPositionAtOffset(Math.Min(anchor.Offset, run.Text.Length)) ?? run.ContentEnd;
        switch (shape)
        {
            case DiagramShape.Rendered when container is not null:
                // The diagram replaced the source and the placeholder's line.
                // #1708 (CB-20, the lead's ruling on Call 11): its line opens
                // with the description's hidden run, and WPF moves a pointer at
                // the container back to that run's end, so a selection's start
                // goes to the diagram's start and its end covers the diagram;
                // the caret (asked for as an end) rests just after the diagram,
                // where its landmark is and where it is drawn at the line's
                // height.
                return atEnd ? container.ElementEnd : paragraph.ContentStart;
            case DiagramShape.Failed:
                return anchor.Kind switch
                {
                    DiagramAnchorKind.InSource when source is not null => InSource(source),
                    // After the placeholder: where the source began.
                    DiagramAnchorKind.AtContainerEnd when source is not null => source.ContentStart,
                    // At the placeholder: the top, where the name stood.
                    _ => paragraph.ContentStart,
                };
            default:
                return anchor.Kind switch
                {
                    DiagramAnchorKind.InSource when source is not null => InSource(source),
                    DiagramAnchorKind.AtContainerStart when container is not null => container.ElementStart,
                    DiagramAnchorKind.AtContainerEnd when container is not null => container.ElementEnd,
                    _ => source?.ContentStart ?? paragraph.ContentStart,
                };
        }
    }

    /// <summary>
    /// Move the built document's blocks into the surface's persistent
    /// document, then re-collect landmarks over the LIVE container — the
    /// built model's own pointers aim into a document this merge just
    /// emptied, and a navigator holding them would throw on the first
    /// caret comparison.
    /// </summary>
    internal void ApplyBuiltDocument(FlowDocument built)
    {
        ApplyProbeForTests?.Invoke();
        // An embedded child (a task checkbox the user just clicked) may
        // hold keyboard focus, and this merge is about to destroy it —
        // WPF's focus recovery from a removed element is undefined
        // territory (measured in the field, 2026-07-26: dead caret
        // after checkbox clicks). Reclaim focus for the document FIRST.
        if (IsKeyboardFocusWithin && !IsKeyboardFocused)
        {
            _ = Focus();
        }

        // A re-projection replaces every block, which would collapse a
        // caret inside the old content to the document start — throwing
        // the reader to the top after every task toggle. Preserve the
        // position by top-level block and the offset inside it: a toggle
        // changes one status char the projection never renders, so the
        // same anchor lands on the same content. A document-wide offset
        // no longer would (#1595, the plan 45 XB-9 review): a math
        // paragraph's length is its speech, which a math prefs change
        // rewrites without touching the note. Caret state is REPAIRABLE
        // UI state: any failure reading or restoring it degrades to the
        // document start and must never abort the content merge into a
        // terminal failure.
        (int Block, int Offset)? caretAnchor;
        try
        {
            caretAnchor = CaretAnchorOf(Document, CaretPosition);
        }
        catch (InvalidOperationException)
        {
            caretAnchor = null;
        }

        // DS-3: a formula's sideways scroll lives in the element this merge replaces.
        // The new one has to know before it joins the document, which lays it out at once.
        foreach (((string Source, int Nth) key, ReadingMathElement element) in NumberedFormulas(_landmarks, []))
        {
            if (element.Scroller is { HorizontalOffset: > 0 } scroller)
            {
                _formulaScrolls[key] = scroller.HorizontalOffset;
            }
        }

        ResumeFormulaScrolls(built, []);

        Document.Blocks.Clear();
        while (built.Blocks.FirstBlock is { } block)
        {
            built.Blocks.Remove(block);
            Document.Blocks.Add(block);
        }
        _landmarks = ReadingDocumentBuilder.CollectLandmarks(Document);
        _navigator?.SetLandmarks(_landmarks);
        InvalidateStructuralPeers();

        if (caretAnchor is { } anchor && anchor != (0, 0))
        {
            try
            {
                RestoreCaret(PositionAt(Document, anchor));
            }
            catch (InvalidOperationException)
            {
                CaretPosition = Document.ContentStart;
            }
        }

        // No focus claim here (W7-7 PR 8, R-10). This merge used to focus
        // the surface whenever it was shown and focus sat elsewhere — the
        // state a reader who moved away leaves behind, so a late merge could
        // steal focus back. The landing the shell asked for is delivered by
        // ApplyModel after the merge, and nothing else lands.
        ShowsSearchableContent = true;
        RaiseContentChanged(ReadingContentChange.Replaced);
    }

    /// <summary>Hands out the places <see cref="_formulaScrolls"/> holds to the formulas
    /// arriving in <paramref name="incoming"/>, counting from those already on the page.</summary>
    private void ResumeFormulaScrolls(FlowDocument incoming, IReadOnlyList<ReadingLandmark> onThePage)
    {
        if (_formulaScrolls.Count == 0)
        {
            return;
        }

        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        _ = NumberedFormulas(onThePage, seen).Count();
        foreach (((string Source, int Nth) key, ReadingMathElement element) in NumberedFormulas(ReadingDocumentBuilder.CollectLandmarks(incoming), seen))
        {
            if (_formulaScrolls.Remove(key, out double offset))
            {
                element.ResumeScrollAt = offset;
            }
        }
    }

    /// <summary>The formulas under <paramref name="landmarks"/>, each named by its source
    /// and by which of that source it is, so a merge can pair them up.</summary>
    private static IEnumerable<((string Source, int Nth) Key, ReadingMathElement Element)> NumberedFormulas(
        IReadOnlyList<ReadingLandmark> landmarks,
        Dictionary<string, int> seen)
    {
        foreach (ReadingLandmark landmark in landmarks)
        {
            if (landmark.Kind == ReadingLandmarkKind.Math
                && landmark.Position.Paragraph is { } paragraph
                && MathElementOf(paragraph) is { } element)
            {
                int nth = seen.GetValueOrDefault(element.Source);
                seen[element.Source] = nth + 1;
                yield return ((element.Source, nth), element);
            }
        }
    }

    /// <summary>The caret as its top-level block's index and the offset
    /// inside that block; a caret between blocks anchors to the next one's
    /// start.</summary>
    private static (int Block, int Offset)? CaretAnchorOf(FlowDocument document, TextPointer caret)
    {
        int index = 0;
        foreach (Block block in document.Blocks)
        {
            if (caret.CompareTo(block.ElementEnd) <= 0)
            {
                return (index, Math.Max(0, block.ContentStart.GetOffsetToPosition(caret)));
            }
            index++;
        }
        return null;
    }

    /// <summary>The position an anchor names, clamped to its block; the
    /// document end when the block is gone.</summary>
    private static TextPointer PositionAt(FlowDocument document, (int Block, int Offset) anchor)
    {
        if (document.Blocks.ElementAtOrDefault(anchor.Block) is not { } block)
        {
            return document.ContentEnd;
        }
        int length = block.ContentStart.GetOffsetToPosition(block.ContentEnd);
        return block.ContentStart.GetPositionAtOffset(Math.Min(anchor.Offset, length))
            ?? block.ContentEnd;
    }

    /// <summary>Whether a shell landing is held for this surface's first
    /// merged projection (<see cref="RequestFocusLanding"/>) — carried by a
    /// rescan reload of its tab included (WB-9).</summary>
    internal bool IsFocusLandingPending => _focusLandingPending || _carried is not null;

    /// <summary>
    /// W7-7 PR 8 (#1253, contract R-10): the shell's editor landing for a
    /// reading-mode tab — <c>MainWindow.FocusEditorPane</c>'s reading arm,
    /// behind Ctrl+Shift+E, F6/Shift+F6, tab cycling, Quick Open and the
    /// dismissal fallbacks. A surface showing its model's CURRENT projection —
    /// applied, with no refresh of it in flight; an empty note's included —
    /// takes focus NOW, or keeps it (focus already inside, a link's included,
    /// is the stop: the reader's place outranks a re-seat), and keeps the
    /// caret where the rebind restore or the merge seated it. Readiness and
    /// failure come FIRST, focus already inside only after them. A surface
    /// still showing the loading placeholder, or a projection a refresh in
    /// flight is about to replace (the reader left reading mode, edited the
    /// note, and came back), HOLDS the landing until that refresh settles:
    /// focused early, NVDA reads the placeholder (or "blank", or the obsolete
    /// text) and never re-reads what replaces it. That holds for a reader
    /// ALREADY inside too (an in-place navigation swapped the note under them;
    /// the tabs' shared cell rebound to one still projecting): NVDA speaks a
    /// focus change, never a document replaced under the caret, so they are
    /// first parked on <paramref name="parkWhileUnready"/> (the shell's stop
    /// for the tab — its tab item), and the settle's seat is then the one
    /// arrival on the applied content, which NVDA reads. (A park no stop takes
    /// leaves them where they are, and the settle lands them in place.) A
    /// held landing calls <paramref name="announceWhenHeldLandingArrives"/>
    /// once focus is really here (the F6 ring's deferred line), or
    /// <paramref name="fallThroughWhenHeldLandingRefused"/> when the content
    /// arrives and focus cannot be taken (the F6 ring moves on); a withdrawn
    /// one calls neither. Answers whether focus is here or held; a hidden
    /// surface, or one showing a failure — the load-failure notice, or what a
    /// failed refresh kept (no note to read) — answers false even with focus
    /// inside, so the caller's fallbacks run.
    /// </summary>
    internal bool RequestFocusLanding(
        Action? announceWhenHeldLandingArrives = null,
        Action? fallThroughWhenHeldLandingRefused = null,
        Func<bool>? parkWhileUnready = null)
    {
        WithdrawFocusLanding();
        // WB-9 (red team): a new request supersedes a carried landing as it
        // does a held one — silently; the reload's end then resumes nothing.
        _carried = null;
        if (!IsVisible)
        {
            return false;
        }
        if (!ShowsAppliedProjection || _model is { RefreshInFlight: true })
        {
            // Parked BEFORE the hold: the park is the landing's own move, made
            // before the window's slot holds it, and the reader's departure is
            // read from the stop they now wait on.
            if (IsKeyboardFocusWithin)
            {
                _ = parkWhileUnready?.Invoke();
            }
            HoldFocusLanding(announceWhenHeldLandingArrives, fallThroughWhenHeldLandingRefused);
            return true;
        }
        if (ShowsFailure)
        {
            return false;
        }
        return IsKeyboardFocusWithin || Focus();
    }

    /// <summary>Ready to land: the BOUND model's projection has been applied
    /// — the merge sets <see cref="_lastMerged"/> for the model it merged,
    /// and a switch to another model clears it as it installs the
    /// placeholder. Never the block count: the placeholder is a block. The
    /// first applied chunk of a streamed note is ready, because appends only
    /// grow the tail and the reader starts at the top, which is there.</summary>
    private bool ShowsAppliedProjection => _lastMerged is not null;

    /// <summary>WB-22 (#1112): raised once at the end of each path that
    /// changes what the document holds — never by the rebind fast path,
    /// which changes nothing.</summary>
    internal event Action<ReadingContentChange>? ContentChanged;

    /// <summary>Bumped with every <see cref="ContentChanged"/>.</summary>
    internal int ContentRevision { get; private set; }

    /// <summary>Bumped with every <see cref="ReadingContentChange.Replaced"/>:
    /// a replacement moves every pointer into the document.</summary>
    internal int ReplacedRevision { get; private set; }

    /// <summary>WB-22: the find match a reveal last made current, kept here
    /// rather than on a find surface the bar rebuilds on every attach;
    /// a replacement drops it.</summary>
    internal object? FindRevealMemory { get; set; }

    /// <summary>WB-22: raised once a model change is fully handled (content
    /// switched, or the rebind fast path taken).</summary>
    internal event EventHandler? ModelChanged;

    /// <summary>The document holds an applied projection, not the "Loading
    /// reading view…" placeholder (true from the merge, not from
    /// <see cref="ApplyModel"/>'s bookkeeping after it, so a direct merge
    /// counts too).</summary>
    internal bool ShowsSearchableContent { get; private set; }

    /// <summary>The text view's render scope: what the template's
    /// <c>PART_ContentHost</c> scroll viewer scrolls. Null before the
    /// template applies.</summary>
    internal FrameworkElement? DocumentRenderScope => ContentScrollViewer?.Content as FrameworkElement;

    internal ScrollViewer? ContentScrollViewer => GetTemplateChild("PART_ContentHost") as ScrollViewer;

    private void RaiseContentChanged(ReadingContentChange change)
    {
        ContentRevision++;
        if (change == ReadingContentChange.Replaced)
        {
            ReplacedRevision++;
            FindRevealMemory = null;
        }
        ContentChanged?.Invoke(change);
    }

    /// <summary>What the surface shows is a failure, not the note: the
    /// model's terminal-failure notice, or the content a failed refresh kept
    /// on screen (stale by the failure's own account). No stop either way —
    /// the failure was announced when it happened.</summary>
    private bool ShowsFailure =>
        _model is { PublishedFailureNotice: true } or { LastRefreshFailed: true };

    /// <summary>Hold the landing, under a token of its own
    /// (<see cref="HeldFocusLanding"/>). The surface seats or refuses it and
    /// lets go of it when it is hidden or rebinds (a rescan reload of the same
    /// tab carries it, WB-9); everything else that cancels
    /// it — the reader leaving where the request found them, a modal opening,
    /// a newer landing — is the window's one slot (OD-12,
    /// <see cref="EditorLandingSlot"/>), which cancels it by that token.</summary>
    private void HoldFocusLanding(Action? announceWhenLanded, Action? fallThroughWhenRefused)
    {
        _focusLandingPending = true;
        _focusLandingToken = new object();
        _focusLandingAnnouncement = announceWhenLanded;
        _focusLandingRefusal = fallThroughWhenRefused;
    }

    /// <summary>R-10: the refresh the landing waits on settled — the ONE
    /// delivery path. Posted behind the publish that settled it, so whatever
    /// that publish merged (a new projection, the failure notice) is on
    /// screen first; a publish that found the note drifted and refreshed
    /// again leaves a refresh in flight, and the landing keeps waiting for
    /// that one. A refresh that failed refuses the landing; any other lands
    /// it — on the merged projection, or on the unchanged one (a memo hit
    /// merges nothing), the caret wherever the merge left it.</summary>
    private void ScheduleSettledLanding() =>
        _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () =>
            {
                if (!_focusLandingPending || _model is not { RefreshInFlight: false } model)
                {
                    return;
                }

                if (model.LastRefreshFailed)
                {
                    Action? refuse = _focusLandingRefusal;
                    WithdrawFocusLanding();
                    refuse?.Invoke();
                    return;
                }

                DeliverFocusLanding();
            });

    /// <summary>The held landing's own token (R-10) — what its holder cancels
    /// it by, the same while a rescan reload carries it (WB-9) — or null when
    /// no landing is held.</summary>
    internal object? HeldFocusLanding => _focusLandingPending ? _focusLandingToken : _carried?.Token;

    /// <summary>Let go of the held landing <paramref name="token"/> names — the
    /// window's slot cancels it (a newer editor landing, the funnel, the
    /// reader's departure, a modal opening), carried or not — and of nothing
    /// a later request holds. Answers whether it was still held.</summary>
    internal bool CancelFocusLanding(object token)
    {
        if (_carried is { } carried && ReferenceEquals(carried.Token, token))
        {
            _carried = null;
            return true;
        }

        if (!_focusLandingPending || !ReferenceEquals(_focusLandingToken, token))
        {
            return false;
        }

        WithdrawFocusLanding();
        return true;
    }

    /// <summary>R-10: the bound model was torn down before its projection
    /// applied, so no apply is coming. A teardown almost always travels with
    /// a move that ends the landing itself — the tab navigated in place (a
    /// rebind) or closed (a rebind or an unload), and those CANCEL it — so
    /// the outcome is decided once that move has run: if THIS landing is
    /// still the one held (nothing cancelled or replaced it; the surface is
    /// still bound to the torn-down model), it is REFUSED and the F6 ring
    /// resumes past the editor; otherwise the callback is stale and does
    /// nothing. One terminal transition per landing, in either order. A
    /// landing a rescan reload carries is not pending here (WB-9): the
    /// reload's end decides it.</summary>
    private void Model_TornDown()
    {
        if (!_focusLandingPending)
        {
            return;
        }

        RefuseHeldLandingAfterThisTurn();
    }

    /// <summary>Refuse the landing held now once the current move has run —
    /// only if it is still the one held by then, so a cancellation before
    /// that turn (the slot's withdraw, a rebind) still withdraws it
    /// silently.</summary>
    private void RefuseHeldLandingAfterThisTurn()
    {
        object? token = _focusLandingToken;
        Action? refuse = _focusLandingRefusal;
        _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () =>
            {
                if (!ReferenceEquals(_focusLandingToken, token))
                {
                    return;
                }

                WithdrawFocusLanding();
                refuse?.Invoke();
            });
    }

    /// <summary>WB-9 (#1312, R-9/R-10): a rescan reload of the bound
    /// projection's tab begins — it tears that projection down and rebinds
    /// this surface to the reloaded one, which would refuse and then withdraw
    /// a held landing. The landing moves onto the reload instead, under its
    /// original token (nothing is minted, so the window's slot still holds
    /// it), and the reload's end takes it back.</summary>
    private void OnReadingRescanReloadBegan(ReadingReloadTransaction reload)
    {
        if (!_focusLandingPending
            || _model is not { } from
            || !ReferenceEquals(from.Tab, reload.Tab))
        {
            return;
        }

        var carried = new CarriedLanding(
            _focusLandingToken!, _focusLandingAnnouncement, _focusLandingRefusal, reload.Tab, from);
        _carried = carried;
        WithdrawFocusLanding();
        reload.WhenEnded(() => EndCarry(carried));
    }

    /// <summary>WB-9 (#1312): the reload that carried <paramref name="carried"/>
    /// ended, in its own turn. Unless the slot cancelled it (or the surface
    /// was hidden) meanwhile, it is held again under its original token: with
    /// a replacement projection of the same tab bound, that projection's
    /// refresh settles it — seated and spoken once, or refused once if it
    /// failed; with none (the reload threw before the rebind, or the surface
    /// let go of the tab), no apply is coming, and it is refused once after
    /// this turn.</summary>
    private void EndCarry(CarriedLanding carried)
    {
        if (!ReferenceEquals(_carried, carried))
        {
            return;
        }

        _carried = null;
        _focusLandingPending = true;
        _focusLandingToken = carried.Token;
        _focusLandingAnnouncement = carried.Announce;
        _focusLandingRefusal = carried.Refuse;
        if (_model is { } model
            && !ReferenceEquals(model, carried.From)
            && ReferenceEquals(model.Tab, carried.Tab))
        {
            // Still refreshing: its settle reaches Model_PropertyChanged.
            if (!model.RefreshInFlight)
            {
                ScheduleSettledLanding();
            }
            return;
        }

        RefuseHeldLandingAfterThisTurn();
    }

    private void WithdrawFocusLanding()
    {
        _focusLandingPending = false;
        _focusLandingToken = null;
        _focusLandingAnnouncement = null;
        _focusLandingRefusal = null;
    }

    /// <summary>
    /// A held landing lands with the projection it waited for — the first
    /// applied one (the placeholder's caret park maps to its start), or the
    /// one the refresh in flight settled on (the reader's place kept) — and
    /// only then speaks its announcement. An empty note's merge delivers too:
    /// the empty document is the note, like the ring's empty editor pane.
    /// When the landing cannot be taken — the refresh failed (<see
    /// cref="ScheduleSettledLanding"/>), focus is refused, or before any apply
    /// the model was torn down (<see cref="Model_TornDown"/>) — it is REFUSED:
    /// the caller falls through (the F6 ring moves on from the editor's
    /// position) and nothing is spoken. It is WITHDRAWN, with neither, when
    /// the window's slot cancels it (the reader leaving where its request
    /// found them — <see cref="EditorLandingSlot"/>), and when the surface is
    /// hidden (unloaded included) or rebinds: the view left under a move the
    /// reader or the shell made, and falling through over it would pull focus
    /// away from where that move put it. The one rebind with no such move — a
    /// rescan reload of the same tab — carries it instead
    /// (<see cref="OnReadingRescanReloadBegan"/>).
    /// </summary>
    private void DeliverFocusLanding()
    {
        if (!_focusLandingPending)
        {
            return;
        }
        Action? announce = _focusLandingAnnouncement;
        Action? refuse = _focusLandingRefusal;
        WithdrawFocusLanding();
        if (!IsVisible)
        {
            return;
        }
        // Focus already here — the reader stepped in while the content
        // arrived (the one move into the landing's own target the watch
        // follows) — is the landing: its line is spoken, once, like one this
        // seats.
        if (IsKeyboardFocusWithin || (Focus() && IsKeyboardFocusWithin))
        {
            announce?.Invoke();
        }
        else
        {
            refuse?.Invoke();
        }
    }

    /// <summary>WB-11 (#1329): the document's structural generation, which
    /// the peers' caches are kept for.</summary>
    internal ReadingStructureGeneration Structure { get; } = new();

    /// <summary>
    /// WB-11 (#1329): the one entry for a change to the document's blocks —
    /// the merge, the streamed append and the model-switch clear today, and
    /// any later change that adds or removes a block, an embedded element or
    /// a hyperlink. It drops the peers' caches (the surface's children and
    /// each list's index), has every kept list peer rebuild its children,
    /// and brings the surface peer's children current. A change that only edits <c>Run</c>s inside a
    /// top-level paragraph, keeping its elements and their peers, needs no
    /// call: no cached child changes and no list reads that text.
    /// </summary>
    internal void InvalidateStructuralPeers()
    {
        Structure.Bump();
        (UIElementAutomationPeer.FromElement(this) as ReadingSurfacePeer)?.ResetListPeers();
        InvalidateAutomationChildren();
    }

    /// <summary>
    /// Bring the surface peer's child list current after a content
    /// merge. WPF refreshes peer children on render-driven automation
    /// ticks; on a sparsely-composited session those ticks stall and a
    /// UIA client keeps reading a child list frozen at an earlier
    /// document state (measured on CI, 2026-08-01: the range text
    /// carried the complete note while the child list held two
    /// elements for the full 90-second wait). No-op unless a UIA
    /// client is attached — that is when a peer exists.
    ///
    /// Called only from <see cref="InvalidateStructuralPeers"/>: on its own
    /// it would rebuild the surface's children from the peers' caches of the
    /// previous structural generation, so a change to the document's
    /// blocks calls that entry, never this one (WB-11, #1329).
    /// </summary>
    private void InvalidateAutomationChildren()
    {
        // The UIA bridge publishes structure updates at the END OF A
        // LAYOUT PASS — and a starved session defers layout
        // indefinitely, so clients keep walking the pre-merge view
        // while the peer's own children list is current (measured
        // 2026-08-01: a clean two-element sibling walk against a
        // nine-child cache). Run the pass NOW, synchronously.
        UpdateLayout();
        if (UIElementAutomationPeer.FromElement(this) is { } peer)
        {
            // Marking the cache dirty is NOT enough: the rebuild waits
            // for a render-driven automation tick, which a throttled
            // session may never run (measured on CI, 2026-08-01 — the
            // dirty flag alone left the stale two-element list live
            // for the full 90-second wait). GetChildren() rebuilds the
            // list synchronously; StructureChanged tells attached
            // clients to drop what they hold.
            peer.ResetChildrenCache();
            List<AutomationPeer>? rebuilt = peer.GetChildren();
            peer.RaiseAutomationEvent(AutomationEvents.StructureChanged);
            peer.InvalidatePeer();
            if (CensusDiagEnabled)
            {
                CensusDiag($"invalidate: children={rebuilt?.Count ?? -1}");
            }
        }
        else
        {
            CensusDiag("invalidate: no peer");
        }
    }

    /// <summary>Whether <see cref="CensusDiag"/> writes: checked before a
    /// caller builds an interpolated message, so production formats
    /// nothing (WB-11, #1329).</summary>
    internal static bool CensusDiagEnabled =>
        Environment.GetEnvironmentVariable("SLATE_CENSUS_INSTANCE_ID") is { Length: > 0 }
        && Environment.GetEnvironmentVariable("SLATE_LOG_DIR") is { Length: > 0 };

    /// <summary>
    /// Census-run diagnostics (SLATE_CENSUS_INSTANCE_ID gates it, so
    /// production writes nothing): the stale-children repro exists
    /// only on CI, and the shared app log is the one channel the
    /// failing gate reads back into its assertion message.
    /// </summary>
    internal static void CensusDiag(string message)
    {
        if (!CensusDiagEnabled
            || Environment.GetEnvironmentVariable("SLATE_LOG_DIR") is not { Length: > 0 } directory)
        {
            return;
        }
        try
        {
            // A SEPARATE file: the app holds slate-windows.log open
            // for the stderr redirect, and a second writer's append
            // dies on a sharing violation — measured 2026-08-01 as a
            // silently empty diagnostic channel.
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(directory, "slate-census-diag.log"),
                $"{DateTime.Now:HH:mm:ss.fff} census-diag {message}\r\n");
        }
        catch (System.IO.IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Activate the link the CARET is inside. A caret position is not
    /// element focus — measured 2026-07-27: Enter with the caret inside
    /// a link did nothing, because the Hyperlink never had keyboard
    /// focus and so never raised Click. Containment is tested against
    /// the landmark index's own elements rather than by walking pointer
    /// parents, which misses at normalized boundary positions — exactly
    /// where a chord landing puts the caret.
    /// </summary>
    internal bool TryActivateAtCaret() => TryActivateAtCaret(brailleRequested: false);

    /// <summary>The end of the authored marker run that opens the caret's
    /// paragraph when the caret is at or inside it; otherwise the caret.</summary>
    private static TextPointer PastListMarker(TextPointer caret) =>
        caret.Paragraph is { Inlines.FirstInline: Run marker }
            && ReadingSemantics.IsListMarker(marker)
            && caret.CompareTo(marker.ElementEnd) <= 0
                ? marker.ElementEnd
                : caret;

    internal bool TryActivateAtCaret(bool brailleRequested)
    {
        if (_model is not { } model || CaretPosition is not { } caret)
        {
            return false;
        }
        // WB-11 (#1422): an ordered item's line opens with its authored
        // marker run, and a caret at the line's start is at what follows it.
        caret = PastListMarker(caret);
        foreach (ReadingLandmark landmark in _landmarks)
        {
            if (landmark.Kind == ReadingLandmarkKind.Link
                && landmark.Element is Hyperlink
                {
                    Tag: uniffi.slate_uniffi.ReadingInlineRunKind kind
                } link
                // A document with live links keeps the caret OFF the
                // interactive interior: a chord landing rests the caret
                // immediately BEFORE the hyperlink element (measured —
                // compare(ElementStart) == -1 at one symbol's distance).
                // "At the link" therefore means: inside its element
                // range, or within one symbol before it.
                && caret.CompareTo(link.ElementEnd) <= 0
                && (caret.CompareTo(link.ElementStart) >= 0
                    || caret.GetOffsetToPosition(link.ElementStart) is >= 0 and <= 1))
            {
                model.Activate(kind);
                return true;
            }
        }
        // No link at the caret: a task checkbox on the caret's LINE is
        // the other activatable thing. The caret can never rest inside
        // an InlineUIContainer, so containment is by paragraph.
        if (TryToggleTaskAtCaret())
        {
            return true;
        }
        // Math block at the caret (W3-2): Enter speaks the canonical
        // MathCAT speech through the landed vocabulary — "{speech},
        // math." — content, never composition. This is the guaranteed
        // layer's on-demand read; the W-E7 appModule upgrades Enter to
        // NVDA's full math interaction later.
        if (CaretPosition?.Paragraph is { } mathParagraph
            && ReadingSemantics.IsMathBlock(mathParagraph)
            && _model is { } mathModel)
        {
            // Ctrl+Enter reads the BRAILLE artifact — the accessible
            // value the Nemeth/UEB pref selects (round 3: a persisted
            // setting must change something a user can retrieve).
            // Plain Enter reads the speech. Both announce core content.
            if (brailleRequested)
            {
                AnnounceMathBraille(mathModel, MathElementOf(mathParagraph));
                return true;
            }
            AnnounceMathSpeech(mathModel, mathParagraph);
            return true;
        }
        // Diagram block at the caret (W3-3): Enter (and Ctrl+Enter —
        // diagrams have no braille-analog artifact) re-reads the
        // canonical structured description through the landed
        // vocabulary — content, never composition.
        if (CaretPosition?.Paragraph is { } diagramParagraph
            && ReadingSemantics.IsDiagramBlock(diagramParagraph)
            && _model is { } diagramModel)
        {
            diagramModel.Announce(new uniffi.slate_uniffi.A11yEvent.ReadingNavLanded(
                new uniffi.slate_uniffi.ReadingNavTarget.Diagram(),
                ReadingSemantics.DiagramDescriptionOf(diagramParagraph)));
            return true;
        }
        // Embed card header at the caret (W3-5): Enter activates the
        // card — a chord landing rests the caret ON the header, and a
        // caret position is not element focus (the W3-1 lesson), so
        // the Jump button's own key handling never fires for it. A
        // core-resolved destination navigates directly; only cards
        // without one fall back to the record match.
        if (CaretPosition?.Paragraph is { } caretParagraph
            && EmbedHeaderFor(caretParagraph) is { } embedParagraph
            && ReadingSemantics.EmbedHeaderKeyOf(embedParagraph) is { } embedKey
            && _model is { } embedModel)
        {
            if (ReadingSemantics.TryGetEmbedJump(
                embedParagraph, out string embedJumpPath, out var embedJumpAnchor))
            {
                embedModel.ActivateResolvedEmbedSource(embedJumpPath, embedJumpAnchor);
                return true;
            }
            embedModel.Activate(new uniffi.slate_uniffi.ReadingInlineRunKind.Embed(embedKey));
            return true;
        }
        // Table at the caret (W4-1): Enter opens the table on the grid
        // substrate — the in-range table stays for linear reading
        // (G23), the grid view adds the §8.7 powers. Runs LAST: a link
        // inside a table cell is still a link first.
        if (CaretPosition?.Paragraph is { } tableParagraph
            && AncestorTableOf(tableParagraph) is { } table
            && ReadingSemantics.TableSourceOf(table) is { } tableSource)
        {
            return (TableGridOpener ?? OpenTableGridWindow)(tableSource);
        }
        return false;
    }

    /// <summary>
    /// #1591 (plan 45 XB-9): Ctrl+Shift+Enter on a math block speaks its
    /// authored source, the one route to it, since line reading and focus
    /// speak the speech. The sentence is core's. Returns false off a math
    /// block, so the key falls through.
    /// </summary>
    internal bool TrySpeakMathSourceAtCaret()
    {
        if (_model is not { } model
            || CaretPosition?.Paragraph is not { } paragraph
            || !ReadingSemantics.IsMathBlock(paragraph))
        {
            return false;
        }
        AnnounceMathSource(model, MathElementOf(paragraph));
        return true;
    }

    /// <summary>
    /// #1591 (the plan 45 XB-9 review): Tab puts keyboard focus ON a math
    /// element, where the caret requests do not reach, so its three
    /// requests are answered for it: Enter its speech, Ctrl+Enter its
    /// braille, Ctrl+Shift+Enter its source. Returns false for any other
    /// modifier state, so the key falls through.
    /// </summary>
    internal bool TryAnswerMath(
        ReadingMathElement element, System.Windows.Input.ModifierKeys modifiers)
    {
        if (_model is not { } model
            || (element.Parent as InlineUIContainer)?.Parent is not Paragraph paragraph
            || !ReadingSemantics.IsMathBlock(paragraph))
        {
            return false;
        }
        switch (modifiers)
        {
            case System.Windows.Input.ModifierKeys.None:
                AnnounceMathSpeech(model, paragraph);
                return true;
            case System.Windows.Input.ModifierKeys.Control:
                AnnounceMathBraille(model, element);
                return true;
            case System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift:
                AnnounceMathSource(model, element);
                return true;
            default:
                return false;
        }
    }

    private static void AnnounceMathSpeech(ReadingContentViewModel model, Paragraph paragraph) =>
        model.Announce(new uniffi.slate_uniffi.A11yEvent.ReadingNavLanded(
            new uniffi.slate_uniffi.ReadingNavTarget.Math(),
            ReadingSemantics.MathSpeechOf(paragraph)));

    private static void AnnounceMathBraille(ReadingContentViewModel model, ReadingMathElement? element) =>
        model.Announce(new uniffi.slate_uniffi.A11yEvent.HostComposed(
            element is { Braille.Length: > 0 } ? element.Braille : "Braille not available.",
            uniffi.slate_uniffi.A11yPriority.Medium));

    private static void AnnounceMathSource(ReadingContentViewModel model, ReadingMathElement? element) =>
        model.Announce(new uniffi.slate_uniffi.A11yEvent.MathSourceSpoken(
            string.IsNullOrWhiteSpace(element?.Source) ? null : element.Source));

    /// <summary>
    /// #1595 (the plan 45 XB-9 review): a math paragraph's text is its
    /// speech, which line reading needs, but a copy wants the formula. A
    /// selection that reaches a math paragraph's speech copies as plain
    /// text, each such paragraph's fenced source in its speech's place;
    /// any other selection copies as WPF builds it.
    /// </summary>
    /// <summary>DS-3: the design's measure, in characters. <c>ColumnWidth</c> stays
    /// infinite, because multi-column reading order is a hazard, so margins hold it:
    /// that keeps the scroll bar at the pane's edge, needs no rebuild, and can't move
    /// the caret.</summary>
    private const int MeasureCharacters = 72;

    private const double PagePaddingFloor = 16;

    /// <summary>The body's size and face, watched so the measure follows them. They're
    /// the surface's own because the document's values lag these callbacks when a theme
    /// or text size swaps dictionaries, which left the measure on the old font.</summary>
    private static readonly DependencyProperty BodySizeProperty = DependencyProperty.Register(
        "BodySize",
        typeof(double),
        typeof(ReadingSurface),
        new PropertyMetadata(0d, (surface, _) => ((ReadingSurface)surface).FitMeasure()));

    private static readonly DependencyProperty BodyFamilyProperty = DependencyProperty.Register(
        "BodyFamily",
        typeof(System.Windows.Media.FontFamily),
        typeof(ReadingSurface),
        new PropertyMetadata(null, (surface, _) => ((ReadingSurface)surface).FitMeasure()));

    private void FitMeasure()
    {
        // Each key passes through unset during a swap, and a surface outside the shell
        // has none, so there's nothing to measure with until they resolve.
        double size = (double)GetValue(BodySizeProperty);
        if (size <= 0 || GetValue(BodyFamilyProperty) is not System.Windows.Media.FontFamily family)
        {
            return;
        }

        double side = Math.Max(PagePaddingFloor, (ActualWidth - MeasureWidth(family, size)) / 2);
        var padding = new Thickness(side, PagePaddingFloor, side, PagePaddingFloor);
        if (Document.PagePadding != padding)
        {
            Document.PagePadding = padding;
        }
    }

    /// <summary>A <c>ch</c> is the advance of a zero, in the face and size on show.</summary>
    private double MeasureWidth(System.Windows.Media.FontFamily family, double size) => MeasureCharacters * new System.Windows.Media.FormattedText(
        "0",
        System.Globalization.CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        new System.Windows.Media.Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
        size,
        System.Windows.Media.Brushes.Black,
        System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace;

    /// <summary>DS-1: WPF's XAML formats name each font by its pack URI, and only
    /// another WPF rich text box reads them. Plain text and RTF serve every
    /// recipient, that box included.</summary>
    private static void OnSettingData(object sender, DataObjectSettingDataEventArgs e)
    {
        if (e.Format == DataFormats.Xaml || e.Format == DataFormats.XamlPackage)
        {
            e.CancelCommand();
        }
    }

    /// <summary>#1708: the caret was last moved along its line by a key
    /// (Left, Right, Home, End), so it stays where the key put it, hidden
    /// speech included. Ctrl+Home and Ctrl+End move across lines.</summary>
    private bool _caretMovedAlongItsLine;

    /// <summary>#1708: where the caret was at the last selection change.</summary>
    private TextPointer? _caretBefore;

    /// <summary>#1708 (the merge judge's M1): the anchor of the selection
    /// while it was not empty, so a selection collapsed back onto its anchor
    /// leaves the caret there.</summary>
    private TextPointer? _selectionAnchor;

    /// <summary>#1708 (the merge judge's M1): a mouse button is down on the
    /// surface. WPF anchors a drag where the press put the caret, so the
    /// caret is left there until the button is released.</summary>
    private bool _pointerHeld;

    /// <summary>#1708 (the merge judge's m2): a republish or a rebind is
    /// putting the reader back where they were, hidden letter included.</summary>
    private bool _restoringCaret;

    /// <summary>Puts the caret back where a republish or a rebind found the
    /// reader, exactly, a hidden letter included.</summary>
    private void RestoreCaret(TextPointer position)
    {
        _restoringCaret = true;
        try
        {
            CaretPosition = position;
        }
        finally
        {
            _restoringCaret = false;
        }
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        // A key while the button is down ends the press's hold.
        _pointerHeld = false;
        _caretMovedAlongItsLine = e.Key is System.Windows.Input.Key.Left or System.Windows.Input.Key.Right
            || (e.Key is System.Windows.Input.Key.Home or System.Windows.Input.Key.End
                && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == 0);
        base.OnPreviewKeyDown(e);
    }

    protected override void OnPreviewKeyUp(System.Windows.Input.KeyEventArgs e)
    {
        // A key that moved nothing leaves no flag for a later move.
        _caretMovedAlongItsLine = false;
        base.OnPreviewKeyUp(e);
    }

    protected override void OnPreviewMouseDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        _caretMovedAlongItsLine = false;
        _pointerHeld = true;
        base.OnPreviewMouseDown(e);
    }

    protected override void OnPreviewMouseUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);
        ReleasePointer();
    }

    /// <summary>The press is over: once WPF has settled the selection, a
    /// plain click in hidden speech rests just after the element, and a
    /// drag keeps its selection.</summary>
    private void ReleasePointer()
    {
        if (!_pointerHeld)
        {
            return;
        }
        _pointerHeld = false;
        // After WPF's own release has settled the selection: a plain click
        // in hidden speech rests just after the element; a drag keeps its
        // selection.
        _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            () =>
            {
                if (Selection.IsEmpty)
                {
                    MoveCaretPastHiddenSpeech(CaretPosition);
                }
            });
    }

    /// <summary>
    /// #1708 (CB-20): a caret that arrives at or inside hidden speech from
    /// another line (Up, Down, a page, a landing) moves just after the
    /// element the speech stands for, on the same line, where it is drawn at
    /// the line's height and not as the sliver of a point the speech is.
    /// A move along the line (Left, Right, Home, End, or a caret set on the
    /// same line) stays, so the speech's letters are still caret stops, as a
    /// formula's have been since #1595. No stop is added or taken away. The
    /// merge judge's findings: a mouse press is left where it was pressed
    /// until the button is released (a drag is anchored there), a selection
    /// collapsed onto its anchor stays on it, a republish or a rebind puts
    /// the reader back exactly, and the caret moves only onto the element's
    /// own line.
    /// </summary>
    protected override void OnSelectionChanged(RoutedEventArgs e)
    {
        base.OnSelectionChanged(e);
        TextPointer? before = _caretBefore;
        TextPointer? anchor = _selectionAnchor;
        bool alongItsLine = _caretMovedAlongItsLine;
        _caretMovedAlongItsLine = false;
        TextPointer caret = CaretPosition;
        _caretBefore = caret;
        if (!Selection.IsEmpty)
        {
            _selectionAnchor = caret.CompareTo(Selection.Start) == 0 ? Selection.End : Selection.Start;
            return;
        }
        _selectionAnchor = null;
        if (_pointerHeld || _restoringCaret || alongItsLine || (anchor is not null && caret.CompareTo(anchor) == 0))
        {
            return;
        }
        // Caret state is repairable UI state (ApplyBuiltDocument's rule): a
        // layout that cannot answer leaves the caret where WPF put it.
        try
        {
            if (before is not null && OnOneLine(before, caret))
            {
                return;
            }
        }
        catch (InvalidOperationException)
        {
            return;
        }
        MoveCaretPastHiddenSpeech(caret);
    }

    /// <summary>#1708: a caret at or inside hidden speech moves just after
    /// the element, when that is on its own line (the merge judge's m1: a
    /// wrap between them would make Up a trap).</summary>
    private void MoveCaretPastHiddenSpeech(TextPointer caret)
    {
        try
        {
            if (ReadingDocumentBuilder.VisibleCaretPast(caret) is { } visible && OnOneLine(caret, visible))
            {
                CaretPosition = visible;
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static bool OnOneLine(TextPointer first, TextPointer second) =>
        first.Paragraph is { } paragraph
        && ReferenceEquals(paragraph, second.Paragraph)
        && first.GetLineStartPosition(0) is { } line
        && second.GetLineStartPosition(0) is { } other
        && line.CompareTo(other) == 0;

    private void OnCopying(object sender, DataObjectCopyingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.Rtf) is string rtf)
        {
            e.DataObject.SetData(DataFormats.Rtf, ShippedFontNames.InRtfFontTable(rtf));
        }

        if (CopyOf(Selection.Start, Selection.End) is not { } copy)
        {
            return;
        }
        string text = copy.Text;
        if (text.Length == 0)
        {
            // #1708: the selection holds only hidden speech, which shows
            // nothing, so there is nothing to copy; the clipboard keeps
            // what it held.
            e.CancelCommand();
            return;
        }
        // #1708 (CB-20; the lead's ruling on Call 12 and the merge judge's
        // m3): a copy that changes the text (a formula's or a rendered
        // diagram's fenced source in its place, hidden speech left out)
        // keeps WPF's formats, a copy and a drag alike. Its plain text is
        // the changed text, and its rich text is made again with the same
        // changes, the rest's formatting and pictures kept, so neither
        // carries hidden speech.
        e.DataObject.SetData(DataFormats.UnicodeText, text);
        e.DataObject.SetData(DataFormats.Text, text);
        e.DataObject.SetData(
            DataFormats.Rtf,
            RichTextOfCopy(Selection.Start, Selection.End, copy.Changes) ?? PlainTextRtf(text));
    }

    /// <summary>The text a copy of [start, end] puts on the clipboard, or
    /// null when the range reaches neither a paragraph with copy text nor a
    /// hidden run, so WPF's own copy stands. A paragraph with copy text (a
    /// formula, a rendered diagram) is replaced whole by its fenced source;
    /// a hidden run anywhere else (#1708: a pending diagram's name, an
    /// embedded image's name) is left out, so the rest copies as shown.</summary>
    internal string? CopyTextOf(TextPointer start, TextPointer end) => CopyOf(start, end)?.Text;

    /// <summary>A copy's text, and the changes that made it from the range
    /// (a paragraph replaced by its fenced source, a hidden run left out),
    /// in document order, for its rich text to make the same.</summary>
    private (string Text, List<(TextPointer From, TextPointer To, string Text)> Changes)? CopyOf(TextPointer start, TextPointer end)
    {
        var changes = new List<(TextPointer From, TextPointer To, string Text)>();
        CollectCopyChanges(Document.Blocks, start, end, changes);
        if (changes.Count == 0)
        {
            return null;
        }
        var text = new System.Text.StringBuilder();
        TextPointer cursor = start;
        foreach ((TextPointer from, TextPointer to, string replacement) in changes)
        {
            if (cursor.CompareTo(from) < 0)
            {
                text.Append(new TextRange(cursor, from).Text);
            }
            text.Append(replacement);
            cursor = to.CompareTo(end) < 0 ? to : end;
        }
        if (cursor.CompareTo(end) < 0)
        {
            text.Append(new TextRange(cursor, end).Text);
        }
        return (text.ToString(), changes);
    }

    /// <summary>#1708: RTF for a copy of [start, end] with
    /// <paramref name="changes"/> made (the merge judge's m3). The parts
    /// between replaced paragraphs go through a XAML package, which keeps a
    /// run's exact size and colour (RTF rounds them) and the pictures (plain
    /// XAML drops them, Codex round 1). Each replaced paragraph, a formula
    /// or a rendered diagram, becomes its fenced source in the code face,
    /// a paragraph in its place. Every run drawn transparent below a point
    /// is emptied (the census holds that those are exactly the hidden runs).
    /// Null when any step throws, and the caller falls back to plain RTF,
    /// so Ctrl+C never fails here (the merge judge's nit 2).</summary>
    private string? RichTextOfCopy(
        TextPointer start, TextPointer end, List<(TextPointer From, TextPointer To, string Text)> changes)
    {
        try
        {
            var scratch = new FlowDocument();
            TextPointer cursor = start;
            foreach ((TextPointer from, TextPointer to, string fencedSource) in changes.Where(change => change.Text.Length > 0))
            {
                AppendRichText(scratch, cursor, from);
                scratch.Blocks.Add(FencedSourceParagraph(fencedSource));
                cursor = to.CompareTo(end) < 0 ? to : end;
            }
            AppendRichText(scratch, cursor, end);
            // RTF writes a run's line breaks as nothing, so a run of several
            // lines (a pending diagram's source) becomes runs with breaks.
            foreach (Run run in RunsIn(scratch.Blocks).Where(run => run.Text.Contains('\n') || run.Text.Contains('\r')).ToList())
            {
                BreakLines(run);
            }
            // Emptied in place, not removed: a scratch document holds no keys,
            // so it needs no edit of a collection, which the focus manifest
            // would ask a witness of; an empty run writes no letter.
            foreach (Run run in RunsIn(scratch.Blocks).Where(LooksHidden).ToList())
            {
                run.Text = string.Empty;
            }
            using var rtf = new System.IO.MemoryStream();
            new TextRange(scratch.ContentStart, scratch.ContentEnd).Save(rtf, DataFormats.Rtf);
            return ShippedFontNames.InRtfFontTable(System.Text.Encoding.ASCII.GetString(rtf.ToArray()));
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>[from, to] of the reading document appended to
    /// <paramref name="scratch"/> through a XAML package, in a paragraph of
    /// its own. A part that begins at a paragraph's end, or ends at one's
    /// start, leaves that empty edge out.</summary>
    private static void AppendRichText(FlowDocument scratch, TextPointer from, TextPointer to)
    {
        if (from.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementEnd)
        {
            from = from.GetNextInsertionPosition(LogicalDirection.Forward) ?? from;
        }
        if (to.GetPointerContext(LogicalDirection.Backward) == TextPointerContext.ElementStart)
        {
            to = to.GetNextInsertionPosition(LogicalDirection.Backward) ?? to;
        }
        if (from.CompareTo(to) >= 0)
        {
            return;
        }
        using var package = new System.IO.MemoryStream();
        new TextRange(from, to).Save(package, DataFormats.XamlPackage);
        package.Position = 0;
        var holder = new Paragraph();
        scratch.Blocks.Add(holder);
        new TextRange(holder.ContentStart, holder.ContentEnd).Load(package, DataFormats.XamlPackage);
    }

    /// <summary>A scratch run of several lines as one run per line, each
    /// with the run's own formatting, a line break between them.</summary>
    private static void BreakLines(Run run)
    {
        string[] lines = run.Text.ReplaceLineEndings("\n").Split('\n');
        run.Text = lines[0];
        Inline after = run;
        for (int index = 1; index < lines.Length; index++)
        {
            var lineBreak = new LineBreak();
            run.SiblingInlines.InsertAfter(after, lineBreak);
            var next = new Run(lines[index]);
            LocalValueEnumerator values = run.GetLocalValueEnumerator();
            while (values.MoveNext())
            {
                if (values.Current.Property != Run.TextProperty && !values.Current.Property.ReadOnly)
                {
                    next.SetValue(values.Current.Property, values.Current.Value);
                }
            }
            run.SiblingInlines.InsertAfter(lineBreak, next);
            after = next;
        }
    }

    /// <summary>A formula's or a rendered diagram's fenced source as one
    /// paragraph in the code face, a line break between its lines.</summary>
    private Paragraph FencedSourceParagraph(string fencedSource)
    {
        var paragraph = new Paragraph();
        if (TryFindResource(ReadingDocumentBuilder.CodeFontKey) is System.Windows.Media.FontFamily code)
        {
            paragraph.FontFamily = code;
        }
        string[] lines = fencedSource.ReplaceLineEndings("\n").Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
            paragraph.Inlines.Add(new Run(lines[index]));
        }
        return paragraph;
    }

    private static bool LooksHidden(Run run) =>
        run.Foreground is System.Windows.Media.SolidColorBrush { Color.A: 0 } && run.FontSize < 1;

    private static IEnumerable<Run> RunsIn(IEnumerable<Block> blocks)
    {
        foreach (Block block in blocks)
        {
            IEnumerable<Run> runs = block switch
            {
                Paragraph paragraph => InlineRuns(paragraph.Inlines),
                Section section => RunsIn(section.Blocks),
                System.Windows.Documents.List list => RunsIn(list.ListItems.SelectMany(item => item.Blocks)),
                Table table => RunsIn(table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks)),
                _ => [],
            };
            foreach (Run run in runs)
            {
                yield return run;
            }
        }
    }

    private static IEnumerable<Run> InlineRuns(IEnumerable<Inline> inlines)
    {
        foreach (Inline inline in inlines)
        {
            if (inline is Run run)
            {
                yield return run;
            }
            else if (inline is Span span)
            {
                foreach (Run inner in InlineRuns(span.Inlines))
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>The parts of [start, end] a copy changes, in document
    /// order, through lists, sections and tables.</summary>
    private static void CollectCopyChanges(
        IEnumerable<Block> blocks,
        TextPointer start,
        TextPointer end,
        List<(TextPointer From, TextPointer To, string Text)> changes)
    {
        foreach (Block block in blocks)
        {
            if (block.ContentStart.CompareTo(end) >= 0)
            {
                return;
            }
            if (block.ContentEnd.CompareTo(start) <= 0)
            {
                continue;
            }
            switch (block)
            {
                case Paragraph paragraph when ReadingSemantics.CopyTextOf(paragraph) is { } fencedSource:
                    changes.Add((paragraph.ContentStart, paragraph.ContentEnd, fencedSource));
                    break;
                case Paragraph paragraph:
                    foreach (Run hidden in paragraph.Inlines.OfType<Run>().Where(ReadingSemantics.IsHiddenSpeech))
                    {
                        if (hidden.ElementEnd.CompareTo(start) > 0 && hidden.ElementStart.CompareTo(end) < 0)
                        {
                            changes.Add((hidden.ElementStart, hidden.ElementEnd, string.Empty));
                        }
                    }
                    break;
                case Section section:
                    CollectCopyChanges(section.Blocks, start, end, changes);
                    break;
                case System.Windows.Documents.List list:
                    CollectCopyChanges(list.ListItems.SelectMany(item => item.Blocks), start, end, changes);
                    break;
                case Table table:
                    CollectCopyChanges(
                        table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells).SelectMany(cell => cell.Blocks),
                        start,
                        end,
                        changes);
                    break;
            }
        }
    }

    /// <summary>#1708: RTF holding <paramref name="text"/> and nothing
    /// else: braces and backslashes escaped, a line break as a paragraph, a
    /// tab as RTF's tab (the merge judge's nit 1), no other control
    /// character, and any character past ASCII as its UTF-16 unit.</summary>
    internal static string PlainTextRtf(string text)
    {
        var rtf = new System.Text.StringBuilder(@"{\rtf1\ansi ");
        foreach (char character in text.ReplaceLineEndings("\n"))
        {
            switch (character)
            {
                case '\\' or '{' or '}':
                    rtf.Append('\\').Append(character);
                    break;
                case '\n':
                    rtf.Append(@"\par ");
                    break;
                case '\t':
                    rtf.Append(@"\tab ");
                    break;
                case < (char)0x20 or (char)0x7F:
                    break;
                case < (char)0x80:
                    rtf.Append(character);
                    break;
                default:
                    rtf.Append(@"\u").Append((short)character).Append('?');
                    break;
            }
        }
        return rtf.Append('}').ToString();
    }

    /// <summary>The embed card header Enter activates from <paramref name="paragraph"/>:
    /// the header itself, or, for an embedded image's line, which reads the
    /// card's name since #1708, its card's header.</summary>
    private static Paragraph? EmbedHeaderFor(Paragraph paragraph)
    {
        if (ReadingSemantics.EmbedHeaderKeyOf(paragraph) is not null)
        {
            return paragraph;
        }
        return paragraph.Parent is Section section
            && ReadingSemantics.IsEmbedSection(section)
            && paragraph.Inlines.OfType<InlineUIContainer>().Any(container => container.Child is ReadingDiagramImage)
                ? section.Blocks.OfType<Paragraph>().FirstOrDefault(header => ReadingSemantics.EmbedHeaderKeyOf(header) is not null)
                : null;
    }

    private static ReadingMathElement? MathElementOf(Paragraph paragraph) =>
        paragraph.Inlines
            .OfType<InlineUIContainer>()
            .Select(container => container.Child)
            .OfType<ReadingMathElement>()
            .FirstOrDefault();

    /// <summary>Test seam for the table→grid hop; production opens
    /// the substrate window. Returns whether a grid actually opened —
    /// a source core cannot re-derive cells from must not swallow
    /// Enter.</summary>
    internal Func<string, bool>? TableGridOpener { get; set; }

    private bool OpenTableGridWindow(string source) =>
        ReadingTableGrid.Show(source, Window.GetWindow(this)) is not null;

    private static Table? AncestorTableOf(Paragraph paragraph)
    {
        DependencyObject? current = paragraph;
        while (current is TextElement element)
        {
            if (current is Table table)
            {
                return table;
            }
            current = element.Parent;
        }
        return null;
    }

    /// <summary>
    /// Toggle the task checkbox on the caret's line (field gap,
    /// 2026-07-26: Space and Enter at the caret did nothing — a caret
    /// position is not element focus, so the checkbox never saw the
    /// key. Mirrors the editor's activate-at-cursor task semantics).
    /// </summary>
    internal bool TryToggleTaskAtCaret()
    {
        if (_model is not { } model || CaretPosition?.Paragraph is not { } paragraph)
        {
            return false;
        }
        foreach (System.Windows.Controls.CheckBox candidate in paragraph.Inlines
            .OfType<InlineUIContainer>()
            .Select(container => container.Child)
            .OfType<System.Windows.Controls.CheckBox>())
        {
            if (ReadingSemantics.TryDecodeTaskRange(
                candidate.Tag, out ulong start, out ulong end))
            {
                model.ToggleTaskAt(start, end);
                return true;
            }
        }
        return false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new ReadingSurfacePeer(this);
}

/// <summary>
/// The surface peer: base RichTextBox children (text pattern, native
/// hyperlinks, embedded controls) plus structural peers for the
/// semantics WPF drops — heading level, list, list item.
///
/// The tree is HIERARCHICAL: list items are children of their list's
/// peer, never flattened into the root collection. The perf spike
/// measured the flat version at 43,848 root children and ~720 ms per
/// screen-reader walk on a 6.9 MB note; hierarchy is what lets a client
/// enumerate a section instead of the note.
/// </summary>
internal sealed class ReadingSurfacePeer : RichTextBoxAutomationPeer
{
    private readonly ReadingSurface _surface;
    // WB-11 (#1329): the children a screen reader's every query asks for,
    // built once per structural generation, and one peer per list for the
    // list's life, so its item peers (and their names) outlive a streamed
    // chunk: with a client listening, WPF walks every peer after every layout
    // pass, and fresh peers for 2,000 items per chunk made a streamed note's
    // entry grow with the square of its length (measured 2026-10-28).
    private List<AutomationPeer>? _children;
    private int _childrenGeneration;
    private readonly ConditionalWeakTable<WpfList, ReadingListPeer> _lists = new();
    // WB-11 (#1330): one Text pattern provider per inner provider, so a
    // client's provider identity is stable across its queries and events.
    private System.Windows.Automation.Provider.ITextProvider? _innerText;
    private HeadingStyleTextProvider? _text;

    public ReadingSurfacePeer(ReadingSurface owner) : base(owner)
    {
        _surface = owner;
    }

    /// <summary>
    /// The Text pattern is served through the StyleId decorator so NVDA
    /// hears heading levels during ordinary line reading — WPF's own
    /// provider never emits the one attribute NVDA's linear reading
    /// keys on. Every other pattern is the base's.
    /// </summary>
    public override object GetPattern(PatternInterface patternInterface)
    {
        object pattern = base.GetPattern(patternInterface);
        if (patternInterface == PatternInterface.Text
            && pattern is System.Windows.Automation.Provider.ITextProvider text)
        {
            if (_text is null || !ReferenceEquals(_innerText, text))
            {
                _innerText = text;
                _text = new HeadingStyleTextProvider(text);
            }
            return _text;
        }
        return pattern;
    }

    /// <summary>How many times the children were built — once per
    /// structural generation however often they are asked for.</summary>
    internal int ChildrenBuildsForTests { get; private set; }

    /// <summary>
    /// The base's children (embedded elements — diagram, math and embed
    /// elements, check boxes — and hyperlinks) plus the structural peers,
    /// cached per structural generation: every change that adds or removes
    /// a block, an embedded element or a hyperlink goes through
    /// <see cref="ReadingSurface.InvalidateStructuralPeers"/>. WPF asks for
    /// these on a client's text-range queries, so building them per query
    /// cost a 2,000-block note seconds per arrow (#1329).
    /// </summary>
    protected override List<AutomationPeer> GetChildrenCore()
    {
        int generation = _surface.Structure.Value;
        if (_children is null || _childrenGeneration != generation)
        {
            _children = BuildChildren();
            _childrenGeneration = generation;
            ChildrenBuildsForTests++;
        }
        return new List<AutomationPeer>(_children);
    }

    private List<AutomationPeer> BuildChildren()
    {
        List<AutomationPeer> children = base.GetChildrenCore() ?? new List<AutomationPeer>();
        int baseCount = children.Count;
        if (((RichTextBox)Owner).Document is not FlowDocument document)
        {
            if (ReadingSurface.CensusDiagEnabled)
            {
                ReadingSurface.CensusDiag($"peer-children base={baseCount} no-flowdoc");
            }
            return children;
        }
        foreach (Block block in document.Blocks)
        {
            AppendStructural(block, children, ListPeerFor);
        }
        if (ReadingSurface.CensusDiagEnabled)
        {
            ReadingSurface.CensusDiag(
                $"peer-children base={baseCount} total={children.Count} "
                + $"blocks={document.Blocks.Count}");
        }
        return children;
    }

    private ReadingListPeer ListPeerFor(WpfList list) =>
        _lists.GetValue(list, key => new ReadingListPeer(key, _surface.Structure));

    /// <summary>
    /// A new structural generation: every list peer still in the document
    /// rebuilds its children now. WPF caches a peer's children and nothing
    /// else resets a kept list peer's cache, so without this a list a later
    /// chunk grows would go on serving its old items to clients and to
    /// WPF's own walk.
    /// </summary>
    internal void ResetListPeers()
    {
        if (_children is null || ((RichTextBox)Owner).Document is not FlowDocument document)
        {
            return;
        }
        foreach (AutomationPeer child in _children)
        {
            if (child is ReadingListPeer list)
            {
                list.ResetWithin(document);
            }
        }
    }

    private static void AppendStructural(
        Block block, List<AutomationPeer> children, Func<WpfList, ReadingListPeer> listPeer)
    {
        switch (block)
        {
            case Paragraph paragraph
                when ReadingSemantics.HeadingLevelOf(paragraph) is byte level and > 0:
                children.Add(new ReadingHeadingPeer(paragraph, level));
                break;

            case Paragraph code when ReadingSemantics.IsCodeBlock(code):
                children.Add(new ReadingCodeBlockPeer(code));
                break;

            case WpfList list when ReadingSemantics.IsList(list):
                children.Add(listPeer(list));
                break;

            case Section section:
                foreach (Block inner in section.Blocks)
                {
                    AppendStructural(inner, children, listPeer);
                }
                break;
        }
    }

    /// <summary>The structural walk over one block, with list peers of a
    /// fresh generation.</summary>
    internal static void AppendStructuralForTest(Block block, List<AutomationPeer> children)
    {
        var structure = new ReadingStructureGeneration();
        AppendStructural(block, children, list => new ReadingListPeer(list, structure));
    }
}

/// <summary>
/// WB-11 (#1329): the reading document's structural generation. Every
/// change to the document's blocks bumps it through
/// <see cref="ReadingSurface.InvalidateStructuralPeers"/>, and the peers'
/// caches — the surface's children and each list's index — are kept for
/// one generation.
/// </summary>
internal sealed class ReadingStructureGeneration
{
    internal int Value { get; private set; }

    internal void Bump() => Value++;
}

/// <summary>
/// A heading paragraph's peer. `AutomationProperties.HeadingLevel` on
/// the element does not survive onto a stock text peer (measured — the
/// spike's §10.6 item 2 answer), so the peer reports it itself.
/// </summary>
internal sealed class ReadingHeadingPeer : TextElementAutomationPeer
{
    private readonly Paragraph _paragraph;
    private readonly byte _level;

    public ReadingHeadingPeer(Paragraph paragraph, byte level) : base(paragraph)
    {
        _paragraph = paragraph;
        _level = level;
    }

    protected override AutomationHeadingLevel GetHeadingLevelCore() => _level switch
    {
        1 => AutomationHeadingLevel.Level1,
        2 => AutomationHeadingLevel.Level2,
        3 => AutomationHeadingLevel.Level3,
        4 => AutomationHeadingLevel.Level4,
        5 => AutomationHeadingLevel.Level5,
        _ => AutomationHeadingLevel.Level6,
    };

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Text;

    protected override string GetNameCore() =>
        new TextRange(_paragraph.ContentStart, _paragraph.ContentEnd).Text.Trim();

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    /// <summary>#1088: see <see cref="ReadingStructuralPeer"/>.</summary>
    protected override List<AutomationPeer> GetChildrenCore() =>
        ReadingStructuralPeer.NoChildren();
}

/// <summary>
/// A code block's peer (W3-4). Its Name is CORE's preamble ("Code
/// block, rust, 3 lines.") — the K contract's "AT preamble behind a
/// UIA peer": object navigation announces the summary, while the
/// interior stays readable through the document's text range (never
/// through this peer, which would double-speak it).
/// </summary>
internal sealed class ReadingCodeBlockPeer : TextElementAutomationPeer
{
    private readonly Paragraph _paragraph;

    public ReadingCodeBlockPeer(Paragraph paragraph) : base(paragraph)
    {
        _paragraph = paragraph;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    protected override string GetNameCore() =>
        AutomationProperties.GetName(_paragraph) ?? string.Empty;

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    /// <summary>#1088: see <see cref="ReadingStructuralPeer"/>. A fenced
    /// block's Copy link is the element at risk here.</summary>
    protected override List<AutomationPeer> GetChildrenCore() =>
        ReadingStructuralPeer.NoChildren();
}

/// <summary>
/// Why every structural peer over a TextElement refuses children
/// (#1088).
///
/// <see cref="ReadingSurfacePeer"/> returns
/// <c>RichTextBoxAutomationPeer</c>'s children — which ALREADY contains
/// every embedded control and link in the document, flattened — and
/// then appends the structural peers. <c>TextElementAutomationPeer</c>'s
/// default <c>GetChildrenCore</c> hands back the peers for the
/// UIElements inside its own element, which are the very same peer
/// INSTANCES. WPF stores one <c>_parent</c>/<c>_index</c> per peer and
/// guards sibling navigation on them, so a peer claimed by two parents
/// has its links overwritten by whichever subtree was built last.
///
/// Measured live (2026-08-07, FlaUI cross-process, 9 provider children):
/// the task CheckBox was child 1 of the surface and a child of its
/// ReadingListItemPeer. A client saw TWO children — <c>GetLastChild</c>
/// answered the 9th, <c>NextSibling</c> died at index 2, and
/// <c>PreviousSibling</c> died at the same peer coming back. A screen
/// reader loses everything past that point with no error.
///
/// Refusing children here costs nothing: the controls stay reachable in
/// the root collection, and the structural peer's own Name already
/// carries its text. It also matches the surface's documented design —
/// "the structural peer walk adds headings and lists but never links".
/// </summary>
internal static class ReadingStructuralPeer
{
    internal static List<AutomationPeer> NoChildren() => new();
}

/// <summary>
/// A list's peer, parenting one <see cref="ReadingListItemPeer"/> per
/// item — the native `List`/`ListItem` exposure the G20 owner call
/// requires and WPF's flow elements do not provide.
/// </summary>
internal sealed class ReadingListPeer : TextElementAutomationPeer
{
    private readonly WpfList _list;
    private readonly ReadingStructureGeneration _structure;
    // WB-11 (#1329): one peer per item and per nested list, for their lives,
    // so a client's per-arrow queries and WPF's walks mint nothing.
    private readonly ConditionalWeakTable<ListItem, ReadingListItemPeer> _items = new();
    private readonly ConditionalWeakTable<WpfList, ReadingListPeer> _nested = new();
    private ListSnapshot? _snapshot;

    public ReadingListPeer(WpfList list, ReadingStructureGeneration structure) : base(list)
    {
        _list = list;
        _structure = structure;
    }

    /// <summary>How many times this list was walked — once per structural
    /// generation however often it is asked about.</summary>
    internal int WalkProbeForTests { get; private set; }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.List;

    protected override string GetNameCore() => Snapshot().Name;

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override List<AutomationPeer> GetChildrenCore() =>
        new(Snapshot().Children);

    /// <summary>
    /// The list's one walk per structural generation: its items' positions,
    /// its count and name, and its children. Every read of the list's items
    /// comes here — WPF computes <c>ListItems.Count</c> by walking the
    /// collection (measured 2026-10-21: 2,000 reads on a 2,000-item list,
    /// 176 ms), so a per-query count or position walk keeps every query
    /// linear in the list (#1329).
    /// </summary>
    internal ListSnapshot Snapshot()
    {
        int generation = _structure.Value;
        if (_snapshot is { } current && current.Generation == generation)
        {
            return current;
        }
        var positions = new Dictionary<ListItem, int>(ReferenceEqualityComparer.Instance);
        var children = new List<AutomationPeer>();
        foreach (ListItem item in _list.ListItems)
        {
            positions[item] = positions.Count + 1;
            children.Add(_items.GetValue(item, key => new ReadingListItemPeer(key, this, _structure)));
            // Nested lists stay under their owning item's sibling peer —
            // structure mirrors the document, so depth is enumerable.
            foreach (Block inner in item.Blocks)
            {
                if (inner is WpfList nested && ReadingSemantics.IsList(nested))
                {
                    children.Add(_nested.GetValue(nested, key => new ReadingListPeer(key, _structure)));
                }
            }
        }
        WalkProbeForTests++;
        int count = positions.Count;
        _snapshot = new ListSnapshot(
            generation, positions, count, count == 1 ? "1 entry" : $"{count} entries", children);
        return _snapshot;
    }

    /// <summary>Rebuilds this list's children, and its nested lists', for
    /// the current generation (<see cref="ReadingSurfacePeer.ResetListPeers"/>);
    /// a list no longer in <paramref name="document"/> is left alone.</summary>
    internal void ResetWithin(FlowDocument document)
    {
        if (!_list.ContentStart.IsInSameDocument(document.ContentStart))
        {
            return;
        }
        ResetChildrenCache();
        foreach (AutomationPeer child in Snapshot().Children)
        {
            if (child is ReadingListPeer nested)
            {
                nested.ResetWithin(document);
            }
        }
    }

    internal sealed record ListSnapshot(
        int Generation,
        Dictionary<ListItem, int> Positions,
        int Count,
        string Name,
        List<AutomationPeer> Children);
}

/// <summary>One list item's peer. Its position and its set's size come
/// from its list's snapshot; its name is read once per structural
/// generation.</summary>
internal sealed class ReadingListItemPeer : TextElementAutomationPeer
{
    private readonly ListItem _item;
    private readonly ReadingListPeer _list;
    private readonly ReadingStructureGeneration _structure;
    private string? _name;
    private int _nameLength;

    /// <summary>How many times the item's text was read for its name.</summary>
    internal int NameReadsForTests { get; private set; }

    public ReadingListItemPeer(ListItem item, ReadingListPeer list, ReadingStructureGeneration structure)
        : base(item)
    {
        _item = item;
        _list = list;
        _structure = structure;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.ListItem;

    /// <summary>The item's text, an ordered item's authored marker first
    /// ("4. Fourth", #1422), read again only when the item's content length
    /// changes: the reading document changes an item in place only when a
    /// later chunk nests a list into it, and a merge replaces every item.</summary>
    protected override string GetNameCore()
    {
        int length = _item.ContentStart.GetOffsetToPosition(_item.ContentEnd);
        if (_name is null || _nameLength != length)
        {
            _name = new TextRange(_item.ContentStart, _item.ContentEnd).Text.Trim();
            _nameLength = length;
            NameReadsForTests++;
        }
        return _name;
    }

    protected override int GetPositionInSetCore() =>
        _list.Snapshot().Positions.TryGetValue(_item, out int position) ? position : -1;

    protected override int GetSizeOfSetCore()
    {
        ReadingListPeer.ListSnapshot snapshot = _list.Snapshot();
        return snapshot.Positions.ContainsKey(_item) ? snapshot.Count : -1;
    }

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    /// <summary>
    /// Not focusable: the caret owns focus in the document, and NVDA's
    /// list quick-nav condition requires non-focusable list items (the
    /// browse-mode research's verified condition for `l`).
    /// </summary>
    protected override bool IsKeyboardFocusableCore() => false;

    /// <summary>#1088: see <see cref="ReadingStructuralPeer"/>. A task
    /// item's CheckBox is the element this bug was found on.</summary>
    protected override List<AutomationPeer> GetChildrenCore() =>
        ReadingStructuralPeer.NoChildren();
}

/// <summary>WB-22 (#1112): what a <see cref="ReadingSurface.ContentChanged"/>
/// did. A replacement moves every pointer; an append only grows the tail;
/// an in-place completion rewrites one diagram paragraph's runs.</summary>
internal enum ReadingContentChange
{
    Replaced,
    Appended,
    CompletedInPlace,
}

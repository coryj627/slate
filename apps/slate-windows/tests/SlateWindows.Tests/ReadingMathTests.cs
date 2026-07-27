// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows.Automation.Peers;
using SlateWindows.Reading;
using WpfMath.Parsers;
using WpfMath.Rendering;
using XamlMath;
using XamlMath.Exceptions;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W3-2 math foundations: the MathML-over-UIA convention layer and
/// the WPFMath rendering spike.
/// </summary>
public sealed class ReadingMathTests
{
    /// <summary>
    /// THE CONDITION THE OWNER CALL ATTACHED to accepting the WPF
    /// private-reflection dependency (gap G23): if dotnet/wpf ever
    /// changes the AutomationPeer property-table shape, this fails
    /// LOUDLY here and in CI instead of the convention layer rotting
    /// silently. InactiveReason names the exact break.
    /// </summary>
    [Fact]
    public void MathMlUiaPropertyBridgeInstalls()
    {
        RunSta(() =>
        {
            MathMlUiaProperty.Initialize();
            Assert.True(
                MathMlUiaProperty.IsActive,
                $"the MathML UIA convention layer is OFF: {MathMlUiaProperty.InactiveReason}");
            Assert.True(MathMlUiaProperty.PropertyIdForTests > 0);
        });
    }

    /// <summary>
    /// The bridge answers through any peer implementing
    /// <see cref="IMathMlUiaSource"/> — verified end to end through
    /// AutomationPeer's own internal property dispatch, the exact
    /// path a UIA client request takes inside WPF.
    /// </summary>
    [Fact]
    public void RegisteredPropertyResolvesThroughThePeerTable()
    {
        RunSta(() =>
        {
            MathMlUiaProperty.Initialize();
            Assert.True(MathMlUiaProperty.IsActive, MathMlUiaProperty.InactiveReason);

            var peer = new FakeMathPeer("<math><mi>x</mi></math>");
            System.Reflection.MethodInfo dispatch = typeof(AutomationPeer).GetMethod(
                "GetPropertyValue",
                System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance,
                types: new[] { typeof(int) })!;
            object? value = dispatch.Invoke(
                peer, new object[] { MathMlUiaProperty.PropertyIdForTests });
            Assert.Equal("<math><mi>x</mi></math>", value);

            var empty = new FakeMathPeer(string.Empty);
            Assert.Null(dispatch.Invoke(
                empty, new object[] { MathMlUiaProperty.PropertyIdForTests }));
        });
    }

    /// <summary>
    /// WPFMath currency spike (w3_spec §W3-2 first task): v2.1.0
    /// parses and builds representative formulas under .NET 10, and
    /// signals unsupported input with TexException — the documented
    /// boundary the renderer degrades on.
    /// </summary>
    [Fact]
    public void WpfMathParsesRepresentativeFormulasAndSignalsGaps()
    {
        RunSta(() =>
        {
            TexFormulaParser parser = WpfTeXFormulaParser.Instance;

            // Representative supported set (docs/matrices.md).
            foreach (string supported in new[]
            {
                @"x^2 + \frac{a}{b}",
                @"\sum_{i=0}^{n} i^2",
                @"\sqrt{b^2 - 4ac}",
                @"\begin{pmatrix} a & b \\ c & d \end{pmatrix}",
                @"\text{speed} = \frac{d}{t}",
            })
            {
                TexFormula formula = parser.Parse(supported);
                TexEnvironment environment =
                    WpfTeXEnvironment.Create(TexStyle.Display, 20.0, "Arial");
                System.Windows.Media.Imaging.BitmapSource bitmap =
                    formula.RenderToBitmap(environment);
                Assert.True(
                    bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0,
                    $"'{supported}' rendered to an empty bitmap");
            }

            // The documented coverage boundary: unsupported environments
            // throw TexException — the signal the reading renderer
            // degrades on (monospace source, matrix-rowed).
            Assert.Throws<TexParseException>(() =>
                parser.Parse(@"\begin{bmatrix} 1 \\ 2 \end{bmatrix}"));
        });
    }

    /// <summary>
    /// W3-2 core rendering path over a REAL vault + MathCAT: a display
    /// fence matches its canonical artifact, renders into a focusable
    /// element whose Name is genuine MathCAT speech (not the empty
    /// fallback), carries the MathML for the convention property, and
    /// lands in the landmark index with the speech as landing text.
    /// </summary>
    [Fact]
    public void MathBlockRendersAFocusableElementSpeakingCanonicalSpeech()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-basic");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "# Math\n\n$$x^2 + 1$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            ReadingMathElement element = FindMathElements(surface.Document).Single();
            Assert.True(element.Focusable);
            string name = System.Windows.Automation.AutomationProperties.GetName(element);
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.NotEqual("Math expression.", name);
            Assert.StartsWith("<math", element.MathMl.TrimStart());

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);
            Assert.Equal(name, landmark.Text);
        });
    }

    /// <summary>
    /// The documented WPFMath coverage boundary degrades to
    /// source-in-range (never silently absent) while the block stays a
    /// nav stop — and the CORE artifacts stay retrievable (round 4: a
    /// host-only rendering failure must not hide valid braille or
    /// MathML, or lie "Braille not available." while core produced it).
    /// </summary>
    [Fact]
    public void UnrenderableMathDegradesToSourceInRange()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-gap");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "$$\\begin{bmatrix} 1 \\\\ 2 \\end{bmatrix}$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            Assert.Contains(
                "bmatrix",
                new System.Windows.Documents.TextRange(
                    surface.Document.ContentStart,
                    surface.Document.ContentEnd).Text,
                StringComparison.Ordinal);
            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);

            ReadingMathElement element =
                Assert.Single(FindMathElements(surface.Document));
            Assert.True(element.Focusable);
            Assert.StartsWith("<math", element.MathMl.TrimStart());
            Assert.NotEmpty(element.Braille);
            Assert.Equal(
                element.Braille,
                System.Windows.Automation.AutomationProperties
                    .GetItemStatus(element));

            surface.CaretPosition = landmark.Position;
            Assert.True(surface.TryActivateAtCaret(brailleRequested: true));
            Assert.Equal(
                element.Braille,
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });
    }

    /// <summary>Enter at the caret on a math block speaks the
    /// canonical landed vocabulary — "{speech}, math."</summary>
    [Fact]
    public void EnterAtTheCaretSpeaksTheMathBlock()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-enter");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "$$x^2$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);
            surface.CaretPosition = landmark.Position;
            Assert.True(surface.TryActivateAtCaret());
            string spoken = SlateUniffiMethods.A11yRender(
                Assert.Single(announced)).Text;
            Assert.EndsWith(", math.", spoken);
            Assert.Contains("squared", spoken, StringComparison.OrdinalIgnoreCase);

            // Ctrl+Enter reads the braille artifact — the accessible
            // detail the Nemeth/UEB pref selects (round 3 [high]). The
            // announced text is the element's decoded braille verbatim.
            announced.Clear();
            Assert.True(surface.TryActivateAtCaret(brailleRequested: true));
            string brailleSpoken = SlateUniffiMethods.A11yRender(
                Assert.Single(announced)).Text;
            Assert.Equal(
                Assert.Single(FindMathElements(surface.Document)).Braille,
                brailleSpoken);
            Assert.NotEmpty(brailleSpoken);
        });
    }

    /// <summary>A math block whose artifact carries no braille (core
    /// degradation, e.g. MathCAT rejecting the expression) answers
    /// Ctrl+Enter honestly instead of staying silent.</summary>
    [Fact]
    public void CtrlEnterWithoutBrailleSaysSo()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-no-braille");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "$$\\alpha < \\beta$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);
            surface.CaretPosition = landmark.Position;
            Assert.True(surface.TryActivateAtCaret(brailleRequested: true));
            Assert.Equal(
                "Braille not available.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });
    }

    /// <summary>The real math element's peer answers the registered
    /// MathML property through WPF's own dispatch and localizes its
    /// control type as "math".</summary>
    [Fact]
    public void MathElementPeerServesTheConventionProperty()
    {
        RunSta(() =>
        {
            MathMlUiaProperty.Initialize();
            Assert.True(MathMlUiaProperty.IsActive, MathMlUiaProperty.InactiveReason);

            var element = new ReadingMathElement(
                "x squared", "<math><mi>x</mi></math>", "x^2", "⠭⠘⠆");
            var peer = new ReadingMathElementPeer(element);
            Assert.Equal("math", peer.GetLocalizedControlType());
            Assert.Equal("⠭⠘⠆", peer.GetItemStatus());

            System.Reflection.MethodInfo dispatch = typeof(AutomationPeer).GetMethod(
                "GetPropertyValue",
                System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance,
                types: new[] { typeof(int) })!;
            Assert.Equal(
                "<math><mi>x</mi></math>",
                dispatch.Invoke(
                    peer, new object[] { MathMlUiaProperty.PropertyIdForTests }));
        });
    }

    /// <summary>
    /// W3-2 MathPrefs parity: preferences persist across view-model
    /// lifetimes, announce through the canonical vocabulary, reject
    /// unknown keys, and — the part nothing else covers — a change
    /// re-renders an OPEN reading projection with the new prefs
    /// (verbosity flips the MathCAT speech with identical live text).
    /// </summary>
    [Fact]
    public void MathPrefsPersistAnnounceAndReprojectOpenReadingViews()
    {
        RunSta(() =>
        {
            string directory = Path.Combine(
                Path.GetTempPath(), $"slate-math-prefs-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                var store = new AppPreferencesStore(
                    Path.Combine(directory, "preferences.json"));
                using var fixture = FixtureVault.Create(1, "reading-math-prefs");
                File.WriteAllText(
                    Path.Combine(fixture.Root, "note0.md"),
                    "$$\\frac{a}{b} + x^2$$\n");
                using var session = VaultSession.OpenFilesystem(fixture.Root);
                using var cancel = new CancelToken();
                session.ScanInitial(cancel);

                var announced = new List<A11yEvent>();
                using var workspace = new WorkspaceViewModel(
                    session,
                    fixture.Root,
                    () => [],
                    announced.Add,
                    startInteractionBackgroundWork: false,
                    preferencesStore: store);
                workspace.OpenPath("note0.md");
                WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
                tab.ToggleViewMode();
                var surface = new ReadingSurface { Model = tab.Reading };
                string mediumSpeech = System.Windows.Automation.AutomationProperties
                    .GetName(FindMathElements(surface.Document).Single());
                string nemethBraille = System.Windows.Automation.AutomationProperties
                    .GetItemStatus(FindMathElements(surface.Document).Single());
                Assert.NotEmpty(nemethBraille);

                announced.Clear();
                System.Windows.Documents.FlowDocument before = tab.Reading!.Document!;
                workspace.EditorPreferences.SetMathVerbosityCommand.Execute("terse");
                Assert.Contains(
                    announced,
                    item => SlateUniffiMethods.A11yRender(item).Text
                        == "Math verbosity: Terse.");
                // The braille code flips the artifact bytes for EVERY
                // formula (verbosity speech can coincide), so it is the
                // deterministic proof that a prefs change reaches the
                // session AND re-projects the open reading view: the
                // content digest misses the memo and the document
                // rebuilds with identical live text.
                workspace.EditorPreferences.SetMathBrailleCodeCommand.Execute("ueb");
                Assert.Contains(
                    announced,
                    item => SlateUniffiMethods.A11yRender(item).Text
                        == "Math braille code: UEB.");
                Assert.NotSame(before, tab.Reading!.Document);
                Assert.Equal(
                    mediumSpeech.Length > 0,
                    System.Windows.Automation.AutomationProperties
                        .GetName(FindMathElements(surface.Document).Single())
                        .Length > 0);
                // Round 3 [high]: the pref must change something an AT
                // user can RETRIEVE, not merely rebuild the document —
                // the element's ItemStatus carries the decoded braille,
                // and Nemeth vs UEB cells differ for this formula.
                string uebBraille = System.Windows.Automation.AutomationProperties
                    .GetItemStatus(FindMathElements(surface.Document).Single());
                Assert.NotEmpty(uebBraille);
                Assert.NotEqual(nemethBraille, uebBraille);

                // Unknown key: no change, no announcement.
                announced.Clear();
                workspace.EditorPreferences.SetMathVerbosityCommand.Execute("shouty");
                Assert.Empty(announced);

                // Persistence across lifetimes.
                using var second = new EditorPreferencesViewModel(
                    _ => { },
                    new FakeEditorSpellingService(),
                    preferencesStore: store);
                Assert.True(second.IsMathVerbosityTerse);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    /// <summary>
    /// W3-2 round 1 [high]: a same-position unsaved edit keeps byte
    /// containment, and speaking the OLD equation would be a lie —
    /// the artifact only applies under live-source coherence; the
    /// stale case degrades to the live source in range.
    /// </summary>
    [Fact]
    public void UnsavedMathEditsNeverSpeakTheStaleFormula()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-stale");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "$$x^2 + 1$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };
            Assert.Single(FindMathElements(surface.Document));

            // Same-length, same-position unsaved edit inside the fence.
            tab.EditorDocument!.Replace(
                tab.Text.IndexOf("x^2 + 1", StringComparison.Ordinal), 7, "y^3 - 2");
            tab.Reading!.Refresh();

            Assert.Empty(FindMathElements(surface.Document));
            string text = new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains("y^3 - 2", text, StringComparison.Ordinal);
            Assert.DoesNotContain("x^2 + 1", text, StringComparison.Ordinal);
            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);
            Assert.Contains("y^3", landmark.Text, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// W3-2 round 1 [medium]: a prefs change must NOT reactivate
    /// hidden projections — an unbound model keeps its document and
    /// starts no background work; it rebuilds with the new prefs on
    /// its next genuine bind.
    /// </summary>
    [Fact]
    public void PrefsChangesLeaveHiddenProjectionsAloneUntilRebind()
    {
        RunSta(() =>
        {
            string directory = Path.Combine(
                Path.GetTempPath(), $"slate-math-hidden-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                var store = new AppPreferencesStore(
                    Path.Combine(directory, "preferences.json"));
                using var fixture = FixtureVault.Create(1, "reading-math-hidden");
                File.WriteAllText(
                    Path.Combine(fixture.Root, "first.md"), "$$a+b$$\n");
                File.WriteAllText(
                    Path.Combine(fixture.Root, "second.md"), "$$c+d$$\n");
                using var session = VaultSession.OpenFilesystem(fixture.Root);
                using var cancel = new CancelToken();
                session.ScanInitial(cancel);

                using var workspace = new WorkspaceViewModel(
                    session,
                    fixture.Root,
                    () => [],
                    _ => { },
                    startInteractionBackgroundWork: false,
                    preferencesStore: store);
                workspace.OpenPath("first.md");
                WorkspaceTabViewModel hidden = workspace.ActiveGroup.ActiveTab!;
                hidden.ToggleViewMode();
                System.Windows.Documents.FlowDocument hiddenDocument =
                    hidden.Reading!.Document!;

                // No surface bound anywhere: the pref flip must not
                // touch the hidden projection.
                workspace.EditorPreferences.SetMathBrailleCodeCommand.Execute("ueb");
                Assert.Same(hiddenDocument, hidden.Reading!.Document);

                // The next genuine bind rebuilds with the new prefs.
                var surface = new ReadingSurface { Model = hidden.Reading };
                Assert.NotSame(hiddenDocument, hidden.Reading!.Document);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    /// <summary>
    /// W3-2 round 2 [medium]: a prefs change while an UNBOUND model's
    /// refresh is in flight poisons that generation — the stale-prefs
    /// publish is rejected, and the next bind restarts fresh instead
    /// of adopting the obsolete artifact (set_math_prefs advances no
    /// session generation, so nothing else would catch it).
    /// </summary>
    [Fact]
    public void PrefsChangeDuringHiddenInFlightRefreshNeverPublishesStale()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-race");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "$$a+b$$ body\n\n# H\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            using var reading = new SlateWindows.Reading.ReadingContentViewModel(
                session, tab, _ => { });

            // Gate the first (hidden) fetch so the prefs change lands
            // while it is in flight.
            using var gate = new ManualResetEventSlim(initialState: false);
            int fetches = 0;
            reading.FetchFaultForTests = () =>
            {
                Interlocked.Increment(ref fetches);
                gate.Wait(TimeSpan.FromSeconds(20));
                return null;
            };

            reading.Refresh();
            reading.InvalidateForPrefsChange();

            // Bind BEFORE the old fetch is released: the poisoned
            // generation means EnsureProjected must start a fresh
            // refresh rather than waiting on the doomed one.
            var surface = new ReadingSurface { Model = reading };
            gate.Set();
            WaitForUi(() => reading.Document is not null);
            WaitForUi(() =>
                new System.Windows.Documents.TextRange(
                    surface.Document.ContentStart,
                    surface.Document.ContentEnd).Text.Contains(
                        "body", StringComparison.Ordinal));
            Assert.True(
                fetches >= 2,
                $"expected the poisoned fetch plus a fresh one, saw {fetches}");
        });
    }

    /// <summary>W3-2 round 2 [medium]: a persisted "mathSpeak" (older
    /// build or hand-edited file) must restore as ClearSpeak — the
    /// disabled style may never come back checked (#1056).</summary>
    [Fact]
    public void PersistedMathSpeakRestoresAsClearSpeak()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), $"slate-mathspeak-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "preferences.json");
            File.WriteAllText(path, "{\"mathSpeechStyle\":\"mathSpeak\"}");
            var store = new AppPreferencesStore(path);
            Assert.Equal("clearSpeak", store.Load().MathSpeechStyle);

            using var preferences = new EditorPreferencesViewModel(
                _ => { },
                new FakeEditorSpellingService(),
                preferencesStore: store);
            Assert.True(preferences.IsMathSpeechClearSpeak);
            Assert.False(preferences.IsMathSpeechMathSpeak);
            Assert.Equal(
                MathSpeechStyle.ClearSpeak,
                preferences.CurrentMathPrefs.SpeechStyle);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>W3-2 round 1 [medium]: MathSpeak is unimplemented
    /// upstream (#1056) — selecting it is rejected with no false
    /// confirmation, and the style stays ClearSpeak.</summary>
    [Fact]
    public void MathSpeakIsNotSelectable()
    {
        var announced = new List<A11yEvent>();
        using var preferences = new EditorPreferencesViewModel(
            announced.Add,
            new FakeEditorSpellingService());
        Assert.True(preferences.IsMathSpeechClearSpeak);

        preferences.SetMathSpeechStyleCommand.Execute("mathSpeak");
        Assert.True(preferences.IsMathSpeechClearSpeak);
        Assert.False(preferences.IsMathSpeechMathSpeak);
        Assert.Empty(announced);
    }

    private static IEnumerable<ReadingMathElement> FindMathElements(
        System.Windows.Documents.FlowDocument document)
    {
        foreach (System.Windows.Documents.Block block in document.Blocks)
        {
            if (block is not System.Windows.Documents.Paragraph paragraph)
            {
                continue;
            }
            foreach (System.Windows.Documents.Inline inline in paragraph.Inlines)
            {
                if (inline is System.Windows.Documents.InlineUIContainer
                    {
                        Child: ReadingMathElement element,
                    })
                {
                    yield return element;
                }
            }
        }
    }

    private sealed class FakeMathPeer
        : FrameworkElementAutomationPeer, IMathMlUiaSource
    {
        public FakeMathPeer(string mathMl)
            : base(new System.Windows.Controls.Border())
        {
            MathMl = mathMl;
        }

        public string MathMl { get; }
    }

    /// <summary>Pump the STA dispatcher until the condition holds.</summary>
    private static void WaitForUi(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Asynchronous action timed out.");
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Thread.Yield();
        }
    }

    /// <summary>WPF objects require STA; xunit runs MTA.</summary>
    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

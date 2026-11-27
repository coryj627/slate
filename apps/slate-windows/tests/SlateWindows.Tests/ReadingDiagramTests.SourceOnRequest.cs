// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-20b (CD12(d), the twin of #1591): a diagram's source is spoken
/// on request, by the reading view's source chord (Ctrl+Shift+Enter) through
/// core's <c>DiagramSourceSpoken</c>, at the caret and on a focused diagram;
/// no HelpText and no keyboard tooltip carries it to Tab.
/// </summary>
public sealed partial class ReadingDiagramTests
{
    /// <summary>A diagram in each completed state: drawn, an unknown kind, a
    /// known kind whose render fails (core's test-fault seam names it by
    /// "cb20bpanic"), and an empty fence.</summary>
    private const string SourceStatesNote =
        "# Diagram\n\nBefore.\n\n```mermaid\nflowchart LR\nA --> B\nB --> C\n```\n\n"
        + "Between.\n\n```mermaid\nweirdDiagram\nstuff\n```\n\n"
        + "Between.\n\n```mermaid\nflowchart LR\ncb20bpanic --> B\n```\n\n"
        + "Between.\n\n```mermaid\n```\n\nThe end.\n";

    private const string EmptyDiagramNote = "# Diagram\n\nBefore.\n\n```mermaid\n```\n\nAfter.\n";

    private const string StageSourceSentence = "Source: flowchart LR\nA --> B\nB --> C.";

    private const string UnsupportedSourceSentence = "Source: weirdDiagram\nstuff.";

    [Fact]
    public void TabOntoADiagramFindsNoHelpTextAndNoKeyboardTooltip()
    {
        RunSta(() =>
        {
            using (EnvFaultLock.Acquire())
            {
                Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_DIAGRAM_PANIC", "cb20bpanic");
                try
                {
                    using var harness = StageHarness.Open("reading-diagram-tab-source", SourceStatesNote, hosted: true);
                    var wrong = new List<string>();
                    var seen = new List<string>();
                    Judge("pending");
                    harness.Land();
                    Judge("landed");
                    // Premise: the seam failed the known kind, so the row is
                    // a failed diagram and not a second drawn one.
                    ReadingDiagramElement knownFailed = FindDiagramElements(harness.Surface.Document)
                        .Single(element => AutomationProperties.GetName(element) == "Flowchart with 1 step.");
                    Assert.Null(knownFailed.Content);
                    Assert.Contains("Diagram could not be rendered", harness.PatternText(), StringComparison.Ordinal);

                    Assert.True(wrong.Count == 0, "A diagram carries its source to Tab:\n  " + string.Join("\n  ", wrong));
                    Assert.Equal(
                        new[]
                        {
                            "pending: Diagram, rendering",
                            "pending: Diagram, rendering",
                            "pending: Diagram, rendering",
                            "pending: Diagram, rendering",
                            "landed: Flowchart with 2 steps.",
                            "landed: Mermaid diagram, source:\nweirdDiagram\nstuff",
                            "landed: Flowchart with 1 step.",
                            "landed: Mermaid diagram, empty source.",
                        },
                        seen);

                    void Judge(string state)
                    {
                        int tooltips = 0;
                        foreach (ReadingDiagramElement element in FindDiagramElements(harness.Surface.Document))
                        {
                            string name = AutomationProperties.GetName(element);
                            seen.Add($"{state}: {name}");
                            string where = $"{state} \"{name}\"";
                            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(element);
                            if (AutomationProperties.GetHelpText(element).Length > 0)
                            {
                                wrong.Add($"{where}: HelpText \"{AutomationProperties.GetHelpText(element)}\"");
                            }
                            if (peer.GetHelpText().Length > 0)
                            {
                                wrong.Add($"{where}: the peer's HelpText \"{peer.GetHelpText()}\"");
                            }
                            if (peer.GetChildren() is { Count: > 0 } children)
                            {
                                wrong.Add($"{where}: the peer has {children.Count} children");
                            }
                            if (ToolTipService.GetShowsToolTipOnKeyboardFocus(element) != false)
                            {
                                wrong.Add($"{where}: its tooltip opens on keyboard focus");
                            }
                            if (element.ToolTip is string { Length: > 0 })
                            {
                                tooltips++;
                            }
                        }
                        // The preview stays for a pointer's hover: every
                        // diagram with a source keeps it.
                        if (tooltips != 3)
                        {
                            wrong.Add($"{state}: {tooltips} diagrams kept a hover preview, not 3");
                        }
                    }
                }
                finally
                {
                    Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_DIAGRAM_PANIC", null);
                }
            }
        });
    }

    /// <summary>The spec red team's B1: a placeholder focused when its stage
    /// finds no artifact keeps focus, and is named by the builder's fallback,
    /// not by its source, which a name change would speak unasked. The source
    /// is still there on request.</summary>
    [Fact]
    public void AFocusedPlaceholderWithNoMatchIsNamedByTheFallback()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open(
                "reading-diagram-nomatch-name",
                StageNote,
                model => model.DiagramFaultForTests = () => new VaultException.Io("injected"),
                hosted: true);
            ReadingDiagramElement placeholder = harness.Placeholder();
            Assert.True(placeholder.Focus(), "premise: the placeholder takes focus");
            harness.Land();

            Assert.Same(placeholder, harness.Placeholder());
            Assert.True(placeholder.IsKeyboardFocused);
            string name = AutomationProperties.GetName(placeholder);
            Assert.Equal("Mermaid diagram.", name);
            Assert.DoesNotContain("flowchart", name, StringComparison.OrdinalIgnoreCase);

            harness.Announced.Clear();
            Assert.True(harness.Surface.TryAnswerDiagram(placeholder, ModifierKeys.Control | ModifierKeys.Shift));
            Assert.Equal(StageSourceSentence, SlateUniffiMethods.A11yRender(Assert.Single(harness.Announced)).Text);
        });
    }

    /// <summary>Ctrl+Shift+Enter at the caret on a diagram, in each state,
    /// says core's source sentence once: from the paragraph's start, and from
    /// inside its source where the source is in range.</summary>
    [Theory]
    [InlineData("pending")]
    [InlineData("rendered")]
    [InlineData("failed")]
    [InlineData("unmatched")]
    public void CtrlShiftEnterSpeaksADiagramsSourceAtTheCaret(string state)
    {
        RunSta(() =>
        {
            bool failed = state == "failed";
            using var harness = StageHarness.Open(
                $"reading-diagram-source-{state}",
                failed ? UnsupportedNote : StageNote,
                state == "unmatched" ? model => model.DiagramFaultForTests = () => new VaultException.Io("injected") : null);
            if (state != "pending")
            {
                harness.Land();
            }

            var positions = new List<TextPointer>
            {
                harness.DiagramParagraph().ContentStart.GetInsertionPosition(LogicalDirection.Forward),
            };
            if (state != "rendered")
            {
                positions.Add(PositionAtText(harness.Surface.Document, failed ? "stuff" : "B --> C"));
            }
            foreach (TextPointer position in positions)
            {
                harness.Announced.Clear();
                harness.Surface.CaretPosition = position;
                Assert.True(harness.Surface.TrySpeakSourceAtCaret(), $"{state}: the chord declined on a diagram");
                A11yEvent spoken = Assert.Single(harness.Announced);
                Assert.IsType<A11yEvent.DiagramSourceSpoken>(spoken);
                Assert.Equal(
                    failed ? UnsupportedSourceSentence : StageSourceSentence,
                    SlateUniffiMethods.A11yRender(spoken).Text);
            }
        });
    }

    /// <summary>The spec red team's M2: a projection after the first landing
    /// builds the diagram complete (the landed outcome, no stage), and the
    /// chord there says the same sentence.</summary>
    [Theory]
    [InlineData("rendered")]
    [InlineData("failed")]
    public void CtrlShiftEnterOnAFreshBuildSpeaksTheSource(string state)
    {
        RunSta(() =>
        {
            bool failed = state == "failed";
            using var harness = StageHarness.Open(
                $"reading-diagram-source-fresh-{state}", failed ? UnsupportedNote : StageNote, hosted: true);
            harness.Land();
            Paragraph landed = harness.DiagramParagraph();
            harness.Tab.EditorDocument!.Insert(
                harness.Tab.EditorDocument.TextLength, "\nA paragraph added at the end.\n");
            harness.Model.Refresh();
            PumpedDispatcher.Drain();
            Paragraph fresh = harness.DiagramParagraph();
            Assert.NotSame(landed, fresh);
            Assert.Null(ReadingSemantics.PendingDiagramKeyOf(fresh));

            harness.Announced.Clear();
            harness.Surface.CaretPosition = fresh.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            Assert.True(harness.Surface.TrySpeakSourceAtCaret());
            Assert.Equal(
                failed ? UnsupportedSourceSentence : StageSourceSentence,
                SlateUniffiMethods.A11yRender(Assert.Single(harness.Announced)).Text);
        });
    }

    /// <summary>The chord says the same exact sentence before a diagram lands
    /// and after it lands in place.</summary>
    [Fact]
    public void TheSourceSurvivesACompletionInPlace()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-source-in-place", StageNote);
            Paragraph pending = harness.DiagramParagraph();
            harness.Surface.CaretPosition = pending.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            Assert.True(harness.Surface.TrySpeakSourceAtCaret());
            harness.Land();
            Assert.Same(pending, harness.DiagramParagraph());
            harness.Surface.CaretPosition = pending.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            Assert.True(harness.Surface.TrySpeakSourceAtCaret());

            Assert.Equal(
                new[] { StageSourceSentence, StageSourceSentence },
                harness.Announced
                    .OfType<A11yEvent.DiagramSourceSpoken>()
                    .Select(spoken => SlateUniffiMethods.A11yRender(spoken).Text));
        });
    }

    /// <summary>An empty fence has no source to speak: core's event carries
    /// none, and says so.</summary>
    [Fact]
    public void CtrlShiftEnterOnAnEmptyDiagramSaysSourceNotAvailable()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-source-empty", EmptyDiagramNote);
            harness.Land();
            harness.Surface.CaretPosition = harness.DiagramParagraph().ContentStart
                .GetInsertionPosition(LogicalDirection.Forward);
            harness.Announced.Clear();
            Assert.True(harness.Surface.TrySpeakSourceAtCaret());
            A11yEvent.DiagramSourceSpoken spoken = Assert.IsType<A11yEvent.DiagramSourceSpoken>(Assert.Single(harness.Announced));
            Assert.Null(spoken.Source);
            Assert.Equal("Source not available.", SlateUniffiMethods.A11yRender(spoken).Text);
        });
    }

    /// <summary>Off a formula or a diagram the chord declines, so the key
    /// falls through and nothing is said.</summary>
    [Fact]
    public void CtrlShiftEnterOffASourceBlockDeclines()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-source-off", StageNote);
            harness.Land();
            harness.Announced.Clear();
            foreach (string text in new[] { "Diagram", "Before the diagram.", "After the diagram." })
            {
                harness.Surface.CaretPosition = PositionAtText(harness.Surface.Document, text);
                Assert.False(harness.Surface.TrySpeakSourceAtCaret(), $"the chord answered on \"{text}\"");
            }
            Assert.Empty(harness.Announced);
        });
    }

    /// <summary>With focus on a diagram (Tab), Ctrl+Shift+Enter speaks its
    /// source; every other modifier state declines, so Enter and Ctrl+Enter
    /// stay as they were (the lead's ruling on CB-20b's Call 3).</summary>
    [Theory]
    [InlineData("pending")]
    [InlineData("rendered")]
    [InlineData("failed")]
    public void AFocusedDiagramAnswersCtrlShiftEnterOnly(string state)
    {
        RunSta(() =>
        {
            bool failed = state == "failed";
            using var harness = StageHarness.Open(
                $"reading-diagram-source-focused-{state}", failed ? UnsupportedNote : StageNote);
            if (state != "pending")
            {
                harness.Land();
            }
            ReadingDiagramElement element = harness.Placeholder();
            harness.Announced.Clear();
            foreach (ModifierKeys declined in new[]
            {
                ModifierKeys.None, ModifierKeys.Control, ModifierKeys.Shift, ModifierKeys.Alt,
                ModifierKeys.Control | ModifierKeys.Alt,
            })
            {
                Assert.False(harness.Surface.TryAnswerDiagram(element, declined), $"{state}: {declined} was answered");
            }
            Assert.Empty(harness.Announced);
            Assert.True(harness.Surface.TryAnswerDiagram(element, ModifierKeys.Control | ModifierKeys.Shift));
            Assert.Equal(
                failed ? UnsupportedSourceSentence : StageSourceSentence,
                SlateUniffiMethods.A11yRender(Assert.Single(harness.Announced)).Text);
        });
    }

    /// <summary>Enter with focus on a diagram, through the surface's own key
    /// route (step 0's H6), is not taken and says nothing, as on main.</summary>
    [Fact]
    public void EnterOnAFocusedDiagramStaysSilent()
    {
        RunSta(() =>
        {
            using var harness = StageHarness.Open("reading-diagram-source-enter", StageNote, hosted: true);
            harness.Land();
            ReadingDiagramElement element = harness.Placeholder();
            Assert.True(element.Focus(), "premise: the diagram takes focus");
            // The route reads the live keyboard: a modifier held on the shared
            // desktop would make this another request.
            Assert.Equal(ModifierKeys.None, Keyboard.Modifiers);
            harness.Announced.Clear();
            var enter = new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(element)!, 0, Key.Return)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            element.RaiseEvent(enter);
            PumpedDispatcher.Drain();
            Assert.False(enter.Handled);
            Assert.Empty(harness.Announced);
        });
    }
}

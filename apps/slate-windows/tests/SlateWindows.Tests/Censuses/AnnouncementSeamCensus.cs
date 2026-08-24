// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-1 PR A (#745): the production announcement wiring, asserted as a
// CHAIN.
//
// Two seams reach a canvas announcement — the event seam every other
// surface uses, and the rendered-pair seam the canvas coalescer needs
// (contract A5) — and every hop between MainWindow and CanvasAnnouncer
// defaults to a no-op when its argument is absent. That default is
// correct for headless facts and catastrophic in production: PR A
// shipped its first cut with `announceRendered` missing from
// MainWindow's construction, so every canvas announcement fell into
// `?? (_ => { })` and died silently, while every unit fact stayed green
// because each one injects its own sink.
//
// A test-injected sink can never catch that. This census reads the
// SHIPPING call expressions instead.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "announcement-seams")]
public sealed class AnnouncementSeamCensus
{
    /// <summary>
    /// Hop 1 — <c>MainWindow</c> hands the dispatcher to the vault
    /// lifecycle on BOTH seams. Mutation-verified: deleting the
    /// <c>announceRendered:</c> argument fails here naming it.
    /// </summary>
    [Fact]
    public void MainWindowThreadsBothAnnouncementSeamsFromTheDispatcher()
    {
        CSharpSource source = CSharpSource.Load("MainWindow.xaml.cs");
        InvocationOrCreationArguments construction = ConstructionOf(
            source, "VaultLifecycleViewModel");

        foreach (string seam in new[] { "announce", "announceRendered" })
        {
            ArgumentSyntax argument = construction.Named(seam);
            // The VALUE, not just the presence: a `_ => { }` passed here
            // would satisfy a name check and post nothing.
            Assert.True(
                argument.Expression is MemberAccessExpressionSyntax access
                    && access.Expression is IdentifierNameSyntax { Identifier.ValueText: "_announcer" }
                    && access.Name.Identifier.ValueText == "Post",
                $"MainWindow must pass `{seam}: _announcer.Post` — the canonical "
                + $"dispatcher — and passes `{argument.Expression}` instead.");
        }
    }

    /// <summary>
    /// Hop 2 — the vault lifecycle forwards the rendered seam it was
    /// given to the workspace. A lifecycle that stored the seam and
    /// then built the workspace without it would leave hop 1 true and
    /// the chain broken.
    /// </summary>
    [Fact]
    public void TheVaultLifecycleForwardsTheRenderedSeamToTheWorkspace()
    {
        CSharpSource source = CSharpSource.Load("VaultLifecycleViewModel.cs");
        InvocationOrCreationArguments construction = ConstructionOf(
            source, "WorkspaceViewModel");
        ArgumentSyntax argument = construction.Named("announceRendered");
        Assert.True(
            argument.Expression is IdentifierNameSyntax
            {
                Identifier.ValueText: "_announceRendered",
            },
            "the vault lifecycle must forward its own stored rendered seam; it "
            + $"forwards `{argument.Expression}`.");
    }

    /// <summary>
    /// Hop 3 — the canvas registry builds every announcer over the
    /// workspace's rendered seam, and over nothing else. This is the
    /// hop that would otherwise be satisfied by a fresh
    /// <c>new CanvasAnnouncer(_ => { })</c>.
    /// </summary>
    [Fact]
    public void TheCanvasRegistryBuildsEveryAnnouncerOverTheWorkspaceSeam()
    {
        CSharpSource source = CSharpSource.Load("Canvas", "WorkspaceViewModel.Canvas.cs");
        ObjectCreationExpressionSyntax[] announcers = source.Root
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => creation.Type.ToString() == "CanvasAnnouncer")
            .ToArray();

        Assert.True(
            announcers.Length == 1,
            $"the canvas registry constructs {announcers.Length} announcers; one "
            + "construction site is what makes the seam checkable at all.");
        ArgumentSyntax argument = Assert.IsType<ArgumentSyntax>(
            announcers[0].ArgumentList?.Arguments.FirstOrDefault());
        Assert.True(
            argument.Expression is IdentifierNameSyntax
            {
                Identifier.ValueText: "_announceRendered",
            },
            "every canvas announcer must post through the workspace's rendered "
            + $"seam; this one posts through `{argument.Expression}`.");
    }

    /// <summary>
    /// And the runtime half: a canvas load drives a REAL
    /// <see cref="AccessibilityNotificationDispatcher"/> over a real,
    /// shown element — the production sink, raising a real UIA
    /// notification, not a lambda standing in for one.
    /// </summary>
    /// <remarks>
    /// What this proves and what it does not, stated exactly. The
    /// recording wrapper is an OBSERVER in front of the dispatcher, not
    /// a replacement for it: the rendered line reaches
    /// <c>Post(RenderedAnnouncement)</c>, which renders no second time,
    /// resolves the peer and raises. What no in-process fact can assert
    /// is that a screen reader HEARD it — `RaiseNotificationEvent` is a
    /// no-op without a listening UIA client, which is the FlaUI
    /// journeys' job. So this fact pins the chain end to end through
    /// production types, and the three syntax facts above pin that the
    /// shipping code builds that chain.
    /// </remarks>
    [Fact]
    public void ACanvasLoadPostsThroughARealDispatcher() => RunSta(() =>
    {
        string root = Path.Combine(
            Path.GetTempPath(), $"slate-seam-census-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // Entries core preserves but cannot show: the one canvas
            // load that speaks without any user interaction (A4).
            File.WriteAllText(
                Path.Combine(root, "skipped.canvas"),
                "{\"nodes\":["
                + "{\"id\":\"kept\",\"type\":\"text\",\"text\":\"kept\","
                + "\"x\":0,\"y\":0,\"width\":100,\"height\":50},"
                + "{\"id\":\"no-x\",\"type\":\"text\",\"text\":\"no x\","
                + "\"y\":0,\"width\":100,\"height\":50}],\"edges\":[]}");
            using var session = uniffi.slate_uniffi.VaultSession.OpenFilesystem(root);
            using (var cancel = new uniffi.slate_uniffi.CancelToken())
            {
                session.ScanInitial(cancel);
            }

            var element = new System.Windows.Controls.TextBlock();
            var host = new System.Windows.Window
            {
                Content = element,
                Width = 400,
                Height = 300,
                ShowInTaskbar = false,
                WindowStyle = System.Windows.WindowStyle.None,
                ShowActivated = false,
            };
            var raised = new List<string>();
            try
            {
                host.Show();
                var dispatcher = new AccessibilityNotificationDispatcher(element);
                using var workspace = new WorkspaceViewModel(
                    session,
                    root,
                    () => [],
                    dispatcher.Post,
                    startInteractionBackgroundWork: false,
                    announceRendered: rendered =>
                    {
                        raised.Add(rendered.Text);
                        dispatcher.Post(rendered);
                    });
                workspace.OpenPath("skipped.canvas");
                Canvas.CanvasDocumentViewModel document =
                    Assert.IsType<Canvas.CanvasDocumentViewModel>(
                        workspace.ActiveGroup.ActiveTab!.Canvas);
                document.Announcer.FlushForTests();

                string spoken = Assert.Single(raised);
                Assert.Equal(document.DegradedBannerText, spoken);
            }
            finally
            {
                host.Close();
            }
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    });

    /// <summary>The arguments of the one construction of
    /// <paramref name="typeName"/> in this file. Ambiguity fails: this
    /// query reads one, so a second would go unchecked.</summary>
    private static InvocationOrCreationArguments ConstructionOf(
        CSharpSource source, string typeName)
    {
        ObjectCreationExpressionSyntax[] creations = source.Root
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => creation.Type.ToString() == typeName)
            .ToArray();
        Assert.True(
            creations.Length == 1,
            $"{source.Path} constructs {typeName} {creations.Length} times; this "
            + "census reads one, so the rest would go unchecked.");
        return new InvocationOrCreationArguments(
            typeName,
            creations[0].ArgumentList?.Arguments
                ?? throw new Xunit.Sdk.XunitException(
                    $"the {typeName} construction has no argument list."));
    }

    private sealed class InvocationOrCreationArguments(
        string typeName, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        internal ArgumentSyntax Named(string name)
        {
            ArgumentSyntax? match = arguments.FirstOrDefault(
                argument => argument.NameColon?.Name.Identifier.ValueText == name);
            Assert.True(
                match is not null,
                $"the {typeName} construction passes no `{name}:` argument. Every "
                + "announcement seam defaults to a no-op when its argument is "
                + "absent, so the omission is silent at run time and green in "
                + "every fact that injects its own sink.");
            return match!;
        }
    }

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA census body timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

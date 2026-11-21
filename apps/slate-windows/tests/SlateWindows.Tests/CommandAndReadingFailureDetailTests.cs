// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using SlateWindows.Commands;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-15c (#1890): the reading view's embedded base card and the
/// palette's command failure say core's detail, never the binding's field
/// dump (<c>@message=…</c>, <c>@path=…</c>). Each fact drives the read's real
/// catch with a real core refusal over a real vault: an embedded base core
/// cannot execute, which ordinary input reaches (an empty base file, one with
/// no views, one that does not parse), and a command whose action lets a
/// vault error escape, run through the palette's own path (the registry,
/// <c>SlateCommandRegistrar.RegisterAll</c> and <c>InvokeById</c>). Each oracle
/// is the literal core writes; both texts are fixed and locale-free.
/// </summary>
public sealed class CommandAndReadingFailureDetailTests
{
    /// <summary>A binding field token: a uniffi exception's Message, never
    /// core's sentence.</summary>
    private const string FieldToken = @"@\w+=";

    /// <summary>Core's words when a base has no view to run: the empty, the
    /// view-less and the unparseable base each parse to zero views.</summary>
    private const string NoViewDetail = "base view 0 is out of range";

    private static VaultSession Scanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private static string DocumentText(ReadingContentViewModel reading)
    {
        System.Windows.Documents.FlowDocument document =
            Assert.IsType<System.Windows.Documents.FlowDocument>(reading.Document);
        return new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd).Text;
    }

    /// <summary>A note embedding a base core cannot execute, shown in reading
    /// mode: the base card's body says core's detail behind the reading
    /// view's lead, and the document holds no field token.</summary>
    [Theory]
    [InlineData("empty", "")]
    [InlineData("no-views", "filters: 'file.ext == \"md\"'\n")]
    [InlineData("unparseable", "views: [\n  - type: table\n")]
    public void AnEmbeddedBaseCoreCannotExecuteSaysCoresDetailInTheReadingView(string label, string baseText) =>
        StaThread.Run(
            () =>
            {
                using FixtureVault fixture = FixtureVault.Create(1, $"cb15c-base-embed-{label}");
                File.WriteAllText(Path.Combine(fixture.Root, "Probe.base"), baseText);
                File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "# Host\n\n![[Probe.base]]\n");
                PumpedDispatcher.Run(() =>
                {
                    using VaultSession session = Scanned(fixture.Root);
                    ulong handle = session.OpenBase("Probe.base");
                    try
                    {
                        using var cancel = new CancelToken();
                        Assert.Equal(
                            NoViewDetail,
                            SlateUniffiMethods.VaultErrorDetail(Assert.Throws<VaultException.InvalidArgument>(
                                () => session.BaseExecute(handle, 0, "host.md", null, cancel))));
                    }
                    finally
                    {
                        session.CloseBase(handle);
                    }
                    using var tab = new WorkspaceTabViewModel(
                        session,
                        new WorkspaceTabState(Guid.NewGuid(), new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                        announce: _ => { },
                        startInteractionBackgroundWork: true);

                    tab.ToggleViewMode();

                    ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
                    Assert.True(
                        PumpedDispatcher.PumpUntil(() => !reading.IsLoading && reading.Document is not null, TimeSpan.FromSeconds(30)),
                        "the projection never landed");
                    string text = DocumentText(reading);
                    string card = Assert.Single(
                        text.Split('\n').Select(line => line.Trim()),
                        line => line.Contains("could not be executed", StringComparison.Ordinal));
                    Assert.Equal($"This base could not be executed: {NoViewDetail}", card);
                    Assert.DoesNotMatch(FieldToken, text);
                });
            },
            TimeSpan.FromSeconds(90),
            "the embedded base fact timed out");

    /// <summary>A command whose action lets core's refusal escape (here an
    /// action creating a folder over an existing note), invoked through the
    /// palette's path: the action failure carries core's detail, and the
    /// palette's sentence for it says that detail at High.</summary>
    [Fact]
    public void AVaultErrorACommandLetsEscapeSaysCoresDetailThroughThePalette()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "cb15c-palette-command");
        using VaultSession session = Scanned(fixture.Root);
        var host = new CommandRegistrationTests.FakeCommandHost
        {
            CloseVaultOverride = new RelayCommand(_ => session.CreateFolder("note0.md"), _ => true),
        };
        using var registry = new CommandRegistry();
        SlateCommandRegistrar.RegisterAll(registry, host, Dispatcher.CurrentDispatcher);

        CommandException.ActionFailed failed = Assert.Throws<CommandException.ActionFailed>(
            () => registry.InvokeById(ChordTable.Ids.VaultClose));

        Assert.Equal("Something named note0.md already exists there.", failed.message);
        Assert.False(SlateCommandRegistrar.IsAvailabilityRejection(failed.message));
        string label = ChordTable.RegisteredRows.First(row => row.Id == ChordTable.Ids.VaultClose).Label;
        RenderedAnnouncement said = SlateUniffiMethods.A11yRender(new A11yEvent.PaletteCommandFailed(label, failed.message));
        Assert.Equal("Close Vault failed: Something named note0.md already exists there.", said.Text);
        Assert.Equal(A11yPriority.High, said.Priority);
        Assert.DoesNotMatch(FieldToken, said.Text);
    }
}

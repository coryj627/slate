// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>Plan 47 CB-15a (#1890): <c>FailureDetail.Of</c>, the shell's one
/// reader of an exception's Message (ShellFailureDetailCensus), says core's
/// detail for a vault error and never the binding's field dump
/// (<c>@message=</c>, <c>@path=</c>), and any other exception's own message.
/// Each vault oracle is the literal core's <c>vault_error_detail</c> writes.
/// CB-15c adds the binding's other error kinds, each read by its typed
/// fields, and the aggregate, read through its inner failures.</summary>
public sealed class FailureDetailTests
{
    /// <summary>A binding field token: a uniffi exception's Message, never
    /// core's sentence.</summary>
    private const string FieldToken = @"@\w+=";

    [Theory]
    [InlineData("Io", "The device is not ready.")]
    [InlineData("DestinationExists", "Something named sub/a.md already exists there.")]
    [InlineData("InvalidPath", "Invalid path sub/Untitled Folder: \"sub\" is not a folder, so no folder was created")]
    [InlineData("InvalidQuery", "\"#\" is not a valid tag.")]
    [InlineData("TrashConfirmationChanged", "Files changed or Trash confirmation expired. Request deletion again")]
    [InlineData("SavedButUnindexed", "Saved, but not indexed yet: the index was busy. It will appear after the next scan.")]
    public void AVaultErrorSaysCoresDetailAndNoFieldDump(string variant, string detail)
    {
        VaultException failure = variant switch
        {
            "Io" => new VaultException.Io("The device is not ready."),
            "DestinationExists" => new VaultException.DestinationExists("sub/a.md"),
            "InvalidPath" => new VaultException.InvalidPath(
                "sub/Untitled Folder", "\"sub\" is not a folder, so no folder was created"),
            "InvalidQuery" => new VaultException.InvalidQuery("\"#\" is not a valid tag."),
            "TrashConfirmationChanged" => new VaultException.TrashConfirmationChanged(
                "Files changed or Trash confirmation expired. Request deletion again"),
            "SavedButUnindexed" => new VaultException.SavedButUnindexed("0123abcd", "the index was busy."),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "no such case"),
        };

        string said = FailureDetail.Of(failure);

        Assert.Equal(detail, said);
        Assert.DoesNotMatch(FieldToken, said);
    }

    [Fact]
    public void AnyOtherExceptionSaysItsOwnMessage() =>
        Assert.Equal(
            "The process cannot access the file because it is being used by another process.",
            FailureDetail.Of(new IOException(
                "The process cannot access the file because it is being used by another process.")));

    /// <summary>Plan 47 CB-15c (#1890, Call 9): core renders only the vault
    /// error, and every other error kind the binding declares dumps its
    /// fields in its Message. The helper reads each by its typed fields, so
    /// none of them can put a dump into text through it.</summary>
    private static void AssertSays(string detail, Exception failure)
    {
        string said = FailureDetail.Of(failure);

        Assert.Equal(detail, said);
        Assert.DoesNotMatch(FieldToken, said);
    }

    /// <summary>A command's action failure says the failure's own words, as
    /// the palette reads them.</summary>
    [Fact]
    public void ACommandActionFailureSaysItsMessageAndNoFieldDump() =>
        AssertSays("the vault exploded", new CommandException.ActionFailed("the vault exploded"));

    /// <summary>An unknown command says core's palette sentence for it, the
    /// one the palette speaks (PaletteCommandNotFound).</summary>
    [Fact]
    public void AnUnknownCommandSaysCoresSentenceAndNoFieldDump() =>
        AssertSays("Command not found: palette.missing", new CommandException.UnknownId("palette.missing"));

    /// <summary>The helper reads the command and hot exit errors variant by
    /// variant. A variant the binding adds would fall to the last arm and
    /// dump its fields, so the variant sets are pinned: a new one fails here
    /// before a reader can meet it. (GraphSearchFailureDetailTests pins the
    /// graph config's.)</summary>
    [Fact]
    public void TheCommandAndHotExitErrorsHaveTheVariantsTheHelperReads()
    {
        Assert.Equal(
            ["ActionFailed", "UnknownId"],
            typeof(CommandException).GetNestedTypes().Where(type => type.IsSubclassOf(typeof(CommandException)))
                .Select(type => type.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            ["Damaged", "HeldElsewhere", "InvalidEntryId", "InvalidIncarnation", "Io"],
            typeof(HotExitException).GetNestedTypes().Where(type => type.IsSubclassOf(typeof(HotExitException)))
                .Select(type => type.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>A graph config refusal says the words the graph's own
    /// surface says for it (GraphConfigStore.Reason).</summary>
    [Theory]
    [InlineData("NewerVersion", "graph.json is a newer version (3); not downgrading")]
    [InlineData("Unparseable", "graph.json is unparseable: expected value at line 1 column 1")]
    public void AGraphConfigRefusalSaysItsReasonAndNoFieldDump(string variant, string detail) =>
        AssertSays(detail, variant switch
        {
            "NewerVersion" => new GraphConfigException.NewerVersion(3),
            "Unparseable" => new GraphConfigException.Unparseable("expected value at line 1 column 1"),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "no such case"),
        });

    /// <summary>A hot exit store refusal says core's reason fragment, the
    /// words the store's own surface says for it
    /// (HotExitStoreHost.ReasonOf).</summary>
    [Theory]
    [InlineData("Io")]
    [InlineData("InvalidEntryId")]
    [InlineData("InvalidIncarnation")]
    [InlineData("Damaged")]
    [InlineData("HeldElsewhere")]
    public void AHotExitRefusalSaysItsReasonAndNoFieldDump(string variant) =>
        AssertSays(Reason, variant switch
        {
            "Io" => new HotExitException.Io("os error 32", Reason),
            "InvalidEntryId" => new HotExitException.InvalidEntryId(Reason),
            "InvalidIncarnation" => new HotExitException.InvalidIncarnation(Reason),
            "Damaged" => new HotExitException.Damaged(Reason),
            "HeldElsewhere" => new HotExitException.HeldElsewhere(Reason),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "no such case"),
        });

    /// <summary>A hot exit store's reason fragment, as core writes one.</summary>
    private const string Reason = "the hot exit store could not be read";

    /// <summary>An aggregate says each inner failure's detail, joined by a
    /// semicolon so two details never run together: a vault error inside one
    /// says core's words, not its dump.</summary>
    [Fact]
    public void AnAggregateSaysEachInnerDetailAndNoFieldDump() =>
        AssertSays(
            "Invalid path ../out.md: escapes the vault; The device is not ready.",
            new AggregateException(
                new VaultException.InvalidPath("../out.md", "escapes the vault"),
                new IOException("The device is not ready.")));

    /// <summary>An aggregate with no inner failure has no detail to join,
    /// and says its own message rather than nothing.</summary>
    [Fact]
    public void AnEmptyAggregateSaysItsOwnMessage() =>
        Assert.Equal(new AggregateException().Message, FailureDetail.Of(new AggregateException()));
}

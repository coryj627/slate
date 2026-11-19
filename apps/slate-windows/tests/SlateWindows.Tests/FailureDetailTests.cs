// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>Plan 47 CB-15a (#1890): <c>FailureDetail.Of</c>, the shell's one
/// reader of an exception's Message (ShellFailureDetailCensus), says core's
/// detail for a vault error and never the binding's field dump
/// (<c>@message=</c>, <c>@path=</c>), and any other exception's own message.
/// Each oracle is the literal core's <c>vault_error_detail</c> writes.</summary>
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

    /// <summary>The helper's documented limit: core renders no command
    /// error, so a <c>CommandException</c> says its own Message, which is the
    /// binding's field dump. The palette reads that error by its typed
    /// fields and never hands it here; this pins the limit so a change to it
    /// is seen.</summary>
    [Fact]
    public void ACommandErrorSaysItsOwnMessageWhichIsTheHelpersDocumentedLimit() =>
        Assert.Equal("@id=palette.missing", FailureDetail.Of(new CommandException.UnknownId("palette.missing")));
}

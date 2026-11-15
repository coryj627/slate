// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests.Censuses;

/// <summary>#1945 probe: a census that reads a Mac source without the
/// census trait. The guard must fail the lane. Throwaway; never merged.</summary>
public sealed class ProbeUntaggedMacReaderCensus
{
    [Fact]
    public void ReadsAMacSource()
    {
        string path = Path.Combine(SourceText.RepoRoot(), "apps", "slate-mac", "Sources", "SlateMac", "SlateCommands.swift");
        Assert.True(File.Exists(path));
    }
}

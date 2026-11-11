// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// #1826: a test process that fails fast leaves a dump the run uploads.
//
// On 2026-11-11, main's app job (run 42932655320, attempt 2) passed all
// 6,740 facts, then xUnit reported a TestPipelineException: the test process
// had ended after the last fact. The job kept hang dumps (--blame-hang) but
// nothing for a crash, so which thread ended it, and in what, was lost. The
// runtime writes its own dump on a fail-fast when DOTNET_DbgEnableMiniDump
// is set, in every .NET process the step starts, so the Test step sets it and
// names a file in the results folder the job uploads even when the step fails.

namespace SlateWindows.Tests.Censuses;

[Trait("census", "test-host-crash-dump")]
public sealed class TestHostCrashDumpCensus
{
    private const string Results = @"${{ runner.temp }}\slate-app-results";

    [Fact]
    public void TheAppTestStepDumpsAProcessThatFailsFastIntoItsUploadedResults()
    {
        string workflow = File.ReadAllText(Path.Combine(SourceText.RepoRoot(), ".github", "workflows", "windows.yml"));
        string test = DeviceStateCensus.Step(workflow, "windows", "Test");

        Assert.Contains("DOTNET_DbgEnableMiniDump: '1'", test, StringComparison.Ordinal);
        // A full dump (4), as StaThread's timeout dumps are (#1641): CI's
        // runtime is installed unregistered, so a heap dump (2) can lack the
        // JIT code and module images a managed stack needs, and a mini dump
        // (1) lacks the managed frames altogether (codex round 1).
        Assert.Contains("DOTNET_DbgMiniDumpType: '4'", test, StringComparison.Ordinal);
        // createdump expands %e and %p to the process's name and id, so two
        // processes that fail in one step write two files.
        Assert.Contains($@"DOTNET_DbgMiniDumpName: '{Results}\crash-%e-%p.dmp'", test, StringComparison.Ordinal);
        // createdump says what it wrote, or why it could not, in the step's log.
        Assert.Contains("DOTNET_CreateDumpDiagnostics: '1'", test, StringComparison.Ordinal);

        string upload = DeviceStateCensus.Step(workflow, "windows", "Upload app test results and hang evidence");
        Assert.Contains("if: ${{ always() }}", upload, StringComparison.Ordinal);
        Assert.Contains($@"path: '{Results}\**\*'", upload, StringComparison.Ordinal);
    }
}

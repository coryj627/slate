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

    /// <summary>A fail-fast the runtime doesn't raise itself never reaches
    /// createdump: a process that calls <c>RaiseFailFastException</c> exits
    /// 0xC0000602 (the code #1826 records) and writes nothing under the
    /// DOTNET_ variables, where <c>Environment.FailFast</c> under the same
    /// variables writes a full dump (measured locally). main's run 43059953344
    /// lost its test process that way with no dump. Windows Error Reporting
    /// still sees such a crash, so the job arms its LocalDumps for each test
    /// process before the Test step, writing full dumps (type 2) into the same
    /// uploaded folder.</summary>
    [Fact]
    public void TheAppJobArmsWindowsErrorReportingToDumpATestProcessIntoItsUploadedResults()
    {
        string workflow = File.ReadAllText(Path.Combine(SourceText.RepoRoot(), ".github", "workflows", "windows.yml"));
        string job = DeviceStateCensus.Job(workflow, "windows");
        string arm = DeviceStateCensus.Step(workflow, "windows", ArmStep);

        Assert.Contains(@"HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\", arm, StringComparison.Ordinal);
        // xunit v3 runs the suite as its own executable; VSTest's host and the
        // dotnet driver are the step's other .NET processes.
        foreach (string executable in (string[])["SlateWindows.Tests.exe", "testhost.exe", "dotnet.exe"])
        {
            Assert.Contains($"'{executable}'", arm, StringComparison.Ordinal);
        }

        Assert.Contains($"$results = '{Results}'", arm, StringComparison.Ordinal);
        Assert.Contains("-Name DumpFolder -PropertyType ExpandString -Value $results", arm, StringComparison.Ordinal);
        Assert.Contains("-Name DumpType -PropertyType DWord -Value 2", arm, StringComparison.Ordinal);
        // A runner that refuses the keys still runs the tests, and says so.
        Assert.Contains("::warning::", arm, StringComparison.Ordinal);

        int armed = job.IndexOf($"- name: {ArmStep}\n", StringComparison.Ordinal);
        int test = job.IndexOf("- name: Test\n", StringComparison.Ordinal);
        Assert.True(armed >= 0 && test > armed, "WER's dumps are armed before the Test step starts its processes.");
    }

    private const string ArmStep = "Arm Windows Error Reporting dumps for the test processes (#1826)";

    /// <summary>The repository is public, so anyone can fetch the job's
    /// artifact, and a full dump holds everything the test process read. It
    /// is safe to publish only while the job gives that process no secret:
    /// no <c>secrets.</c> reference, no GITHUB_TOKEN (a run step gets it only
    /// when it is mapped in) and no id-token grant (Codoki's review of
    /// #1892). A change that adds one fails here, so it has to stop dumping
    /// or stop publishing the dumps first.</summary>
    [Fact]
    public void TheJobThatPublishesTheTestProcessDumpHoldsNoSecret()
    {
        string workflow = File.ReadAllText(Path.Combine(SourceText.RepoRoot(), ".github", "workflows", "windows.yml"));
        string[] found = SecretReferences(DeviceStateCensus.Job(workflow, "windows"));
        Assert.True(
            found.Length == 0,
            $"The windows job now references {string.Join(", ", found.Select(secret => $"`{secret}`"))}. Its test process "
            + "is dumped in full into a public artifact when it fails fast (DOTNET_DbgEnableMiniDump) or a fact times out "
            + "(StaThread), so a secret there would be published. Keep the secret out of this job, or stop uploading the dumps.");
    }

    /// <summary>The secret guard's teeth: each kind of reference it looks for
    /// is found in a job that has one, and a comment that only names one is
    /// not a reference.</summary>
    [Theory]
    [InlineData("        env:\n          TOKEN: ${{ secrets.SIGNING_KEY }}", true)]
    [InlineData("        env:\n          GH_TOKEN: ${{ github.token }}", true)]
    [InlineData("        env:\n          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}", true)]
    [InlineData("    permissions:\n      id-token: write", true)]
    [InlineData("    uses: ./.github/workflows/build.yml\n    secrets: inherit", true)]
    [InlineData("        env:\n          ACTIONS_RUNTIME_TOKEN: ${{ env.ACTIONS_RUNTIME_TOKEN }}", true)]
    [InlineData("        env:\n          ACTIONS_ID_TOKEN_REQUEST_TOKEN: x", true)]
    [InlineData("      # no secrets. here, no GITHUB_TOKEN, no id-token grant", false)]
    public void TheSecretGuardSeesEachKindOfSecret(string planted, bool isSecret)
    {
        string workflow = "jobs:\n  windows:\n    runs-on: x\n" + planted + "\n  other:\n    runs-on: y\n";
        Assert.Equal(isSecret, SecretReferences(DeviceStateCensus.Job(workflow, "windows")).Length > 0);
    }

    /// <summary>The secret references in a job's YAML, its comment lines
    /// aside (the workflow's own comment explains the guard in these words).</summary>
    private static string[] SecretReferences(string job)
    {
        string yaml = string.Join(
            "\n",
            job.Split('\n').Where(line => !line.TrimStart().StartsWith('#')));
        return [.. SecretVectors.Where(secret => yaml.Contains(secret, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>Every way a job can hand its steps a secret or token: a
    /// secret reference, inherited secrets (<c>secrets: inherit</c> on a
    /// reusable workflow), the workflow token by either name, an id-token
    /// grant, and the runner's own tokens exported into a step (Codoki's
    /// second review of #1892).</summary>
    private static readonly string[] SecretVectors =
    [
        "secrets.",
        "secrets:",
        "GITHUB_TOKEN",
        "github.token",
        "id-token",
        "ACTIONS_RUNTIME_TOKEN",
        "ACTIONS_ID_TOKEN_REQUEST",
    ];
}

# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Behaviour of ci/branch-cleanup/branch-cleanup.sh, the weekly claude/*
# branch report that branch-cleanup.yml runs. The script runs under bash
# with a fake gh first on PATH: it answers each endpoint from files in the
# case's TestDrive directory and records every call, and the real gh is
# never reached (no token, an invalid host, and every call the script makes
# is pinned in order). Each case reads the exit code, the summary table, the
# annotations and log lines on stdout, and the calls.
#
# Needs only bash (the script uses awk, date and mktemp, and gh's own --jq,
# which the fake answers as canned text). On Linux that is bash on PATH; on
# Windows it is Git for Windows' bash, found beside git, because the bash on
# PATH there can be WSL's, a different machine. Without it every case is
# skipped and says why.

BeforeAll {
    $script:repo = 'fake-owner/fake-repo'
    $script:sh = (Resolve-Path (Join-Path $PSScriptRoot '..' '..' 'branch-cleanup' 'branch-cleanup.sh')).Path -replace '\\', '/'
    $script:bash = $null
    if ($IsWindows) {
        $git = Get-Command git -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        $dir = if ($git) { Split-Path $git.Source } else { '' }
        while ($dir -and -not $script:bash) {
            $candidate = Join-Path $dir 'bin' 'bash.exe'
            if (Test-Path $candidate) { $script:bash = $candidate }
            $dir = Split-Path $dir
        }
    }
    else {
        $script:bash = (Get-Command bash -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    }
    $script:skipReason = if ($bash) { $null } else { 'no bash was found (on Windows, Git for Windows'' bash beside git)' }

    # The fake gh: records each call, then answers it with canned text from $FAKE_GH_DIR/answers.tsv
    # (a key, then the answer's number n: n.out goes to stdout, n.err to stderr, n.code is the exit
    # status). The key is the method and path of `gh api`, or `pr list`; anything else has no answer.
    # It never evaluates --jq: each answer is the text gh would print after it, and each endpoint is
    # read one way only, so the path is key enough (the calls pin every --jq expression).
    $script:fakeBin = Join-Path $TestDrive 'fake-bin'
    New-Item -ItemType Directory -Path $fakeBin | Out-Null
    [IO.File]::WriteAllText((Join-Path $fakeBin 'gh'), @'
#!/usr/bin/env bash
printf '%s\n' "$*" >>"$FAKE_GH_DIR/calls.log"
key=''
if [ "${1:-}" = api ]; then
  shift
  method=GET path=''
  while [ $# -gt 0 ]; do
    case $1 in
      -X | --method) method=$2; shift 2 || break ;;
      --jq | -q) shift 2 || break ;;
      -*) shift ;;
      *) [ -n "$path" ] || path=$1; shift ;;
    esac
  done
  key="$method $path"
elif [ "${1:-} ${2:-}" = 'pr list' ]; then
  key='pr list'
fi
while IFS=$'\t' read -r k n; do
  if [ "$k" = "$key" ]; then
    cat "$FAKE_GH_DIR/$n.out"
    cat "$FAKE_GH_DIR/$n.err" >&2
    exit "$(cat "$FAKE_GH_DIR/$n.code")"
  fi
done <"$FAKE_GH_DIR/answers.tsv"
printf 'fake gh: no answer for %s\n' "$key" >&2
exit 99
'@.Replace("`r`n", "`n"))
    # Runs the script with the case's answers, the fake gh first on PATH and no GitHub credentials.
    $script:runner = Join-Path $TestDrive 'run-fake.sh'
    [IO.File]::WriteAllText($runner, @'
#!/usr/bin/env bash
case_dir=$(cd "$1" && pwd) || exit 97
fake_bin=$(cd "$3" && pwd) || exit 97
chmod +x "$fake_bin/gh" || exit 97
unset GH_TOKEN GITHUB_TOKEN GH_ENTERPRISE_TOKEN GITHUB_ENTERPRISE_TOKEN
export GH_HOST=fake-gh.invalid GH_CONFIG_DIR="$case_dir/gh-config" GH_PROMPT_DISABLED=1 FAKE_GH_DIR="$case_dir"
export PATH="$fake_bin:$PATH"
"$BASH" "$2" >"$case_dir/stdout" 2>"$case_dir/stderr"
'@.Replace("`r`n", "`n"))

    function Get-TipDate([int]$DaysAgo) {
        [DateTime]::UtcNow.AddDays(-$DaysAgo).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", [cultureinfo]::InvariantCulture)
    }
    function Get-ListingKey { "GET repos/$repo/git/matching-refs/heads/claude/?per_page=100" }
    function Get-TipKey([string]$Name) { "GET repos/$repo/branches/$([uri]::EscapeDataString($Name))" }
    function Get-CompareKey([string]$Sha) { "GET repos/$repo/compare/main...${Sha}?per_page=1" }
    function Get-RecheckKey([string]$Name) { "GET repos/$repo/git/ref/heads/$([uri]::EscapeDataString($Name))" }
    function Get-DeleteKey([string]$Name) { "DELETE repos/$repo/git/refs/heads/$([uri]::EscapeDataString($Name))" }
    # The calls the script must make, verbatim (gh's arguments joined by spaces).
    function Get-ListingCall { "api repos/$repo/git/matching-refs/heads/claude/?per_page=100 --paginate --jq .[].ref" }
    function Get-SnapshotCall { "pr list --repo $repo --state open --limit 1000 --json headRefName,baseRefName --jq " + '"open\t\(length)", (.[] | "\(.headRefName)\t\(.baseRefName)")' }
    function Get-TipCall([string]$Encoded) { "api repos/$repo/branches/$Encoded --jq .commit.sha + `" `" + .commit.commit.committer.date" }
    function Get-CompareCall([string]$Sha) { "api repos/$repo/compare/main...${Sha}?per_page=1 --jq .ahead_by" }
    function Get-RecheckCall([string]$Encoded) { "api repos/$repo/git/ref/heads/$Encoded --jq .object.sha" }
    function Get-DeleteCall([string]$Encoded) { "api -X DELETE repos/$repo/git/refs/heads/$Encoded" }

    # A listing of these refs and the open-pull-request snapshot as gh's --jq prints it: "open<TAB>N",
    # then one "head<TAB>base" line per pull request.
    function New-Answers([string[]]$Refs = @(), [string[]]$Pulls = @()) {
        $answers = [ordered]@{}
        $answers[(Get-ListingKey)] = @{ Out = (@($Refs | ForEach-Object { "$_`n" }) -join ''); Err = ''; Code = 0 }
        $answers['pr list'] = @{ Out = "open`t$(@($Pulls).Count)`n" + (@($Pulls | ForEach-Object { "$_`n" }) -join ''); Err = ''; Code = 0 }
        $answers
    }
    # One branch's answers: its tip (sha, committed DaysAgo days ago), the compare, the same sha at the
    # re-check, and a delete that works. Returns the tip date for the row.
    function Add-Branch([System.Collections.IDictionary]$Answers, [string]$Name, [string]$Sha = ('a' * 40), [int]$DaysAgo = 30, [string]$Ahead = '0') {
        $date = Get-TipDate $DaysAgo
        $Answers[(Get-TipKey $Name)] = @{ Out = "$Sha $date`n"; Err = ''; Code = 0 }
        $Answers[(Get-CompareKey $Sha)] = @{ Out = "$Ahead`n"; Err = ''; Code = 0 }
        $Answers[(Get-RecheckKey $Name)] = @{ Out = "$Sha`n"; Err = ''; Code = 0 }
        $Answers[(Get-DeleteKey $Name)] = @{ Out = ''; Err = ''; Code = 0 }
        $date
    }
    function Invoke-Cleanup([System.Collections.IDictionary]$Answers, [string]$Delete = 'false', [string]$MinAge = '') {
        $case = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $case | Out-Null
        $index = [System.Text.StringBuilder]::new()
        $n = 0
        foreach ($key in $Answers.Keys) {
            $n++
            [IO.File]::WriteAllText((Join-Path $case "$n.out"), [string]$Answers[$key].Out)
            [IO.File]::WriteAllText((Join-Path $case "$n.err"), [string]$Answers[$key].Err)
            [IO.File]::WriteAllText((Join-Path $case "$n.code"), [string]$Answers[$key].Code)
            [void]$index.Append("$key`t$n`n")
        }
        [IO.File]::WriteAllText((Join-Path $case 'answers.tsv'), $index.ToString())
        [IO.File]::WriteAllText((Join-Path $case 'calls.log'), '')
        $summary = Join-Path $case 'summary.md'
        # An empty value removes the variable, so the script's default applies.
        $vars = @{ REPO = $repo; DELETE = $Delete; GITHUB_STEP_SUMMARY = ($summary -replace '\\', '/'); BRANCH_MIN_AGE_DAYS = $MinAge }
        $saved = @{}
        foreach ($name in $vars.Keys) {
            $saved[$name] = [Environment]::GetEnvironmentVariable($name)
            [Environment]::SetEnvironmentVariable($name, $vars[$name])
        }
        try {
            & $bash ($runner -replace '\\', '/') ($case -replace '\\', '/') $sh ($fakeBin -replace '\\', '/')
            $code = $LASTEXITCODE
        }
        finally {
            foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
        }
        $out = [IO.File]::ReadAllText((Join-Path $case 'stdout'))
        [pscustomobject]@{
            Code        = $code
            Out         = $out
            Annotations = @($out -split "`n" | Where-Object { $_.StartsWith('::') })
            Logged      = @($out -split "`n" | Where-Object { $_ -and -not $_.StartsWith('::') })
            Err         = [IO.File]::ReadAllText((Join-Path $case 'stderr'))
            Summary     = if (Test-Path $summary) { [IO.File]::ReadAllText($summary) } else { '' }
            Calls       = @([IO.File]::ReadAllText((Join-Path $case 'calls.log')) -split "`n" | Where-Object { $_ })
        }
    }
    # One whole summary row, its cells exactly as given (already escaped).
    function Get-RowPattern([string[]]$Cells) { '(?m)^' + [regex]::Escape('| ' + ($Cells -join ' | ') + ' |') + '$' }
    function Get-RedNote([string]$Message) { '(?m)^' + [regex]::Escape("$Message; this run is red.") + '$' }
    # Every call, in order, verbatim: a call the fake did not record would be missing here.
    function Assert-Calls($Result, [string[]]$Expected) {
        ($Result.Calls -join "`n") | Should -BeExactly ($Expected -join "`n")
    }
    function Assert-Annotations($Result, [string[]]$Expected) {
        ($Result.Annotations -join "`n") | Should -BeExactly (@($Expected) -join "`n")
    }
    $script:a40 = 'a' * 40
    $script:b40 = 'b' * 40
}

Describe 'branch-cleanup.sh' {
    BeforeEach {
        if ($skipReason) { Set-ItResult -Skipped -Because $skipReason }
    }

    Context 'the listing' {
        It 'reports (none) and passes when there is no claude/* branch, and reads nothing else' {
            $r = Invoke-Cleanup (New-Answers)
            $r.Code | Should -Be 0
            $r.Summary | Should -Match '(?m)^## claude/\* branches \(\d{4}-\d\d-\d\dT\d\d:\d\dZ, delete=false, minimum age 7 days\)$'
            $r.Summary | Should -Match (Get-RowPattern @('branch', 'sha', 'ahead of main', 'last commit', 'open PR as head', 'open PR as base', 'action'))
            $r.Summary | Should -Match (Get-RowPattern @('(none)', '', '', '', '', '', 'no claude/* branches'))
            Assert-Annotations $r @()
            Assert-Calls $r @((Get-ListingCall))
        }
        It 'fails the run with an error and the red-run note when the listing fails, and classifies nothing' {
            $answers = New-Answers
            $answers[(Get-ListingKey)] = @{ Out = ''; Err = "HTTP 502: Bad Gateway`n"; Code = 1 }
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            Assert-Annotations $r @('::error::could not list the claude/* branches; nothing was classified or deleted: HTTP 502: Bad Gateway')
            $r.Summary | Should -Match (Get-RedNote 'could not list the claude/* branches; nothing was classified or deleted')
            $r.Summary | Should -Not -Match '\(none\)'
            Assert-Calls $r @((Get-ListingCall))
        }
        It 'refuses a ref outside claude/ and goes on with the others' {
            $answers = New-Answers @('refs/heads/main', 'refs/heads/claude/old')
            $date = Add-Branch $answers 'claude/old'
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            $r.Summary | Should -Match (Get-RowPattern @('refs/heads/main', '?', '?', '?', '?', '?', 'skipped: not a claude/* branch'))
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '0', $date, '0', '0', 'deleted'))
            Assert-Annotations $r @('::error::unexpected ref refs/heads/main; not classified', '::error::1 branch(es) could not be classified or deleted')
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fold'), (Get-CompareCall $a40), (Get-RecheckCall 'claude%2Fold'), (Get-DeleteCall 'claude%2Fold'))
        }
    }

    Context 'the open-pull-request snapshot' {
        It 'fails the run before any branch is read when the snapshot fails' {
            $answers = New-Answers @('refs/heads/claude/old')
            $null = Add-Branch $answers 'claude/old'
            $answers['pr list'] = @{ Out = ''; Err = "GraphQL: API rate limit exceeded`n"; Code = 1 }
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            Assert-Annotations $r @('::error::could not read the open pull requests; nothing was classified or deleted: GraphQL: API rate limit exceeded')
            $r.Summary | Should -Match (Get-RedNote 'could not read the open pull requests; nothing was classified or deleted')
            $r.Logged | Should -BeNullOrEmpty
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall))
        }
        It 'reads a snapshot answer that is <Label> as a failure, never as no open pull request' -ForEach @(
            @{ Label = 'empty'; Snapshot = '' }
            @{ Label = 'without its count line'; Snapshot = '{"message":"Bad credentials"}' }
            @{ Label = 'shorter than its count'; Snapshot = "open`t2`nclaude/x`tmain`n" }
            @{ Label = 'cut at the limit'; Snapshot = "open`t1000`n" + ((1..1000 | ForEach-Object { "claude/x`tmain`n" }) -join '') }
        ) {
            $answers = New-Answers @('refs/heads/claude/old')
            $answers['pr list'] = @{ Out = $Snapshot; Err = ''; Code = 0 }
            $null = Add-Branch $answers 'claude/old'
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            $r.Annotations.Count | Should -Be 1
            $r.Annotations[0] | Should -BeLike '::error::the open pull requests could not be read as one complete list; nothing was classified or deleted: *'
            $r.Summary | Should -Match (Get-RedNote 'the open pull requests could not be read as one complete list; nothing was classified or deleted')
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall))
        }
    }

    Context 'a branch main contains, with no open pull request, last committed 30 days ago' {
        It 'reports that it would delete it, and writes nothing, when DELETE is <Label>' -ForEach @(
            @{ Label = 'false'; Delete = 'false' }
            @{ Label = 'unset'; Delete = '' }
            @{ Label = 'TRUE'; Delete = 'TRUE' }
        ) {
            $answers = New-Answers @('refs/heads/claude/old')
            $date = Add-Branch $answers 'claude/old'
            $r = Invoke-Cleanup $answers -Delete $Delete
            $r.Code | Should -Be 0
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '0', $date, '0', '0', 'would delete'))
            ($r.Logged -join "`n") | Should -BeExactly 'claude/old: would delete'
            Assert-Annotations $r @()
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fold'), (Get-CompareCall $a40))
        }
        It 'deletes it with DELETE=true, once, by the percent-encoded ref, after re-reading its tip' {
            $answers = New-Answers @('refs/heads/claude/old')
            $date = Add-Branch $answers 'claude/old'
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 0
            $r.Summary | Should -Match '(?m)^## claude/\* branches \([^)]*, delete=true, minimum age 7 days\)$'
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '0', $date, '0', '0', 'deleted'))
            ($r.Logged -join "`n") | Should -BeExactly 'claude/old: deleted'
            Assert-Annotations $r @()
            Assert-Calls $r @(
                (Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fold'), (Get-CompareCall $a40),
                'api repos/fake-owner/fake-repo/git/ref/heads/claude%2Fold --jq .object.sha',
                'api -X DELETE repos/fake-owner/fake-repo/git/refs/heads/claude%2Fold')
        }
    }

    Context 'branches it keeps, with DELETE=true' {
        It 'keeps a branch ahead of main' {
            $answers = New-Answers @('refs/heads/claude/old')
            $date = Add-Branch $answers 'claude/old' -Ahead '3'
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 0
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '3', $date, '0', '0', 'kept: ahead 3'))
            ($r.Logged -join "`n") | Should -BeExactly 'claude/old: kept: ahead 3'
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fold'), (Get-CompareCall $a40))
        }
        It 'keeps a branch an open pull request comes from, and counts only that exact name' {
            $answers = New-Answers @('refs/heads/claude/old') -Pulls @("claude/old`tmain", "claude/old-2`tclaude/older")
            $date = Add-Branch $answers 'claude/old'
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 0
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '0', $date, '1', '0', 'kept: open PR head'))
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fold'), (Get-CompareCall $a40))
        }
        It 'keeps a branch an open pull request goes into' {
            $answers = New-Answers @('refs/heads/claude/old') -Pulls @("claude/next`tclaude/old")
            $date = Add-Branch $answers 'claude/old'
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 0
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '0', $date, '0', '1', 'kept: open PR base'))
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fold'), (Get-CompareCall $a40))
        }
        It 'keeps a branch younger than the age floor; BRANCH_MIN_AGE_DAYS moves the floor' {
            $answers = New-Answers @('refs/heads/claude/new')
            $date = Add-Branch $answers 'claude/new' -DaysAgo 2
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 0
            $r.Summary | Should -Match (Get-RowPattern @('claude/new', 'aaaaaaaa', '0', $date, '0', '0', 'kept: too young'))
            ($r.Logged -join "`n") | Should -BeExactly 'claude/new: kept: too young'
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fnew'), (Get-CompareCall $a40))
            $r = Invoke-Cleanup $answers -Delete 'true' -MinAge '1'
            $r.Code | Should -Be 0
            $r.Summary | Should -Match '(?m)^## claude/\* branches \([^)]*, minimum age 1 days\)$'
            $r.Summary | Should -Match (Get-RowPattern @('claude/new', 'aaaaaaaa', '0', $date, '0', '0', 'deleted'))
            $r.Calls[-1] | Should -BeExactly (Get-DeleteCall 'claude%2Fnew')
        }
        It 'names every reason it kept a branch' {
            $answers = New-Answers @('refs/heads/claude/busy') -Pulls @("claude/busy`tmain", "claude/next`tclaude/busy")
            $date = Add-Branch $answers 'claude/busy' -DaysAgo 1 -Ahead '2'
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 0
            $r.Summary | Should -Match (Get-RowPattern @('claude/busy', 'aaaaaaaa', '2', $date, '1', '1', 'kept: ahead 2, too young, open PR head, open PR base'))
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fbusy'), (Get-CompareCall $a40))
        }
        It 'keeps a branch whose tip moved since classification, and fails the run' {
            $answers = New-Answers @('refs/heads/claude/old')
            $date = Add-Branch $answers 'claude/old'
            $answers[(Get-RecheckKey 'claude/old')] = @{ Out = "$b40`n"; Err = ''; Code = 0 }
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '0', $date, '0', '0', 'kept: moved since classification'))
            $r.Summary | Should -Match (Get-RedNote '1 branch(es) could not be classified or deleted')
            Assert-Annotations $r @('::warning::claude/old moved since classification (aaaaaaaa to bbbbbbbb); kept', '::error::1 branch(es) could not be classified or deleted')
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fold'), (Get-CompareCall $a40), (Get-RecheckCall 'claude%2Fold'))
        }
    }

    Context 'lookups that fail, with DELETE=true' {
        It 'warns, skips the branch and fails the run when the compare fails' {
            $answers = New-Answers @('refs/heads/claude/old')
            $date = Add-Branch $answers 'claude/old'
            $answers[(Get-CompareKey $a40)] = @{ Out = ''; Err = "HTTP 404: Not Found`n"; Code = 1 }
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '?', $date, '?', '?', 'skipped: compare failed'))
            $r.Summary | Should -Match (Get-RedNote '1 branch(es) could not be classified or deleted')
            Assert-Annotations $r @('::warning::could not compare claude/old with main: HTTP 404: Not Found', '::error::1 branch(es) could not be classified or deleted')
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fold'), (Get-CompareCall $a40))
        }
        It 'reads a compare answer that is <Label> as a failure, never as ahead 0' -ForEach @(
            @{ Label = 'empty'; Answer = ''; Shown = '""' }
            @{ Label = 'null'; Answer = "null`n"; Shown = '"null"' }
        ) {
            $answers = New-Answers @('refs/heads/claude/old')
            $date = Add-Branch $answers 'claude/old'
            $answers[(Get-CompareKey $a40)] = @{ Out = $Answer; Err = ''; Code = 0 }
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '?', $date, '?', '?', 'skipped: compare failed'))
            Assert-Annotations $r @("::warning::could not compare claude/old with main: unexpected answer $Shown", '::error::1 branch(es) could not be classified or deleted')
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fold'), (Get-CompareCall $a40))
        }
        It 'warns, skips the branch and fails the run when the last commit lookup fails' {
            $answers = New-Answers @('refs/heads/claude/old')
            $null = Add-Branch $answers 'claude/old'
            $answers[(Get-TipKey 'claude/old')] = @{ Out = ''; Err = "HTTP 500: Internal Server Error`n"; Code = 1 }
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', '?', '?', '?', '?', '?', 'skipped: last commit lookup failed'))
            Assert-Annotations $r @('::warning::could not read the last commit of claude/old: HTTP 500: Internal Server Error', '::error::1 branch(es) could not be classified or deleted')
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fold'))
        }
        It 'does not delete, and fails the run, when the tip re-check fails' {
            $answers = New-Answers @('refs/heads/claude/old')
            $date = Add-Branch $answers 'claude/old'
            $answers[(Get-RecheckKey 'claude/old')] = @{ Out = ''; Err = "HTTP 404: Not Found`n"; Code = 1 }
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '0', $date, '0', '0', 'skipped: tip re-check failed'))
            Assert-Annotations $r @('::warning::could not re-read the tip of claude/old before deleting it: HTTP 404: Not Found', '::error::1 branch(es) could not be classified or deleted')
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fold'), (Get-CompareCall $a40), (Get-RecheckCall 'claude%2Fold'))
        }
        It 'says why a delete failed, and fails the run' {
            $answers = New-Answers @('refs/heads/claude/old')
            $date = Add-Branch $answers 'claude/old'
            $answers[(Get-DeleteKey 'claude/old')] = @{ Out = ''; Err = "HTTP 422: Reference does not exist`n"; Code = 1 }
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '0', $date, '0', '0', 'delete failed'))
            Assert-Annotations $r @('::warning::could not delete claude/old: HTTP 422: Reference does not exist', '::error::1 branch(es) could not be classified or deleted')
        }
        It 'goes on to the next branch after one fails, and fails the run at the end' {
            $answers = New-Answers @('refs/heads/claude/broken', 'refs/heads/claude/old')
            $brokenDate = Add-Branch $answers 'claude/broken' -Sha $b40
            $answers[(Get-CompareKey $b40)] = @{ Out = ''; Err = "HTTP 404: Not Found`n"; Code = 1 }
            $date = Add-Branch $answers 'claude/old'
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            $r.Summary | Should -Match (Get-RowPattern @('claude/broken', 'bbbbbbbb', '?', $brokenDate, '?', '?', 'skipped: compare failed'))
            $r.Summary | Should -Match (Get-RowPattern @('claude/old', 'aaaaaaaa', '0', $date, '0', '0', 'deleted'))
            Assert-Calls $r @(
                (Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fbroken'), (Get-CompareCall $b40),
                (Get-TipCall 'claude%2Fold'), (Get-CompareCall $a40), (Get-RecheckCall 'claude%2Fold'), (Get-DeleteCall 'claude%2Fold'))
        }
    }

    Context 'text from branch names and from GitHub' {
        It 'keeps a branch name with | and an error text with % and CR intact in the table and the annotation' {
            $answers = New-Answers @('refs/heads/claude/a|b%c')
            $date = Add-Branch $answers 'claude/a|b%c'
            $answers[(Get-CompareKey $a40)] = @{ Out = ''; Err = "100% gone`r`nsecond line`n"; Code = 1 }
            $r = Invoke-Cleanup $answers -Delete 'true'
            $r.Code | Should -Be 1
            $r.Summary | Should -Match (Get-RowPattern @('claude/a\|b%c', 'aaaaaaaa', '?', $date, '?', '?', 'skipped: compare failed'))
            Assert-Annotations $r @('::warning::could not compare claude/a|b%25c with main: 100%25 gone%0D%0Asecond line', '::error::1 branch(es) could not be classified or deleted')
            $r.Out | Should -Not -Match "`r"
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2Fa%7Cb%25c'), (Get-CompareCall $a40))
        }
        It 'encodes a branch name for the URL byte by byte, keeping only unreserved characters' {
            # claude/, e-acute (UTF-8 C3 A9), then ~x_y.z-1+2: / and + and both bytes of the e-acute are encoded.
            $name = "claude/$([char]0xE9)~x_y.z-1+2"
            $answers = New-Answers @("refs/heads/$name")
            $date = Add-Branch $answers $name
            $r = Invoke-Cleanup $answers
            $r.Code | Should -Be 0
            $r.Summary | Should -Match (Get-RowPattern @($name, 'aaaaaaaa', '0', $date, '0', '0', 'would delete'))
            Assert-Calls $r @((Get-ListingCall), (Get-SnapshotCall), (Get-TipCall 'claude%2F%C3%A9~x_y.z-1%2B2'), (Get-CompareCall $a40))
        }
    }

    Context 'its environment' {
        It 'fails before any call when BRANCH_MIN_AGE_DAYS is not a whole number of days' {
            $answers = New-Answers @('refs/heads/claude/old')
            $null = Add-Branch $answers 'claude/old'
            $r = Invoke-Cleanup $answers -Delete 'true' -MinAge 'seven'
            $r.Code | Should -Be 1
            Assert-Annotations $r @('::error::BRANCH_MIN_AGE_DAYS is not a whole number of days; nothing was classified or deleted')
            Assert-Calls $r @()
        }
    }
}

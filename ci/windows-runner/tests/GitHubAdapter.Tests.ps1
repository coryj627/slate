# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    . (Join-Path $PSScriptRoot '..' 'adapters' 'GitHub.ps1')
    Initialize-GitHubAdapter -Owner 'coryj627' -Repo 'slate' -Token (ConvertTo-SecureString 'ghp_test' -AsPlainText -Force)
    $script:routed = @('.github/workflows/windows.yml', '.github/workflows/nightly.yml', '.github/workflows/windows-ci-pilot.yml')

    function Set-ResponseHeaders([string]$Name, $Headers) {
        # Plays the cmdlet's part in a mock: -ResponseHeadersVariable sets
        # the named variable in its caller's scope (Invoke-GhApi's), which
        # sits some scopes above a mock body. Invoke-GhApi creates it first.
        for ($scope = 1; $scope -lt 64; $scope++) {
            try { $null = Get-Variable -Name $Name -Scope $scope -ErrorAction Stop } catch { continue }
            Set-Variable -Name $Name -Value $Headers -Scope $scope
            return
        }
        throw "no caller scope holds `$$Name"
    }
    function Set-OkStatus([string]$Name) {
        # A conditional call reads its status from -StatusCodeVariable; an
        # unconditional one binds no such parameter, so there is nothing to set.
        if ($Name) { Set-ResponseHeaders $Name 200 }
    }
}

Describe 'Invoke-GhApi' {
    It 'sends bearer auth, the API version header and the repository base' {
        Mock Invoke-RestMethod { [pscustomobject]@{ ok = $true } }
        Invoke-GhApi -Path '/actions/runners' | Out-Null
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
            $Uri -eq 'https://api.github.com/repos/coryj627/slate/actions/runners' -and
            $Method -eq 'GET' -and
            $Headers.Authorization -eq 'Bearer ghp_test' -and
            $Headers['X-GitHub-Api-Version'] -eq '2022-11-28' -and
            $Headers.Accept -eq 'application/vnd.github+json' -and
            $OperationTimeoutSeconds -eq 30
        }
    }
    It 'serialises a body as JSON for POST' {
        Mock Invoke-RestMethod { [pscustomobject]@{} }
        Invoke-GhApi -Method 'POST' -Path '/x' -Body @{ a = 1 } | Out-Null
        Should -Invoke Invoke-RestMethod -Times 1 -ParameterFilter { $Method -eq 'POST' -and $ContentType -eq 'application/json' -and ($Body | ConvertFrom-Json).a -eq 1 }
    }
    It 'never enables debug tracing' {
        # Pester does not set $PSBoundParameters inside a ParameterFilter;
        # $PesterBoundParameters holds the call's bound parameters.
        Mock Invoke-RestMethod { [pscustomobject]@{} }
        Invoke-GhApi -Path '/actions/runners' -Debug | Out-Null
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter { $PesterBoundParameters.ContainsKey('Debug') -and -not $Debug }
    }
    It 'removes the token from the request kept in the error record' {
        Mock Invoke-RestMethod {
            $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, 'https://api.github.com/repos/coryj627/slate/x')
            $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', 'ghp_test')
            Write-Error -Message 'boom' -TargetObject $request -ErrorAction Stop
        }
        $record = $null
        try { Invoke-GhApi -Path '/x' } catch { $record = $_ }
        "$record" | Should -Be 'boom'
        $record.TargetObject | Should -BeOfType [System.Net.Http.HttpRequestMessage]
        $record.TargetObject.Headers.Contains('Authorization') | Should -BeFalse
        "$($record.TargetObject)" | Should -Not -BeLike '*ghp_test*'
    }
}

Describe 'Invoke-GhApi transient retry' {
    BeforeAll {
        $script:fastFailureDefault = $script:GhRetryFastFailureSeconds
        $script:slowCapDefault = $script:GhRetrySlowCapSeconds
        function New-GhConnectionError([System.Exception]$Inner, [string]$RequestError = 'Unknown') {
            # The shape a failure before the response has: the cmdlet keeps
            # its HttpRequestException wrapper and the request as TargetObject.
            # The wrapper's HttpRequestError says whether the request ever
            # went out (ConnectionError, SecureConnectionError, NameResolutionError).
            $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, 'https://api.github.com/repos/coryj627/slate/x')
            $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', 'ghp_test')
            $exception = [System.Net.Http.HttpRequestException]::new([System.Net.Http.HttpRequestError]$RequestError, 'An error occurred while sending the request.', $Inner)
            Write-Error -Exception $exception -TargetObject $request -ErrorAction Stop
        }
        function New-GhTlsStall {
            # What api.github.com shows this host: the TLS handshake stalls
            # for 19 s on a new connection and is then reset.
            New-GhConnectionError -RequestError SecureConnectionError -Inner ([System.IO.IOException]::new('Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host.', [System.Net.Sockets.SocketException]::new(10054)))
        }
    }
    BeforeEach {
        $script:retryNotices = [System.Collections.ArrayList]::new()
        $script:GhRetryNotice = { param($message) [void]$script:retryNotices.Add($message) }
        $script:sleeps = [System.Collections.ArrayList]::new()
        Mock Start-Sleep { [void]$script:sleeps.Add($Seconds) }
    }
    AfterEach {
        $script:GhRetryFastFailureSeconds = $script:fastFailureDefault
        $script:GhRetrySlowCapSeconds = $script:slowCapDefault
    }
    AfterAll {
        $script:GhRetryNotice = $null
    }
    It 'tries a GET again after a reset mid-body, waiting 1 s then 2 s, and reports the retry once it is answered' {
        # A reset or early end while the body is read arrives bare, as an
        # IOException (HttpIOException), with no TargetObject.
        $script:calls = 0
        Mock Invoke-RestMethod {
            $script:calls++
            if ($script:calls -lt 3) {
                throw [System.IO.IOException]::new('Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host.', [System.Net.Sockets.SocketException]::new(10054))
            }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/actions/runs?status=queued').ok | Should -BeTrue
        Should -Invoke Invoke-RestMethod -Times 3 -Exactly
        @($script:sleeps) | Should -Be @(1, 2)
        @($script:retryNotices).Count | Should -Be 1
        $script:retryNotices[0] | Should -Be 'GitHub API: GET /actions/runs?status=queued answered on attempt 3 after a connection failure'
    }
    It 'retries a refused connection and a failed name lookup, which keep the cmdlet''s wrapper' {
        foreach ($inner in [System.Net.Sockets.SocketException]::new(10061), [System.Net.Sockets.SocketException]::new(11001)) {
            $script:calls = 0
            $script:failWith = $inner
            Mock Invoke-RestMethod {
                $script:calls++
                if ($script:calls -eq 1) { New-GhConnectionError -Inner $script:failWith }
                [pscustomobject]@{ ok = $true }
            }
            (Invoke-GhApi -Path '/x').ok | Should -BeTrue -Because $inner.SocketErrorCode
        }
        Should -Invoke Invoke-RestMethod -Times 4 -Exactly
        @($script:sleeps) | Should -Be @(1, 1)
    }
    It 'gives up after the third attempt, rethrowing with the token removed from the kept request' {
        Mock Invoke-RestMethod { New-GhConnectionError -Inner ([System.IO.IOException]::new('reset')) }
        $record = $null
        try { Invoke-GhApi -Path '/x' } catch { $record = $_ }
        $record | Should -Not -BeNullOrEmpty
        Should -Invoke Invoke-RestMethod -Times 3 -Exactly
        @($script:sleeps) | Should -Be @(1, 2)
        $record.TargetObject.Headers.Contains('Authorization') | Should -BeFalse
        "$($record.TargetObject)" | Should -Not -BeLike '*ghp_test*'
        # The evidence line: why the GET was not tried again, the failure's
        # shape as exception types (no messages: nothing to leak), the time.
        @($script:retryNotices).Count | Should -Be 1
        $script:retryNotices[0] | Should -Match '^GitHub API: GET /x gave up on attempt 3 of 3 after [0-9]+\.[0-9] s \(the last attempt\): HttpRequestException > IOException$'
    }
    It 'never retries a POST, an HTTP status error or a timeout' {
        # A retried generate-jitconfig would register a second runner.
        Mock Invoke-RestMethod { New-GhConnectionError -Inner ([System.IO.IOException]::new('reset')) }
        { Invoke-GhApi -Method 'POST' -Path '/actions/runners/generate-jitconfig' -Body @{ name = 'x' } } | Should -Throw
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly
        # GitHub answered: the caller decides (Get-GhRunner reads a 404).
        Mock Invoke-RestMethod {
            $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::Forbidden)
            throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('forbidden', $response)
        }
        { Invoke-GhApi -Path '/x' } | Should -Throw
        Should -Invoke Invoke-RestMethod -Times 2 -Exactly
        # A stalled body must not triple the stall: both timeout shapes.
        Mock Invoke-RestMethod { throw [System.Threading.Tasks.TaskCanceledException]::new('The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.', [System.TimeoutException]::new('timeout')) }
        { Invoke-GhApi -Path '/x' } | Should -Throw
        Mock Invoke-RestMethod { throw [System.TimeoutException]::new('The operation timed out.') }
        { Invoke-GhApi -Path '/x' } | Should -Throw
        Should -Invoke Invoke-RestMethod -Times 4 -Exactly
        @($script:sleeps).Count | Should -Be 0
        # A status is GitHub's answer and a POST is never retried by design:
        # no line for either. A timeout is named, with its shape.
        @($script:retryNotices).Count | Should -Be 2
        $script:retryNotices[0] | Should -Match '^GitHub API: GET /x gave up on attempt 1 of 3 after [0-9]+\.[0-9] s \(not a connection failure\): TaskCanceledException > TimeoutException$'
        $script:retryNotices[1] | Should -Match '\(not a connection failure\): TimeoutException$'
    }
    It 'never retries a connect Windows gave up on after 21 s, nor an attempt at the slow cap or beyond' {
        # The socket error names the first; Windows reports it at 21 s, so
        # the 5 s bound would catch it too, but the name is the surer test.
        Mock Invoke-RestMethod { New-GhConnectionError -RequestError ConnectionError -Inner ([System.Net.Sockets.SocketException]::new(10060)) }
        { Invoke-GhApi -Path '/x' } | Should -Throw
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly
        @($script:sleeps).Count | Should -Be 0
        $script:retryNotices[0] | Should -Match '\(a connect that timed out\): HttpRequestException\(ConnectionError\) > SocketException\(TimedOut\)$'
        # Measured, not typed: the cap is lowered to 0 s so any attempt is at it.
        $script:GhRetrySlowCapSeconds = 0
        $script:GhRetryFastFailureSeconds = 0
        Mock Invoke-RestMethod { New-GhTlsStall }
        { Invoke-GhApi -Path '/x' } | Should -Throw
        Should -Invoke Invoke-RestMethod -Times 2 -Exactly
        @($script:sleeps).Count | Should -Be 0
        $script:retryNotices[1] | Should -Match '\(too slow to try again\): HttpRequestException\(SecureConnectionError\) > IOException > SocketException\(ConnectionReset\)$'
    }
    It 'tries a slow failure again once when the request never went out, such as a TLS handshake that stalled and was reset' {
        # Measured, not typed: the bound is lowered to 0 s so any attempt counts as slow.
        $script:GhRetryFastFailureSeconds = 0
        foreach ($kind in 'SecureConnectionError', 'ConnectionError', 'NameResolutionError') {
            $script:calls = 0
            $script:failWith = $kind
            Mock Invoke-RestMethod {
                $script:calls++
                if ($script:calls -eq 1) { New-GhConnectionError -RequestError $script:failWith -Inner ([System.IO.IOException]::new('stalled', [System.Net.Sockets.SocketException]::new(10054))) }
                [pscustomobject]@{ ok = $true }
            }
            (Invoke-GhApi -Path '/x').ok | Should -BeTrue -Because $kind
        }
        Should -Invoke Invoke-RestMethod -Times 6 -Exactly
        @($script:sleeps) | Should -Be @(1, 1, 1)
        @($script:retryNotices).Count | Should -Be 3
        $script:retryNotices[0] | Should -Be 'GitHub API: GET /x answered on attempt 2 after a connection failure'
    }
    It 'gives up on a second slow failure, and at once on a slow failure after the request went out' {
        $script:GhRetryFastFailureSeconds = 0
        Mock Invoke-RestMethod { New-GhTlsStall }
        { Invoke-GhApi -Path '/x' } | Should -Throw
        Should -Invoke Invoke-RestMethod -Times 2 -Exactly
        @($script:sleeps) | Should -Be @(1)
        $script:retryNotices[0] | Should -Match '^GitHub API: GET /x gave up on attempt 2 of 3 after [0-9]+\.[0-9] s \(a second slow failure\): HttpRequestException\(SecureConnectionError\)'
        # A reset while the body was read: the request went out, so the
        # server may have acted on it and the wait was not the whole cost.
        Mock Invoke-RestMethod { throw [System.IO.IOException]::new('Unable to read data from the transport connection', [System.Net.Sockets.SocketException]::new(10054)) }
        { Invoke-GhApi -Path '/x' } | Should -Throw
        Should -Invoke Invoke-RestMethod -Times 3 -Exactly
        @($script:sleeps) | Should -Be @(1)
        $script:retryNotices[1] | Should -Match '\(a slow failure after the request went out\): IOException > SocketException\(ConnectionReset\)$'
    }
    It 'names a failure shape the rule does not know, so the rule can be tuned from the log' {
        Mock Invoke-RestMethod { throw [System.Security.Authentication.AuthenticationException]::new('The remote certificate is invalid.') }
        { Invoke-GhApi -Path '/x' } | Should -Throw
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly
        @($script:retryNotices).Count | Should -Be 1
        $script:retryNotices[0] | Should -Match '^GitHub API: GET /x gave up on attempt 1 of 3 after [0-9]+\.[0-9] s \(not a connection failure\): AuthenticationException$'
        # A reset wrapped two deep still reads as a chain, inner error code included.
        $script:retryNotices.Clear()
        $script:GhRetryFastFailureSeconds = 0
        Mock Invoke-RestMethod { New-GhConnectionError -Inner ([System.IO.IOException]::new('Unable to read data from the transport connection', [System.Net.Sockets.SocketException]::new(10054))) }
        { Invoke-GhApi -Path '/actions/runs?status=queued' } | Should -Throw
        $script:retryNotices[0] | Should -Match '^GitHub API: GET /actions/runs\?status=queued gave up on attempt 1 of 3 after [0-9]+\.[0-9] s \(a slow failure after the request went out\): HttpRequestException > IOException > SocketException\(ConnectionReset\)$'
    }
    It 'writes the line with a dot for the seconds whatever the culture, with types only, and survives a throwing hook' {
        $culture = [cultureinfo]::CurrentCulture
        try {
            [cultureinfo]::CurrentCulture = [cultureinfo]::new('de-DE')
            # The messages carry the test token: the line must not.
            Mock Invoke-RestMethod { throw [System.IO.IOException]::new('reset while sending Bearer ghp_test', [System.Net.Sockets.SocketException]::new(10054)) }
            $script:GhRetryFastFailureSeconds = 0
            { Invoke-GhApi -Path '/x' } | Should -Throw
            $script:retryNotices[0] | Should -Match ' after [0-9]+\.[0-9] s '
            $script:retryNotices[0] | Should -Not -BeLike '*ghp_test*'
            $script:retryNotices[0] | Should -Match ': IOException > SocketException\(ConnectionReset\)$'
        } finally {
            [cultureinfo]::CurrentCulture = $culture
        }
        # A hook that throws on the give-up line leaves the original failure to the caller.
        $script:GhRetryNotice = { throw 'log disk full' }
        $record = $null
        try { Invoke-GhApi -Path '/x' } catch { $record = $_ }
        $record.Exception | Should -BeOfType [System.IO.IOException]
    }
    It 'formats an exception chain as type names with the socket and request error codes, six deep at most' {
        $deep = [System.Exception]::new('0')
        1..9 | ForEach-Object { $deep = [System.Exception]::new("$_", $deep) }
        (Format-GhExceptionChain -Exception $deep) | Should -Be 'Exception > Exception > Exception > Exception > Exception > Exception'
        (Format-GhExceptionChain -Exception ([System.IO.IOException]::new('bare'))) | Should -Be 'IOException'
        $wrapped = [System.Net.Http.HttpRequestException]::new([System.Net.Http.HttpRequestError]::ConnectionError, 'sending', [System.Net.Sockets.SocketException]::new(10061))
        (Format-GhExceptionChain -Exception $wrapped) | Should -Be 'HttpRequestException(ConnectionError) > SocketException(ConnectionRefused)'
        (Format-GhExceptionChain -Exception ([System.Net.Http.HttpRequestException]::new('plain'))) | Should -Be 'HttpRequestException'
    }
    It 'stays quiet when the first attempt is answered, and when no notice is wired' {
        Mock Invoke-RestMethod { [pscustomobject]@{ ok = $true } }
        (Invoke-GhApi -Path '/x').ok | Should -BeTrue
        @($script:retryNotices).Count | Should -Be 0
        $script:GhRetryNotice = $null
        $script:calls = 0
        Mock Invoke-RestMethod {
            $script:calls++
            if ($script:calls -eq 1) { New-GhConnectionError -Inner ([System.Net.Sockets.SocketException]::new(10061)) }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/x').ok | Should -BeTrue
        Should -Invoke Invoke-RestMethod -Times 3 -Exactly
        @($script:sleeps) | Should -Be @(1)
    }
    It 'never turns an answered call into a failure when the notice itself throws' {
        $script:GhRetryNotice = { throw 'log disk full' }
        $script:calls = 0
        Mock Invoke-RestMethod {
            $script:calls++
            if ($script:calls -eq 1) { New-GhConnectionError -Inner ([System.IO.IOException]::new('reset')) }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/x').ok | Should -BeTrue
    }
    It 'resends the ETag on a retried conditional call and still answers a 304 from its cache' {
        $script:GhEtags = @{}
        try {
            $script:calls = 0
            Mock Invoke-RestMethod {
                $script:calls++
                # Attempts 1 and 3 lose the connection; 2 answers with an ETag; 4 is a 304.
                if ($script:calls -in 1, 3) { throw [System.IO.IOException]::new('The response ended prematurely.') }
                Set-ResponseHeaders $ResponseHeadersVariable @{ ETag = @('W/"abc"') }
                if ($script:calls -eq 2) {
                    Set-ResponseHeaders $StatusCodeVariable 200
                    return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 1 }) }
                }
                Set-ResponseHeaders $StatusCodeVariable 304
                return $null
            }
            $first = Invoke-GhApi -Path '/actions/runs?status=queued' -Conditional
            $first.workflow_runs[0].id | Should -Be 1
            $second = Invoke-GhApi -Path '/actions/runs?status=queued' -Conditional
            $second.workflow_runs[0].id | Should -Be 1
            Should -Invoke Invoke-RestMethod -Times 4 -Exactly
            Should -Invoke Invoke-RestMethod -Times 2 -Exactly -ParameterFilter { -not $Headers.ContainsKey('If-None-Match') }
            Should -Invoke Invoke-RestMethod -Times 2 -Exactly -ParameterFilter { $Headers['If-None-Match'] -eq 'W/"abc"' -and $SkipHttpErrorCheck }
            @($script:retryNotices).Count | Should -Be 2
            $script:GhEtags.Count | Should -Be 1
        } finally {
            $script:GhEtags = @{}
        }
    }
}

Describe 'Invoke-GhApi rate budget' {
    BeforeEach {
        $script:budgetWarnings = [System.Collections.ArrayList]::new()
        $script:GhLowBudgetWarning = { param($message) [void]$script:budgetWarnings.Add($message) }
        $script:GhLowBudgetWarnedReset = $null
    }
    AfterAll {
        $script:GhLowBudgetWarning = $null
        $script:GhLowBudgetWarnedReset = $null
    }
    It 'reads the response headers and warns, with the reset time, when fewer than 500 requests remain' {
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('499'); 'X-RateLimit-Reset' = @('1791640800') }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/actions/runners').ok | Should -BeTrue
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter { $ResponseHeadersVariable -eq 'ghResponseHeaders' }
        $budgetWarnings.Count | Should -Be 1
        $budgetWarnings[0] | Should -Match '\b499 requests left\b'
        $budgetWarnings[0] | Should -Match ([regex]::Escape([datetimeoffset]::FromUnixTimeSeconds(1791640800).ToString('o')))
        $budgetWarnings[0] | Should -Match '5,000'
    }
    It 'matches the header names whatever their case' {
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'x-ratelimit-remaining' = @('12'); 'x-ratelimit-reset' = @('1791640800') }
            [pscustomobject]@{}
        }
        Invoke-GhApi -Path '/x' | Out-Null
        $budgetWarnings.Count | Should -Be 1
        $budgetWarnings[0] | Should -Match '\b12 requests left\b'
    }
    It 'warns once per rate-limit window, and again in the next one' {
        $script:reset = '1791640800'
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('400'); 'X-RateLimit-Reset' = @($script:reset) }
            [pscustomobject]@{}
        }
        1..3 | ForEach-Object { Invoke-GhApi -Path '/x' | Out-Null }
        $budgetWarnings.Count | Should -Be 1
        $script:reset = '1791644400'
        Invoke-GhApi -Path '/x' | Out-Null
        $budgetWarnings.Count | Should -Be 2
    }
    It 'stays quiet with 500 or more left, without the headers, or when no warning is wired' {
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('500'); 'X-RateLimit-Reset' = @('1791640800') }
            [pscustomobject]@{}
        }
        Invoke-GhApi -Path '/x' | Out-Null
        Mock Invoke-RestMethod { [pscustomobject]@{} }
        Invoke-GhApi -Path '/x' | Out-Null
        $budgetWarnings.Count | Should -Be 0
        $script:GhLowBudgetWarning = $null
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('1'); 'X-RateLimit-Reset' = @('1791640800') }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/x').ok | Should -BeTrue
    }
    It 'never turns a successful call into a failure over a malformed budget header' {
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('99999999999999999999'); 'X-RateLimit-Reset' = @('1791640800') }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/x').ok | Should -BeTrue
        $budgetWarnings.Count | Should -Be 0
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('7'); 'X-RateLimit-Reset' = @('99999999999999999') }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/x').ok | Should -BeTrue
        $budgetWarnings.Count | Should -Be 1
        $budgetWarnings[0] | Should -Match 'an unknown time'
    }
    It 'never turns a successful call into a failure when the warning itself throws' {
        $script:GhLowBudgetWarning = { param($message) throw 'log volume full' }
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ 'X-RateLimit-Remaining' = @('3'); 'X-RateLimit-Reset' = @('1791640800') }
            [pscustomobject]@{ ok = $true }
        }
        (Invoke-GhApi -Path '/x').ok | Should -BeTrue
    }
}

Describe 'Invoke-GhApi conditional requests' {
    BeforeEach { $script:GhEtags = @{} }
    AfterAll { $script:GhEtags = @{} }
    It 'sends the last ETag back and answers a 304 from its cache' {
        $script:conditionalCalls = 0
        Mock Invoke-RestMethod {
            $script:conditionalCalls++
            Set-ResponseHeaders $ResponseHeadersVariable @{ ETag = @('W/"abc"') }
            if ($script:conditionalCalls -eq 1) {
                Set-ResponseHeaders $StatusCodeVariable 200
                return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 1 }) }
            }
            Set-ResponseHeaders $StatusCodeVariable 304
            return $null
        }
        $first = Invoke-GhApi -Path '/actions/runs?status=queued' -Conditional
        $first.workflow_runs[0].id | Should -Be 1
        $second = Invoke-GhApi -Path '/actions/runs?status=queued' -Conditional
        $second.workflow_runs[0].id | Should -Be 1
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter { -not $Headers.ContainsKey('If-None-Match') -and $SkipHttpErrorCheck -and $StatusCodeVariable -eq 'ghStatusCode' }
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter { $Headers['If-None-Match'] -eq 'W/"abc"' -and $SkipHttpErrorCheck }
        $script:GhEtags.Count | Should -Be 1
    }
    It 'raises any other status itself, with GitHub''s reply' {
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $StatusCodeVariable 403
            [pscustomobject]@{ message = 'API rate limit exceeded for user ID 1.'; status = '403' }
        }
        { Invoke-GhApi -Path '/actions/runs?status=queued' -Conditional } | Should -Throw '*403*API rate limit exceeded*'
        $script:GhEtags.Count | Should -Be 0
    }
    It 'never skips the error check, sends an ETag or caches on an unconditional call' {
        Mock Invoke-RestMethod { [pscustomobject]@{ ok = $true } }
        Invoke-GhApi -Path '/actions/jobs/1' | Out-Null
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
            -not $PesterBoundParameters.ContainsKey('SkipHttpErrorCheck') -and -not $PesterBoundParameters.ContainsKey('StatusCodeVariable') -and -not $Headers.ContainsKey('If-None-Match')
        }
        $script:GhEtags.Count | Should -Be 0
    }
    It 'keeps the cache to the paths used in the last hour once it grows' {
        for ($i = 0; $i -lt 201; $i++) { $script:GhEtags["/old/$i"] = @{ ETag = 'x'; Body = $null; UsedAt = [datetimeoffset]::UtcNow.AddHours(-2) } }
        Mock Invoke-RestMethod {
            Set-ResponseHeaders $ResponseHeadersVariable @{ ETag = @('"n"') }
            Set-ResponseHeaders $StatusCodeVariable 200
            [pscustomobject]@{}
        }
        Invoke-GhApi -Path '/new' -Conditional | Out-Null
        $script:GhEtags.Count | Should -Be 1
        $script:GhEtags.ContainsKey('/new') | Should -BeTrue
    }
}

Describe 'New-GhJitRunner' {
    It 'posts name, runner_group_id 1, labels and work_folder, returning id and config' {
        Mock Invoke-RestMethod { [pscustomobject]@{ runner = [pscustomobject]@{ id = 77 }; encoded_jit_config = 'abc' } }
        $r = New-GhJitRunner -Name 'slate-win-app-deadbeef' -Labels @('slate-win-app')
        $r.RunnerId | Should -Be 77
        $r.EncodedJitConfig | Should -Be 'abc'
        Should -Invoke Invoke-RestMethod -Times 1 -ParameterFilter {
            $body = $Body | ConvertFrom-Json
            $Method -eq 'POST' -and $Uri -like '*/actions/runners/generate-jitconfig' -and
            $body.name -eq 'slate-win-app-deadbeef' -and $body.runner_group_id -eq 1 -and
            @($body.labels) -contains 'slate-win-app' -and $body.work_folder -eq '_work'
        }
    }
}

Describe 'Get-GhLaneJobs' {
    It 'lists queued and in-progress runs and returns their queued and running jobs, all as conditional requests' {
        Mock Invoke-RestMethod {
            Set-OkStatus $StatusCodeVariable
            if ($Uri -like '*actions/runs?status=queued*') { return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 1; path = '.github/workflows/windows.yml' }) } }
            if ($Uri -like '*actions/runs?status=in_progress*') { return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 2; path = '.github/workflows/nightly.yml' }) } }
            if ($Uri -like '*/runs/1/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 11; run_id = 1; status = 'queued'; labels = @('slate-win-app'); created_at = '2026-10-10T10:00:00Z' }) }
            }
            if ($Uri -like '*/runs/2/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @(
                    [pscustomobject]@{ id = 21; run_id = 2; status = 'in_progress'; labels = @('slate-win-rust'); created_at = '2026-10-10T10:00:00Z' },
                    [pscustomobject]@{ id = 22; run_id = 2; status = 'queued'; labels = @('slate-win-model'); created_at = '2026-10-10T10:00:01Z' }
                ) }
            }
            throw "unexpected $Uri"
        }
        $jobs = @(Get-GhLaneJobs -RoutedWorkflows $routed)
        # The running job carries runner_name: it tells the host which job its runner took.
        @($jobs | ForEach-Object { $_.id }) | Should -Be @(11, 21, 22)
        Should -Invoke Invoke-RestMethod -Times 4 -Exactly
        # Every listing and jobs call is conditional: a 304 costs no budget.
        Should -Invoke Invoke-RestMethod -Times 4 -Exactly -ParameterFilter { $SkipHttpErrorCheck -and $StatusCodeVariable -eq 'ghStatusCode' }
    }
    It 'returns an empty array when nothing is queued' {
        Mock Invoke-RestMethod { Set-OkStatus $StatusCodeVariable; [pscustomobject]@{ workflow_runs = @() } }
        @(Get-GhLaneJobs -RoutedWorkflows $routed).Count | Should -Be 0
    }
    It 'returns a run''s queued jobs once when it appears in both listings' {
        # The run moved from queued to in_progress between the two listings.
        Mock Invoke-RestMethod {
            Set-OkStatus $StatusCodeVariable
            if ($Uri -like '*actions/runs?status=*') { return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 1; path = '.github/workflows/windows.yml' }) } }
            if ($Uri -like '*/runs/1/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 11; run_id = 1; status = 'queued'; labels = @('slate-win-app'); created_at = '2026-10-10T10:00:00Z' }) }
            }
            throw "unexpected $Uri"
        }
        $jobs = @(Get-GhLaneJobs -RoutedWorkflows $routed)
        $jobs.Count | Should -Be 1
        $jobs[0].id | Should -Be 11
        Should -Invoke Invoke-RestMethod -Times 3 -Exactly
    }
    It 'never reads the jobs of a run whose workflow is not routed' {
        # Every other workflow's runs cost a jobs call per tick for nothing:
        # only the routed workflows carry slate-win-* labels.
        Mock Invoke-RestMethod {
            Set-OkStatus $StatusCodeVariable
            if ($Uri -like '*actions/runs?status=queued*') {
                return [pscustomobject]@{ workflow_runs = @(
                    [pscustomobject]@{ id = 1; path = '.github/workflows/rust.yml' },
                    [pscustomobject]@{ id = 2; path = '.github/workflows/windows-ci-pilot.yml' },
                    [pscustomobject]@{ id = 3 }
                ) }
            }
            if ($Uri -like '*actions/runs?status=in_progress*') {
                return [pscustomobject]@{ workflow_runs = @(
                    [pscustomobject]@{ id = 4; path = '.github/workflows/Windows.yml' },
                    [pscustomobject]@{ id = 5; path = '.github/workflows/windows.yml@refs/heads/main' }
                ) }
            }
            if ($Uri -like '*/runs/2/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 21; run_id = 2; status = 'queued'; labels = @('slate-win-app'); created_at = '2026-10-10T10:00:00Z' }) }
            }
            if ($Uri -like '*/runs/5/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 51; run_id = 5; status = 'queued'; labels = @('slate-win-rust'); created_at = '2026-10-10T10:00:01Z' }) }
            }
            throw "unexpected $Uri"
        }
        $jobs = @(Get-GhLaneJobs -RoutedWorkflows $routed)
        @($jobs | ForEach-Object { $_.id }) | Should -Be @(21, 51)
        foreach ($skipped in 1, 3, 4) {
            Should -Invoke Invoke-RestMethod -Times 0 -Exactly -ParameterFilter { $Uri -like "*/runs/$skipped/jobs*" }
        }
        Should -Invoke Invoke-RestMethod -Times 4 -Exactly
    }
    It 'requires the routed workflow list' {
        $parameter = (Get-Command Get-GhLaneJobs).Parameters['RoutedWorkflows']
        @($parameter.Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] } | ForEach-Object { $_.Mandatory }) | Should -Contain $true
    }
}

Describe 'Get-GhRunner' {
    It 'returns null on 404 and rethrows anything else' {
        Mock Invoke-RestMethod {
            $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::NotFound)
            throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('not found', $response)
        }
        Get-GhRunner -RunnerId 5 | Should -BeNullOrEmpty
        Mock Invoke-RestMethod {
            $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::InternalServerError)
            throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('boom', $response)
        }
        { Get-GhRunner -RunnerId 5 } | Should -Throw
    }
}

Describe 'Remove-GhRunner / Get-GhRunners' {
    It 'deletes by id and lists runners' {
        Mock Invoke-RestMethod { [pscustomobject]@{ runners = @([pscustomobject]@{ id = 1; name = 'slate-win-app-a'; status = 'offline' }) } }
        Remove-GhRunner -RunnerId 9
        Should -Invoke Invoke-RestMethod -Times 1 -ParameterFilter { $Method -eq 'DELETE' -and $Uri -like '*/actions/runners/9' }
        @(Get-GhRunners).Count | Should -Be 1
    }
}

Describe 'Set-GhVariable' {
    It 'patches the variable, and creates it with POST when the patch answers 404' {
        Mock Invoke-RestMethod { [pscustomobject]@{} }
        Set-GhVariable -Name 'WINDOWS_HOME_STATE' -Value '{"v":1}'
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
            $Method -eq 'PATCH' -and $Uri -like '*/actions/variables/WINDOWS_HOME_STATE' -and
            ($Body | ConvertFrom-Json).name -eq 'WINDOWS_HOME_STATE' -and ($Body | ConvertFrom-Json).value -eq '{"v":1}'
        }
        Should -Invoke Invoke-RestMethod -Times 0 -Exactly -ParameterFilter { $Method -eq 'POST' }
        Mock Invoke-RestMethod {
            if ($Method -eq 'PATCH') {
                $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::NotFound)
                throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('not found', $response)
            }
            [pscustomobject]@{}
        }
        Set-GhVariable -Name 'WINDOWS_HOME_STATE' -Value '{"v":1}'
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter {
            $Method -eq 'POST' -and $Uri -like '*/actions/variables' -and
            ($Body | ConvertFrom-Json).name -eq 'WINDOWS_HOME_STATE' -and ($Body | ConvertFrom-Json).value -eq '{"v":1}'
        }
    }
    It 'rethrows a patch that fails with anything but 404, and never tries POST then' {
        Mock Invoke-RestMethod {
            $response = [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]::Forbidden)
            throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('forbidden', $response)
        }
        { Set-GhVariable -Name 'WINDOWS_HOME_STATE' -Value '{"v":1}' } | Should -Throw '*forbidden*'
        Should -Invoke Invoke-RestMethod -Times 1 -Exactly -ParameterFilter { $Method -eq 'PATCH' }
        Should -Invoke Invoke-RestMethod -Times 0 -Exactly -ParameterFilter { $Method -eq 'POST' }
    }
}

Describe 'New-GitHubAdapters' {
    It 'exposes exactly the keys the orchestrator invokes' {
        @((New-GitHubAdapters -RoutedWorkflows $routed).Keys | Sort-Object) | Should -Be @('GetJob', 'GetLaneJobs', 'GetRun', 'GetRunner', 'ListRunners', 'NewJitRunner', 'PublishState', 'RemoveRunner')
    }
    It 'requires the routed workflow list' {
        $parameter = (Get-Command New-GitHubAdapters).Parameters['RoutedWorkflows']
        @($parameter.Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] } | ForEach-Object { $_.Mandatory }) | Should -Contain $true
    }
    It 'binds the routed workflows into the closures the module invokes later' {
        Mock Invoke-RestMethod {
            Set-OkStatus $StatusCodeVariable
            if ($Uri -like '*actions/runs?status=queued*') {
                return [pscustomobject]@{ workflow_runs = @([pscustomobject]@{ id = 1; path = '.github/workflows/windows.yml' }, [pscustomobject]@{ id = 2; path = '.github/workflows/nightly.yml' }) }
            }
            if ($Uri -like '*actions/runs?status=in_progress*') { return [pscustomobject]@{ workflow_runs = @() } }
            if ($Uri -like '*/runs/1/jobs?filter=latest*') {
                return [pscustomobject]@{ jobs = @([pscustomobject]@{ id = 11; run_id = 1; status = 'queued'; labels = @('slate-win-app'); created_at = '2026-10-10T10:00:00Z' }) }
            }
            if ($Uri -like '*/actions/jobs/7') { return [pscustomobject]@{ id = 7; runner_name = 'slate-win-app-deadbeef' } }
            throw "unexpected $Uri"
        }
        $a = New-GitHubAdapters -RoutedWorkflows @('.github/workflows/windows.yml')
        @(& $a.GetLaneJobs | ForEach-Object { $_.id }) | Should -Be @(11)
        Should -Invoke Invoke-RestMethod -Times 0 -Exactly -ParameterFilter { $Uri -like '*/runs/2/jobs*' }
        (& $a.GetJob 7).runner_name | Should -Be 'slate-win-app-deadbeef'
    }
    It 'refuses to work before initialisation' {
        . (Join-Path $PSScriptRoot '..' 'adapters' 'GitHub.ps1')
        { Invoke-GhApi -Path '/x' } | Should -Throw '*Initialize-GitHubAdapter*'
        Initialize-GitHubAdapter -Owner 'coryj627' -Repo 'slate' -Token (ConvertTo-SecureString 'ghp_test' -AsPlainText -Force)
    }
}

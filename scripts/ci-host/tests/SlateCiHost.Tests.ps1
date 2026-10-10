# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '..' 'SlateCiHost.psm1') -Force
}

Describe 'Get-LaneFromLabels' {
    It 'returns the lane when exactly one lane label is present' {
        Get-LaneFromLabels -Labels @('self-hosted', 'Windows', 'X64', 'slate-win-app') | Should -Be 'app'
    }
    It 'returns null when no lane label is present' {
        Get-LaneFromLabels -Labels @('self-hosted', 'windows-latest') | Should -BeNullOrEmpty
    }
    It 'returns null for two lane labels' {
        Get-LaneFromLabels -Labels @('slate-win-app', 'slate-win-model') | Should -BeNullOrEmpty
    }
    It 'ignores an unknown lane name' {
        Get-LaneFromLabels -Labels @('slate-win-nightly') | Should -BeNullOrEmpty
    }
    It 'ignores labels that merely contain the prefix' {
        Get-LaneFromLabels -Labels @('slate-win-app-old', 'xslate-win-app') | Should -BeNullOrEmpty
    }
    It 'handles an empty label list' {
        Get-LaneFromLabels -Labels @() | Should -BeNullOrEmpty
    }
    It 'normalises the lane to lower case' {
        Get-LaneFromLabels -Labels @('SLATE-WIN-APP') | Should -BeExactly 'app'
    }
}

Describe 'New-RunnerName' {
    It 'produces slate-win-`<lane`>-`<8 hex`>' {
        New-RunnerName -Lane 'model' | Should -Match '^slate-win-model-[0-9a-f]{8}$'
    }
    It 'is unique across calls' {
        (1..20 | ForEach-Object { New-RunnerName -Lane 'rust' } | Sort-Object -Unique).Count | Should -Be 20
    }
}

Describe 'Split-KvpChunks / Join-KvpChunks' {
    It 'splits into 1000-character chunks with a count item' {
        $text = 'a' * 2500
        $items = Split-KvpChunks -Text $text
        $items['slate.jit.count'] | Should -Be '3'
        $items['slate.jit.0'].Length | Should -Be 1000
        $items['slate.jit.2'].Length | Should -Be 500
        $items.Keys.Count | Should -Be 4
    }
    It 'round-trips an arbitrary payload' {
        $text = -join ((1..3333) | ForEach-Object { [char](33 + ($_ % 90)) })
        Join-KvpChunks -Items (Split-KvpChunks -Text $text) | Should -Be $text
    }
    It 'round-trips a payload that is an exact multiple of the chunk size (boundary)' {
        $text = 'b' * 3000
        $items = Split-KvpChunks -Text $text
        $items['slate.jit.count'] | Should -Be '3'
        Join-KvpChunks -Items $items | Should -Be $text
    }
    It 'round-trips an empty payload' {
        $items = Split-KvpChunks -Text ''
        $items['slate.jit.count'] | Should -Be '0'
        Join-KvpChunks -Items $items | Should -Be ''
    }
    It 'never emits a value longer than 1024 characters at any chunk size' {
        foreach ($size in 1, 7, 999, 1000) {
            $items = Split-KvpChunks -Text ('c' * 2048) -ChunkSize $size
            foreach ($k in $items.Keys) { $items[$k].Length | Should -BeLessOrEqual 1024 }
        }
    }
    It 'refuses a chunk size above 1024' {
        { Split-KvpChunks -Text 'x' -ChunkSize 1025 } | Should -Throw
    }
    It 'returns null when the count item is missing' {
        Join-KvpChunks -Items @{ 'slate.jit.0' = 'x' } | Should -BeNullOrEmpty
    }
    It 'returns null when a chunk is missing' {
        Join-KvpChunks -Items @{ 'slate.jit.count' = '2'; 'slate.jit.0' = 'x' } | Should -BeNullOrEmpty
    }
    It 'honours a custom prefix' {
        $items = Split-KvpChunks -Text 'hello' -Prefix 'p'
        $items['p.count'] | Should -Be '1'
        Join-KvpChunks -Items $items -Prefix 'p' | Should -Be 'hello'
    }
}

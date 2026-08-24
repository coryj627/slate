// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-1 PR A (#745): the source-shape pin for the media gate's ONE
// coherent snapshot (codex round 4, B1). The swap sub-window between the
// containment decision and the identity CAPTURE cannot be driven by an
// unprivileged in-process race — an attacker would need to win a swap
// inside a single method's handle-held region — so the coherence
// property is pinned STRUCTURALLY here, and the check→launch swap (which
// CAN be driven) is exercised by
// `CanvasDocumentTests.ASwapInTheTocTouWindowIsCaughtByRevalidation`.
// The full swap-during-capture E2E is recorded as manual in CD-38.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "canvas-media-gate")]
public sealed class CanvasMediaGateCensus
{
    private const string GateFile = "ExternalLinkPolicy.cs";

    /// <summary>The path-reopening identity call: a fresh
    /// <c>CreateFile</c> by string.</summary>
    private const string PathReopen = "IdentityOf";

    /// <summary>The handle-reading identity call: reads the OS identity
    /// off an already-open, still-HELD handle.</summary>
    private const string HandleRead = "IdentityOfHandle";

    /// <summary>
    /// Round 4, B1: the snapshot must capture the leaf identity from the
    /// SAME held handle it made the containment decision on — never a
    /// fresh re-open by path. The old shape returned
    /// <c>(resolved, IdentityOf(resolved))</c>, re-opening the path to
    /// capture; a swap between the containment open and that re-open made
    /// the captured identity the OUTSIDE object, and revalidating
    /// outside-against-outside passed. So <c>ResolveContained</c> must
    /// read identity ONLY off handles (<c>IdentityOfHandle</c>) and must
    /// NOT call the path-reopening <c>IdentityOf</c> at all.
    /// </summary>
    [Fact]
    public void TheSnapshotCapturesIdentityFromTheHeldHandleNotAReopen()
    {
        MethodDeclarationSyntax resolve =
            CSharpSource.Load(GateFile).Method("ResolveContained");

        // It reads identity off a held handle...
        Assert.True(
            InvokesBareName(resolve, HandleRead),
            $"ResolveContained must capture identity from a held handle "
            + $"({HandleRead}); without it there is no coherent snapshot.");

        // ...and it must NEVER re-open by path to capture — that reopen IS
        // the check→capture window (round 4, B1). The old buggy shape,
        // `return (resolved, IdentityOf(resolved))`, is exactly this call,
        // and reinstating it trips here.
        Assert.False(
            InvokesBareName(resolve, PathReopen),
            $"ResolveContained must NOT call the path-reopening {PathReopen}: "
            + "capturing the identity by re-opening the resolved path opens the "
            + "very swap window the snapshot exists to close (codex round 4, B1). "
            + "Capture must come off the handle already held for containment.");

        // The handles are HELD to the decision: a List<SafeFileHandle>
        // accumulates them and a finally disposes them, so the leaf
        // identity, the resolved path and the ancestor chain are one
        // coherent view rather than three independent opens.
        Assert.Contains(
            resolve.DescendantNodes().OfType<GenericNameSyntax>(),
            generic => generic.Identifier.ValueText == "List"
                && generic.TypeArgumentList.Arguments.ToString()
                    .Contains("SafeFileHandle", StringComparison.Ordinal));
        Assert.NotNull(
            resolve.Body?.DescendantNodes().OfType<TryStatementSyntax>()
                .FirstOrDefault(node => node.Finally is not null));
    }

    /// <summary>
    /// Two-sided (the guard must not pass because the mechanism vanished):
    /// the path-reopening <c>IdentityOf</c> DOES exist and IS what
    /// revalidation calls in <c>OpenMediaInVault</c>, immediately before
    /// launch. If it were deleted, the census above ("must not call
    /// IdentityOf in ResolveContained") would pass for the wrong reason —
    /// there would be no revalidation left to gate the launch.
    /// </summary>
    [Fact]
    public void TheLaunchIsRevalidatedByAFreshIdentityRead()
    {
        MethodDeclarationSyntax open =
            CSharpSource.Load(GateFile).Method("OpenMediaInVault");

        Assert.True(
            InvokesBareName(open, PathReopen),
            $"OpenMediaInVault must re-read identity ({PathReopen}) before launch: "
            + "the snapshot captured coherently, but a swap can still redirect the "
            + "path between snapshot and launch, so the launch is gated on the "
            + "resolved path still naming the SAME identity.");

        // And the launch is downstream of that re-read: the revalidation
        // returns false when the identities differ, so `launch(...)` can
        // only be reached past a passing compare.
        Assert.Contains(
            open.DescendantNodes().OfType<InvocationExpressionSyntax>(),
            invocation => invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "launch" });
    }

    /// <summary>
    /// Round 4 #3: the ancestor walk carries NO fixed depth cap. The
    /// reparse-cycle bound belonged to link resolution, not to the lexical
    /// parent walk, which shortens strictly and ends at the volume root.
    /// A surviving <c>ResolveRounds</c>-style constant on the walk refuses
    /// valid media past that depth (fail-closed availability), so the
    /// symbol must be gone from the gate entirely.
    /// </summary>
    [Fact]
    public void TheAncestorWalkCarriesNoDepthCap()
    {
        CSharpSource gate = CSharpSource.Load(GateFile);
        Assert.DoesNotContain(
            gate.Root.DescendantNodes().OfType<IdentifierNameSyntax>(),
            name => name.Identifier.ValueText == "ResolveRounds");
    }

    /// <summary>
    /// Round 5: there is exactly ONE identity method in the gate. The
    /// legacy 64-bit <c>BY_HANDLE_FILE_INFORMATION</c> path is DELETED,
    /// not merely unreached — a per-call fallback downgraded any transient
    /// primary failure to the ReFS-non-unique <c>nFileIndex</c>, and the
    /// app's minimum OS postdates <c>FileIdInfo</c> by years, so it served
    /// no supported platform. Its existence WAS the mixed-method class, so
    /// the symbols must be absent rather than dormant.
    /// </summary>
    [Fact]
    public void TheGateHasExactlyOneIdentityMethod()
    {
        CSharpSource gate = CSharpSource.Load(GateFile);
        string[] banned =
        [
            "GetFileInformationByHandle",
            "ByHandleFileInformation",
            "FileIndexHigh",
            "FileIndexLow",
        ];

        var found = gate.Root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Select(name => name.Identifier.ValueText)
            .Where(name => banned.Contains(name, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            found.Length == 0,
            "the legacy 64-bit identity path must not exist in the media gate — a "
            + "per-call fallback turns any transient FileIdInfo failure into a "
            + "ReFS-unsafe identity (codex round 5). Found: "
            + string.Join(", ", found));

        // Two-sided: the ONE identity method is still here. Without this,
        // deleting identity queries altogether would satisfy the ban above.
        Assert.True(
            CSharpSource.References(gate.Root, "TryGetFileIdInfo"),
            "the gate must still query the 128-bit FILE_ID_INFO identity; the "
            + "ban above would otherwise pass with no identity method at all.");
    }

    /// <summary>Whether <paramref name="node"/> calls a bare, un-qualified
    /// method of this name — <c>Name(...)</c>, not <c>x.Name(...)</c>.
    /// The gate's own helpers are called bare.</summary>
    private static bool InvokesBareName(SyntaxNode node, string name) =>
        node.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation => invocation.Expression
                is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == name);
}

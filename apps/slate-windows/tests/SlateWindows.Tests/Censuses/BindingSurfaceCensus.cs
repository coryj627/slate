// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Full-surface binding smoke (w0_spec §W0-3 item 1, #715): the generated
// binding assembly exposes the complete slate-uniffi surface — all four
// object types, all three foreign-callback traits, and the free-function
// entry points — and a representative call on each object type works.
// The binding is generated (git-ignored) so this census is what proves
// "the entire surface bound" on every CI run rather than once.

using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "binding-surface")]
public class BindingSurfaceCensus
{
    [Fact]
    public void AllFiveObjectTypes_AndAllThreeForeignTraits_AreBound()
    {
        var assembly = typeof(VaultSession).Assembly;

        // uniffi::Object types: the w0-baseline four plus LayoutSession
        // (Milestone P). Keep in lockstep with the census_live counters in
        // crates/slate-uniffi/src/lib.rs.
        foreach (var name in new[] { "VaultSession", "CancelToken", "DocumentBuffer", "CommandRegistry", "LayoutSession" })
        {
            var type = assembly.GetTypes().SingleOrDefault(t => t.Name == name && t.IsClass);
            Assert.True(type != null, $"object type {name} missing from the binding");
            Assert.True(
                typeof(IDisposable).IsAssignableFrom(type),
                $"{name} lost its IDisposable lifetime contract");
        }

        // Foreign-callback traits arrive as interfaces the host implements.
        foreach (var name in new[] { "ScanProgressListener", "VaultEventListener", "CommandAction" })
        {
            Assert.True(
                assembly.GetTypes().Any(t => t.Name == name && t.IsInterface),
                $"foreign trait {name} missing from the binding");
        }
    }

    [Fact]
    public void RepresentativeCallOnEachObjectType_Works()
    {
        using var vault = FixtureVault.Create(3);
        using var session = VaultSession.OpenFilesystem(vault.Root);
        using var token = new CancelToken();
        Assert.Equal(3UL, session.ScanInitial(token).FilesIndexed);
        Assert.False(token.IsCancelled());

        const string initialText = "abc 中 😀";
        using var buffer = new DocumentBuffer(initialText);
        buffer.ApplyEdit(3, 0, "\r\n");
        Assert.Equal((uint)(initialText.Length + 2), buffer.LenUtf16());
        Assert.Equal("abc\r\n 中 😀", buffer.Text());
        Assert.Equal(
            SlateUniffiMethods.EditorTextContentHash(buffer.Text()),
            buffer.ContentHash());

        using var registry = new CommandRegistry();
        var action = new ScriptedAction(() => { });
        _ = registry.Register(new Command("census.ok", "OK", null, null, CommandSection.File), action);
        registry.InvokeById("census.ok");
        Assert.Equal(1, action.InvocationCount);

        using var layout = session.StartGraphLayout(
            new GraphFilter(false, false, false), new LayoutForces(), new LayoutConfig());
        Assert.True(layout.Tick(1).Iteration >= 1);

        // Free functions bind through the static entry point.
        var spans = SlateUniffiMethods.EditorHighlightSpans("# Heading\n\nBody with #tag\n");
        Assert.NotEmpty(spans);
    }
}

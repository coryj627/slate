// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using SlateWindows;

internal static class Program
{
    internal static string Fixture => "# Heading one\n## Heading two\nPlain text and 😀 Unicode.\n"
        + "Before [[Target]] after.\nEmbed ![[Target]] ends.\nTag #project ends.\nCitation [@smith2020] ends.\n"
        + "Website [Website](https://example.org) ends.\nImage ![Picture](picture.png) ends.\n"
        + "*Emphasis* and **Strong** and ~~Strike~~ and `inline code`.\n"
        + "> A [quoted link](https://example.org/quote).\n%% [[hidden comment]] %%\n"
        + "```rust\nlet value = \"[[hidden code]]\";\n```\n"
        + string.Concat(Enumerable.Range(1, 45).Select(i => $"Plain filler line {i}.\n"))
        + "Offscreen [[FarAway]] ends.\n";

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--probe") { return Probe.Run(args[1], args[2]); }
        if (args.FirstOrDefault() == "--bench") { return Benchmark(args[1]); }
        if (args.FirstOrDefault() == "--self-check") { return SelfCheck(args[1]); }
        string evidence = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(AppContext.BaseDirectory, "evidence"));
        Directory.CreateDirectory(evidence);
        var app = new Application();
        app.DispatcherUnhandledException += (_, e) => File.AppendAllText(Path.Combine(evidence, "server-errors.log"), e.Exception + "\n");
        using var session = new AvalonDocumentBufferSession(Fixture, _ => { });
        var editor = CreateEditor(session);
        var status = new TextBox { IsReadOnly = true, Text = "Ready", Margin = new Thickness(6) };
        AutomationProperties.SetAutomationId(status, "PrototypeStatus");
        var panel = new DockPanel();
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(controls, Dock.Top); panel.Children.Add(controls);
        DockPanel.SetDock(status, Dock.Bottom); panel.Children.Add(status); panel.Children.Add(editor);
        var window = new Window { Title = "Slate editor Hyperlink PROTOTYPE", Content = panel, Width = 1000, Height = 760 };
        void Log(string text)
        {
            status.Text = text;
            File.AppendAllText(Path.Combine(evidence, "server-actions.log"), $"{DateTime.UtcNow:O} {text}\n");
        }
        void Button(string id, string name, Action action)
        {
            var button = new Button { Content = name, Margin = new Thickness(4), Padding = new Thickness(8) };
            AutomationProperties.SetAutomationId(button, id);
            button.Click += (_, _) => { action(); editor.FocusInputOwner(); };
            controls.Children.Add(button);
        }
        Button("ResetFixture", "Reset fixture", () => { session.Document.Text = Fixture; editor.CaretOffset = 0; Log("Reset"); });
        Button("InsertPrefix", "Insert prefix", () => { session.Document.Insert(0, "prefix\n"); Log("Prefix inserted"); });
        Button("RemoveFirst", "Remove first link", () =>
        {
            var peer = (SlateTextEditorAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(editor);
            var provider = (EditorSemanticTextProvider)peer.GetPattern(PatternInterface.Text)!;
            var first = provider.PrototypeLinks.Links().First();
            session.Document.Remove(first.Start, first.End - first.Start); Log("First link removed");
        });
        Button("GoStart", "Go to start", () => { editor.CaretOffset = 0; editor.ScrollToHome(); Log("At start"); });
        Button("GoLink", "Go to first link", () =>
        {
            editor.CaretOffset = editor.Text.IndexOf("[[Target]]", StringComparison.Ordinal);
            editor.ScrollToLine(editor.Document.GetLineByOffset(editor.CaretOffset).LineNumber); Log("At first link");
        });
        editor.PrototypeLinkInvoked = offset => Log($"Invoked link at {offset}; {session.Document.GetText(offset, Math.Min(35, session.Document.TextLength - offset)).Replace('\n', ' ')}");
        window.Loaded += (_, _) =>
        {
            File.WriteAllText(Path.Combine(evidence, "server.hwnd"), new WindowInteropHelper(window).Handle.ToInt64().ToString());
            File.WriteAllText(Path.Combine(evidence, "fixture.md"), Fixture);
            editor.FocusInputOwner();
        };
        return app.Run(window);
    }

    private static SlateTextEditor CreateEditor(AvalonDocumentBufferSession session)
    {
        var editor = new SlateTextEditor { Document = session.Document, HighlightSession = session, FontSize = 18, FontFamily = new FontFamily("Consolas"), WordWrap = true };
        AutomationProperties.SetAutomationId(editor, "PrototypeEditor");
        AutomationProperties.SetName(editor, "Prototype Markdown editor");
        foreach (string key in new[] { EditorSyntaxPalette.HeadingBrushKey, EditorSyntaxPalette.CodeBrushKey,
            EditorSyntaxPalette.WikilinkBrushKey, EditorSyntaxPalette.TagBrushKey, EditorSyntaxPalette.MetadataBrushKey })
        { editor.Resources[key] = Brushes.DarkBlue; }
        return editor;
    }

    private static int Benchmark(string path)
    {
        var lines = new List<string> { "Prototype costs: full canonical rebuild on revision change; cached navigation thereafter." };
        foreach (int size in new[] { 100 * 1024, 1024 * 1024, 8 * 1024 * 1024 })
        {
            string block = "## Heading\nPlain text for size fixture.\n\n";
            string text = string.Concat(Enumerable.Repeat(block, size / block.Length)) + "[[Target]]";
            using var session = new AvalonDocumentBufferSession(text, _ => { });
            var editor = CreateEditor(session);
            var peer = (SlateTextEditorAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(editor);
            var provider = (EditorSemanticTextProvider)peer.GetPattern(PatternInterface.Text)!;
            var watch = Stopwatch.StartNew(); provider.PrototypeLinks.Links(); watch.Stop();
            double cold = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); for (int i = 0; i < 1000; i++) provider.PrototypeLinks.Enclosing(0, 10); watch.Stop();
            double cached = watch.Elapsed.TotalMilliseconds / 1000;
            session.Document.Insert(0, "x"); watch.Restart(); provider.PrototypeLinks.Links(); watch.Stop();
            lines.Add($"{size} bytes: initial={cold:F3}ms cached={cached:F5}ms edit-rebuild={watch.Elapsed.TotalMilliseconds:F3}ms");
        }
        File.WriteAllLines(path, lines); return 0;
    }

    private static int SelfCheck(string path)
    {
        var log = new List<string> { "In-process checks only; not a substitute for cross-process UIA or NVDA." };
        try
        {
            void Check(bool value, string label) { if (!value) throw new Exception(label); log.Add("PASS " + label); }
            using var session = new AvalonDocumentBufferSession(Fixture, _ => { });
            var editor = CreateEditor(session);
            var peer = (SlateTextEditorAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(editor);
            var provider = (EditorSemanticTextProvider)peer.GetPattern(PatternInterface.Text)!;
            var links = provider.PrototypeLinks.Links();
            Check(links.Count == 8, "canonical links include offscreen text and exclude code/comments");
            Check(provider.DocumentRange.GetText(-1) == Fixture, "continuous source unchanged");
            Check(ReferenceEquals(provider.DocumentRange.GetAttributeValue(40035), AutomationElement.NotSupported), "generic Link text attribute removed");
            var start = System.Windows.Automation.Text.TextPatternRangeEndpoint.Start;
            var end = System.Windows.Automation.Text.TextPatternRangeEndpoint.End;
            foreach (var link in links)
            {
                var range = provider.Range(link.Start, link.End);
                var found = provider.DocumentRange.FindText(range.GetText(-1), false, false)!;
                Check(range.Compare(found) && found.Compare(range), "Compare both directions: " + link.GetName());
                Check(range.CompareEndpoints(start, found, start) == 0 && found.CompareEndpoints(end, range, end) == 0, "CompareEndpoints both directions");
                var ordinary = provider.DocumentRange.Clone();
                ordinary.MoveEndpointByRange(start, range, start); ordinary.MoveEndpointByRange(end, range, end);
                var reverse = range.Clone(); reverse.MoveEndpointByRange(start, found, start); reverse.MoveEndpointByRange(end, found, end);
                Check(ordinary.Compare(reverse), "MoveEndpointByRange both directions");
                Check(provider.PrototypeLinks.Enclosing(link.Start, link.End) == link, "link owns full range");
                Check(provider.PrototypeLinks.Enclosing(link.End, link.End) != link, "end boundary exits link");
            }
            var first = links[0]; int before = first.Start;
            session.Document.Insert(0, "prefix\n");
            Check(ReferenceEquals(provider.PrototypeLinks.Links()[0], first) && first.Start == before + 7, "identity and anchors survive prefix insertion");
            session.Document.Remove(first.Start, first.End - first.Start);
            Check(provider.PrototypeLinks.Links().Count == 7 && !provider.PrototypeLinks.Contains(first), "deleted child removed");
            session.Document.UndoStack.Undo();
            Check(provider.PrototypeLinks.Links().Count == 8, "undo restores semantic link");
            log.Add("ALL IN-PROCESS CHECKS PASSED"); File.WriteAllLines(path, log); return 0;
        }
        catch (Exception error) { log.Add("FAIL " + error); File.WriteAllLines(path, log); return 1; }
    }
}

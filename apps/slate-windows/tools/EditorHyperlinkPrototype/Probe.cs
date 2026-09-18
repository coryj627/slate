// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.IO;
using UIA = Interop.UIAutomationClient;

internal static class Probe
{
    internal static int Run(string hwndPath, string output)
    {
        var log = new List<string>();
        try
        {
            foreach (bool version8 in new[] { false, true })
            {
                UIA.IUIAutomation automation = version8 ? new UIA.CUIAutomation8() : new UIA.CUIAutomation();
                var window = automation.ElementFromHandle(new IntPtr(long.Parse(File.ReadAllText(hwndPath))));
                UIA.IUIAutomationElement Find(string id) => window.FindFirst(UIA.TreeScope.TreeScope_Descendants, automation.CreatePropertyCondition(30011, id));
                void Click(string id) => ((UIA.IUIAutomationInvokePattern)Find(id).GetCurrentPattern(10000)).Invoke();
                void Check(bool condition, string label) { if (!condition) throw new Exception(label); log.Add("PASS " + label); }
                void Wait(Func<bool> predicate)
                {
                    var watch = Stopwatch.StartNew(); while (!predicate() && watch.ElapsedMilliseconds < 10000) Thread.Sleep(50);
                    Check(predicate(), "deferred action completed");
                }
                log.Add($"CLIENT {(version8 ? "CUIAutomation8" : "CUIAutomation")} PID={Environment.ProcessId}");
                Click("ResetFixture");
                var editor = Find("PrototypeEditor");
                var text = (UIA.IUIAutomationTextPattern)editor.GetCurrentPattern(10014);
                Wait(() => text.DocumentRange.GetText(-1) == Program.Fixture);
                var document = text.DocumentRange;
                var children = document.GetChildren();
                Check(children.Length == 8, "8 canonical links, excluding code/comment lookalikes and including offscreen");
                Check(document.GetText(-1) == Program.Fixture, "one continuous byte-exact source stream");
                var start = UIA.TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start;
                var end = UIA.TextPatternRangeEndpoint.TextPatternRangeEndpoint_End;
                Check((int)document.FindText("Heading two", 0, 0).GetAttributeValue(40034) == 70002, "heading StyleId retained");
                var first = children.GetElement(0);
                var firstRange = text.RangeFromChild(first);
                for (int i = 0; i < children.Length; i++)
                {
                    var child = children.GetElement(i);
                    var range = text.RangeFromChild(child);
                    Check(child.CurrentControlType == 50005, $"child {i} has Hyperlink role");
                    Check(range.GetText(-1) == child.CurrentName, $"child {i} range text equals source name");
                    Check(automation.CompareElements(range.GetEnclosingElement(), child) != 0, $"child {i} owns its range");
                    Check(range.GetChildren().Length == 0, $"child {i} has no recursive self child");
                    var found = document.FindText(child.CurrentName, 0, 0);
                    Check(range.Compare(found) != 0 && found.Compare(range) != 0, $"child {i} Compare works both directions");
                    Check(range.CompareEndpoints(start, found, start) == 0 && found.CompareEndpoints(end, range, end) == 0,
                        $"child {i} CompareEndpoints works both directions");
                    var ordinary = document.Clone(); ordinary.MoveEndpointByRange(start, range, start); ordinary.MoveEndpointByRange(end, range, end);
                    var reverse = range.Clone(); reverse.MoveEndpointByRange(start, found, start); reverse.MoveEndpointByRange(end, found, end);
                    Check(ordinary.Compare(reverse) != 0, $"child {i} MoveEndpointByRange works both directions without special clone normalization");
                }
                var firstId = first.CurrentAutomationId;
                Click("InsertPrefix"); Wait(() => text.DocumentRange.GetText(7) == "prefix\n");
                var shifted = text.DocumentRange.GetChildren().GetElement(0);
                Check(shifted.CurrentAutomationId == firstId, "child identity survives insertion before it");
                Check(text.RangeFromChild(first).GetText(-1) == "[[Target]]", "retained child maps to shifted source");
                ((UIA.IUIAutomationInvokePattern)first.GetCurrentPattern(10000)).Invoke();
                Wait(() => ((UIA.IUIAutomationValuePattern)Find("PrototypeStatus").GetCurrentPattern(10002)).CurrentValue.StartsWith("Invoked link at "));
                Click("RemoveFirst"); Wait(() => text.DocumentRange.GetChildren().Length == 7);
                bool staleRejected = false;
                try { text.RangeFromChild(first); }
                catch (Exception error) when (error is System.Runtime.InteropServices.COMException or ArgumentException)
                { staleRejected = true; }
                Check(staleRejected, "deleted child rejected instead of silently targeting another link");
                Click("ResetFixture");
            }
            log.Add("ALL PROTOTYPE UIA CHECKS PASSED"); File.WriteAllLines(output, log); return 0;
        }
        catch (Exception error) { log.Add("FAIL " + error); File.WriteAllLines(output, log); return 1; }
    }
}

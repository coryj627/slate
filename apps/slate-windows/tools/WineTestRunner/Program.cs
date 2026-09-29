// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Xunit.Runners;

// Usage: WineTestRunner.exe <class full name> [method name]
// Runs the class (or one of its facts) from SlateWindows.Tests.dll beside
// this exe and exits 0 only when at least one fact ran and none failed.
string className = args[0];
string? methodName = args.Length > 1 ? args[1] : null;
string assembly = System.IO.Path.Combine(AppContext.BaseDirectory, "SlateWindows.Tests.dll");
int failed = 0, passed = 0, skipped = 0;
object gate = new();
using var done = new ManualResetEventSlim();
using var runner = AssemblyRunner.WithoutAppDomain(assembly);
if (methodName is not null)
{
    runner.TestCaseFilter = testCase => testCase.TestMethod.Method.Name == methodName;
}
runner.OnTestPassed = info => { lock (gate) { passed++; Console.WriteLine($"PASS {info.MethodName} ({info.ExecutionTime:F3}s)"); } };
runner.OnTestFailed = info => { lock (gate) { failed++; Console.WriteLine($"FAIL {info.MethodName} ({info.ExecutionTime:F3}s): {info.ExceptionMessage}\n{info.ExceptionStackTrace}"); } };
runner.OnTestSkipped = info => { lock (gate) { skipped++; Console.WriteLine($"SKIP {info.MethodName}: {info.SkipReason}"); } };
runner.OnErrorMessage = info => { lock (gate) { failed++; Console.WriteLine($"ERROR {info.ExceptionType}: {info.ExceptionMessage}\n{info.ExceptionStackTrace}"); } };
runner.OnExecutionComplete = _ => done.Set();
runner.Start(new AssemblyRunnerStartOptions { TypesToRun = [className] });
done.Wait();
while (runner.Status != AssemblyRunnerStatus.Idle)
{
    Thread.Sleep(50);
}
Console.WriteLine($"RESULT passed={passed} failed={failed} skipped={skipped}");
return failed == 0 && passed > 0 ? 0 : 1;

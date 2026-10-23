// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Child-process probe for the test process's exit (#1555). It leaves WPF
// weak-event tables on STA threads that end without shutting their
// dispatcher down, as the test suite's thread helpers do. At exit WPF's
// shutdown listener Invokes each table's clean-up on its dispatcher and waits
// up to 300 ms for it, unless
// Switch.MS.Internal.DoNotInvokeInWeakEventTableShutdownListener is set.
// WeakEventExitTests times the exit with and without the switch.
//
//   WeakEventExitProbe <tables> [--switch]
//
// Prints "ready" once the tables exist; the process then exits.

using System.ComponentModel;
using System.Globalization;

namespace WeakEventExitProbe;

internal static class Program
{
    private static int Main(string[] args)
    {
        int tables = int.Parse(args[0], CultureInfo.InvariantCulture);
        if (args.Contains("--switch"))
        {
            // As SlateWindows.Tests' runtime config sets it: before WPF reads it.
            AppContext.SetSwitch("Switch.MS.Internal.DoNotInvokeInWeakEventTableShutdownListener", true);
        }
        for (int table = 0; table < tables; table++)
        {
            var thread = new Thread(() =>
            {
                var source = new Notifier();
                PropertyChangedEventManager.AddHandler(source, (_, _) => { }, string.Empty);
                GC.KeepAlive(source);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
        Console.WriteLine("ready");
        return 0;
    }

    private sealed class Notifier : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }
}

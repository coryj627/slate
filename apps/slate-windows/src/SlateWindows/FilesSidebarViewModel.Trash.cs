// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

internal sealed partial class FilesSidebarViewModel
{
    private bool _isTrashing;
    private Task _trashCompletion = Task.CompletedTask;
    internal Task TrashCompletion => _trashCompletion;
    internal Action<StagedTrash>? TrashStagedForTesting { get; set; }

    public bool IsTrashing
    {
        get => _isTrashing;
        private set
        {
            if (SetField(ref _isTrashing, value))
            {
                RaiseCommandStates();
            }
        }
    }

    // The recursive inventory and final revalidation stay off the dispatcher.
    // Shell Trash uses COM, so the worker owns an STA and a session lease.
    private Task<T> RunTrashWorkAsync<T>(Func<T> work)
    {
        if (!TryBeginSessionWork(out SessionWorkLease? lease))
        {
            return Task.FromException<T>(new OperationCanceledException());
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            try
            {
                T result;
                using (lease)
                {
                    result = work();
                }
                completion.TrySetResult(result);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        })
        { IsBackground = true, Name = "Slate Trash" };
        try
        {
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }
        catch
        {
            lease?.Dispose();
            throw;
        }
        return completion.Task;
    }
}

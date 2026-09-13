// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;
using System.Windows.Input;

namespace SlateWindows;

internal sealed partial class FilesSidebarViewModel
{
    private bool _isTrashing;
    private Task _trashCompletion = Task.CompletedTask;
    internal Task TrashCompletion => _trashCompletion;
    internal Action<StagedTrash>? TrashStagedForTesting { get; set; }
    internal Action<CancelToken>? TrashWorkerStartingForTesting { get; set; }
    private readonly object _trashGate = new();
    private TrashWorkOwner? _trashOwner;
    private TrashPhase _trashPhase;

    public ICommand CancelTrashCommand { get; private set; } = null!;
    public bool ShowTrashProgress => IsTrashing && _trashPhase != TrashPhase.Confirming;
    public bool CanCancelTrash => ShowTrashProgress && _trashPhase != TrashPhase.Stopping;
    public string TrashStatus => _trashPhase switch
    {
        TrashPhase.Preparing => "Preparing to move items to the Recycle Bin…",
        TrashPhase.Executing => "Moving items to the Recycle Bin…",
        TrashPhase.Stopping => "Stopping. Waiting for any item already in progress to finish…",
        _ => string.Empty,
    };

    public void CancelTrash()
    {
        lock (_trashGate)
        {
            if (_trashOwner is null || !_trashOwner.RequestCancellation()) { return; }
        }
        SetTrashPhase(TrashPhase.Stopping);
    }

    private TrashWorkOwner BeginTrash()
    {
        var owner = new TrashWorkOwner();
        lock (_trashGate) { _trashOwner = owner; }
        IsTrashing = true;
        BeginStructuralResult();
        SetTrashPhase(TrashPhase.Preparing);
        return owner;
    }

    private void SetTrashPhase(TrashPhase phase)
    {
        _trashPhase = phase;
        OnPropertyChanged(nameof(ShowTrashProgress));
        OnPropertyChanged(nameof(CanCancelTrash));
        OnPropertyChanged(nameof(TrashStatus));
        ((RelayCommand)CancelTrashCommand).RaiseCanExecuteChanged();
        if (!SessionShutdownStarted && phase != TrashPhase.Confirming)
        {
            ReportResult(TrashStatus);
        }
    }

    private bool ConfirmTrash(ref TrashWorkOwner owner, (string Title, string Message) request)
    {
        SetTrashPhase(TrashPhase.Confirming);
        if (!ConfirmRecycle(request) || SessionShutdownStarted || owner.CancellationRequested)
        {
            if (!SessionShutdownStarted)
            {
                ReportMutationResult("Cancelled. No items were moved to the Recycle Bin.");
            }
            return false;
        }
        // Confirmation starts a fresh execution owner. The enclosing structural
        // gate remains held, so another delete/undo cannot slip between phases.
        lock (_trashGate)
        {
            owner.Dispose();
            owner = new TrashWorkOwner();
            _trashOwner = owner;
        }
        return true;
    }

    private void EndTrash(TrashWorkOwner owner, StagedTrash? staged)
    {
        // Exact-token discard cannot erase a newer confirmation. Once shutdown
        // closes admission, session disposal retires its remaining confirmation.
        try
        {
            if (staged is not null)
            {
                TryRunSessionWork(() => _session.DiscardStagedTrash(staged.Token));
            }
        }
        finally
        {
            lock (_trashGate)
            {
                if (ReferenceEquals(_trashOwner, owner)) { _trashOwner = null; }
                owner.Dispose();
            }
            IsTrashing = false;
            OnPropertyChanged(nameof(ShowTrashProgress));
            OnPropertyChanged(nameof(CanCancelTrash));
        }
    }

    private Task<StagedTrash> PrepareTrashAsync(BatchTrashRequest request, TrashWorkOwner owner) =>
        RunTrashWorkAsync(() =>
        {
            StagedTrash staged = _session.StageTrashCancellable(request, owner.Token);
            if (owner.CancellationRequested || SessionShutdownStarted)
            {
                _session.DiscardStagedTrash(staged.Token);
                throw new OperationCanceledException();
            }
            return staged;
        }, owner);

    private enum TrashPhase { Preparing, Confirming, Executing, Stopping }

    private sealed class TrashWorkOwner : IDisposable
    {
        public CancelToken Token { get; } = new();
        private int _cancelled;
        public bool CancellationRequested => Volatile.Read(ref _cancelled) != 0;
        public bool RequestCancellation()
        {
            if (Interlocked.Exchange(ref _cancelled, 1) != 0) { return false; }
            Token.Cancel();
            return true;
        }
        public void Dispose() => Token.Dispose();
    }

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
    private Task<T> RunTrashWorkAsync<T>(Func<T> work, TrashWorkOwner owner)
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
                    TrashWorkerStartingForTesting?.Invoke(owner.Token);
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

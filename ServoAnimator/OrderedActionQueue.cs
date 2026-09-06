using System.Collections.Concurrent;
using System.Diagnostics;

namespace ServoAnimator
{
    /// <summary>
    /// Runs slow device operations on one background thread. A single consumer
    /// preserves the authored command order while keeping serial/ticcmd latency
    /// out of WPF's render and input thread.
    /// </summary>
    internal sealed class OrderedActionQueue : IDisposable
    {
        private readonly BlockingCollection<(long Generation, Action Action)> _actions = new();
        private long _generation;
        private readonly Task _worker;
        private bool _disposed;

        public OrderedActionQueue(string workerName)
        {
            _worker = Task.Factory.StartNew(() => Run(workerName),
                CancellationToken.None, TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void Enqueue(Action action)
        {
            if (action == null || _disposed) return;
            try { _actions.Add((Interlocked.Read(ref _generation), action)); }
            catch (InvalidOperationException) { }
        }

        public void ClearPending()
        {
            if (_disposed) return;
            Interlocked.Increment(ref _generation);
        }

        public void EnqueueBarrier(Action action)
        {
            if (action == null || _disposed) return;
            try { _actions.Add((-1, action)); }
            catch (InvalidOperationException) { }
        }

        private void Run(string workerName)
        {
            try { Thread.CurrentThread.Name ??= workerName; }
            catch { }

            foreach (var work in _actions.GetConsumingEnumerable())
            {
                // Also reject work already removed from the collection by the
                // consumer when cancellation occurred. An active device write
                // must finish; subsequent commands from that run are skipped.
                if (work.Generation >= 0 && work.Generation != Interlocked.Read(ref _generation)) continue;
                try { work.Action(); }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{workerName}] {ex.Message}");
                }
            }
        }

        public void Dispose() => Dispose(null);

        public void Dispose(Action afterDrain)
        {
            if (_disposed) return;
            ClearPending();
            if (afterDrain != null) EnqueueBarrier(afterDrain);
            _disposed = true;
            _actions.CompleteAdding();
            // Device disposal is itself ordered after an in-flight write.
            // A slow device may outlive this short UI wait but never races
            // cleanup on the closing window's thread.
            try { _worker.Wait(500); } catch { }
        }
    }
}

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
        private readonly BlockingCollection<Action> _actions = new();
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
            try { _actions.Add(action); }
            catch (InvalidOperationException) { }
        }

        public void ClearPending()
        {
            if (_disposed) return;
            while (_actions.TryTake(out _)) { }
        }

        private void Run(string workerName)
        {
            try { Thread.CurrentThread.Name ??= workerName; }
            catch { }

            foreach (Action action in _actions.GetConsumingEnumerable())
            {
                try { action(); }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{workerName}] {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            while (_actions.TryTake(out _)) { }
            _actions.CompleteAdding();
            try { _worker.Wait(500); } catch { }
        }
    }
}

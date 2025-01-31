namespace Dawn.Apps.ToggleRoundedCorners.Tools;

using System.Collections.Concurrent;
using System.Timers;
using System.Windows.Threading;

public class QueuedUpdater
{
    private readonly Dispatcher _dispatcher;
    private readonly ConcurrentQueue<Action> _updateQueue = [];
    private readonly Timer _timer;

    public QueuedUpdater(Dispatcher dispatcher, uint delay_ms)
    {
        _dispatcher = dispatcher;
        _timer = new(delay_ms);
        _timer.Elapsed += ProcessQueue;
        _timer.Start();
    }

    public void QueueUpdate(Action updateAction)
    {
        _updateQueue.Enqueue(updateAction);
    }

    private void ProcessQueue(object? sender, ElapsedEventArgs e)
    {
        // Process one action at a time from the queue
        if (_updateQueue.TryDequeue(out var action))
        {
            // Invoke the action (UI updates should be dispatched)
            _dispatcher.Invoke(action);
        }
    }

    public void Stop()
    {
        _timer.Stop();
    }
}
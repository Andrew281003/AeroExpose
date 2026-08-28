using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;

namespace AeroExpose.Animation;

/// <summary>Runs short UI animations on WPF's composition clock instead of a polling timer.</summary>
internal sealed class AnimationService
{
    private readonly Dispatcher _dispatcher;

    public AnimationService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task AnimateAsync(
        TimeSpan duration,
        Action<double> update,
        Func<double, double> easing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(easing);
        cancellationToken.ThrowIfCancellationRequested();

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();
        EventHandler? renderingHandler = null;
        CancellationTokenRegistration cancellationRegistration = default;
        var finished = false;

        void Finish(bool canceled)
        {
            if (finished)
            {
                return;
            }

            finished = true;
            if (renderingHandler is not null)
            {
                CompositionTarget.Rendering -= renderingHandler;
            }

            cancellationRegistration.Dispose();
            if (canceled)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            else
            {
                completion.TrySetResult();
            }
        }

        renderingHandler = (_, _) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Finish(true);
                return;
            }

            var rawProgress = duration <= TimeSpan.Zero
                ? 1d
                : Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0d, 1d);
            update(easing(rawProgress));
            if (rawProgress >= 1d)
            {
                Finish(false);
            }
        };

        cancellationRegistration = cancellationToken.Register(() =>
        {
            _dispatcher.BeginInvoke(() => Finish(true), DispatcherPriority.Send);
        });
        CompositionTarget.Rendering += renderingHandler;
        return completion.Task;
    }
}

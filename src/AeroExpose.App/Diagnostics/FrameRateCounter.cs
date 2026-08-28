using System.Diagnostics;
using System.Windows.Media;

namespace AeroExpose.Diagnostics;

internal sealed class FrameRateCounter : IDisposable
{
    private readonly Stopwatch _stopwatch = new();
    private Action<double>? _report;
    private int _frames;
    private bool _running;

    public void Start(Action<double> report)
    {
        Stop();
        _report = report;
        _frames = 0;
        _stopwatch.Restart();
        CompositionTarget.Rendering += OnRendering;
        _running = true;
    }

    public void Stop()
    {
        if (_running)
        {
            CompositionTarget.Rendering -= OnRendering;
        }

        _running = false;
        _stopwatch.Stop();
        _report = null;
    }

    public void Dispose() => Stop();

    private void OnRendering(object? sender, EventArgs eventArgs)
    {
        _frames++;
        if (_stopwatch.Elapsed.TotalMilliseconds < 500d)
        {
            return;
        }

        var framesPerSecond = _frames / _stopwatch.Elapsed.TotalSeconds;
        _frames = 0;
        _stopwatch.Restart();
        _report?.Invoke(framesPerSecond);
    }
}

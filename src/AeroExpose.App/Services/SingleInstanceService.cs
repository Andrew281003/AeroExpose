using System.IO.Pipes;
using System.Text;

namespace AeroExpose.Services;

internal sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\AeroExpose.BackgroundHost.v1";
    private const string PipeName = "AeroExpose.BackgroundHost.Commands.v1";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenerTask;
    private bool _disposed;

    public SingleInstanceService()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsPrimary = createdNew;
        Trace($"Instance created. Primary={IsPrimary}.");
    }

    public bool IsPrimary { get; }
    public event EventHandler<string>? CommandReceived;

    public void StartListening()
    {
        if (!IsPrimary || _listenerTask is not null)
        {
            return;
        }

        Trace("Starting command listener.");
        _listenerTask = ListenAsync(_cancellation.Token);
    }

    public static async Task<bool> SendAsync(string command, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var client = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(1500, cancellationToken).ConfigureAwait(false);
            var bytes = Encoding.UTF8.GetBytes(command + "\n");
            await client.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await client.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or OperationCanceledException)
        {
            Trace($"Command send failed: {exception}");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        if (IsPrimary)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex.Dispose();
        _cancellation.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                Trace("Waiting for command.");
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                var command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(command))
                {
                    Trace($"Received {command}.");
                    CommandReceived?.Invoke(this, command);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Trace($"Listener failed: {exception}");
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal static void Trace(string message)
    {
        var path = Environment.GetEnvironmentVariable("AEROEXPOSE_IPC_TRACE_PATH");
        if (string.IsNullOrWhiteSpace(path)) return;
        try { File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); }
        catch (IOException) { }
    }
}

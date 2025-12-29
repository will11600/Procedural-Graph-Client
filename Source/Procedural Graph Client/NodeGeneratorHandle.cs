#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProceduralGraph.Client;

internal sealed class NodeGeneratorHandle : IDisposable
{
    public readonly SemaphoreSlim semaphore;
    public bool isDirty;

    private readonly CancellationTokenSource _cts;
    public CancellationToken StoppingToken => _cts.Token;

    private bool _disposed;

    public NodeGeneratorHandle(Node node, CancellationToken stoppingToken)
    {
        semaphore = new(1, 1);
        isDirty = false;
        _disposed = false;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _cts.Token.Register(node.OnStopping);
    }

    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Task cancellation = _cts.CancelAsync();
        await cancellation.WaitAsync(cancellationToken);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _cts.Dispose();
            semaphore.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
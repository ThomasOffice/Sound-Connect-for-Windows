namespace Xm6Control.Bluetooth.Windows;

/// <summary>
/// Owns an optional long-lived Tandem connection.
/// <para>
/// While <see cref="KeepAlive"/> is true the device connection, GATT discovery,
/// notification subscription, and handshake are reused across operations.
/// Setting it to false (for example when the window is hidden to the tray)
/// releases the connection and every later request becomes a one-shot session.
/// </para>
/// </summary>
public sealed class TandemSessionHost : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TandemGattSession? _session;
    private TandemSessionReport? _lastConnectReport;
    private string? _connectedName;
    private string? _connectedAddress;
    private bool _disposed;

    public bool KeepAlive { get; set; }

    /// <summary>
    /// Opens the connection without running a command. Best effort; callers do
    /// not need the report because the next operation retries on failure.
    /// </summary>
    public async Task EnsureConnectedAsync(
        ProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!KeepAlive)
            {
                return;
            }

            await EnsureConnectedCoreAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TandemSessionReport> ExecuteAsync(
        ProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!KeepAlive)
            {
                return await new TandemGattSession()
                    .RunAsync(request, cancellationToken)
                    .ConfigureAwait(false);
            }

            await EnsureConnectedCoreAsync(request, cancellationToken).ConfigureAwait(false);
            var session = _session;
            if (session is null || !session.IsConnected)
            {
                return _lastConnectReport
                    ?? new TandemSessionReport
                    {
                        StartedAt = DateTimeOffset.Now,
                        CompletedAt = DateTimeOffset.Now,
                        Status = "not_connected",
                        Message = "The Tandem session is not connected.",
                    };
            }

            var report = new TandemSessionReport
            {
                StartedAt = DateTimeOffset.Now,
            };
            await session.ExecuteAsync(request, report, cancellationToken).ConfigureAwait(false);
            report.CompletedAt = DateTimeOffset.Now;

            if (!session.IsConnected)
            {
                // The link dropped (or the device went away); reconnect next time.
                session.Disconnect(report);
                _session = null;
            }

            return report;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Waits for any in-flight operation, then closes the connection.</summary>
    public async Task ReleaseAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            DisconnectCurrent();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureConnectedCoreAsync(
        ProbeRequest request,
        CancellationToken cancellationToken)
    {
        if (_session is { IsConnected: true } && TargetMatches(request))
        {
            return;
        }

        DisconnectCurrent();

        var session = new TandemGattSession();
        var report = new TandemSessionReport
        {
            StartedAt = DateTimeOffset.Now,
        };
        await session.ConnectAsync(request, report, cancellationToken).ConfigureAwait(false);
        report.CompletedAt = DateTimeOffset.Now;
        _lastConnectReport = report;

        if (session.IsConnected)
        {
            _session = session;
            _connectedName = request.Name;
            _connectedAddress = request.Address;
            return;
        }

        session.Disconnect(report);
    }

    private bool TargetMatches(ProbeRequest request)
    {
        return string.Equals(_connectedName, request.Name, StringComparison.Ordinal)
            && string.Equals(_connectedAddress, request.Address, StringComparison.Ordinal);
    }

    private void DisconnectCurrent()
    {
        if (_session is { } session)
        {
            session.Disconnect();
            _session = null;
        }

        _connectedName = null;
        _connectedAddress = null;
        _lastConnectReport = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        KeepAlive = false;
        await ReleaseAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}

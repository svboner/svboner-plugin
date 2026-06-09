using Microsoft.AspNetCore.SignalR;
using Svboner.Core.Services;
using Svboner.App.Hubs;

namespace Svboner.App;

/// <summary>
/// Bridges SvbonerOrchestrator status events to SignalR hub clients.
/// </summary>
public sealed class OrchestratorBroadcaster : BackgroundService
{
    private readonly SvbonerOrchestrator _orchestrator;
    private readonly IHubContext<StatusHub> _hub;

    public OrchestratorBroadcaster(SvbonerOrchestrator orchestrator, IHubContext<StatusHub> hub)
    {
        _orchestrator = orchestrator;
        _hub          = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _orchestrator.StatusChanged += OnStatusChanged;

        await _orchestrator.StartAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _orchestrator.StatusChanged -= OnStatusChanged;
            await _orchestrator.StopAsync();
        }
    }

    private void OnStatusChanged(object? _, RuntimeStatus status)
    {
        _ = _hub.Clients.All.SendAsync("StatusUpdate", status);
    }
}

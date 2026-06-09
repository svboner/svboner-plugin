using Microsoft.AspNetCore.SignalR;
using Svboner.Core.Services;

namespace Svboner.App.Hubs;

/// <summary>
/// SignalR hub. The server pushes status updates to all connected browser clients.
/// Clients don't send messages through the hub — they use the REST API instead.
/// </summary>
public sealed class StatusHub : Hub
{
    private readonly SvbonerOrchestrator _orchestrator;

    public StatusHub(SvbonerOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public override async Task OnConnectedAsync()
    {
        // Send the current status immediately so the UI doesn't wait for the next tick.
        await Clients.Caller.SendAsync("StatusUpdate", _orchestrator.GetStatus());
        await base.OnConnectedAsync();
    }
}

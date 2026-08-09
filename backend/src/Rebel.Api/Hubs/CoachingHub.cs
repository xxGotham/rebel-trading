using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Rebel.Api.Hubs;

[Authorize]
public class CoachingHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"].ToString();
        if (!string.IsNullOrEmpty(sessionId))
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"].ToString();
        if (!string.IsNullOrEmpty(sessionId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
        await base.OnDisconnectedAsync(exception);
    }
}

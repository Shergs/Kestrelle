using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kestrelle.Api.Hubs;

[Authorize]
public sealed class MusicHub : Hub
{
    public static string GroupName(string guildId) => $"guild:{guildId}";

    public Task JoinGuild(string guildId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(guildId));

    public Task LeaveGuild(string guildId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(guildId));
}

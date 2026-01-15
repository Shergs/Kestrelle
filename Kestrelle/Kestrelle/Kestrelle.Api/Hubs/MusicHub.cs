using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kestrelle.Api.Hubs;

[Authorize]
public sealed class MusicHub : Hub
{
    public static string GroupName(ulong guildId) => $"guild:{guildId}";

    public Task JoinGuild(ulong guildId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(guildId));

    public Task LeaveGuild(ulong guildId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(guildId));
}

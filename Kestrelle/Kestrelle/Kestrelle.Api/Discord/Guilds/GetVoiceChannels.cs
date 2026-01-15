using Microsoft.AspNetCore.Authorization;
using Kestrelle.Api.Discord;

namespace Kestrelle.Api.Discord.Guilds;

public static class GetVoiceChannels
{
    public sealed record VoiceChannelSummary(string Id, string Name);

    public static IEndpointRouteBuilder MapGetVoiceChannels(this IEndpointRouteBuilder api)
    {
        api.MapGet("/discord/guilds/{guildId}/voice-channels", async (
                string guildId,
                IConfiguration config,
                DiscordApiClient discord,
                CancellationToken ct) =>
        {
            // Match your existing env var: Discord__Token
            var botToken = config["Discord:Token"];
            if (string.IsNullOrWhiteSpace(botToken))
            {
                return Results.Problem("Discord:Token is missing from configuration.");
            }

            var channels = await discord.GetGuildChannelsAsync(botToken, guildId, ct);

            // Discord channel type 2 = voice
            var voice = channels
                .Where(c => c.Type == 2)
                .Select(c => new VoiceChannelSummary(c.Id, c.Name))
                .OrderBy(c => c.Name)
                .ToList();

            return Results.Ok(voice);
        })
            .RequireAuthorization();

        return api;
    }
}

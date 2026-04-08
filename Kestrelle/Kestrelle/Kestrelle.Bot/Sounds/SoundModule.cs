using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Kestrelle.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kestrelle.Bot.Sounds;

[Group("sound", "Soundboard commands")]
public sealed partial class SoundModule(
    IServiceScopeFactory scopeFactory,
    SoundPlaybackService playbackService,
    ILogger<SoundModule> logger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("play", "Play a soundboard clip in your current voice channel.", runMode: RunMode.Async)]
    public async Task PlayAsync([Autocomplete(typeof(SoundTriggerAutocompleteHandler))] string trigger)
    {
        await DeferAsync(ephemeral: true).ConfigureAwait(false);

        if (Context.Guild is null)
        {
            await FollowupAsync("This command can only be used in a server.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var guildUser = Context.User as SocketGuildUser ?? Context.Guild.GetUser(Context.User.Id);
        if (guildUser?.VoiceChannel is null)
        {
            await FollowupAsync("Join a voice channel first.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var normalizedTrigger = NormalizeTrigger(trigger);
        if (string.IsNullOrWhiteSpace(normalizedTrigger))
        {
            await FollowupAsync("Provide a valid sound trigger.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        Guid soundId;
        string displayName;

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KestrelleDbContext>();
            var sound = await db.Sounds
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.Trigger == normalizedTrigger)
                .ConfigureAwait(false);

            if (sound is null)
            {
                await FollowupAsync($"No sound exists for `{normalizedTrigger}`.", ephemeral: true).ConfigureAwait(false);
                return;
            }

            soundId = sound.Id;
            displayName = sound.DisplayName;
        }

        var result = await playbackService.PlayAsync(
            Context.Guild.Id,
            guildUser.VoiceChannel.Id,
            soundId,
            Context.User.Username,
            CancellationToken.None).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("Sound slash play failed in guild {GuildId}: {Message}", Context.Guild.Id, result.Message);
            await FollowupAsync(result.Message, ephemeral: true).ConfigureAwait(false);
            return;
        }

        await FollowupAsync($"Playing **{displayName}** (`{normalizedTrigger}`).", ephemeral: true).ConfigureAwait(false);
    }

    [SlashCommand("stop", "Stop the currently playing sound clip.", runMode: RunMode.Async)]
    public async Task StopAsync()
    {
        await DeferAsync(ephemeral: true).ConfigureAwait(false);

        if (Context.Guild is null)
        {
            await FollowupAsync("This command can only be used in a server.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var result = await playbackService.StopAsync(Context.Guild.Id, CancellationToken.None).ConfigureAwait(false);
        await FollowupAsync(result.Message, ephemeral: true).ConfigureAwait(false);
    }

    [SlashCommand("list", "List the available sound triggers for this server.", runMode: RunMode.Async)]
    public async Task ListAsync()
    {
        await DeferAsync(ephemeral: true).ConfigureAwait(false);

        if (Context.Guild is null)
        {
            await FollowupAsync("This command can only be used in a server.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        List<(string DisplayName, string Trigger)> sounds;

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KestrelleDbContext>();
            sounds = await db.Sounds
                .AsNoTracking()
                .Where(x => x.GuildId == Context.Guild.Id)
                .OrderBy(x => x.DisplayName)
                .Select(x => new ValueTuple<string, string>(x.DisplayName, x.Trigger))
                .ToListAsync()
                .ConfigureAwait(false);
        }

        if (sounds.Count == 0)
        {
            await FollowupAsync("This server does not have any uploaded sounds yet.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        var preview = sounds.Take(20).Select(x => $"`{x.Trigger}` - {x.DisplayName}");
        var suffix = sounds.Count > 20 ? $"\n...and {sounds.Count - 20} more." : string.Empty;
        await FollowupAsync(string.Join("\n", preview) + suffix, ephemeral: true).ConfigureAwait(false);
    }

    private static string NormalizeTrigger(string trigger)
    {
        var cleaned = TriggerCleanupRegex().Replace(trigger.Trim().ToLowerInvariant(), "-").Trim('-');
        if (cleaned.Length > 64)
            cleaned = cleaned[..64].Trim('-');

        return cleaned;
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex TriggerCleanupRegex();
}

public sealed class SoundTriggerAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        if (context.Guild is null)
            return AutocompletionResult.FromSuccess([]);

        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KestrelleDbContext>();

        var currentValue = autocompleteInteraction.Data.Current.Value?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;

        var sounds = await db.Sounds
            .AsNoTracking()
            .Where(x => x.GuildId == context.Guild.Id &&
                (string.IsNullOrWhiteSpace(currentValue) || x.Trigger.Contains(currentValue) || x.DisplayName.ToLower().Contains(currentValue)))
            .OrderBy(x => x.DisplayName)
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.DisplayName} ({x.Trigger})", x.Trigger))
            .ToListAsync()
            .ConfigureAwait(false);

        return AutocompletionResult.FromSuccess(sounds);
    }
}

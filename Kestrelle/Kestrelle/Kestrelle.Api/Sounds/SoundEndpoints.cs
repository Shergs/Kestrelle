using System.Text.RegularExpressions;
using Kestrelle.Api.Discord;
using Kestrelle.Api.Discord.Guilds;
using Kestrelle.Api.Hubs;
using Kestrelle.Models.Data;
using Kestrelle.Models.Entities;
using Kestrelle.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Kestrelle.Api.Sounds;

public static partial class SoundEndpoints
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(10);

    private static readonly Dictionary<string, string[]> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp3"] = ["audio/mpeg", "audio/mp3"],
        [".wav"] = ["audio/wav", "audio/x-wav", "audio/wave"],
        [".ogg"] = ["audio/ogg"],
        [".m4a"] = ["audio/mp4", "audio/x-m4a", "audio/m4a"],
    };

    public static void Map(RouteGroupBuilder api)
    {
        var group = api.MapGroup("/sounds/guilds/{guildId}")
            .RequireAuthorization();

        group.MapGet("", async (
            string guildId,
            HttpContext http,
            GuildAccessService guildAccess,
            KestrelleDbContext db,
            CancellationToken ct) =>
        {
            var (access, failure) = await guildAccess.AuthorizeAsync(http, guildId, ct);
            if (failure is not null)
                return failure;

            var sounds = await db.Sounds
                .AsNoTracking()
                .Include(x => x.UploadedByUser)
                .Where(x => x.GuildId == access!.GuildId)
                .OrderBy(x => x.DisplayName)
                .Select(x => ToDto(guildId, x, x.UploadedByUser.Username))
                .ToListAsync(ct);

            return Results.Ok(sounds);
        });

        group.MapPost("", async (
            string guildId,
            HttpContext http,
            GuildAccessService guildAccess,
            KestrelleDbContext db,
            ISoundStorage storage,
            ISoundFileMetadataReader metadataReader,
            CancellationToken ct) =>
        {
            var (access, failure) = await guildAccess.AuthorizeAsync(http, guildId, ct);
            if (failure is not null)
                return failure;

            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files["file"];
            if (file is null)
                return Results.BadRequest(new { error = "A sound file is required." });

            if (file.Length <= 0)
                return Results.BadRequest(new { error = "Uploaded file is empty." });

            if (file.Length > MaxFileSizeBytes)
                return Results.BadRequest(new { error = "Sound files must be 5 MiB or smaller." });

            var displayName = form["displayName"].ToString().Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                return Results.BadRequest(new { error = "Display name is required." });

            var trigger = NormalizeTrigger(form["trigger"].ToString(), displayName);
            if (string.IsNullOrWhiteSpace(trigger))
                return Results.BadRequest(new { error = "Trigger is required." });

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedTypes.TryGetValue(extension, out var allowedContentTypes))
                return Results.BadRequest(new { error = "Only mp3, wav, ogg, and m4a files are supported." });

            if (!IsSupportedContentType(file.ContentType, allowedContentTypes))
                return Results.BadRequest(new { error = "Uploaded file type does not match the selected audio format." });

            var duplicate = await db.Sounds.AnyAsync(
                x => x.GuildId == access!.GuildId && x.Trigger == trigger,
                ct);

            if (duplicate)
                return Results.BadRequest(new { error = "A sound with that trigger already exists in this guild." });

            var soundId = Guid.NewGuid();
            var storageKey = $"guilds/{guildId}/{soundId}{extension.ToLowerInvariant()}";

            await EnsureGuildAndUserAsync(db, access!, ct);

            await using var uploadStream = file.OpenReadStream();
            await storage.SaveAsync(storageKey, uploadStream, ct);

            try
            {
                var metadata = await metadataReader.ReadAsync(storage.ResolveAbsolutePath(storageKey), ct);
                if (metadata.Duration > MaxDuration)
                {
                    await storage.DeleteAsync(storageKey, ct);
                    return Results.BadRequest(new { error = "Sound clips must be 10 seconds or shorter." });
                }

                var sound = new Sound
                {
                    Id = soundId,
                    GuildId = access.GuildId,
                    UploadedByUserId = access.UserId,
                    DisplayName = displayName,
                    Trigger = trigger,
                    OriginalFileName = Path.GetFileName(file.FileName),
                    StorageProvider = SoundStorageProvider.LocalDisk,
                    StorageKey = storageKey,
                    ContentType = allowedContentTypes[0],
                    SizeBytes = file.Length,
                    Duration = metadata.Duration,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    UpdatedUtc = DateTimeOffset.UtcNow,
                    IsPublicWithinGuild = true,
                };

                db.Sounds.Add(sound);
                await db.SaveChangesAsync(ct);

                return Results.Created($"/api/sounds/guilds/{guildId}/{sound.Id}", ToDto(guildId, sound, access.Username));
            }
            catch
            {
                await storage.DeleteAsync(storageKey, ct);
                throw;
            }
        });

        group.MapPatch("/{soundId:guid}", async (
            string guildId,
            Guid soundId,
            UpdateSoundRequest request,
            HttpContext http,
            GuildAccessService guildAccess,
            KestrelleDbContext db,
            CancellationToken ct) =>
        {
            var (access, failure) = await guildAccess.AuthorizeAsync(http, guildId, ct);
            if (failure is not null)
                return failure;

            var sound = await db.Sounds
                .Include(x => x.UploadedByUser)
                .FirstOrDefaultAsync(x => x.Id == soundId && x.GuildId == access!.GuildId, ct);

            if (sound is null)
                return Results.NotFound();

            var displayName = request.DisplayName.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                return Results.BadRequest(new { error = "Display name is required." });

            var trigger = NormalizeTrigger(request.Trigger, displayName);
            if (string.IsNullOrWhiteSpace(trigger))
                return Results.BadRequest(new { error = "Trigger is required." });

            var duplicate = await db.Sounds.AnyAsync(
                x => x.GuildId == access!.GuildId && x.Id != soundId && x.Trigger == trigger,
                ct);

            if (duplicate)
                return Results.BadRequest(new { error = "A sound with that trigger already exists in this guild." });

            sound.DisplayName = displayName;
            sound.Trigger = trigger;
            sound.UpdatedUtc = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);

            return Results.Ok(ToDto(guildId, sound, sound.UploadedByUser.Username));
        });

        group.MapDelete("/{soundId:guid}", async (
            string guildId,
            Guid soundId,
            HttpContext http,
            GuildAccessService guildAccess,
            KestrelleDbContext db,
            ISoundStorage storage,
            CancellationToken ct) =>
        {
            var (access, failure) = await guildAccess.AuthorizeAsync(http, guildId, ct);
            if (failure is not null)
                return failure;

            var sound = await db.Sounds.FirstOrDefaultAsync(x => x.Id == soundId && x.GuildId == access!.GuildId, ct);
            if (sound is null)
                return Results.NotFound();

            db.Sounds.Remove(sound);
            await db.SaveChangesAsync(ct);
            await storage.DeleteAsync(sound.StorageKey, ct);

            return Results.Ok(new { ok = true });
        });

        group.MapGet("/{soundId:guid}/content", async (
            string guildId,
            Guid soundId,
            HttpContext http,
            GuildAccessService guildAccess,
            KestrelleDbContext db,
            ISoundStorage storage,
            CancellationToken ct) =>
        {
            var (access, failure) = await guildAccess.AuthorizeAsync(http, guildId, ct);
            if (failure is not null)
                return failure;

            var sound = await db.Sounds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == soundId && x.GuildId == access!.GuildId, ct);
            if (sound is null)
                return Results.NotFound();

            var stream = await storage.OpenReadAsync(sound.StorageKey, ct);
            return Results.File(stream, sound.ContentType, enableRangeProcessing: true);
        });

        group.MapPost("/{soundId:guid}/play", async (
            string guildId,
            Guid soundId,
            PlaySoundRequest? request,
            HttpContext http,
            GuildAccessService guildAccess,
            KestrelleDbContext db,
            DiscordApiClient discord,
            IConfiguration config,
            IHubContext<SoundControlHub> hub,
            CancellationToken ct) =>
        {
            var (access, failure) = await guildAccess.AuthorizeAsync(http, guildId, ct);
            if (failure is not null)
                return failure;

            var sound = await db.Sounds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == soundId && x.GuildId == access!.GuildId, ct);
            if (sound is null)
                return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(request?.VoiceChannelId))
            {
                var botToken = config["Discord:Token"];
                if (string.IsNullOrWhiteSpace(botToken))
                    return Results.Problem("Discord:Token is missing from configuration.");

                var channels = await discord.GetGuildChannelsAsync(botToken, guildId, ct);
                var voiceChannelValid = channels.Any(c => c.Type == 2 && string.Equals(c.Id, request.VoiceChannelId, StringComparison.Ordinal));
                if (!voiceChannelValid)
                    return Results.BadRequest(new { error = "Selected voice channel is not valid for this guild." });
            }

            var payload = new SoundControlRequest(
                GuildId: guildId,
                Action: "play",
                RequestedUtc: DateTimeOffset.UtcNow,
                SoundId: soundId,
                VoiceChannelId: request?.VoiceChannelId,
                User: access.Username);

            await hub.Clients.All.SendAsync("ControlRequested", payload, ct);
            return Results.Accepted();
        });
    }

    private static SoundSummaryDto ToDto(string guildId, Sound sound, string uploadedByUsername) => new(
        Id: sound.Id,
        DisplayName: sound.DisplayName,
        Trigger: sound.Trigger,
        OriginalFileName: sound.OriginalFileName,
        ContentType: sound.ContentType,
        SizeBytes: sound.SizeBytes,
        DurationMs: (long)(sound.Duration ?? TimeSpan.Zero).TotalMilliseconds,
        UploadedByUsername: uploadedByUsername,
        ContentUrl: $"/api/sounds/guilds/{guildId}/{sound.Id}/content",
        CreatedUtc: sound.CreatedUtc,
        UpdatedUtc: sound.UpdatedUtc);

    private static bool IsSupportedContentType(string? contentType, IReadOnlyCollection<string> allowedContentTypes)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return true;

        if (string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return true;

        return allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task EnsureGuildAndUserAsync(KestrelleDbContext db, GuildAccessContext access, CancellationToken ct)
    {
        var guild = await db.Guilds.FirstOrDefaultAsync(x => x.Id == access.GuildId, ct);
        if (guild is null)
        {
            db.Guilds.Add(new DiscordGuild
            {
                Id = access.GuildId,
                Name = access.GuildName,
                CreatedUtc = DateTimeOffset.UtcNow,
            });
        }
        else if (!string.Equals(guild.Name, access.GuildName, StringComparison.Ordinal))
        {
            guild.Name = access.GuildName;
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == access.UserId, ct);
        if (user is null)
        {
            db.Users.Add(new DiscordUser
            {
                Id = access.UserId,
                Username = access.Username,
                CreatedUtc = DateTimeOffset.UtcNow,
            });
        }
        else if (!string.Equals(user.Username, access.Username, StringComparison.Ordinal))
        {
            user.Username = access.Username;
        }
    }

    private static string NormalizeTrigger(string? requestedTrigger, string fallbackDisplayName)
    {
        var source = string.IsNullOrWhiteSpace(requestedTrigger) ? fallbackDisplayName : requestedTrigger.Trim();
        var lower = source.ToLowerInvariant();
        var cleaned = TriggerCleanupRegex().Replace(lower, "-").Trim('-');

        if (cleaned.Length > 64)
            cleaned = cleaned[..64].Trim('-');

        return cleaned;
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex TriggerCleanupRegex();
}


using Kestrelle.Api.Auth;
using Kestrelle.Api.Discord;
using Kestrelle.Api.Discord.Guilds;
using Kestrelle.Api.Hubs;
using Kestrelle.Api.Music;
using Kestrelle.Api.Sounds;
using Kestrelle.Api.Status;
using Kestrelle.Models.Data;
using Kestrelle.Shared;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Auth: cookies + Discord OAuth
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = DiscordAuthDefaults.Scheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "kestrelle_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    })
    .AddDiscordOAuth(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddKestrelleData(builder.Configuration);
builder.Services.Configure<SoundStorageOptions>(builder.Configuration.GetSection("Sounds"));
builder.Services.AddScoped<GuildAccessService>();
builder.Services.AddSingleton<ISoundStorage, LocalDiskSoundStorage>();
builder.Services.AddSingleton<ISoundFileMetadataReader, FfprobeSoundFileMetadataReader>();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient<DiscordApiClient>(client =>
{
    client.BaseAddress = new Uri("https://discord.com/api/");
});

builder.Services.AddSignalR();

builder.Services.AddSingleton<MusicStateStore>();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders =
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;

    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KestrelleDbContext>();
    await EnsureLegacyMigrationHistoryAsync(db);
    await db.Database.MigrateAsync();
}

app.UseForwardedHeaders();

app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");

GetStatus.Map(api);
DiscordAuthEndpoints.Map(api);
GetAvailableGuilds.Map(api);
MusicEndpoints.Map(api);
MusicRealtimeIngestEndpoints.Map(api);
MusicControlEndpoints.Map(api);
SoundEndpoints.Map(api);

api.MapGetVoiceChannels();

app.MapHub<MusicHub>("/hubs/music");
app.MapHub<MusicControlHub>("/hubs/music-control");
app.MapHub<SoundControlHub>("/hubs/sound-control");

app.Run();

static async Task EnsureLegacyMigrationHistoryAsync(KestrelleDbContext db)
{
    const string initialMigrationId = "20260114060433_InitialCreate";
    const string initialProductVersion = "10.0.0";

    await db.Database.OpenConnectionAsync();

    try
    {
        var connection = db.Database.GetDbConnection();

        await using var createHistory = connection.CreateCommand();
        createHistory.CommandText = @"
CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
    ""MigrationId"" character varying(150) NOT NULL,
    ""ProductVersion"" character varying(32) NOT NULL,
    CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
);";
        await createHistory.ExecuteNonQueryAsync();

        await using var hasGuildsTableCommand = connection.CreateCommand();
        hasGuildsTableCommand.CommandText = @"
SELECT EXISTS (
    SELECT 1
    FROM information_schema.tables
    WHERE table_schema = 'kestrelle' AND table_name = 'Guilds'
);";
        var hasGuildsTable = (bool?)await hasGuildsTableCommand.ExecuteScalarAsync() ?? false;

        if (!hasGuildsTable)
            return;

        await using var hasInitialMigrationCommand = connection.CreateCommand();
        hasInitialMigrationCommand.CommandText = @"
SELECT EXISTS (
    SELECT 1
    FROM ""__EFMigrationsHistory""
    WHERE ""MigrationId"" = @migrationId
);";

        var migrationIdParameter = hasInitialMigrationCommand.CreateParameter();
        migrationIdParameter.ParameterName = "@migrationId";
        migrationIdParameter.Value = initialMigrationId;
        hasInitialMigrationCommand.Parameters.Add(migrationIdParameter);

        var hasInitialMigration = (bool?)await hasInitialMigrationCommand.ExecuteScalarAsync() ?? false;
        if (hasInitialMigration)
            return;

        await using var insertInitialMigrationCommand = connection.CreateCommand();
        insertInitialMigrationCommand.CommandText = @"
INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
VALUES (@migrationId, @productVersion);";

        var insertMigrationIdParameter = insertInitialMigrationCommand.CreateParameter();
        insertMigrationIdParameter.ParameterName = "@migrationId";
        insertMigrationIdParameter.Value = initialMigrationId;
        insertInitialMigrationCommand.Parameters.Add(insertMigrationIdParameter);

        var productVersionParameter = insertInitialMigrationCommand.CreateParameter();
        productVersionParameter.ParameterName = "@productVersion";
        productVersionParameter.Value = initialProductVersion;
        insertInitialMigrationCommand.Parameters.Add(productVersionParameter);

        await insertInitialMigrationCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        await db.Database.CloseConnectionAsync();
    }
}

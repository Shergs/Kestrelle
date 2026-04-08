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
    await EnsureSoundboardMigrationStateAsync(db);
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

        if (!await TableExistsAsync(connection, "kestrelle", "Guilds"))
            return;

        if (await MigrationExistsAsync(connection, initialMigrationId))
            return;

        await InsertMigrationAsync(connection, initialMigrationId, initialProductVersion);
    }
    finally
    {
        await db.Database.CloseConnectionAsync();
    }
}

static async Task EnsureSoundboardMigrationStateAsync(KestrelleDbContext db)
{
    const string migrationId = "20260408033748_AddSoundboardMetadata";
    const string productVersion = "10.0.0";

    await db.Database.OpenConnectionAsync();

    try
    {
        var connection = db.Database.GetDbConnection();

        if (!await TableExistsAsync(connection, "kestrelle", "Sounds"))
            return;

        if (await MigrationExistsAsync(connection, migrationId))
            return;

        await ExecuteNonQueryAsync(connection, @"
ALTER TABLE kestrelle.""Sounds"" ADD COLUMN IF NOT EXISTS ""OriginalFileName"" character varying(260) NOT NULL DEFAULT '';
ALTER TABLE kestrelle.""Sounds"" ADD COLUMN IF NOT EXISTS ""Trigger"" character varying(64) NOT NULL DEFAULT '';
ALTER TABLE kestrelle.""Sounds"" ADD COLUMN IF NOT EXISTS ""UpdatedUtc"" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
");

        await ExecuteNonQueryAsync(connection, @"
UPDATE kestrelle.""Sounds""
SET ""Trigger"" = 'sound-' || substring(replace(cast(""Id"" as text), '-', ''), 1, 12)
WHERE coalesce(""Trigger"", '') = '';
");

        await ExecuteNonQueryAsync(connection, @"
CREATE TABLE IF NOT EXISTS kestrelle.""DiscordOAuthTokens"" (
    ""DiscordUserId"" numeric(20,0) NOT NULL,
    ""AccessToken"" text NOT NULL,
    ""RefreshToken"" text NOT NULL,
    ""Scope"" text NOT NULL,
    ""ExpiresAtUtc"" timestamp with time zone NOT NULL,
    ""UserId"" numeric(20,0) NOT NULL,
    CONSTRAINT ""PK_DiscordOAuthTokens"" PRIMARY KEY (""DiscordUserId""),
    CONSTRAINT ""FK_DiscordOAuthTokens_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES kestrelle.""Users"" (""Id"") ON DELETE CASCADE
);
");

        await ExecuteNonQueryAsync(connection, @"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Sounds_GuildId_Trigger"" ON kestrelle.""Sounds"" (""GuildId"", ""Trigger"");
CREATE INDEX IF NOT EXISTS ""IX_DiscordOAuthTokens_UserId"" ON kestrelle.""DiscordOAuthTokens"" (""UserId"");
");

        await InsertMigrationAsync(connection, migrationId, productVersion);
    }
    finally
    {
        await db.Database.CloseConnectionAsync();
    }
}

static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string schema, string table)
{
    await using var command = connection.CreateCommand();
    command.CommandText = @"
SELECT EXISTS (
    SELECT 1
    FROM information_schema.tables
    WHERE table_schema = @schema AND table_name = @table
);";

    AddParameter(command, "@schema", schema);
    AddParameter(command, "@table", table);

    return (bool?)await command.ExecuteScalarAsync() ?? false;
}

static async Task<bool> MigrationExistsAsync(System.Data.Common.DbConnection connection, string migrationId)
{
    await using var command = connection.CreateCommand();
    command.CommandText = @"
SELECT EXISTS (
    SELECT 1
    FROM ""__EFMigrationsHistory""
    WHERE ""MigrationId"" = @migrationId
);";

    AddParameter(command, "@migrationId", migrationId);
    return (bool?)await command.ExecuteScalarAsync() ?? false;
}

static async Task InsertMigrationAsync(System.Data.Common.DbConnection connection, string migrationId, string productVersion)
{
    await using var command = connection.CreateCommand();
    command.CommandText = @"
INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
VALUES (@migrationId, @productVersion)
ON CONFLICT (""MigrationId"") DO NOTHING;";

    AddParameter(command, "@migrationId", migrationId);
    AddParameter(command, "@productVersion", productVersion);
    await command.ExecuteNonQueryAsync();
}

static async Task ExecuteNonQueryAsync(System.Data.Common.DbConnection connection, string sql)
{
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    await command.ExecuteNonQueryAsync();
}

static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
{
    var parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.Value = value;
    command.Parameters.Add(parameter);
}

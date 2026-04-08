using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kestrelle.Models.Migrations
{
    /// <inheritdoc />
    public partial class AddSoundboardMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                schema: "kestrelle",
                table: "Sounds",
                type: "character varying(260)",
                maxLength: 260,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Trigger",
                schema: "kestrelle",
                table: "Sounds",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedUtc",
                schema: "kestrelle",
                table: "Sounds",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "DiscordOAuthTokens",
                schema: "kestrelle",
                columns: table => new
                {
                    DiscordUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordOAuthTokens", x => x.DiscordUserId);
                    table.ForeignKey(
                        name: "FK_DiscordOAuthTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "kestrelle",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sounds_GuildId_Trigger",
                schema: "kestrelle",
                table: "Sounds",
                columns: new[] { "GuildId", "Trigger" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscordOAuthTokens_UserId",
                schema: "kestrelle",
                table: "DiscordOAuthTokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscordOAuthTokens",
                schema: "kestrelle");

            migrationBuilder.DropIndex(
                name: "IX_Sounds_GuildId_Trigger",
                schema: "kestrelle",
                table: "Sounds");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                schema: "kestrelle",
                table: "Sounds");

            migrationBuilder.DropColumn(
                name: "Trigger",
                schema: "kestrelle",
                table: "Sounds");

            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                schema: "kestrelle",
                table: "Sounds");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_TelegramIdFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop and recreate without IDENTITY so Id = 1 can be inserted explicitly (singleton row)
            migrationBuilder.Sql("DROP TABLE IF EXISTS [TelegramBotSettings]");

            migrationBuilder.Sql(@"
                CREATE TABLE [TelegramBotSettings] (
                    [Id]        int          NOT NULL,
                    [BotToken]  nvarchar(max) NULL,
                    [ChatId]    nvarchar(max) NULL,
                    [IsEnabled] bit          NOT NULL DEFAULT 0,
                    [UpdatedAt] datetime2    NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT [PK_TelegramBotSettings] PRIMARY KEY ([Id])
                )");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [TelegramBotSettings]");

            migrationBuilder.Sql(@"
                CREATE TABLE [TelegramBotSettings] (
                    [Id]        int          NOT NULL IDENTITY(1,1),
                    [BotToken]  nvarchar(max) NULL,
                    [ChatId]    nvarchar(max) NULL,
                    [IsEnabled] bit          NOT NULL DEFAULT 0,
                    [UpdatedAt] datetime2    NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT [PK_TelegramBotSettings] PRIMARY KEY ([Id])
                )");
        }
    }
}

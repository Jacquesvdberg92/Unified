using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserNotificationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AudioNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AudioVolume = table.Column<int>(type: "int", nullable: false, defaultValue: 50),
                    DesktopNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ToastNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    BadgeNotificationsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    NotifyOnMessages = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    NotifyOnMentions = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    NotifyOnSystemAlerts = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotificationSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotificationSettings_UserId",
                table: "UserNotificationSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserNotificationSettings");
        }
    }
}

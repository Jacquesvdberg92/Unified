using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_Vault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VaultCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IconCssClass = table.Column<string>(type: "TEXT", nullable: false),
                    IsCustom = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaultCategories_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VaultEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptedPassword = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProvisionedByUserId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaultEntries_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VaultEntries_AspNetUsers_ProvisionedByUserId",
                        column: x => x.ProvisionedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VaultEntries_VaultCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "VaultCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VaultAccessLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    EntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultAccessLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaultAccessLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VaultAccessLogs_VaultEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "VaultEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "VaultCategories",
                columns: new[] { "Id", "CreatedByUserId", "IconCssClass", "IsCustom", "Name" },
                values: new object[,]
                {
                    { 1, null, "bx bx-data", false, "CRM" },
                    { 2, null, "bx bx-bar-chart", false, "Quemetrics" },
                    { 3, null, "bx bx-task", false, "Redmine" },
                    { 4, null, "bx bx-phone-call", false, "Call System" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_VaultAccessLogs_EntryId",
                table: "VaultAccessLogs",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultAccessLogs_UserId",
                table: "VaultAccessLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultCategories_CreatedByUserId",
                table: "VaultCategories",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultEntries_CategoryId",
                table: "VaultEntries",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultEntries_OwnerId",
                table: "VaultEntries",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultEntries_ProvisionedByUserId",
                table: "VaultEntries",
                column: "ProvisionedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VaultAccessLogs");

            migrationBuilder.DropTable(
                name: "VaultEntries");

            migrationBuilder.DropTable(
                name: "VaultCategories");
        }
    }
}

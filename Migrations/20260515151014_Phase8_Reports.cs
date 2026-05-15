using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class Phase8_Reports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportedByLeaderId = table.Column<string>(type: "TEXT", nullable: false),
                    PeriodType = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalChats = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalTickets = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalCalls = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalFTD = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamReports_AspNetUsers_ReportedByLeaderId",
                        column: x => x.ReportedByLeaderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamReports_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReportId = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentId = table.Column<string>(type: "TEXT", nullable: false),
                    Chats = table.Column<int>(type: "INTEGER", nullable: false),
                    Tickets = table.Column<int>(type: "INTEGER", nullable: false),
                    Calls = table.Column<int>(type: "INTEGER", nullable: false),
                    FTD = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: true),
                    IsTopChatPicker = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsTopTicketSolver = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsTopCallMaker = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentStats_AspNetUsers_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentStats_TeamReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "TeamReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FTDLanguageStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReportId = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    FTDCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FTDLanguageStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FTDLanguageStats_TeamReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "TeamReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentStats_AgentId",
                table: "AgentStats",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentStats_ReportId",
                table: "AgentStats",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_FTDLanguageStats_ReportId",
                table: "FTDLanguageStats",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamReports_ReportedByLeaderId",
                table: "TeamReports",
                column: "ReportedByLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamReports_TeamId",
                table: "TeamReports",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentStats");

            migrationBuilder.DropTable(
                name: "FTDLanguageStats");

            migrationBuilder.DropTable(
                name: "TeamReports");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class AddCsLiveHelp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCsLiveHelp",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CsLiveHelpSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlotHour = table.Column<int>(type: "int", nullable: false),
                    Agent1Id = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Agent2Id = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CsLiveHelpSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CsLiveHelpSlots_AspNetUsers_Agent1Id",
                        column: x => x.Agent1Id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CsLiveHelpSlots_AspNetUsers_Agent2Id",
                        column: x => x.Agent2Id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CsLiveHelpSlots_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CsLiveHelpSwapLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SlotId = table.Column<int>(type: "int", nullable: false),
                    AgentPosition = table.Column<int>(type: "int", nullable: false),
                    PreviousAgentId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    NewAgentId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ChangedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CsLiveHelpSwapLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CsLiveHelpSwapLogs_AspNetUsers_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CsLiveHelpSwapLogs_AspNetUsers_NewAgentId",
                        column: x => x.NewAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CsLiveHelpSwapLogs_AspNetUsers_PreviousAgentId",
                        column: x => x.PreviousAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CsLiveHelpSwapLogs_CsLiveHelpSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "CsLiveHelpSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CsLiveHelpSlots_Agent1Id",
                table: "CsLiveHelpSlots",
                column: "Agent1Id");

            migrationBuilder.CreateIndex(
                name: "IX_CsLiveHelpSlots_Agent2Id",
                table: "CsLiveHelpSlots",
                column: "Agent2Id");

            migrationBuilder.CreateIndex(
                name: "IX_CsLiveHelpSlots_CreatedById",
                table: "CsLiveHelpSlots",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CsLiveHelpSwapLogs_ChangedById",
                table: "CsLiveHelpSwapLogs",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_CsLiveHelpSwapLogs_NewAgentId",
                table: "CsLiveHelpSwapLogs",
                column: "NewAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_CsLiveHelpSwapLogs_PreviousAgentId",
                table: "CsLiveHelpSwapLogs",
                column: "PreviousAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_CsLiveHelpSwapLogs_SlotId",
                table: "CsLiveHelpSwapLogs",
                column: "SlotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CsLiveHelpSwapLogs");

            migrationBuilder.DropTable(
                name: "CsLiveHelpSlots");

            migrationBuilder.DropColumn(
                name: "HasCsLiveHelp",
                table: "AspNetUsers");
        }
    }
}

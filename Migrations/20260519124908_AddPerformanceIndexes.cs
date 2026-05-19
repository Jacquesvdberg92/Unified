using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VaultEntries_OwnerId",
                table: "VaultEntries");

            migrationBuilder.DropIndex(
                name: "IX_PoiSimulations_LoggedById",
                table: "PoiSimulations");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceReviews_AgentId",
                table: "PerformanceReviews");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceLogs_AgentId",
                table: "AttendanceLogs");

            migrationBuilder.DropIndex(
                name: "IX_AgentSchedules_AgentId",
                table: "AgentSchedules");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "DashboardWidgets",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_WorkDistributions_Date",
                table: "WorkDistributions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_VaultEntries_OwnerId_CategoryId",
                table: "VaultEntries",
                columns: new[] { "OwnerId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Updates_IsArchived_IsPinned_CreatedAt",
                table: "Updates",
                columns: new[] { "IsArchived", "IsPinned", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamReports_PeriodType_PeriodStart",
                table: "TeamReports",
                columns: new[] { "PeriodType", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_PoiSimulations_LoggedById_SimulatedAt",
                table: "PoiSimulations",
                columns: new[] { "LoggedById", "SimulatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_AgentId_ReviewDate",
                table: "PerformanceReviews",
                columns: new[] { "AgentId", "ReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DashboardWidgets_UserId",
                table: "DashboardWidgets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CsLiveHelpSlots_Date",
                table: "CsLiveHelpSlots",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_AgentId_WorkDate",
                table: "AttendanceLogs",
                columns: new[] { "AgentId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSchedules_AgentId_Date",
                table: "AgentSchedules",
                columns: new[] { "AgentId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkDistributions_Date",
                table: "WorkDistributions");

            migrationBuilder.DropIndex(
                name: "IX_VaultEntries_OwnerId_CategoryId",
                table: "VaultEntries");

            migrationBuilder.DropIndex(
                name: "IX_Updates_IsArchived_IsPinned_CreatedAt",
                table: "Updates");

            migrationBuilder.DropIndex(
                name: "IX_TeamReports_PeriodType_PeriodStart",
                table: "TeamReports");

            migrationBuilder.DropIndex(
                name: "IX_PoiSimulations_LoggedById_SimulatedAt",
                table: "PoiSimulations");

            migrationBuilder.DropIndex(
                name: "IX_PerformanceReviews_AgentId_ReviewDate",
                table: "PerformanceReviews");

            migrationBuilder.DropIndex(
                name: "IX_DashboardWidgets_UserId",
                table: "DashboardWidgets");

            migrationBuilder.DropIndex(
                name: "IX_CsLiveHelpSlots_Date",
                table: "CsLiveHelpSlots");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceLogs_AgentId_WorkDate",
                table: "AttendanceLogs");

            migrationBuilder.DropIndex(
                name: "IX_AgentSchedules_AgentId_Date",
                table: "AgentSchedules");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "DashboardWidgets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_VaultEntries_OwnerId",
                table: "VaultEntries",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PoiSimulations_LoggedById",
                table: "PoiSimulations",
                column: "LoggedById");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceReviews_AgentId",
                table: "PerformanceReviews",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceLogs_AgentId",
                table: "AttendanceLogs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSchedules_AgentId",
                table: "AgentSchedules",
                column: "AgentId");
        }
    }
}

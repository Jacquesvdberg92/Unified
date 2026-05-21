using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class Phase1d_CsRequest_ClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "CsRequests",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "CsRequests");
        }
    }
}

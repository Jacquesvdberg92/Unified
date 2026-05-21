using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class Phase1dv_AssignedTo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedToId",
                table: "CsRequests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToId",
                table: "CsRequestArchives",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CsRequests_AssignedToId",
                table: "CsRequests",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_CsRequestArchives_AssignedToId",
                table: "CsRequestArchives",
                column: "AssignedToId");

            migrationBuilder.AddForeignKey(
                name: "FK_CsRequestArchives_AspNetUsers_AssignedToId",
                table: "CsRequestArchives",
                column: "AssignedToId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CsRequests_AspNetUsers_AssignedToId",
                table: "CsRequests",
                column: "AssignedToId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CsRequestArchives_AspNetUsers_AssignedToId",
                table: "CsRequestArchives");

            migrationBuilder.DropForeignKey(
                name: "FK_CsRequests_AspNetUsers_AssignedToId",
                table: "CsRequests");

            migrationBuilder.DropIndex(
                name: "IX_CsRequests_AssignedToId",
                table: "CsRequests");

            migrationBuilder.DropIndex(
                name: "IX_CsRequestArchives_AssignedToId",
                table: "CsRequestArchives");

            migrationBuilder.DropColumn(
                name: "AssignedToId",
                table: "CsRequests");

            migrationBuilder.DropColumn(
                name: "AssignedToId",
                table: "CsRequestArchives");
        }
    }
}

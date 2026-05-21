using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class Phase1div_IsInternal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AccountManagerId",
                table: "CsRequests",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<bool>(
                name: "IsInternal",
                table: "CsRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "AccountManagerId",
                table: "CsRequestArchives",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<bool>(
                name: "IsInternal",
                table: "CsRequestArchives",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInternal",
                table: "CsRequests");

            migrationBuilder.DropColumn(
                name: "IsInternal",
                table: "CsRequestArchives");

            migrationBuilder.AlterColumn<string>(
                name: "AccountManagerId",
                table: "CsRequests",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountManagerId",
                table: "CsRequestArchives",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}

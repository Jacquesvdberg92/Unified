using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class RenameCallSystemToQuemetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "VaultCategories",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.RenameColumn(
                name: "CallSystemUrl",
                table: "Brands",
                newName: "QuemetricsUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "QuemetricsUrl",
                table: "Brands",
                newName: "CallSystemUrl");

            migrationBuilder.InsertData(
                table: "VaultCategories",
                columns: new[] { "Id", "CreatedByUserId", "IconCssClass", "IsCustom", "Name" },
                values: new object[] { 4, null, "bx bx-phone-call", false, "Call System" });
        }
    }
}

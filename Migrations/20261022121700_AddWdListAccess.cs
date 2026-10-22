using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class AddWdListAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasWdListAccess",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasWdListAccess",
                table: "AspNetUsers");
        }
    }
}

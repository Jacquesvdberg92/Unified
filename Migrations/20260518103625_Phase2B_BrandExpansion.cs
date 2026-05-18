using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class Phase2B_BrandExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WebsiteLinksJson",
                table: "Brands",
                newName: "BrandLinksJson");

            migrationBuilder.AddColumn<string>(
                name: "EmailAml",
                table: "Brands",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailAssign",
                table: "Brands",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailDealing",
                table: "Brands",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailDemo",
                table: "Brands",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedmineUrl",
                table: "Brands",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteUrl",
                table: "Brands",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailAml",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "EmailAssign",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "EmailDealing",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "EmailDemo",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "RedmineUrl",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "SiteUrl",
                table: "Brands");

            migrationBuilder.RenameColumn(
                name: "BrandLinksJson",
                table: "Brands",
                newName: "WebsiteLinksJson");
        }
    }
}

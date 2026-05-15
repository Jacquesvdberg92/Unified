using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_EmailTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LogoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    PrimaryColour = table.Column<string>(type: "TEXT", nullable: true),
                    WebsiteLinksJson = table.Column<string>(type: "TEXT", nullable: false),
                    CrmUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CallSystemUrl = table.Column<string>(type: "TEXT", nullable: true),
                    FooterSignatureHtml = table.Column<string>(type: "TEXT", nullable: true),
                    ZohoSignatureNote = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    SubjectLine = table.Column<string>(type: "TEXT", nullable: false),
                    BodyHtml = table.Column<string>(type: "TEXT", nullable: false),
                    BrandId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTemplates_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentBrands_BrandId",
                table: "AgentBrands",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_BrandId",
                table: "EmailTemplates",
                column: "BrandId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentBrands_Brands_BrandId",
                table: "AgentBrands",
                column: "BrandId",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentBrands_Brands_BrandId",
                table: "AgentBrands");

            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropIndex(
                name: "IX_AgentBrands_BrandId",
                table: "AgentBrands");
        }
    }
}

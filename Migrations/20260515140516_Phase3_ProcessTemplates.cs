using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_ProcessTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TemplateCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IconCssClass = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BodyText = table.Column<string>(type: "TEXT", nullable: false),
                    GuidanceNotes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessTemplates_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcessTemplates_TemplateCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TemplateCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessTemplateBrands",
                columns: table => new
                {
                    ProcessTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    BrandId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessTemplateBrands", x => new { x.ProcessTemplateId, x.BrandId });
                    table.ForeignKey(
                        name: "FK_ProcessTemplateBrands_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessTemplateBrands_ProcessTemplates_ProcessTemplateId",
                        column: x => x.ProcessTemplateId,
                        principalTable: "ProcessTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTemplateBrands_BrandId",
                table: "ProcessTemplateBrands",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTemplates_CategoryId",
                table: "ProcessTemplates",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTemplates_CreatedByUserId",
                table: "ProcessTemplates",
                column: "CreatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessTemplateBrands");

            migrationBuilder.DropTable(
                name: "ProcessTemplates");

            migrationBuilder.DropTable(
                name: "TemplateCategories");
        }
    }
}

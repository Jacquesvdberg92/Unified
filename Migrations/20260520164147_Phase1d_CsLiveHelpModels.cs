using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class Phase1d_CsLiveHelpModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AmAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CsRequestTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsOther = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CsRequestTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CsRequestArchives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalRequestId = table.Column<int>(type: "int", nullable: false),
                    AccountManagerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BrandId = table.Column<int>(type: "int", nullable: false),
                    RequestTypeId = table.Column<int>(type: "int", nullable: false),
                    CustomDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CsRequestArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CsRequestArchives_AspNetUsers_AccountManagerId",
                        column: x => x.AccountManagerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CsRequestArchives_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CsRequestArchives_CsRequestTypes_RequestTypeId",
                        column: x => x.RequestTypeId,
                        principalTable: "CsRequestTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CsRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountManagerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BrandId = table.Column<int>(type: "int", nullable: false),
                    RequestTypeId = table.Column<int>(type: "int", nullable: false),
                    CustomDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CsRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CsRequests_AspNetUsers_AccountManagerId",
                        column: x => x.AccountManagerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CsRequests_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CsRequests_CsRequestTypes_RequestTypeId",
                        column: x => x.RequestTypeId,
                        principalTable: "CsRequestTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CsRequestComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    AuthorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSystemMessage = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CsRequestComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CsRequestComments_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CsRequestComments_CsRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "CsRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CsRequestTypes",
                columns: new[] { "Id", "IsOther", "Name" },
                values: new object[,]
                {
                    { 1, false, "Simulate POI" },
                    { 2, false, "Reset Password" },
                    { 3, true, "Other" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AmAuditLogs_UserId_Timestamp",
                table: "AmAuditLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_CsRequestArchives_AccountManagerId",
                table: "CsRequestArchives",
                column: "AccountManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_CsRequestArchives_BrandId",
                table: "CsRequestArchives",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_CsRequestArchives_RequestTypeId",
                table: "CsRequestArchives",
                column: "RequestTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CsRequestComments_AuthorId",
                table: "CsRequestComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_CsRequestComments_RequestId",
                table: "CsRequestComments",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CsRequests_AccountManagerId",
                table: "CsRequests",
                column: "AccountManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_CsRequests_BrandId",
                table: "CsRequests",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_CsRequests_CreatedAt",
                table: "CsRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CsRequests_RequestTypeId",
                table: "CsRequests",
                column: "RequestTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CsRequests_Status",
                table: "CsRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmAuditLogs");

            migrationBuilder.DropTable(
                name: "CsRequestArchives");

            migrationBuilder.DropTable(
                name: "CsRequestComments");

            migrationBuilder.DropTable(
                name: "CsRequests");

            migrationBuilder.DropTable(
                name: "CsRequestTypes");
        }
    }
}

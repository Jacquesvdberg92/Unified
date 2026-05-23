using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class CsMessagingConversationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CsConversations_IsArchived_UpdatedAt",
                table: "CsConversations",
                columns: new[] { "IsArchived", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CsConversations_IsGroup_IsArchived_UpdatedAt",
                table: "CsConversations",
                columns: new[] { "IsGroup", "IsArchived", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CsConversations_IsArchived_UpdatedAt",
                table: "CsConversations");

            migrationBuilder.DropIndex(
                name: "IX_CsConversations_IsGroup_IsArchived_UpdatedAt",
                table: "CsConversations");
        }
    }
}

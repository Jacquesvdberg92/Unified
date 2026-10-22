using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unified.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSwissArmyKnifeRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Move every SwissArmyKnife user to CSAgent (single role per user), make them
            // sign in again so the new role takes effect, then delete the role itself.
            migrationBuilder.Sql(@"
DECLARE @sak NVARCHAR(450) = (SELECT TOP 1 Id FROM AspNetRoles WHERE NormalizedName = 'SWISSARMYKNIFE');
DECLARE @cs  NVARCHAR(450) = (SELECT TOP 1 Id FROM AspNetRoles WHERE NormalizedName = 'CSAGENT');

IF @sak IS NOT NULL
BEGIN
    IF @cs IS NULL
    BEGIN
        SET @cs = CONVERT(NVARCHAR(450), NEWID());
        INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
        VALUES (@cs, 'CSAgent', 'CSAGENT', CONVERT(NVARCHAR(450), NEWID()));
    END

    UPDATE u SET SecurityStamp = CONVERT(NVARCHAR(450), NEWID())
    FROM AspNetUsers u
    WHERE u.Id IN (SELECT UserId FROM AspNetUserRoles WHERE RoleId = @sak);

    INSERT INTO AspNetUserRoles (UserId, RoleId)
    SELECT ur.UserId, @cs
    FROM AspNetUserRoles ur
    WHERE ur.RoleId = @sak
      AND NOT EXISTS (SELECT 1 FROM AspNetUserRoles x WHERE x.UserId = ur.UserId AND x.RoleId = @cs);

    DELETE FROM AspNetUserRoles WHERE RoleId = @sak;
    DELETE FROM AspNetRoleClaims WHERE RoleId = @sak;
    DELETE FROM AspNetRoles WHERE Id = @sak;
END
");

            migrationBuilder.DropColumn(
                name: "IsSwissArmyKnife",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The role and column come back empty: which users were SwissArmyKnife is not recoverable.
            migrationBuilder.AddColumn<bool>(
                name: "IsSwissArmyKnife",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}

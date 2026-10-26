using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberRoles_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberRoles_MemberId_Role",
                table: "MemberRoles",
                columns: new[] { "MemberId", "Role" },
                unique: true);

            // Single role column -> role rows. "Standard" meant "no role" and gets no row.
            migrationBuilder.Sql(
                "INSERT INTO MemberRoles (MemberId, Role) SELECT Id, 'Admin' FROM Members WHERE Role = 'Admin';");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Members");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Members",
                type: "TEXT",
                nullable: false,
                defaultValue: "Standard");

            // Lossy on purpose: the old column holds one role, so ContentManager is dropped.
            migrationBuilder.Sql(
                "UPDATE Members SET Role = 'Admin' WHERE Id IN (SELECT MemberId FROM MemberRoles WHERE Role = 'Admin');");

            migrationBuilder.DropTable(
                name: "MemberRoles");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class ExtendDataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInternal",
                table: "VideoLinks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAttendanceVisible",
                table: "Members",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Dances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Dances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DanceNumbers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Lyrics = table.Column<string>(type: "TEXT", nullable: true),
                    DanceId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DanceNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DanceNumbers_Dances_DanceId",
                        column: x => x.DanceId,
                        principalTable: "Dances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DanceNumbers_DanceId",
                table: "DanceNumbers",
                column: "DanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DanceNumbers");

            migrationBuilder.DropColumn(
                name: "IsInternal",
                table: "VideoLinks");

            migrationBuilder.DropColumn(
                name: "IsAttendanceVisible",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Dances");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Dances");
        }
    }
}

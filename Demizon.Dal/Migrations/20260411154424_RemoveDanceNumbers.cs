using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDanceNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoLinks_Dances_DanceId",
                table: "VideoLinks");

            migrationBuilder.DropTable(
                name: "DanceNumbers");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoLinks_Dances_DanceId",
                table: "VideoLinks",
                column: "DanceId",
                principalTable: "Dances",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoLinks_Dances_DanceId",
                table: "VideoLinks");

            migrationBuilder.CreateTable(
                name: "DanceNumbers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DanceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Lyrics = table.Column<string>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
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

            migrationBuilder.AddForeignKey(
                name: "FK_VideoLinks_Dances_DanceId",
                table: "VideoLinks",
                column: "DanceId",
                principalTable: "Dances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

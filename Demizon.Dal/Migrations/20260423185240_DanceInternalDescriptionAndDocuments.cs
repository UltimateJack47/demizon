using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class DanceInternalDescriptionAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Files",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InternalDescription",
                table: "Dances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_DanceId_Kind",
                table: "Files",
                columns: new[] { "DanceId", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Files_DanceId_Kind",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "InternalDescription",
                table: "Dances");
        }
    }
}

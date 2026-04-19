using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class PhotoStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Data",
                table: "Files",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ThumbnailData",
                table: "Files",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Data",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "ThumbnailData",
                table: "Files");
        }
    }
}

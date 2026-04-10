using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class ImproveDataIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Dances_DanceId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoLinks_Dances_DanceId",
                table: "VideoLinks");

            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_MemberId",
                table: "PushSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_MemberId_Endpoint",
                table: "PushSubscriptions",
                columns: new[] { "MemberId", "Endpoint" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Dances_DanceId",
                table: "Files",
                column: "DanceId",
                principalTable: "Dances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VideoLinks_Dances_DanceId",
                table: "VideoLinks",
                column: "DanceId",
                principalTable: "Dances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Dances_DanceId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoLinks_Dances_DanceId",
                table: "VideoLinks");

            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_MemberId_Endpoint",
                table: "PushSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_MemberId",
                table: "PushSubscriptions",
                column: "MemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Dances_DanceId",
                table: "Files",
                column: "DanceId",
                principalTable: "Dances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoLinks_Dances_DanceId",
                table: "VideoLinks",
                column: "DanceId",
                principalTable: "Dances",
                principalColumn: "Id");
        }
    }
}

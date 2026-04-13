using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class RefineDeviceTokenAndRefreshTokenIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_TokenPrefix",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_DeviceTokens_MemberId_Token",
                table: "DeviceTokens");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenPrefix_IsRevoked",
                table: "RefreshTokens",
                columns: new[] { "TokenPrefix", "IsRevoked" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_MemberId",
                table: "DeviceTokens",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_Token",
                table: "DeviceTokens",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_TokenPrefix_IsRevoked",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_DeviceTokens_MemberId",
                table: "DeviceTokens");

            migrationBuilder.DropIndex(
                name: "IX_DeviceTokens_Token",
                table: "DeviceTokens");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenPrefix",
                table: "RefreshTokens",
                column: "TokenPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_MemberId_Token",
                table: "DeviceTokens",
                columns: new[] { "MemberId", "Token" },
                unique: true);
        }
    }
}

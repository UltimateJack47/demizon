using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceTokenWalModeAndRefreshTokenPrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenPrefix",
                table: "RefreshTokens",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DeviceTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceTokens_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_TokenPrefix",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "TokenPrefix",
                table: "RefreshTokens");
        }
    }
}

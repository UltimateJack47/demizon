using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class NotificationRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Events",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "SentNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MemberId = table.Column<int>(type: "INTEGER", nullable: true),
                    EventId = table.Column<int>(type: "INTEGER", nullable: true),
                    RehearsalDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NotificationType = table.Column<string>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SentNotifications_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SentNotifications_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SentNotifications_EventId",
                table: "SentNotifications",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_SentNotifications_MemberId_EventId_NotificationType",
                table: "SentNotifications",
                columns: new[] { "MemberId", "EventId", "NotificationType" });

            migrationBuilder.CreateIndex(
                name: "IX_SentNotifications_MemberId_RehearsalDate_NotificationType",
                table: "SentNotifications",
                columns: new[] { "MemberId", "RehearsalDate", "NotificationType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SentNotifications");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Events");
        }
    }
}

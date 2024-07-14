using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class Attendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DanceId",
                table: "VideoLinks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DanceId",
                table: "Files",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lyrics",
                table: "Dances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Attends = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventId = table.Column<int>(type: "INTEGER", nullable: true),
                    MemberId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Attendances_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoLinks_DanceId",
                table: "VideoLinks",
                column: "DanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_DanceId",
                table: "Files",
                column: "DanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EventId",
                table: "Attendances",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_MemberId",
                table: "Attendances",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Dances_DanceId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_VideoLinks_Dances_DanceId",
                table: "VideoLinks");

            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_VideoLinks_DanceId",
                table: "VideoLinks");

            migrationBuilder.DropIndex(
                name: "IX_Files_DanceId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "DanceId",
                table: "VideoLinks");

            migrationBuilder.DropColumn(
                name: "DanceId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "Lyrics",
                table: "Dances");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class MergeMembersAndUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Members_MemberId",
                table: "Files");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.RenameColumn(
                name: "MemberId",
                table: "Files",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Files_MemberId",
                table: "Files",
                newName: "IX_Files_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<DateTime>(
                name: "BirthDate",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsVisible",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MemberSince",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Users_UserId",
                table: "Files",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Users_UserId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsVisible",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MemberSince",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Files",
                newName: "MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_Files_UserId",
                table: "Files",
                newName: "IX_Files_MemberId");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BirthDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    Gender = table.Column<string>(type: "TEXT", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    MemberSince = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Members_MemberId",
                table: "Files",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id");
        }
    }
}

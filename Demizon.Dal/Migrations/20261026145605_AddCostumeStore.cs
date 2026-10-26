using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demizon.Dal.Migrations
{
    /// <inheritdoc />
    public partial class AddCostumeStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostumeQrToken",
                table: "Members",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CostumeInventoryChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StartedById = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumeInventoryChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostumeInventoryChecks_Members_StartedById",
                        column: x => x.StartedById,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Costumes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Costumes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CostumeTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromMemberId = table.Column<int>(type: "INTEGER", nullable: true),
                    ToMemberId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolvedById = table.Column<int>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumeTransfers", x => x.Id);
                    table.CheckConstraint("CK_CostumeTransfers_Parties", "(FromMemberId IS NOT NULL OR ToMemberId IS NOT NULL) AND (FromMemberId IS NULL OR ToMemberId IS NULL OR FromMemberId <> ToMemberId)");
                    table.ForeignKey(
                        name: "FK_CostumeTransfers_Members_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostumeTransfers_Members_FromMemberId",
                        column: x => x.FromMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostumeTransfers_Members_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostumeTransfers_Members_ToMemberId",
                        column: x => x.ToMemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostumeParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CostumeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TrackingMode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumeParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostumeParts_Costumes_CostumeId",
                        column: x => x.CostumeId,
                        principalTable: "Costumes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostumeItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Size = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ValueCzk = table.Column<int>(type: "INTEGER", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumeItems", x => x.Id);
                    table.CheckConstraint("CK_CostumeItems_Quantity", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_CostumeItems_CostumeParts_PartId",
                        column: x => x.PartId,
                        principalTable: "CostumeParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostumeHoldings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    MemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumeHoldings", x => x.Id);
                    table.CheckConstraint("CK_CostumeHoldings_Quantity", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_CostumeHoldings_CostumeItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "CostumeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostumeHoldings_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostumeInventoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CheckId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    CountedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumeInventoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostumeInventoryEntries_CostumeInventoryChecks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "CostumeInventoryChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostumeInventoryEntries_CostumeItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "CostumeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostumeMaintenances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CostCzk = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumeMaintenances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostumeMaintenances_CostumeItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "CostumeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostumeMaintenances_Members_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostumeTransferLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransferId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ConditionNote = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumeTransferLines", x => x.Id);
                    table.CheckConstraint("CK_CostumeTransferLines_Quantity", "Quantity > 0");
                    table.ForeignKey(
                        name: "FK_CostumeTransferLines_CostumeItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "CostumeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CostumeTransferLines_CostumeTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "CostumeTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostumePhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransferLineId = table.Column<int>(type: "INTEGER", nullable: true),
                    PartId = table.Column<int>(type: "INTEGER", nullable: true),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ThumbnailData = table.Column<byte[]>(type: "BLOB", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedById = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostumePhotos", x => x.Id);
                    table.CheckConstraint("CK_CostumePhotos_Owner", "(TransferLineId IS NULL) <> (PartId IS NULL)");
                    table.ForeignKey(
                        name: "FK_CostumePhotos_CostumeParts_PartId",
                        column: x => x.PartId,
                        principalTable: "CostumeParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostumePhotos_CostumeTransferLines_TransferLineId",
                        column: x => x.TransferLineId,
                        principalTable: "CostumeTransferLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostumePhotos_Members_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Members_CostumeQrToken",
                table: "Members",
                column: "CostumeQrToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostumeHoldings_ItemId_MemberId",
                table: "CostumeHoldings",
                columns: new[] { "ItemId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostumeHoldings_MemberId",
                table: "CostumeHoldings",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeInventoryEntries_CheckId_ItemId",
                table: "CostumeInventoryEntries",
                columns: new[] { "CheckId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostumeInventoryEntries_ItemId",
                table: "CostumeInventoryEntries",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeInventoryChecks_StartedById",
                table: "CostumeInventoryChecks",
                column: "StartedById");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeItems_Code",
                table: "CostumeItems",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostumeItems_PartId",
                table: "CostumeItems",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeMaintenances_CreatedById",
                table: "CostumeMaintenances",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeMaintenances_ItemId",
                table: "CostumeMaintenances",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeParts_CostumeId",
                table: "CostumeParts",
                column: "CostumeId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumePhotos_PartId",
                table: "CostumePhotos",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumePhotos_TransferLineId",
                table: "CostumePhotos",
                column: "TransferLineId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumePhotos_UploadedById",
                table: "CostumePhotos",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeTransferLines_ItemId",
                table: "CostumeTransferLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeTransferLines_TransferId_ItemId",
                table: "CostumeTransferLines",
                columns: new[] { "TransferId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostumeTransfers_CreatedById",
                table: "CostumeTransfers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeTransfers_FromMemberId",
                table: "CostumeTransfers",
                column: "FromMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeTransfers_ResolvedById",
                table: "CostumeTransfers",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeTransfers_Status",
                table: "CostumeTransfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CostumeTransfers_ToMemberId",
                table: "CostumeTransfers",
                column: "ToMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostumeHoldings");

            migrationBuilder.DropTable(
                name: "CostumeInventoryEntries");

            migrationBuilder.DropTable(
                name: "CostumeMaintenances");

            migrationBuilder.DropTable(
                name: "CostumePhotos");

            migrationBuilder.DropTable(
                name: "CostumeInventoryChecks");

            migrationBuilder.DropTable(
                name: "CostumeTransferLines");

            migrationBuilder.DropTable(
                name: "CostumeItems");

            migrationBuilder.DropTable(
                name: "CostumeTransfers");

            migrationBuilder.DropTable(
                name: "CostumeParts");

            migrationBuilder.DropTable(
                name: "Costumes");

            migrationBuilder.DropIndex(
                name: "IX_Members_CostumeQrToken",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "CostumeQrToken",
                table: "Members");
        }
    }
}

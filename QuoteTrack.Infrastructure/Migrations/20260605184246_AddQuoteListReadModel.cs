using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteListReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuoteListItems",
                columns: table => new
                {
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsDeleteRequested = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: true),
                    OwnerName = table.Column<string>(type: "text", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientName = table.Column<string>(type: "text", nullable: false),
                    SenderEmail = table.Column<string>(type: "text", nullable: false),
                    SenderName = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    QuoteReference = table.Column<string>(type: "text", nullable: false),
                    QuoteValue = table.Column<decimal>(type: "numeric", nullable: true),
                    WinProbability = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    NextFollowUpDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmailReceivedDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RepCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastNoteAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastNotePreview = table.Column<string>(type: "text", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    IsUnassigned = table.Column<bool>(type: "boolean", nullable: false),
                    MissingFollowUp = table.Column<bool>(type: "boolean", nullable: false),
                    ValueTbd = table.Column<bool>(type: "boolean", nullable: false),
                    MissingClientLink = table.Column<bool>(type: "boolean", nullable: false),
                    DeleteRequestReason = table.Column<string>(type: "text", nullable: false),
                    DeleteRequestedByUserId = table.Column<string>(type: "text", nullable: false),
                    LeadSource = table.Column<string>(type: "text", nullable: false),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    RefreshedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteListItems", x => x.QuoteId);
                });

            migrationBuilder.CreateTable(
                name: "ReadModelStates",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    LastRefreshedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRefreshing = table.Column<bool>(type: "boolean", nullable: false),
                    IsStale = table.Column<bool>(type: "boolean", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadModelStates", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteListItems_EmailReceivedDateTime",
                table: "QuoteListItems",
                column: "EmailReceivedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteListItems_LastNoteAt",
                table: "QuoteListItems",
                column: "LastNoteAt");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteListItems_NextFollowUpDate",
                table: "QuoteListItems",
                column: "NextFollowUpDate");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteListItems_OwnerId_IsDeleteRequested_CreatedAt",
                table: "QuoteListItems",
                columns: new[] { "OwnerId", "IsDeleteRequested", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteListItems_RecordType_IsDeleteRequested_CreatedAt",
                table: "QuoteListItems",
                columns: new[] { "RecordType", "IsDeleteRequested", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteListItems_RecordType_Status_IsDeleteRequested_OwnerId",
                table: "QuoteListItems",
                columns: new[] { "RecordType", "Status", "IsDeleteRequested", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadModelStates_IsStale",
                table: "ReadModelStates",
                column: "IsStale");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuoteListItems");

            migrationBuilder.DropTable(
                name: "ReadModelStates");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LeadQuoteWorkflowAndMerges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedByUserId",
                table: "Quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureNotes",
                table: "Quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMerged",
                table: "Quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MergeNotes",
                table: "Quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MergedIntoQuoteId",
                table: "Quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordType",
                table: "Quotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RepAttachedQuoteReference",
                table: "Quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepCompletedAt",
                table: "Quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepCompletionNotes",
                table: "Quotes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MergeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceQuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetQuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MergeRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MergeRequests_Status",
                table: "MergeRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MergeRequests");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ClosureNotes",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "IsMerged",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "MergeNotes",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "MergedIntoQuoteId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RecordType",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RepAttachedQuoteReference",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RepCompletedAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RepCompletionNotes",
                table: "Quotes");
        }
    }
}

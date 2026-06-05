using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandCenterSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommandCenterActivityItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKey = table.Column<string>(type: "text", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhenUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WhenLocal = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Link = table.Column<string>(type: "text", nullable: false),
                    SortRank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandCenterActivityItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommandCenterQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKey = table.Column<string>(type: "text", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<int>(type: "integer", nullable: false),
                    RecordLabel = table.Column<string>(type: "text", nullable: false),
                    StatusLabel = table.Column<string>(type: "text", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    OwnerLabel = table.Column<string>(type: "text", nullable: false),
                    ClientLabel = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    DueUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueLocalText = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: true),
                    ValueText = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastNotePreview = table.Column<string>(type: "text", nullable: false),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    SortRank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandCenterQueueItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommandCenterRadarItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKey = table.Column<string>(type: "text", nullable: false),
                    RadarType = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    Percent = table.Column<double>(type: "double precision", nullable: false),
                    ValueText = table.Column<string>(type: "text", nullable: false),
                    SortRank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandCenterRadarItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommandCenterSnapshots",
                columns: table => new
                {
                    ScopeKey = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    OwnerId = table.Column<string>(type: "text", nullable: true),
                    IsAdminScope = table.Column<bool>(type: "boolean", nullable: false),
                    LastRefreshedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RefreshStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRefreshing = table.Column<bool>(type: "boolean", nullable: false),
                    IsStale = table.Column<bool>(type: "boolean", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    PipelineValue = table.Column<decimal>(type: "numeric", nullable: false),
                    ActiveQuotesCount = table.Column<int>(type: "integer", nullable: false),
                    WonThisMonth = table.Column<decimal>(type: "numeric", nullable: false),
                    WonCountThisMonth = table.Column<int>(type: "integer", nullable: false),
                    OverdueCount = table.Column<int>(type: "integer", nullable: false),
                    OverdueValue = table.Column<decimal>(type: "numeric", nullable: false),
                    DueTodayCount = table.Column<int>(type: "integer", nullable: false),
                    DueTodayValue = table.Column<decimal>(type: "numeric", nullable: false),
                    NewLeads7d = table.Column<int>(type: "integer", nullable: false),
                    UnassignedLeads = table.Column<int>(type: "integer", nullable: false),
                    UnassignedCount = table.Column<int>(type: "integer", nullable: false),
                    HighValueCount = table.Column<int>(type: "integer", nullable: false),
                    MissingFollowUpCount = table.Column<int>(type: "integer", nullable: false),
                    ValueTbdCount = table.Column<int>(type: "integer", nullable: false),
                    MissingClientLinkCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandCenterSnapshots", x => x.ScopeKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandCenterActivityItems_ScopeKey_SortRank",
                table: "CommandCenterActivityItems",
                columns: new[] { "ScopeKey", "SortRank" });

            migrationBuilder.CreateIndex(
                name: "IX_CommandCenterActivityItems_ScopeKey_WhenUtc",
                table: "CommandCenterActivityItems",
                columns: new[] { "ScopeKey", "WhenUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CommandCenterQueueItems_ScopeKey_RecordType",
                table: "CommandCenterQueueItems",
                columns: new[] { "ScopeKey", "RecordType" });

            migrationBuilder.CreateIndex(
                name: "IX_CommandCenterQueueItems_ScopeKey_SortRank",
                table: "CommandCenterQueueItems",
                columns: new[] { "ScopeKey", "SortRank" });

            migrationBuilder.CreateIndex(
                name: "IX_CommandCenterRadarItems_ScopeKey_RadarType_SortRank",
                table: "CommandCenterRadarItems",
                columns: new[] { "ScopeKey", "RadarType", "SortRank" });

            migrationBuilder.CreateIndex(
                name: "IX_CommandCenterSnapshots_IsStale",
                table: "CommandCenterSnapshots",
                column: "IsStale");

            migrationBuilder.CreateIndex(
                name: "IX_CommandCenterSnapshots_LastRefreshedAt",
                table: "CommandCenterSnapshots",
                column: "LastRefreshedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandCenterActivityItems");

            migrationBuilder.DropTable(
                name: "CommandCenterQueueItems");

            migrationBuilder.DropTable(
                name: "CommandCenterRadarItems");

            migrationBuilder.DropTable(
                name: "CommandCenterSnapshots");
        }
    }
}

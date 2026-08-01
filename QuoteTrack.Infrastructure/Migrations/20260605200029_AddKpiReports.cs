using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKpiReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KpiReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: false),
                    ReportType = table.Column<string>(type: "text", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WeekNumber = table.Column<int>(type: "integer", nullable: true),
                    PeriodLabel = table.Column<string>(type: "text", nullable: false),
                    ManagerReview = table.Column<string>(type: "text", nullable: false),
                    MainAchievements = table.Column<string>(type: "text", nullable: false),
                    AdditionalComments = table.Column<string>(type: "text", nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KpiReportLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KpiReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    MetricName = table.Column<string>(type: "text", nullable: false),
                    ValueText = table.Column<string>(type: "text", nullable: false),
                    AutoValueText = table.Column<string>(type: "text", nullable: false),
                    ManualValueText = table.Column<string>(type: "text", nullable: false),
                    Guidance = table.Column<string>(type: "text", nullable: false),
                    EvidenceText = table.Column<string>(type: "text", nullable: false),
                    IsManualInput = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiReportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KpiReportLines_KpiReports_KpiReportId",
                        column: x => x.KpiReportId,
                        principalTable: "KpiReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KpiReportLines_KpiReportId_SortOrder",
                table: "KpiReportLines",
                columns: new[] { "KpiReportId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_KpiReports_EmployeeId_ReportType_PeriodStart",
                table: "KpiReports",
                columns: new[] { "EmployeeId", "ReportType", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KpiReports_ReportType_PeriodStart",
                table: "KpiReports",
                columns: new[] { "ReportType", "PeriodStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KpiReportLines");

            migrationBuilder.DropTable(
                name: "KpiReports");
        }
    }
}

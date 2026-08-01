using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKpiTemplateSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KpiReports_EmployeeId_ReportType_PeriodStart",
                table: "KpiReports");

            migrationBuilder.DropIndex(
                name: "IX_KpiReports_ReportType_PeriodStart",
                table: "KpiReports");

            migrationBuilder.AddColumn<string>(
                name: "TemplateCode",
                table: "KpiReports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "KpiReports"
                SET "TemplateCode" = CASE
                    WHEN LOWER("EmployeeName") LIKE '%asiya%' THEN 'Sales 01'
                    WHEN LOWER("EmployeeName") LIKE '%faraj%' THEN 'ELV 02'
                    ELSE 'ELV 01'
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_KpiReports_EmployeeId_TemplateCode_ReportType_PeriodStart",
                table: "KpiReports",
                columns: new[] { "EmployeeId", "TemplateCode", "ReportType", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KpiReports_TemplateCode_ReportType_PeriodStart",
                table: "KpiReports",
                columns: new[] { "TemplateCode", "ReportType", "PeriodStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KpiReports_EmployeeId_TemplateCode_ReportType_PeriodStart",
                table: "KpiReports");

            migrationBuilder.DropIndex(
                name: "IX_KpiReports_TemplateCode_ReportType_PeriodStart",
                table: "KpiReports");

            migrationBuilder.DropColumn(
                name: "TemplateCode",
                table: "KpiReports");

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
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastingAndROI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LeadSource",
                table: "Quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinProbability",
                table: "Quotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeadSource",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "WinProbability",
                table: "Quotes");
        }
    }
}

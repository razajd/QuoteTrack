using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteDeletionWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeleteRequestReason",
                table: "Quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteRequestedByUserId",
                table: "Quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleteRequested",
                table: "Quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeleteRequestReason",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "DeleteRequestedByUserId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "IsDeleteRequested",
                table: "Quotes");
        }
    }
}

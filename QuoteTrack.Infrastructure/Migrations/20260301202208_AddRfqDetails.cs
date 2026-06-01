using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRfqDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactNumber",
                table: "Rfqs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalForwarderEmail",
                table: "Rfqs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Rfqs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactNumber",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "OriginalForwarderEmail",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Rfqs");
        }
    }
}

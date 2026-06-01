using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Quotes",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Quotes");
        }
    }
}

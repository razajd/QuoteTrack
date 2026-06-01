using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastFollowUpDate",
                table: "Quotes");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Quotes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_OwnerId",
                table: "Quotes",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_AspNetUsers_OwnerId",
                table: "Quotes",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_AspNetUsers_OwnerId",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_OwnerId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Quotes");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFollowUpDate",
                table: "Quotes",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}

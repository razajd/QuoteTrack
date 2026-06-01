using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRfqTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_AspNetUsers_OwnerId",
                table: "Quotes");

            migrationBuilder.CreateTable(
                name: "Rfqs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientEmail = table.Column<string>(type: "text", nullable: false),
                    ClientName = table.Column<string>(type: "text", nullable: true),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rfqs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rfqs_AspNetUsers_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rfqs_AssignedUserId",
                table: "Rfqs",
                column: "AssignedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_AspNetUsers_OwnerId",
                table: "Quotes",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_AspNetUsers_OwnerId",
                table: "Quotes");

            migrationBuilder.DropTable(
                name: "Rfqs");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_AspNetUsers_OwnerId",
                table: "Quotes",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}

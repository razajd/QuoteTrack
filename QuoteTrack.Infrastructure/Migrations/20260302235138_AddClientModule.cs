using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "Quotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    Industry = table.Column<string>(type: "text", nullable: true),
                    PrimaryContactName = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    CommercialRegistrationNumber = table.Column<string>(type: "text", nullable: true),
                    TaxRegistrationNumber = table.Column<string>(type: "text", nullable: true),
                    BillingAddress = table.Column<string>(type: "text", nullable: true),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "text", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Clients_AspNetUsers_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ClientId",
                table: "Quotes",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ApprovedByUserId",
                table: "Clients",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_SubmittedByUserId",
                table: "Clients",
                column: "SubmittedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_Clients_ClientId",
                table: "Quotes",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_Clients_ClientId",
                table: "Quotes");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_ClientId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Quotes");
        }
    }
}

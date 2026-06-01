using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace QuoteTrack.Infrastructure.Migrations
{
    [DbContext(typeof(QuoteTrack.Infrastructure.Data.AppDbContext))]
    [Migration("20260309000100_AddClientExtraFieldsAndFollowUpCreator")]
    public partial class AddClientExtraFieldsAndFollowUpCreator : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Quotes"" ADD COLUMN IF NOT EXISTS ""DiscoveryNotes"" text;");
            migrationBuilder.Sql(@"ALTER TABLE ""Quotes"" ADD COLUMN IF NOT EXISTS ""DiscoveryNotesUpdatedAt"" timestamptz;");
            migrationBuilder.Sql(@"ALTER TABLE ""Quotes"" ADD COLUMN IF NOT EXISTS ""DiscoveryNotesUpdatedByUserId"" text;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Quotes"" DROP COLUMN IF EXISTS ""DiscoveryNotes"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Quotes"" DROP COLUMN IF EXISTS ""DiscoveryNotesUpdatedAt"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Quotes"" DROP COLUMN IF EXISTS ""DiscoveryNotesUpdatedByUserId"";");

            migrationBuilder.DropColumn(name: "PrimaryContactDesignation", table: "Clients");
            migrationBuilder.DropColumn(name: "Website", table: "Clients");
            migrationBuilder.DropColumn(name: "AddressLine1", table: "Clients");
            migrationBuilder.DropColumn(name: "AddressLine2", table: "Clients");
            migrationBuilder.DropColumn(name: "City", table: "Clients");
            migrationBuilder.DropColumn(name: "Country", table: "Clients");
            migrationBuilder.DropColumn(name: "Notes", table: "Clients");
        }
    }
}

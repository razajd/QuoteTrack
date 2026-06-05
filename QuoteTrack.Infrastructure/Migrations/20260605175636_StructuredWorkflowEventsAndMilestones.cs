using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StructuredWorkflowEventsAndMilestones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Quotes"" ADD COLUMN IF NOT EXISTS ""AssignedAt"" timestamptz;
                ALTER TABLE ""Quotes"" ADD COLUMN IF NOT EXISTS ""FirstContactedAt"" timestamptz;
                ALTER TABLE ""Quotes"" ADD COLUMN IF NOT EXISTS ""LostAt"" timestamptz;
                ALTER TABLE ""Quotes"" ADD COLUMN IF NOT EXISTS ""QuotedAt"" timestamptz;
                ALTER TABLE ""Quotes"" ADD COLUMN IF NOT EXISTS ""WonAt"" timestamptz;

                ALTER TABLE ""FollowUps"" ADD COLUMN IF NOT EXISTS ""CreatedByUserId"" text;

                ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""AddressLine1"" text;
                ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""AddressLine2"" text;
                ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""City"" text;
                ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""Country"" text;
                ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""Notes"" text;
                ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""PrimaryContactDesignation"" text;
                ALTER TABLE ""Clients"" ADD COLUMN IF NOT EXISTS ""Website"" text;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Quotes""
                SET ""AssignedAt"" = COALESCE(""AssignedAt"", ""CreatedAt"")
                WHERE ""OwnerId"" IS NOT NULL AND ""OwnerId"" <> '';

                UPDATE ""Quotes""
                SET ""FirstContactedAt"" = COALESCE(""FirstContactedAt"", ""UpdatedAt"", ""CreatedAt"")
                WHERE ""Status"" IN (1, 11, 15, 16, 17, 18);

                UPDATE ""Quotes""
                SET ""QuotedAt"" = COALESCE(
                    ""QuotedAt"",
                    CASE
                        WHEN ""EmailSentDateTime"" > '1900-01-01'::timestamptz THEN ""EmailSentDateTime""
                        ELSE ""CreatedAt""
                    END)
                WHERE ""RecordType"" = 1 AND ""Status"" IN (1, 3, 4, 20, 21, 22);

                UPDATE ""Quotes""
                SET ""WonAt"" = COALESCE(""WonAt"", ""ClosedAt"", ""UpdatedAt"", ""CreatedAt"")
                WHERE ""Status"" = 3;

                UPDATE ""Quotes""
                SET ""LostAt"" = COALESCE(""LostAt"", ""ClosedAt"", ""UpdatedAt"", ""CreatedAt"")
                WHERE ""Status"" IN (4, 5);
            ");

            migrationBuilder.CreateTable(
                name: "QuoteEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: true),
                    FromOwnerId = table.Column<string>(type: "text", nullable: true),
                    ToOwnerId = table.Column<string>(type: "text", nullable: true),
                    ActorUserId = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QuoteEvents_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Rfqs_ReceivedAt"" ON ""Rfqs"" (""ReceivedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_AssignedAt"" ON ""Quotes"" (""AssignedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_CreatedAt"" ON ""Quotes"" (""CreatedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_EmailMessageId"" ON ""Quotes"" (""EmailMessageId"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_FirstContactedAt"" ON ""Quotes"" (""FirstContactedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_IsDeleteRequested"" ON ""Quotes"" (""IsDeleteRequested"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_LostAt"" ON ""Quotes"" (""LostAt"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_NextFollowUpDate"" ON ""Quotes"" (""NextFollowUpDate"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_OwnerId_IsDeleteRequested_CreatedAt"" ON ""Quotes"" (""OwnerId"", ""IsDeleteRequested"", ""CreatedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_QuotedAt"" ON ""Quotes"" (""QuotedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_RecordType"" ON ""Quotes"" (""RecordType"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_RecordType_IsDeleteRequested_CreatedAt"" ON ""Quotes"" (""RecordType"", ""IsDeleteRequested"", ""CreatedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_RecordType_IsDeleteRequested_NextFollowUpDate"" ON ""Quotes"" (""RecordType"", ""IsDeleteRequested"", ""NextFollowUpDate"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_RecordType_OwnerId_Status"" ON ""Quotes"" (""RecordType"", ""OwnerId"", ""Status"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_RecordType_Status"" ON ""Quotes"" (""RecordType"", ""Status"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_Status"" ON ""Quotes"" (""Status"");
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_WonAt"" ON ""Quotes"" (""WonAt"");

                CREATE INDEX IF NOT EXISTS ""IX_FollowUps_CreatedAt"" ON ""FollowUps"" (""CreatedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_FollowUps_CreatedByUserId"" ON ""FollowUps"" (""CreatedByUserId"");
                CREATE INDEX IF NOT EXISTS ""IX_FollowUps_QuoteId_CreatedAt"" ON ""FollowUps"" (""QuoteId"", ""CreatedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_FollowUps_QuoteId_DueDate"" ON ""FollowUps"" (""QuoteId"", ""DueDate"");

                CREATE INDEX IF NOT EXISTS ""IX_Clients_CompanyName"" ON ""Clients"" (""CompanyName"");

                CREATE INDEX IF NOT EXISTS ""IX_ActivityLogs_RelatedQuoteId"" ON ""ActivityLogs"" (""RelatedQuoteId"");
                CREATE INDEX IF NOT EXISTS ""IX_ActivityLogs_Timestamp"" ON ""ActivityLogs"" (""Timestamp"");
                CREATE INDEX IF NOT EXISTS ""IX_ActivityLogs_UserId_Timestamp"" ON ""ActivityLogs"" (""UserId"", ""Timestamp"");

                CREATE INDEX IF NOT EXISTS ""IX_QuoteEvents_ActorUserId"" ON ""QuoteEvents"" (""ActorUserId"");
                CREATE INDEX IF NOT EXISTS ""IX_QuoteEvents_EventType"" ON ""QuoteEvents"" (""EventType"");
                CREATE INDEX IF NOT EXISTS ""IX_QuoteEvents_EventType_OccurredAt"" ON ""QuoteEvents"" (""EventType"", ""OccurredAt"");
                CREATE INDEX IF NOT EXISTS ""IX_QuoteEvents_OccurredAt"" ON ""QuoteEvents"" (""OccurredAt"");
                CREATE INDEX IF NOT EXISTS ""IX_QuoteEvents_QuoteId"" ON ""QuoteEvents"" (""QuoteId"");
                CREATE INDEX IF NOT EXISTS ""IX_QuoteEvents_QuoteId_OccurredAt"" ON ""QuoteEvents"" (""QuoteId"", ""OccurredAt"");
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_FollowUps_AspNetUsers_CreatedByUserId'
                    ) THEN
                        ALTER TABLE ""FollowUps""
                        ADD CONSTRAINT ""FK_FollowUps_AspNetUsers_CreatedByUserId""
                        FOREIGN KEY (""CreatedByUserId"") REFERENCES ""AspNetUsers"" (""Id"")
                        ON DELETE SET NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_AspNetUsers_CreatedByUserId",
                table: "FollowUps");

            migrationBuilder.DropTable(
                name: "QuoteEvents");

            migrationBuilder.DropIndex(
                name: "IX_Rfqs_ReceivedAt",
                table: "Rfqs");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_AssignedAt",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_CreatedAt",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_EmailMessageId",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_FirstContactedAt",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_IsDeleteRequested",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_LostAt",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_NextFollowUpDate",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_OwnerId_IsDeleteRequested_CreatedAt",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_QuotedAt",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_RecordType",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_RecordType_IsDeleteRequested_CreatedAt",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_RecordType_IsDeleteRequested_NextFollowUpDate",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_RecordType_OwnerId_Status",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_RecordType_Status",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_Status",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_WonAt",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_CreatedAt",
                table: "FollowUps");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_CreatedByUserId",
                table: "FollowUps");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_QuoteId_CreatedAt",
                table: "FollowUps");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_QuoteId_DueDate",
                table: "FollowUps");

            migrationBuilder.DropIndex(
                name: "IX_Clients_CompanyName",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_RelatedQuoteId",
                table: "ActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_Timestamp",
                table: "ActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_UserId_Timestamp",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "FirstContactedAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "LostAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "QuotedAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "WonAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "PrimaryContactDesignation",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Clients");
        }
    }
}

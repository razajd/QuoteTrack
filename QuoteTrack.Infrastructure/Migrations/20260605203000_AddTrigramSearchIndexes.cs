using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuoteTrack.Infrastructure.Data;

namespace QuoteTrack.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260605203000_AddTrigramSearchIndexes")]
    public partial class AddTrigramSearchIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Quotes_ClientName_Trgm""
                    ON ""Quotes"" USING gin (lower(""ClientName"") gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS ""IX_Quotes_SenderEmail_Trgm""
                    ON ""Quotes"" USING gin (lower(""SenderEmail"") gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS ""IX_Quotes_QuoteReference_Trgm""
                    ON ""Quotes"" USING gin (lower(""QuoteReference"") gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS ""IX_Quotes_Subject_Trgm""
                    ON ""Quotes"" USING gin (lower(""Subject"") gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS ""IX_Clients_CompanyName_Trgm""
                    ON ""Clients"" USING gin (lower(""CompanyName"") gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS ""IX_Quotes_RecordType_Status_Delete_Owner""
                    ON ""Quotes"" (""RecordType"", ""Status"", ""IsDeleteRequested"", ""OwnerId"");

                CREATE INDEX IF NOT EXISTS ""IX_Quotes_RecordType_Delete_Created_Id""
                    ON ""Quotes"" (""RecordType"", ""IsDeleteRequested"", ""CreatedAt"" DESC, ""Id"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_Quotes_RecordType_Delete_Created_Id"";
                DROP INDEX IF EXISTS ""IX_Quotes_RecordType_Status_Delete_Owner"";
                DROP INDEX IF EXISTS ""IX_Clients_CompanyName_Trgm"";
                DROP INDEX IF EXISTS ""IX_Quotes_Subject_Trgm"";
                DROP INDEX IF EXISTS ""IX_Quotes_QuoteReference_Trgm"";
                DROP INDEX IF EXISTS ""IX_Quotes_SenderEmail_Trgm"";
                DROP INDEX IF EXISTS ""IX_Quotes_ClientName_Trgm"";
            ");
        }
    }
}

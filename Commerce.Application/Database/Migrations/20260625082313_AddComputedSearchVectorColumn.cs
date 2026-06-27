using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Commerce.Application.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddComputedSearchVectorColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop existing column (if any) – safe to do
            migrationBuilder.Sql("ALTER TABLE \"Products\" DROP COLUMN IF EXISTS \"SearchVector\";");

            // Add computed column with the expression
            migrationBuilder.Sql(
                @"ALTER TABLE ""Products"" 
                  ADD COLUMN ""SearchVector"" tsvector 
                  GENERATED ALWAYS AS (to_tsvector('english', ""Name"" || ' ' || COALESCE(""Description"", ''))) STORED;");

            // Create GIN index for fast searching
            migrationBuilder.Sql(
                @"CREATE INDEX ""IX_Products_SearchVector"" ON ""Products"" USING GIN (""SearchVector"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Products_SearchVector\";");
            migrationBuilder.Sql("ALTER TABLE \"Products\" DROP COLUMN IF EXISTS \"SearchVector\";");
        }
    }
}
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Commerce.Application.Database.Migrations
{
    /// <inheritdoc />
    public partial class FixSearchVectorComputedColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Products\" DROP COLUMN IF EXISTS \"SearchVector\";");
                migrationBuilder.Sql(
                    @"ALTER TABLE ""Products"" 
                      ADD COLUMN ""SearchVector"" tsvector 
                      GENERATED ALWAYS AS (to_tsvector('english', ""Name"" || ' ' || COALESCE(""Description"", ''))) STORED;");
                migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Products_SearchVector\" ON \"Products\" USING GIN (\"SearchVector\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Products",
                type: "tsvector",
                nullable: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldNullable: true,
                oldComputedColumnSql: "to_tsvector('english', \"Name\" || ' ' || COALESCE(\"Description\", ''))");
        }
    }
}

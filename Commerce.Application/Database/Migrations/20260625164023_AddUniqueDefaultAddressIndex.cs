using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Application.Database.Migrations
{
    public partial class AddUniqueDefaultAddressIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure at most one default address per user
            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX IX_Address_UserId_IsDefault_Unique 
                  ON ""Addresses"" (""UserId"") 
                  WHERE ""IsDefault"" = true;"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP INDEX IX_Address_UserId_IsDefault_Unique;"
            );
        }
    }
}
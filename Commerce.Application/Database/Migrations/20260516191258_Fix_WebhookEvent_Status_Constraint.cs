using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Application.Database.Migrations
{
    /// <inheritdoc />
    public partial class Fix_WebhookEvent_Status_Constraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WebhookEvent_Status",
                table: "WebhookEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WebhookEvent_Status",
                table: "WebhookEvents",
                sql: "\"Status\" IN ('Pending','Processed','Failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WebhookEvent_Status",
                table: "WebhookEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WebhookEvent_Status",
                table: "WebhookEvents",
                sql: "\"Status\" IN ('PENDING','PROCESSED','FAILED')");
        }
    }
}

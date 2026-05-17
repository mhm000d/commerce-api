using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Application.Database.Migrations
{
    /// <inheritdoc />
    public partial class update_OrderState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payment_Status",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Status",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmailNotification_Status",
                table: "EmailNotifications");

            migrationBuilder.AlterColumn<string>(
                name: "OrderNumber",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payment_Status",
                table: "Payments",
                sql: "\"Status\" IN ('Pending','Completed','Failed','Refunded')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Status",
                table: "Orders",
                sql: "\"Status\" IN ('Placed','Paid','Shipped','Delivered','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmailNotification_Status",
                table: "EmailNotifications",
                sql: "\"Status\" IN ('Queued','Sent','Failed','PermanentlyFailed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Payment_Status",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Order_Status",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmailNotification_Status",
                table: "EmailNotifications");

            migrationBuilder.AlterColumn<string>(
                name: "OrderNumber",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payment_Status",
                table: "Payments",
                sql: "\"Status\" IN ('PENDING','COMPLETED','FAILED','REFUNDED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Order_Status",
                table: "Orders",
                sql: "\"Status\" IN ('PLACED','PAID','SHIPPED','DELIVERED','CANCELLED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmailNotification_Status",
                table: "EmailNotifications",
                sql: "\"Status\" IN ('PENDING','SENT','FAILED','PERMANENTLY_FAILED')");
        }
    }
}

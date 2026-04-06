using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notifications_retailer_accounts_retailer_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "idx_notifications_retailer_createdat",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "notifications",
                type: "varchar(50)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "notifications",
                type: "varchar(200)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "idx_notifications_retailer_createdat",
                table: "notifications",
                columns: new[] { "retailer_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_notifications_retailer_unread",
                table: "notifications",
                columns: new[] { "retailer_id", "is_read" },
                filter: "is_read = false");

            migrationBuilder.AddCheckConstraint(
                name: "chk_notifications_type",
                table: "notifications",
                sql: "type IN ('LowStock','OrderStatusChanged','SubscriptionExpiring','PaymentFailed','AccountDeletion')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_notifications_retailer_createdat",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "idx_notifications_retailer_unread",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "chk_notifications_type",
                table: "notifications");

            migrationBuilder.AlterColumn<string>(
                name: "type",
                table: "notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateIndex(
                name: "idx_notifications_retailer_createdat",
                table: "notifications",
                columns: new[] { "retailer_id", "created_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('LowStock','OrderStatusChanged','SubscriptionExpiring','PaymentFailed','AccountDeletion')");

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_retailer_accounts_retailer_id",
                table: "notifications",
                column: "retailer_id",
                principalTable: "retailer_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

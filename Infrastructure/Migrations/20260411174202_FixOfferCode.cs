using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixOfferCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "amount",
                table: "commission_records");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "commission_records",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<decimal>(
                name: "commission_rate",
                table: "commission_records",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "commission_records",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<decimal>(
                name: "commission_amount",
                table: "commission_records",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "commission_records",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "delivered_at",
                table: "commission_records",
                type: "timestamptz",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "order_total",
                table: "commission_records",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "subscription_plan_id",
                table: "commission_records",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "idx_commission_records_order_id",
                table: "commission_records",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_commission_records_retailer_delivered_at",
                table: "commission_records",
                columns: new[] { "retailer_id", "delivered_at" });

            migrationBuilder.CreateIndex(
                name: "idx_commission_records_retailer_id",
                table: "commission_records",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "ix_commission_records_subscription_plan_id",
                table: "commission_records",
                column: "subscription_plan_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_commission_records_amounts_positive",
                table: "commission_records",
                sql: "order_total >= 0 AND commission_amount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_commission_records_rate",
                table: "commission_records",
                sql: "commission_rate >= 0 AND commission_rate <= 1");

            migrationBuilder.AddForeignKey(
                name: "fk_commission_records_orders_order_id",
                table: "commission_records",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_commission_records_retailer_accounts_retailer_id",
                table: "commission_records",
                column: "retailer_id",
                principalTable: "retailer_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_commission_records_subscription_plans_subscription_plan_id",
                table: "commission_records",
                column: "subscription_plan_id",
                principalTable: "subscription_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_commission_records_orders_order_id",
                table: "commission_records");

            migrationBuilder.DropForeignKey(
                name: "fk_commission_records_retailer_accounts_retailer_id",
                table: "commission_records");

            migrationBuilder.DropForeignKey(
                name: "fk_commission_records_subscription_plans_subscription_plan_id",
                table: "commission_records");

            migrationBuilder.DropIndex(
                name: "idx_commission_records_order_id",
                table: "commission_records");

            migrationBuilder.DropIndex(
                name: "idx_commission_records_retailer_delivered_at",
                table: "commission_records");

            migrationBuilder.DropIndex(
                name: "idx_commission_records_retailer_id",
                table: "commission_records");

            migrationBuilder.DropIndex(
                name: "ix_commission_records_subscription_plan_id",
                table: "commission_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_commission_records_amounts_positive",
                table: "commission_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_commission_records_rate",
                table: "commission_records");

            migrationBuilder.DropColumn(
                name: "commission_amount",
                table: "commission_records");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "commission_records");

            migrationBuilder.DropColumn(
                name: "delivered_at",
                table: "commission_records");

            migrationBuilder.DropColumn(
                name: "order_total",
                table: "commission_records");

            migrationBuilder.DropColumn(
                name: "subscription_plan_id",
                table: "commission_records");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "offers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "offers",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "commission_records",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamptz",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<decimal>(
                name: "commission_rate",
                table: "commission_records",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,4)",
                oldPrecision: 5,
                oldScale: 4);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "commission_records",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<decimal>(
                name: "amount",
                table: "commission_records",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}

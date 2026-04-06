using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notification_preferences_retailer_accounts_retailer_id",
                table: "notification_preferences");

            migrationBuilder.RenameIndex(
                name: "ux_notification_preferences_retailer_id",
                table: "notification_preferences",
                newName: "uq_notification_preferences_retailer_id");

            migrationBuilder.AddColumn<string>(
                name: "avatar_url",
                table: "retailer_accounts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "retailer_accounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "notification_preferences",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddForeignKey(
                name: "fk_notification_preferences_retailer_accounts",
                table: "notification_preferences",
                column: "retailer_id",
                principalTable: "retailer_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notification_preferences_retailer_accounts",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "avatar_url",
                table: "retailer_accounts");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "retailer_accounts");

            migrationBuilder.RenameIndex(
                name: "uq_notification_preferences_retailer_id",
                table: "notification_preferences",
                newName: "ux_notification_preferences_retailer_id");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "notification_preferences",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "fk_notification_preferences_retailer_accounts_retailer_id",
                table: "notification_preferences",
                column: "retailer_id",
                principalTable: "retailer_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

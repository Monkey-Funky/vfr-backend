using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial_RetailerAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "retailer_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    brand_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    business_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    has3d_models = table.Column<bool>(type: "boolean", nullable: false),
                    brand_logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    google_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    is_email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    refresh_token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_remember_me_session = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    account_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false),
                    lockout_end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retailer_accounts", x => x.id);
                    table.CheckConstraint("chk_retailer_accounts_status", "account_status IN ('Active','PendingEmailVerification','Suspended','PendingDeletion')");
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    low_stock_alerts = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    order_status_alerts = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    subscription_alerts = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    email_notifications = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    in_app_notifications = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_preferences_retailer_accounts_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_notification_preferences_retailer_id",
                table: "notification_preferences",
                column: "retailer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_retailer_accounts_google_id",
                table: "retailer_accounts",
                column: "google_id",
                filter: "google_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_retailer_accounts_brand_name_active",
                table: "retailer_accounts",
                column: "brand_name",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_retailer_accounts_email_active",
                table: "retailer_accounts",
                column: "email",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "retailer_accounts");
        }
    }
}

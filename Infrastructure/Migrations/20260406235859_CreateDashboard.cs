using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_data = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dashboard_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_revenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    total_profit = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    total_orders = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    active_products = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    low_stock_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    conversion_rate = table.Column<decimal>(type: "numeric(5,4)", nullable: false, defaultValue: 0m),
                    try_on_engagement = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    computed_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dashboard_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fit_accuracies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    predicted_size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actual_size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    was_accurate = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fit_accuracies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    range_from = table.Column<DateOnly>(type: "date", nullable: false),
                    range_to = table.Column<DateOnly>(type: "date", nullable: false),
                    report_url = table.Column<string>(type: "text", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                    table.CheckConstraint("ck_reports_status", "status IN ('Pending','Processing','Ready','Failed')");
                });

            migrationBuilder.CreateTable(
                name: "return_reasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    returned_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_return_reasons", x => x.id);
                    table.CheckConstraint("ck_return_reasons_reason", "reason IN ('WrongSize','DefectivItem','NotAsDescribed','ChangedMind','LateDelivery','DamagedInShipping','Other')");
                });

            migrationBuilder.CreateTable(
                name: "try_on_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_duration_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    resulted_in_purchase = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_try_on_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vfr_engagement_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_try_ons = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    unique_customers = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    avg_session_seconds = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    conversion_rate = table.Column<decimal>(type: "numeric(5,4)", nullable: false, defaultValue: 0m),
                    top_product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vfr_engagement_metrics", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_activity_events_retailer_createdat",
                table: "activity_events",
                columns: new[] { "retailer_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_activity_events_retailer_id",
                table: "activity_events",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "idx_dashboard_snapshots_retailer_id",
                table: "dashboard_snapshots",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "uidx_dashboard_snapshots_retailer_date",
                table: "dashboard_snapshots",
                columns: new[] { "retailer_id", "snapshot_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_fit_accuracies_retailer_id",
                table: "fit_accuracies",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "idx_fit_accuracies_retailer_recordedat",
                table: "fit_accuracies",
                columns: new[] { "retailer_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "idx_reports_retailer_id",
                table: "reports",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "idx_reports_retailer_status",
                table: "reports",
                columns: new[] { "retailer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_return_reasons_product_id",
                table: "return_reasons",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_return_reasons_retailer_id",
                table: "return_reasons",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "idx_return_reasons_retailer_returnedat",
                table: "return_reasons",
                columns: new[] { "retailer_id", "returned_at" });

            migrationBuilder.CreateIndex(
                name: "idx_try_on_sessions_product_id",
                table: "try_on_sessions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_try_on_sessions_retailer_id",
                table: "try_on_sessions",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "idx_vfr_engagement_metrics_retailer_id",
                table: "vfr_engagement_metrics",
                column: "retailer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_events");

            migrationBuilder.DropTable(
                name: "dashboard_snapshots");

            migrationBuilder.DropTable(
                name: "fit_accuracies");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "return_reasons");

            migrationBuilder.DropTable(
                name: "try_on_sessions");

            migrationBuilder.DropTable(
                name: "vfr_engagement_metrics");
        }
    }
}

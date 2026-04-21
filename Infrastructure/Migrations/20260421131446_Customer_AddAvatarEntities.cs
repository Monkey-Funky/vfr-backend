using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Customer_AddAvatarEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_products_status",
                table: "products");

            //migrationBuilder.DropIndex(
            //    name: "idx_notifications_retailer_id",
            //    table: "notifications");

            //migrationBuilder.DropIndex(
            //    name: "idx_notifications_retailer_unread",
            //    table: "notifications");

            //migrationBuilder.DropCheckConstraint(
            //    name: "chk_notifications_type",
            //    table: "notifications");

            //migrationBuilder.DropColumn(
            //    name: "created_by",
            //    table: "offers");

            //migrationBuilder.DropColumn(
            //    name: "updated_by",
            //    table: "offers");

            //migrationBuilder.DropColumn(
            //    name: "amount",
            //    table: "commission_records");

            //migrationBuilder.RenameIndex(
            //    name: "ix_products_sub_category_id",
            //    table: "products",
            //    newName: "idx_products_sub_category_id");

            //migrationBuilder.RenameIndex(
            //    name: "idx_notifications_retailer_createdat",
            //    table: "notifications",
            //    newName: "idx_notifications_retailer_created_at");

            migrationBuilder.AlterColumn<string>(
                name: "phone_number",
                table: "retailer_accounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "avatar_url",
                table: "retailer_accounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "available_balance",
                table: "retailer_accounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric");

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

            //migrationBuilder.AddColumn<decimal>(
            //    name: "commission_amount",
            //    table: "commission_records",
            //    type: "numeric(18,2)",
            //    precision: 18,
            //    scale: 2,
            //    nullable: false,
            //    defaultValue: 0m);

            //migrationBuilder.AddColumn<string>(
            //    name: "currency",
            //    table: "commission_records",
            //    type: "character varying(10)",
            //    maxLength: 10,
            //    nullable: false,
            //    defaultValue: "");

            //migrationBuilder.AddColumn<DateTime>(
            //    name: "delivered_at",
            //    table: "commission_records",
            //    type: "timestamptz",
            //    nullable: false,
            //    defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            //migrationBuilder.AddColumn<decimal>(
            //    name: "order_total",
            //    table: "commission_records",
            //    type: "numeric(18,2)",
            //    precision: 18,
            //    scale: 2,
            //    nullable: false,
            //    defaultValue: 0m);

            //migrationBuilder.AddColumn<Guid>(
            //    name: "subscription_plan_id",
            //    table: "commission_records",
            //    type: "uuid",
            //    nullable: false,
            //    defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            //migrationBuilder.AddColumn<NpgsqlTsVector>(
            //    name: "search_vector",
            //    table: "products",
            //    type: "tsvector",
            //    nullable: true,
            //    computedColumnSql: "to_tsvector('english', coalesce(name, '') || ' ' || coalesce(description, '') || ' ' || coalesce(barcode, ''))",
            //    stored: true);

            //migrationBuilder.CreateTable(
            //    name: "activity_events",
            //    columns: table => new
            //    {
            //        id = table.Column<Guid>(type: "uuid", nullable: false),
            //        retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
            //        event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
            //        resource_id = table.Column<Guid>(type: "uuid", nullable: true),
            //        event_data = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
            //        created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("pk_activity_events", x => x.id);
            //    });

            migrationBuilder.CreateTable(
                name: "avatars",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    height_cm = table.Column<decimal>(type: "numeric(5,1)", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(5,1)", nullable: false),
                    chest_cm = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                    waist_cm = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                    hips_cm = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                    shoulder_width_cm = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                    inseam_cm = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                    neck_cm = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                    arm_length_cm = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                    shoe_size_eu = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    body_shape = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    avatar_3d_model_url = table.Column<string>(type: "text", nullable: true),
                    last_measured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avatars", x => x.id);
                    table.CheckConstraint("ck_avatars_body_shape", "body_shape IN ('Rectangle', 'Triangle', 'InvertedTriangle', 'Hourglass', 'Apple', 'Pear')");
                    table.ForeignKey(
                        name: "fk_avatars_customer_accounts_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            //migrationBuilder.CreateTable(
            //    name: "dashboard_snapshots",
            //    columns: table => new
            //    {
            //        id = table.Column<Guid>(type: "uuid", nullable: false),
            //        retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
            //        snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
            //        total_revenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
            //        total_profit = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
            //        total_orders = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
            //        active_products = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
            //        low_stock_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
            //        conversion_rate = table.Column<decimal>(type: "numeric(5,4)", nullable: false, defaultValue: 0m),
            //        try_on_engagement = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
            //        computed_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("pk_dashboard_snapshots", x => x.id);
            //    });

            //migrationBuilder.CreateTable(
            //    name: "fit_accuracies",
            //    columns: table => new
            //    {
            //        id = table.Column<Guid>(type: "uuid", nullable: false),
            //        retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
            //        product_id = table.Column<Guid>(type: "uuid", nullable: true),
            //        predicted_size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            //        actual_size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            //        was_accurate = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
            //        session_id = table.Column<Guid>(type: "uuid", nullable: true),
            //        recorded_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("pk_fit_accuracies", x => x.id);
            //    });

            //migrationBuilder.CreateTable(
            //    name: "reports",
            //    columns: table => new
            //    {
            //        id = table.Column<Guid>(type: "uuid", nullable: false),
            //        retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
            //        status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
            //        range_from = table.Column<DateOnly>(type: "date", nullable: false),
            //        range_to = table.Column<DateOnly>(type: "date", nullable: false),
            //        report_url = table.Column<string>(type: "text", nullable: true),
            //        failure_reason = table.Column<string>(type: "text", nullable: true),
            //        created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
            //        completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("pk_reports", x => x.id);
            //        table.CheckConstraint("ck_reports_status", "status IN ('Pending','Processing','Ready','Failed')");
            //    });

            //migrationBuilder.CreateTable(
            //    name: "return_reasons",
            //    columns: table => new
            //    {
            //        id = table.Column<Guid>(type: "uuid", nullable: false),
            //        retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
            //        order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
            //        product_id = table.Column<Guid>(type: "uuid", nullable: true),
            //        reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
            //        returned_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("pk_return_reasons", x => x.id);
            //        table.CheckConstraint("ck_return_reasons_reason", "reason IN ('WrongSize','DefectivItem','NotAsDescribed','ChangedMind','LateDelivery','DamagedInShipping','Other')");
            //    });

            //migrationBuilder.CreateTable(
            //    name: "try_on_sessions",
            //    columns: table => new
            //    {
            //        id = table.Column<Guid>(type: "uuid", nullable: false),
            //        retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
            //        product_id = table.Column<Guid>(type: "uuid", nullable: true),
            //        customer_id = table.Column<Guid>(type: "uuid", nullable: true),
            //        session_duration_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
            //        resulted_in_purchase = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
            //        created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("pk_try_on_sessions", x => x.id);
            //    });

            //migrationBuilder.CreateTable(
            //    name: "vfr_engagement_metrics",
            //    columns: table => new
            //    {
            //        id = table.Column<Guid>(type: "uuid", nullable: false),
            //        retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
            //        metric_date = table.Column<DateOnly>(type: "date", nullable: false),
            //        total_try_ons = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
            //        unique_customers = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
            //        avg_session_seconds = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
            //        conversion_rate = table.Column<decimal>(type: "numeric(5,4)", nullable: false, defaultValue: 0m),
            //        top_product_id = table.Column<Guid>(type: "uuid", nullable: true),
            //        recorded_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("pk_vfr_engagement_metrics", x => x.id);
            //    });

            migrationBuilder.CreateTable(
                name: "avatar_measurement_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    avatar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    measurement_data = table.Column<string>(type: "jsonb", nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avatar_measurement_history", x => x.id);
                    table.CheckConstraint("ck_avatar_measurement_history_source", "source IN ('Manual', 'BodyScan', 'AIEstimate')");
                    table.ForeignKey(
                        name: "fk_avatar_measurement_history_avatars_avatar_id",
                        column: x => x.avatar_id,
                        principalTable: "avatars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            //migrationBuilder.CreateIndex(
            //    name: "idx_products_retailer_created_at",
            //    table: "products",
            //    columns: new[] { "retailer_id", "created_at" });

            //migrationBuilder.CreateIndex(
            //    name: "idx_products_search_vector",
            //    table: "products",
            //    column: "search_vector")
            //    .Annotation("Npgsql:IndexMethod", "gin");

            //migrationBuilder.CreateIndex(
            //    name: "uidx_products_retailer_barcode",
            //    table: "products",
            //    columns: new[] { "retailer_id", "barcode" },
            //    unique: true,
            //    filter: "is_deleted = false AND barcode IS NOT NULL");

            //migrationBuilder.AddCheckConstraint(
            //    name: "ck_products_status",
            //    table: "products",
            //    sql: "status IN ('Active', 'Inactive', 'Draft', 'OutOfStock')");

            //migrationBuilder.CreateIndex(
            //    name: "idx_notifications_retailer_is_read",
            //    table: "notifications",
            //    columns: new[] { "retailer_id", "is_read" });

            //migrationBuilder.AddCheckConstraint(
            //    name: "ck_notifications_type",
            //    table: "notifications",
            //    sql: "type IN ('LowStock','NewOrder','OrderStatusChanged','SubscriptionExpiring','PaymentFailed','SystemAlert')");

            //migrationBuilder.CreateIndex(
            //    name: "idx_commission_records_order_id",
            //    table: "commission_records",
            //    column: "order_id",
            //    unique: true);

            //migrationBuilder.CreateIndex(
            //    name: "idx_commission_records_retailer_delivered_at",
            //    table: "commission_records",
            //    columns: new[] { "retailer_id", "delivered_at" });

            //migrationBuilder.CreateIndex(
            //    name: "idx_commission_records_retailer_id",
            //    table: "commission_records",
            //    column: "retailer_id");

            //migrationBuilder.CreateIndex(
            //    name: "ix_commission_records_subscription_plan_id",
            //    table: "commission_records",
            //    column: "subscription_plan_id");

            //migrationBuilder.AddCheckConstraint(
            //    name: "ck_commission_records_amounts_positive",
            //    table: "commission_records",
            //    sql: "order_total >= 0 AND commission_amount >= 0");

            //migrationBuilder.AddCheckConstraint(
            //    name: "ck_commission_records_rate",
            //    table: "commission_records",
            //    sql: "commission_rate >= 0 AND commission_rate <= 1");

            //migrationBuilder.CreateIndex(
            //    name: "idx_activity_events_retailer_createdat",
            //    table: "activity_events",
            //    columns: new[] { "retailer_id", "created_at" },
            //    descending: new[] { false, true });

            //migrationBuilder.CreateIndex(
            //    name: "idx_activity_events_retailer_id",
            //    table: "activity_events",
            //    column: "retailer_id");

            //migrationBuilder.CreateIndex(
            //    name: "idx_avatar_measurement_history_avatar_recorded",
            //    table: "avatar_measurement_history",
            //    columns: new[] { "avatar_id", "recorded_at" },
            //    descending: new[] { false, true });

            //migrationBuilder.CreateIndex(
            //    name: "uq_avatars_customer_id",
            //    table: "avatars",
            //    column: "customer_id",
            //    unique: true,
            //    filter: "is_deleted = false");

            //migrationBuilder.CreateIndex(
            //    name: "idx_dashboard_snapshots_retailer_id",
            //    table: "dashboard_snapshots",
            //    column: "retailer_id");

            //migrationBuilder.CreateIndex(
            //    name: "uidx_dashboard_snapshots_retailer_date",
            //    table: "dashboard_snapshots",
            //    columns: new[] { "retailer_id", "snapshot_date" },
            //    unique: true);

            //migrationBuilder.CreateIndex(
            //    name: "idx_fit_accuracies_retailer_id",
            //    table: "fit_accuracies",
            //    column: "retailer_id");

            //migrationBuilder.CreateIndex(
            //    name: "idx_fit_accuracies_retailer_recordedat",
            //    table: "fit_accuracies",
            //    columns: new[] { "retailer_id", "recorded_at" });

            //migrationBuilder.CreateIndex(
            //    name: "idx_reports_retailer_id",
            //    table: "reports",
            //    column: "retailer_id");

            //migrationBuilder.CreateIndex(
            //    name: "idx_reports_retailer_status",
            //    table: "reports",
            //    columns: new[] { "retailer_id", "status" });

            //migrationBuilder.CreateIndex(
            //    name: "idx_return_reasons_product_id",
            //    table: "return_reasons",
            //    column: "product_id");

            //migrationBuilder.CreateIndex(
            //    name: "idx_return_reasons_retailer_id",
            //    table: "return_reasons",
            //    column: "retailer_id");

            //migrationBuilder.CreateIndex(
            //    name: "idx_return_reasons_retailer_returnedat",
            //    table: "return_reasons",
            //    columns: new[] { "retailer_id", "returned_at" });

            //migrationBuilder.CreateIndex(
            //    name: "idx_try_on_sessions_product_id",
            //    table: "try_on_sessions",
            //    column: "product_id");

            //migrationBuilder.CreateIndex(
            //    name: "idx_try_on_sessions_retailer_id",
            //    table: "try_on_sessions",
            //    column: "retailer_id");

            //migrationBuilder.CreateIndex(
            //    name: "idx_vfr_engagement_metrics_retailer_id",
            //    table: "vfr_engagement_metrics",
            //    column: "retailer_id");

            //migrationBuilder.AddForeignKey(
            //    name: "fk_commission_records_orders_order_id",
            //    table: "commission_records",
            //    column: "order_id",
            //    principalTable: "orders",
            //    principalColumn: "id",
            //    onDelete: ReferentialAction.Restrict);

            //migrationBuilder.AddForeignKey(
            //    name: "fk_commission_records_retailer_accounts_retailer_id",
            //    table: "commission_records",
            //    column: "retailer_id",
            //    principalTable: "retailer_accounts",
            //    principalColumn: "id",
            //    onDelete: ReferentialAction.Restrict);

            //migrationBuilder.AddForeignKey(
            //    name: "fk_commission_records_subscription_plans_subscription_plan_id",
            //    table: "commission_records",
            //    column: "subscription_plan_id",
            //    principalTable: "subscription_plans",
            //    principalColumn: "id",
            //    onDelete: ReferentialAction.Restrict);

            //migrationBuilder.AddForeignKey(
            //    name: "fk_notifications_retailer_accounts_retailer_id",
            //    table: "notifications",
            //    column: "retailer_id",
            //    principalTable: "retailer_accounts",
            //    principalColumn: "id",
            //    onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.DropForeignKey(
                name: "fk_notifications_retailer_accounts_retailer_id",
                table: "notifications");

            migrationBuilder.DropTable(
                name: "activity_events");

            migrationBuilder.DropTable(
                name: "avatar_measurement_history");

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

            migrationBuilder.DropTable(
                name: "avatars");

            migrationBuilder.DropIndex(
                name: "idx_products_retailer_created_at",
                table: "products");

            migrationBuilder.DropIndex(
                name: "idx_products_search_vector",
                table: "products");

            migrationBuilder.DropIndex(
                name: "uidx_products_retailer_barcode",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_status",
                table: "products");

            migrationBuilder.DropIndex(
                name: "idx_notifications_retailer_is_read",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

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
                name: "search_vector",
                table: "products");

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

            migrationBuilder.RenameIndex(
                name: "idx_products_sub_category_id",
                table: "products",
                newName: "ix_products_sub_category_id");

            migrationBuilder.RenameIndex(
                name: "idx_notifications_retailer_created_at",
                table: "notifications",
                newName: "idx_notifications_retailer_createdat");

            migrationBuilder.AlterColumn<string>(
                name: "phone_number",
                table: "retailer_accounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "avatar_url",
                table: "retailer_accounts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "available_balance",
                table: "retailer_accounts",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldDefaultValue: 0m);

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

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_status",
                table: "products",
                sql: "status IN ('Active', 'Inactive', 'Draft')");

            migrationBuilder.CreateIndex(
                name: "idx_notifications_retailer_id",
                table: "notifications",
                column: "retailer_id");

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
    }
}

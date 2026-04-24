using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinalMigration : Migration
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
                name: "customer_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    google_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    refresh_token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Active"),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    lockout_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    remember_me = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_accounts", x => x.id);
                    table.CheckConstraint("ck_customer_accounts_gender", "gender IN ('Male','Female','Other','PreferNotToSay')");
                    table.CheckConstraint("ck_customer_accounts_status", "status IN ('Active','PendingEmailVerification','Suspended','PendingDeletion')");
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
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    order_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "EGP"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "NotProcessed"),
                    row_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                    table.CheckConstraint("ck_orders_status", "status IN ('NotProcessed','Processing','Shipped','Delivered','Cancelled')");
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
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    available_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
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
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    billing_cycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                    commission_rate = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    max_active_products = table.Column<int>(type: "integer", nullable: true),
                    max_monthly_try_ons = table.Column<int>(type: "integer", nullable: true),
                    support_level = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_white_label = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    includes_source_code = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    includes_mobile_apps = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    has_sla = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    has_dedicated_team = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_plans", x => x.id);
                    table.CheckConstraint("chk_subscription_plans_billing_cycle", "billing_cycle IN ('Monthly','Yearly','SaaS')");
                    table.CheckConstraint("chk_subscription_plans_commission_rate", "commission_rate >= 0 AND commission_rate <= 1");
                    table.CheckConstraint("chk_subscription_plans_price_amount", "price_amount >= 0");
                    table.CheckConstraint("chk_subscription_plans_tier", "tier IN ('Basic','Standard','Enterprise','SaaS')");
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

            migrationBuilder.CreateTable(
                name: "customer_addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state_province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_addresses", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_addresses_customer_accounts_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "varchar(150)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    cover_image_url = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by = table.Column<string>(type: "varchar(200)", nullable: true),
                    updated_by = table.Column<string>(type: "varchar(200)", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.CheckConstraint("ck_categories_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_categories_retailer_accounts_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_preferences_retailer_accounts",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_type", "type IN ('LowStock','NewOrder','OrderStatusChanged','SubscriptionExpiring','PaymentFailed','SystemAlert')");
                    table.ForeignKey(
                        name: "fk_notifications_retailer_accounts_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_methods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cardholder_name_encrypted = table.Column<string>(type: "text", nullable: false),
                    card_number_last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    expiry_date = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    stripe_payment_method_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_saved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_methods", x => x.id);
                    table.CheckConstraint("chk_payment_methods_provider_type", "provider_type IN ('Visa', 'Mastercard', 'PayPal', 'ApplePay', 'Stripe', 'GooglePay', 'Bitpay')");
                    table.ForeignKey(
                        name: "fk_payment_methods_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saas_enquiries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Pending'"),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saas_enquiries", x => x.id);
                    table.CheckConstraint("chk_saas_enquiries_status", "status IN ('Pending','InProgress','Closed')");
                    table.ForeignKey(
                        name: "fk_saas_enquiries_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "commission_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commission_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    order_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    commission_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    delivered_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commission_records", x => x.id);
                    table.CheckConstraint("ck_commission_records_amounts_positive", "order_total >= 0 AND commission_amount >= 0");
                    table.CheckConstraint("ck_commission_records_rate", "commission_rate >= 0 AND commission_rate <= 1");
                    table.ForeignKey(
                        name: "fk_commission_records_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_commission_records_retailer_accounts_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_commission_records_subscription_plans_subscription_plan_id",
                        column: x => x.subscription_plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValueSql: "'None'"),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    trial_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pending_downgrade_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pending_downgrade_eff_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_recurring_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                    table.CheckConstraint("chk_subscriptions_status", "status IN ('None','Trial','Active','PendingDowngrade','Expired','Cancelled')");
                    table.ForeignKey(
                        name: "fk_subscriptions_pending_downgrade_plan_id",
                        column: x => x.pending_downgrade_plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscriptions_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscriptions_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sub_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "varchar(150)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sub_categories", x => x.id);
                    table.CheckConstraint("ck_sub_categories_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_sub_categories_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sub_categories_retailer_accounts_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscription_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValueSql: "'Pending'"),
                    is_recurring = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    period_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    stripe_payment_intent_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_payments", x => x.id);
                    table.CheckConstraint("chk_subscription_payments_amount", "amount >= 0");
                    table.CheckConstraint("chk_subscription_payments_status", "status IN ('Pending','Processing','Completed','Failed','Refunded')");
                    table.ForeignKey(
                        name: "fk_subscription_payments_payment_method_id",
                        column: x => x.payment_method_id,
                        principalTable: "payment_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_subscription_payments_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_subscription_payments_subscription_plan_id",
                        column: x => x.subscription_plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sub_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "EGP"),
                    barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('english', coalesce(name, '') || ' ' || coalesce(description, '') || ' ' || coalesce(barcode, ''))", stored: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.CheckConstraint("ck_products_status", "status IN ('Active', 'Inactive', 'Draft', 'OutOfStock')");
                    table.ForeignKey(
                        name: "fk_products_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_products_retailer_accounts_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_products_sub_categories_sub_category_id",
                        column: x => x.sub_category_id,
                        principalTable: "sub_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "inventory_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    current_stock = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    sold_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    low_stock_threshold = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    row_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "InStock"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_records", x => x.id);
                    table.CheckConstraint("ck_inventory_records_current_stock_non_negative", "current_stock >= 0");
                    table.CheckConstraint("ck_inventory_records_status", "status IN ('InStock','LowStock','OutOfStock')");
                    table.ForeignKey(
                        name: "fk_inventory_records_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventory_records_retailer_accounts_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "offers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    offer_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    discount_type = table.Column<string>(type: "varchar(20)", nullable: false),
                    discount_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    cover_image_url = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offers", x => x.id);
                    table.CheckConstraint("ck_offers_discount_type", "discount_type IN ('Percentage', 'Fixed')");
                    table.CheckConstraint("ck_offers_offer_type", "offer_type IN ('Product', 'Category')");
                    table.CheckConstraint("ck_offers_status", "status IN ('Active', 'Inactive', 'Expired')");
                    table.CheckConstraint("ck_offers_type_target_mutual_exclusivity", "(offer_type = 'Product'   AND product_id  IS NOT NULL AND category_id IS NULL) OR (offer_type = 'Category'  AND category_id IS NOT NULL AND product_id  IS NULL)");
                    table.ForeignKey(
                        name: "fk_offers_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_offers_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_offers_retailer_accounts_retailer_id",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_items", x => x.id);
                    table.CheckConstraint("ck_order_items_quantity_positive", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_images", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_images_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjustment_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    old_quantity = table.Column<int>(type: "integer", nullable: false),
                    new_quantity = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    adjusted_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjusted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_adjustments", x => x.id);
                    table.CheckConstraint("ck_stock_adjustments_type", "adjustment_type IN ('ManualIncrease','ManualDecrease','OrderSale','ReturnRestock')");
                    table.ForeignKey(
                        name: "fk_stock_adjustments_inventory_records_inventory_record_id",
                        column: x => x.inventory_record_id,
                        principalTable: "inventory_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_stock_adjustments_retailer_accounts_adjusted_by_id",
                        column: x => x.adjusted_by_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "ix_categories_retailer_id",
                table: "categories",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_retailer_id_name_active",
                table: "categories",
                columns: new[] { "retailer_id", "name" },
                unique: true,
                filter: "is_deleted = false");

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

            migrationBuilder.CreateIndex(
                name: "idx_customer_accounts_google_id",
                table: "customer_accounts",
                column: "google_id",
                filter: "google_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_customer_accounts_email",
                table: "customer_accounts",
                column: "email",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_customer_addresses_one_default",
                table: "customer_addresses",
                column: "customer_id",
                unique: true,
                filter: "is_default = true AND is_deleted = false");

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
                name: "idx_inventory_records_product_id",
                table: "inventory_records",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_inventory_records_retailer_id",
                table: "inventory_records",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "idx_inventory_records_retailer_sold_qty",
                table: "inventory_records",
                columns: new[] { "retailer_id", "sold_quantity" });

            migrationBuilder.CreateIndex(
                name: "uq_inventory_records_retailer_product",
                table: "inventory_records",
                columns: new[] { "retailer_id", "product_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_notification_preferences_retailer_id",
                table: "notification_preferences",
                column: "retailer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_notifications_retailer_created_at",
                table: "notifications",
                columns: new[] { "retailer_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_notifications_retailer_is_read",
                table: "notifications",
                columns: new[] { "retailer_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "idx_offers_category_id",
                table: "offers",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "idx_offers_end_date_active",
                table: "offers",
                column: "end_date",
                filter: "status = 'Active' AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_offers_product_id",
                table: "offers",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_offers_retailer_id",
                table: "offers",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "idx_offers_retailer_status",
                table: "offers",
                columns: new[] { "retailer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_order_items_product_id",
                table: "order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_orders_retailer_createdat",
                table: "orders",
                columns: new[] { "retailer_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_orders_retailer_id",
                table: "orders",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "idx_orders_retailer_status",
                table: "orders",
                columns: new[] { "retailer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_methods_expires_at",
                table: "payment_methods",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_payment_methods_retailer_default",
                table: "payment_methods",
                columns: new[] { "retailer_id", "is_default" },
                filter: "is_deleted = false AND is_default = true");

            migrationBuilder.CreateIndex(
                name: "ix_payment_methods_retailer_id",
                table: "payment_methods",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "idx_product_images_product_id",
                table: "product_images",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "idx_products_retailer_created_at",
                table: "products",
                columns: new[] { "retailer_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_products_retailer_id",
                table: "products",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "idx_products_retailer_status",
                table: "products",
                columns: new[] { "retailer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_products_search_vector",
                table: "products",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "idx_products_sub_category_id",
                table: "products",
                column: "sub_category_id");

            migrationBuilder.CreateIndex(
                name: "uidx_products_retailer_barcode",
                table: "products",
                columns: new[] { "retailer_id", "barcode" },
                unique: true,
                filter: "is_deleted = false AND barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uidx_products_retailer_name",
                table: "products",
                columns: new[] { "retailer_id", "name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_reports_retailer_id",
                table: "reports",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "idx_reports_retailer_status",
                table: "reports",
                columns: new[] { "retailer_id", "status" });

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
                name: "ix_saas_enquiries_retailer_id",
                table: "saas_enquiries",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "ix_saas_enquiries_status",
                table: "saas_enquiries",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_stock_adjustments_adjusted_by_id",
                table: "stock_adjustments",
                column: "adjusted_by_id");

            migrationBuilder.CreateIndex(
                name: "idx_stock_adjustments_inventory_record_id",
                table: "stock_adjustments",
                column: "inventory_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_sub_categories_category_id",
                table: "sub_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_sub_categories_category_id_name_active",
                table: "sub_categories",
                columns: new[] { "category_id", "name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_sub_categories_retailer_id",
                table: "sub_categories",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_payments_created_at",
                table: "subscription_payments",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_payments_payment_method_id",
                table: "subscription_payments",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_payments_retailer_id",
                table: "subscription_payments",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_payments_status",
                table: "subscription_payments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_payments_status_stripe_intent",
                table: "subscription_payments",
                columns: new[] { "status", "stripe_payment_intent_id" },
                filter: "status = 'Completed' AND stripe_payment_intent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_payments_subscription_plan_id",
                table: "subscription_payments",
                column: "subscription_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_plans_is_active",
                table: "subscription_plans",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_plans_tier_billing_cycle",
                table: "subscription_plans",
                columns: new[] { "tier", "billing_cycle" });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_end_date",
                table: "subscriptions",
                column: "end_date");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_pending_downgrade_plan_id",
                table: "subscriptions",
                column: "pending_downgrade_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_plan_id",
                table: "subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_retailer_id",
                table: "subscriptions",
                column: "retailer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_status",
                table: "subscriptions",
                column: "status");

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
                name: "commission_records");

            migrationBuilder.DropTable(
                name: "customer_addresses");

            migrationBuilder.DropTable(
                name: "dashboard_snapshots");

            migrationBuilder.DropTable(
                name: "fit_accuracies");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "offers");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "product_images");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "return_reasons");

            migrationBuilder.DropTable(
                name: "saas_enquiries");

            migrationBuilder.DropTable(
                name: "stock_adjustments");

            migrationBuilder.DropTable(
                name: "subscription_payments");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "try_on_sessions");

            migrationBuilder.DropTable(
                name: "vfr_engagement_metrics");

            migrationBuilder.DropTable(
                name: "customer_accounts");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "inventory_records");

            migrationBuilder.DropTable(
                name: "payment_methods");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "sub_categories");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "retailer_accounts");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionEntities : Migration
    {
        /// <inheritdoc />
        private static readonly Guid BasicMonthlyId = new("11111111-1111-1111-1111-111111111001");
        private static readonly Guid BasicYearlyId = new("11111111-1111-1111-1111-111111111002");
        private static readonly Guid StandardMonthlyId = new("22222222-2222-2222-2222-222222222001");
        private static readonly Guid StandardYearlyId = new("22222222-2222-2222-2222-222222222002");
        private static readonly Guid EnterpriseMthlyId = new("33333333-3333-3333-3333-333333333001");
        private static readonly Guid EnterpriseYearId = new("33333333-3333-3333-3333-333333333002");
        private static readonly Guid SaasPlanId = new("44444444-4444-4444-4444-444444444001");

        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "ix_saas_enquiries_retailer_id",
                table: "saas_enquiries",
                column: "retailer_id");

            migrationBuilder.CreateIndex(
                name: "ix_saas_enquiries_status",
                table: "saas_enquiries",
                column: "status");

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


            // ── Seed Data — 7 Subscription Plans ─────────────────────────────
            migrationBuilder.InsertData(
                table: "subscription_plans",
                columns: new[]
                {
                    "id", "name", "tier", "billing_cycle",
                    "price_amount", "currency", "commission_rate",
                    "max_active_products", "max_monthly_try_ons", "support_level",
                    "is_active", "is_white_label", "includes_source_code",
                    "includes_mobile_apps", "has_sla", "has_dedicated_team",
                    "created_at"
                },
                values: new object[,]
                {
                    // Basic Monthly — ID matches StartTrialCommandHandler.TrialPlanId
                    {
                        BasicMonthlyId, "Basic Monthly", "Basic", "Monthly",
                        50m, "USD", 0.0500m,
                        250, 500, "Email Support",
                        true, false, false, false, false, false,
                        DateTime.UtcNow
                    },
                    // Basic Yearly
                    {
                        BasicYearlyId, "Basic Yearly", "Basic", "Yearly",
                        500m, "USD", 0.0500m,
                        250, 500, "Email Support",
                        true, false, false, false, false, false,
                        DateTime.UtcNow
                    },
                    // Standard Monthly
                    {
                        StandardMonthlyId, "Standard Monthly", "Standard", "Monthly",
                        150m, "USD", 0.0300m,
                        1000, 2500, "Email & Chat Support",
                        true, false, false, false, false, false,
                        DateTime.UtcNow
                    },
                    // Standard Yearly
                    {
                        StandardYearlyId, "Standard Yearly", "Standard", "Yearly",
                        1500m, "USD", 0.0300m,
                        1000, 2500, "Email & Chat Support",
                        true, false, false, false, false, false,
                        DateTime.UtcNow
                    },
                    // Enterprise Monthly
                    {
                        EnterpriseMthlyId, "Enterprise Monthly", "Enterprise", "Monthly",
                        500m, "USD", 0.0150m,
                        null, null, "Priority Support + SLA",
                        true, false, false, false, true, false,
                        DateTime.UtcNow
                    },
                    // Enterprise Yearly
                    {
                        EnterpriseYearId, "Enterprise Yearly", "Enterprise", "Yearly",
                        5000m, "USD", 0.0150m,
                        null, null, "Priority Support + SLA",
                        true, false, false, false, true, false,
                        DateTime.UtcNow
                    },
                    // SaaS / White-Label
                    {
                        SaasPlanId, "SaaS White-Label", "SaaS", "SaaS",
                        0m, "USD", 0.0000m,
                        null, null, "Dedicated Team + Custom SLA",
                        true, true, true, true, true, true,
                        DateTime.UtcNow
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saas_enquiries");

            migrationBuilder.DropTable(
                name: "subscription_payments");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "payment_methods");

            migrationBuilder.DropTable(
                name: "subscription_plans");
        }
    }
}

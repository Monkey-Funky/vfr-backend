using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCOrderTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_order_items");

            migrationBuilder.DropTable(
                name: "customer_orders");

            migrationBuilder.CreateTable(
                name: "c_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "text", nullable: false),
                    current_status = table.Column<string>(type: "text", nullable: false),
                    order_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    order_description = table.Column<string>(type: "text", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    discount = table.Column<decimal>(type: "numeric", nullable: false),
                    grand_total = table.Column<decimal>(type: "numeric", nullable: false),
                    shipping_method = table.Column<string>(type: "text", nullable: false),
                    method_cost = table.Column<decimal>(type: "numeric", nullable: false),
                    delivery_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_c_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "c_order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    c_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    total_unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_c_order_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_c_order_items_c_orders_c_order_id",
                        column: x => x.c_order_id,
                        principalTable: "c_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_c_order_items_c_order_id",
                table: "c_order_items",
                column: "c_order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "c_order_items");

            migrationBuilder.DropTable(
                name: "c_orders");

            migrationBuilder.CreateTable(
                name: "customer_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    current_status = table.Column<string>(type: "text", nullable: false),
                    customer_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    discount = table.Column<decimal>(type: "numeric", nullable: false),
                    grand_total = table.Column<decimal>(type: "numeric", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    method_cost = table.Column<decimal>(type: "numeric", nullable: false),
                    order_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    order_description = table.Column<string>(type: "text", nullable: true),
                    order_number = table.Column<string>(type: "text", nullable: false),
                    shipping_method = table.Column<string>(type: "text", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customer_order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    total_unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_order_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_order_items_customer_orders_customer_order_id",
                        column: x => x.customer_order_id,
                        principalTable: "customer_orders",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_order_items_customer_order_id",
                table: "customer_order_items",
                column: "customer_order_id");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_inventory_records_low_stock",
                table: "inventory_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_records_status",
                table: "inventory_records");

            migrationBuilder.RenameIndex(
                name: "uidx_inventory_records_retailer_product",
                table: "inventory_records",
                newName: "uq_inventory_records_retailer_product");

            migrationBuilder.CreateIndex(
                name: "idx_inventory_records_retailer_sold_qty",
                table: "inventory_records",
                columns: new[] { "retailer_id", "sold_quantity" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_records_status",
                table: "inventory_records",
                sql: "status IN ('InStock','LowStock','OutOfStock')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_inventory_records_retailer_sold_qty",
                table: "inventory_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_records_status",
                table: "inventory_records");

            migrationBuilder.RenameIndex(
                name: "uq_inventory_records_retailer_product",
                table: "inventory_records",
                newName: "uidx_inventory_records_retailer_product");

            migrationBuilder.CreateIndex(
                name: "idx_inventory_records_low_stock",
                table: "inventory_records",
                columns: new[] { "retailer_id", "status" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_records_status",
                table: "inventory_records",
                sql: "status IN ('InStock', 'LowStock', 'OutOfStock')");
        }
    }
}

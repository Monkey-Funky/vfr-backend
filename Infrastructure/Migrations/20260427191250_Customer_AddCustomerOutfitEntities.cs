using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Customer_AddCustomerOutfitEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "available_colors",
                table: "products",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "available_sizes",
                table: "products",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "brand",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closure",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "features",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "length",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lining",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "material",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "neckline",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "occasion",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pattern",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sleeves",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "views_count",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "wash_instructions",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_outfits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    style = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_outfits", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_outfits_customer_accounts",
                        column: x => x.customer_id,
                        principalTable: "customer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_outfit_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    outfit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_outfit_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_outfit_items_customer_outfits_outfit_id",
                        column: x => x.outfit_id,
                        principalTable: "customer_outfits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_customer_outfit_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_outfit_items_outfit_id_slot_product_id",
                table: "customer_outfit_items",
                columns: new[] { "outfit_id", "slot", "product_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_customer_outfit_items_product_id",
                table: "customer_outfit_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_outfits_customer_id",
                table: "customer_outfits",
                column: "customer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_outfit_items");

            migrationBuilder.DropTable(
                name: "customer_outfits");

            migrationBuilder.DropColumn(
                name: "available_colors",
                table: "products");

            migrationBuilder.DropColumn(
                name: "available_sizes",
                table: "products");

            migrationBuilder.DropColumn(
                name: "brand",
                table: "products");

            migrationBuilder.DropColumn(
                name: "closure",
                table: "products");

            migrationBuilder.DropColumn(
                name: "features",
                table: "products");

            migrationBuilder.DropColumn(
                name: "length",
                table: "products");

            migrationBuilder.DropColumn(
                name: "lining",
                table: "products");

            migrationBuilder.DropColumn(
                name: "material",
                table: "products");

            migrationBuilder.DropColumn(
                name: "neckline",
                table: "products");

            migrationBuilder.DropColumn(
                name: "occasion",
                table: "products");

            migrationBuilder.DropColumn(
                name: "pattern",
                table: "products");

            migrationBuilder.DropColumn(
                name: "sleeves",
                table: "products");

            migrationBuilder.DropColumn(
                name: "views_count",
                table: "products");

            migrationBuilder.DropColumn(
                name: "wash_instructions",
                table: "products");
        }
    }
}

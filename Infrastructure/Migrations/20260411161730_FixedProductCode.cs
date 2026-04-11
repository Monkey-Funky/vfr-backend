using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixedProductCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_products_status",
                table: "products");

            migrationBuilder.RenameIndex(
                name: "ix_products_sub_category_id",
                table: "products",
                newName: "idx_products_sub_category_id");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                table: "products",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('english', coalesce(name, '') || ' ' || coalesce(description, '') || ' ' || coalesce(barcode, ''))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "idx_products_retailer_created_at",
                table: "products",
                columns: new[] { "retailer_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_products_search_vector",
                table: "products",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "uidx_products_retailer_barcode",
                table: "products",
                columns: new[] { "retailer_id", "barcode" },
                unique: true,
                filter: "is_deleted = false AND barcode IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_status",
                table: "products",
                sql: "status IN ('Active', 'Inactive', 'Draft', 'OutOfStock')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "products");

            migrationBuilder.RenameIndex(
                name: "idx_products_sub_category_id",
                table: "products",
                newName: "ix_products_sub_category_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_status",
                table: "products",
                sql: "status IN ('Active', 'Inactive', 'Draft')");
        }
    }
}

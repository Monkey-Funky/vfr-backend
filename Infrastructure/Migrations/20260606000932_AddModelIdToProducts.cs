using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModelIdToProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Add the model_id column ────────────────────────────────────
            //
            // Nullable string (varchar 50).
            // NULL = normal retailer-created product.
            // Non-null = seeded AI-catalogue product whose ID the style-recommendation
            //            model knows (e.g. "78_y3ppkj").
            migrationBuilder.AddColumn<string>(
                name: "model_id",
                table: "products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // ── 2. Back-fill existing seed products ───────────────────────────
            //
            // The ExcelDataSeeder uses ON CONFLICT (id) DO NOTHING, so rows inserted
            // before this migration already exist and will NOT be re-inserted.
            // We back-fill model_id for those rows by deriving it from the Cloudinary
            // image URL that was stored in product_images at seed time.
            //
            // Strategy:
            //   • Join products → product_images (display_order = 0, not deleted)
            //   • Extract the filename-without-extension from the image URL using
            //     PostgreSQL string functions:
            //       split_part(image_url, '/', -1)   → "78_y3ppkj.jpg"
            //       regexp_replace(..., '\\.\\w+$','') → "78_y3ppkj"
            //   • Only update rows whose model_id IS NULL and that belong to the
            //     known seed retailer (aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa001) so
            //     we never touch real retailer products.
            migrationBuilder.Sql("""
                UPDATE products p
                SET    model_id = regexp_replace(
                                      split_part(pi.image_url, '/', -1),
                                      '\.\w+$', '')
                FROM   product_images pi
                WHERE  pi.product_id   = p.id
                  AND  pi.display_order = 0
                  AND  pi.is_deleted    = false
                  AND  p.retailer_id    = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaa001'
                  AND  p.model_id       IS NULL;
            """);

            // ── 3. Unique partial index ───────────────────────────────────────
            //
            // Scoped to non-NULL rows only so the many NULLs (retailer products)
            // do not interfere.  PostgreSQL treats each NULL as distinct anyway,
            // but the partial filter makes the intent explicit and avoids the index
            // growing to include every non-seeded product row.
            migrationBuilder.CreateIndex(
                name: "uidx_products_model_id",
                table: "products",
                column: "model_id",
                unique: true,
                filter: "model_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uidx_products_model_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "model_id",
                table: "products");
        }
    }
}
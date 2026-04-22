using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Customer_AddTryOnEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "virtual_try_on_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retailer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    avatar_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    recommended_size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    result_image_url = table.Column<string>(type: "text", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_virtual_try_on_sessions", x => x.id);
                    table.CheckConstraint("ck_virtual_try_on_sessions_type", "session_type IN ('Overlay2D', 'Model3D', 'ARLiveView')");
                    table.ForeignKey(
                        name: "fk_tryon_avatars",
                        column: x => x.avatar_id,
                        principalTable: "avatars",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_tryon_customer_accounts",
                        column: x => x.customer_id,
                        principalTable: "customer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tryon_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tryon_retailer_accounts",
                        column: x => x.retailer_id,
                        principalTable: "retailer_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_tryon_sessions_avatar_id",
                table: "virtual_try_on_sessions",
                column: "avatar_id",
                filter: "avatar_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_tryon_sessions_customer_created",
                table: "virtual_try_on_sessions",
                columns: new[] { "customer_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_tryon_sessions_product_id",
                table: "virtual_try_on_sessions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "idx_tryon_sessions_retailer_created",
                table: "virtual_try_on_sessions",
                columns: new[] { "retailer_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "virtual_try_on_sessions");
        }
    }
}

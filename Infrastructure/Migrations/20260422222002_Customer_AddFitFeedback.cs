using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Customer_AddFitFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fit_feedback",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    try_on_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    predicted_size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    actual_size_needed = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    fit_rating = table.Column<int>(type: "integer", nullable: false),
                    feedback_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fit_feedback", x => x.id);
                    table.CheckConstraint("ck_fit_feedback_rating", "fit_rating BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateIndex(
                name: "idx_fit_feedback_customer_id",
                table: "fit_feedback",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_fit_feedback_product_id",
                table: "fit_feedback",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "uq_fit_feedback_order_item",
                table: "fit_feedback",
                column: "order_item_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fit_feedback");
        }
    }
}

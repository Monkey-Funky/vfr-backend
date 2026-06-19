using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiGenerationCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_generation_cache",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    pipeline_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    input_json = table.Column<string>(type: "text", nullable: false),
                    result_json = table.Column<string>(type: "text", nullable: true),
                    result_image_url = table.Column<string>(type: "text", nullable: true),
                    result_model_url = table.Column<string>(type: "text", nullable: true),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_generation_cache", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_ai_generation_cache_request_hash",
                table: "ai_generation_cache",
                column: "request_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_ai_generation_cache_status",
                table: "ai_generation_cache",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_ai_generation_cache_created_at",
                table: "ai_generation_cache",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_ai_generation_cache_customer_type_created",
                table: "ai_generation_cache",
                columns: new[] { "customer_id", "type", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ai_generation_cache");
        }
    }
}

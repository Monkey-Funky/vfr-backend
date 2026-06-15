using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCOrderERDFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "carrier_name",
                table: "c_order_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shipping_address",
                table: "c_order_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "size",
                table: "c_order_items",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "carrier_name",
                table: "c_order_items");

            migrationBuilder.DropColumn(
                name: "shipping_address",
                table: "c_order_items");

            migrationBuilder.DropColumn(
                name: "size",
                table: "c_order_items");
        }
    }
}

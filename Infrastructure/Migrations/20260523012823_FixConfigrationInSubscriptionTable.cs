using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixConfigrationInSubscriptionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_avatar_measurement_history_avatars_avatar_id1",
                table: "avatar_measurement_history");

            migrationBuilder.DropIndex(
                name: "ix_avatar_measurement_history_avatar_id1",
                table: "avatar_measurement_history");

            migrationBuilder.DropColumn(
                name: "avatar_id1",
                table: "avatar_measurement_history");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "avatar_id1",
                table: "avatar_measurement_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_avatar_measurement_history_avatar_id1",
                table: "avatar_measurement_history",
                column: "avatar_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_avatar_measurement_history_avatars_avatar_id1",
                table: "avatar_measurement_history",
                column: "avatar_id1",
                principalTable: "avatars",
                principalColumn: "id");
        }
    }
}

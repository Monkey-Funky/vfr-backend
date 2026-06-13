using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Corrective migration: removes the spurious shadow FK column "avatar_id1",
    /// its index, and its foreign key from the avatar_measurement_history table.
    ///
    /// Root cause: an earlier version of AvatarMeasurementHistoryConfiguration
    /// configured the Avatar → AvatarMeasurementHistory relationship from BOTH
    /// sides, causing EF Core to register two relationships and generate the
    /// AddFeildForAvatarTable migration (20260519011749) which added the
    /// redundant column. The configuration has since been corrected (relationship
    /// is only configured from the Avatar side in AvatarConfiguration), but the
    /// database still has the three orphan artifacts.
    /// </summary>
    public partial class RemoveShadowAvatarFkColumn : Migration
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

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
    ///
    /// NOTE: Migration 20260523 (FixConfigrationInSubscriptionTable) already
    /// removed these artifacts on some database instances. The IF EXISTS guards
    /// below make this migration idempotent so it succeeds regardless.
    /// </summary>
    public partial class RemoveShadowAvatarFkColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use idempotent raw SQL so the migration succeeds whether or not
            // a previous migration already removed these artifacts.
            migrationBuilder.Sql(
                """
                ALTER TABLE avatar_measurement_history
                    DROP CONSTRAINT IF EXISTS fk_avatar_measurement_history_avatars_avatar_id1;
                """);

            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS ix_avatar_measurement_history_avatar_id1;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE avatar_measurement_history
                    DROP COLUMN IF EXISTS avatar_id1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty — re-adding the shadow column is not desired.
            // The avatar_id1 column was a spurious artifact that should never exist.
        }
    }
}


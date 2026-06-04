using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenopairings.Migrations
{
    /// <inheritdoc />
    public partial class FixTiebreakersDefault : Migration
    {
        private const string DefaultJson = """["Points","StrengthOfSchedule","Random"]""";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix tournaments created before Phase 3b whose TiebreakersJson was
            // migrated as an empty string instead of the correct JSON default.
            migrationBuilder.Sql(
                $"""UPDATE "Tournaments" SET "TiebreakersJson" = '{DefaultJson}' WHERE "TiebreakersJson" = '' OR "TiebreakersJson" IS NULL""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No rollback — empty string was invalid and shouldn't be restored.
        }
    }
}

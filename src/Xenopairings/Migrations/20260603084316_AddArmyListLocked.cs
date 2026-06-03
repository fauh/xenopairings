using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenopairings.Migrations
{
    /// <inheritdoc />
    public partial class AddArmyListLocked : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ArmyListLocked",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArmyListLocked",
                table: "Tournaments");
        }
    }
}

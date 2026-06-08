using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenopairings.Migrations
{
    /// <inheritdoc />
    public partial class AddIsTestEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTestEvent",
                table: "Tournaments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTestEvent",
                table: "Tournaments");
        }
    }
}

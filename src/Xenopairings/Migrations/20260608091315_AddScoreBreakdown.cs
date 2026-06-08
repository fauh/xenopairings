using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenopairings.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Player1BattleReady",
                table: "Matches",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Player1PrimaryScore",
                table: "Matches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Player1SecondaryScore",
                table: "Matches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Player2BattleReady",
                table: "Matches",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Player2PrimaryScore",
                table: "Matches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Player2SecondaryScore",
                table: "Matches",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Player1BattleReady",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Player1PrimaryScore",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Player1SecondaryScore",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Player2BattleReady",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Player2PrimaryScore",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Player2SecondaryScore",
                table: "Matches");
        }
    }
}

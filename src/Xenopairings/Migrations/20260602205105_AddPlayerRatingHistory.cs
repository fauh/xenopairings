using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenopairings.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerRatingHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProfilePublic",
                table: "PlayerRatings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlayerRatingHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerRatingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentTitle = table.Column<string>(type: "TEXT", nullable: false),
                    TournamentSlug = table.Column<string>(type: "TEXT", nullable: false),
                    OpponentName = table.Column<string>(type: "TEXT", nullable: true),
                    OpponentEmail = table.Column<string>(type: "TEXT", nullable: true),
                    MyRawScore = table.Column<int>(type: "INTEGER", nullable: false),
                    OpponentRawScore = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualOutcome = table.Column<double>(type: "REAL", nullable: false),
                    RatingBefore = table.Column<double>(type: "REAL", nullable: false),
                    RatingAfter = table.Column<double>(type: "REAL", nullable: false),
                    PlayedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRatingHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerRatingHistories_PlayerRatings_PlayerRatingId",
                        column: x => x.PlayerRatingId,
                        principalTable: "PlayerRatings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRatingHistories_PlayerRatingId",
                table: "PlayerRatingHistories",
                column: "PlayerRatingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerRatingHistories");

            migrationBuilder.DropColumn(
                name: "IsProfilePublic",
                table: "PlayerRatings");
        }
    }
}

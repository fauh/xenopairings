using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenopairings.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamAndScoringSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTeamEvent",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ScoringSystem",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TeamSize",
                table: "Tournaments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamMatchupId",
                table: "Matches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CaptainPlayerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InviteToken = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamMatchups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Team1Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Team2Id = table.Column<Guid>(type: "TEXT", nullable: true),
                    TableGroupStart = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMatchups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMatchups_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMatchups_Teams_Team1Id",
                        column: x => x.Team1Id,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMatchups_Teams_Team2Id",
                        column: x => x.Team2Id,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_TeamId",
                table: "Players",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_TeamMatchupId",
                table: "Matches",
                column: "TeamMatchupId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchups_RoundId",
                table: "TeamMatchups",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchups_Team1Id",
                table: "TeamMatchups",
                column: "Team1Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchups_Team2Id",
                table: "TeamMatchups",
                column: "Team2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_InviteToken",
                table: "Teams",
                column: "InviteToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TournamentId",
                table: "Teams",
                column: "TournamentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_TeamMatchups_TeamMatchupId",
                table: "Matches",
                column: "TeamMatchupId",
                principalTable: "TeamMatchups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Teams_TeamId",
                table: "Players",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_TeamMatchups_TeamMatchupId",
                table: "Matches");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Teams_TeamId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "TeamMatchups");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Players_TeamId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Matches_TeamMatchupId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "IsTeamEvent",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "ScoringSystem",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "TeamSize",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "TeamMatchupId",
                table: "Matches");
        }
    }
}

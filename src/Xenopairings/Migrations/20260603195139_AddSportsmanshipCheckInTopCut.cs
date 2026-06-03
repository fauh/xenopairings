using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Xenopairings.Migrations
{
    /// <inheritdoc />
    public partial class AddSportsmanshipCheckInTopCut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CheckInEnabled",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TiebreakersJson",
                table: "Tournaments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TopCutSize",
                table: "Tournaments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCheckedIn",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Player1SportsRating",
                table: "Matches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Player2SportsRating",
                table: "Matches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlayerReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReporterPlayerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReportedPlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    FiledAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsResolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    OrganizerNote = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerReports_Players_ReportedPlayerId",
                        column: x => x.ReportedPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlayerReports_Players_ReporterPlayerId",
                        column: x => x.ReporterPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlayerReports_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TopCutMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BracketRound = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Seed1 = table.Column<int>(type: "INTEGER", nullable: false),
                    Seed2 = table.Column<int>(type: "INTEGER", nullable: false),
                    Player1Id = table.Column<Guid>(type: "TEXT", nullable: true),
                    Player2Id = table.Column<Guid>(type: "TEXT", nullable: true),
                    Player1Score = table.Column<int>(type: "INTEGER", nullable: true),
                    Player2Score = table.Column<int>(type: "INTEGER", nullable: true),
                    IsScored = table.Column<bool>(type: "INTEGER", nullable: false),
                    WinnerId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopCutMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopCutMatches_Players_Player1Id",
                        column: x => x.Player1Id,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TopCutMatches_Players_Player2Id",
                        column: x => x.Player2Id,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TopCutMatches_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReports_ReportedPlayerId",
                table: "PlayerReports",
                column: "ReportedPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReports_ReporterPlayerId",
                table: "PlayerReports",
                column: "ReporterPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerReports_TournamentId",
                table: "PlayerReports",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TopCutMatches_Player1Id",
                table: "TopCutMatches",
                column: "Player1Id");

            migrationBuilder.CreateIndex(
                name: "IX_TopCutMatches_Player2Id",
                table: "TopCutMatches",
                column: "Player2Id");

            migrationBuilder.CreateIndex(
                name: "IX_TopCutMatches_TournamentId",
                table: "TopCutMatches",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerReports");

            migrationBuilder.DropTable(
                name: "TopCutMatches");

            migrationBuilder.DropColumn(
                name: "CheckInEnabled",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "TiebreakersJson",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "TopCutSize",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "IsCheckedIn",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Player1SportsRating",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Player2SportsRating",
                table: "Matches");
        }
    }
}

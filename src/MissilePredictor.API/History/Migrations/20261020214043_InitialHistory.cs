using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MissilePredictor.API.History.Migrations
{
    /// <inheritdoc />
    public partial class InitialHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NeptunAlerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AreaTitle = table.Column<string>(type: "TEXT", nullable: false),
                    ThreatId = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Region = table.Column<string>(type: "TEXT", nullable: true),
                    Locality = table.Column<string>(type: "TEXT", nullable: true),
                    Lat = table.Column<double>(type: "REAL", nullable: false),
                    Lon = table.Column<double>(type: "REAL", nullable: false),
                    DistanceKm = table.Column<double>(type: "REAL", nullable: false),
                    ExplanationShort = table.Column<string>(type: "TEXT", nullable: true),
                    AlertSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeptunAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NeptunTrackPoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ThreatId = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Lat = table.Column<double>(type: "REAL", nullable: false),
                    Lon = table.Column<double>(type: "REAL", nullable: false),
                    Heading = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NeptunTrackPoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NeptunAlerts_OccurredAt",
                table: "NeptunAlerts",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_NeptunAlerts_ThreatId",
                table: "NeptunAlerts",
                column: "ThreatId");

            migrationBuilder.CreateIndex(
                name: "IX_NeptunTrackPoints_ThreatId_ReceivedAt",
                table: "NeptunTrackPoints",
                columns: new[] { "ThreatId", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NeptunAlerts");

            migrationBuilder.DropTable(
                name: "NeptunTrackPoints");
        }
    }
}

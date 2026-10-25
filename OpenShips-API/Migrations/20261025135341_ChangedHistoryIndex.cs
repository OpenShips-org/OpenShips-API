using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenShipsAPI.Migrations
{
    /// <inheritdoc />
    public partial class ChangedHistoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HistoricalAisPositions_Mmsi_EventTimestamp",
                table: "HistoricalAisPositions");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalAisPositions_Mmsi_EventTimestamp",
                table: "HistoricalAisPositions",
                columns: new[] { "Mmsi", "EventTimestamp" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HistoricalAisPositions_Mmsi_EventTimestamp",
                table: "HistoricalAisPositions");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalAisPositions_Mmsi_EventTimestamp",
                table: "HistoricalAisPositions",
                columns: new[] { "Mmsi", "EventTimestamp" });
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AndonDashBroad.Migrations
{
    /// <inheritdoc />
    public partial class TaoBangTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentTickets",
                columns: table => new
                {
                    TicketId = table.Column<string>(type: "TEXT", nullable: false),
                    LineNumber = table.Column<string>(type: "TEXT", nullable: true),
                    StationName = table.Column<string>(type: "TEXT", nullable: true),
                    AlarmTypeIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TechCheckinAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TechFixedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LeaderConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorReason = table.Column<string>(type: "TEXT", nullable: true),
                    FixNote = table.Column<string>(type: "TEXT", nullable: true),
                    OperatorName = table.Column<string>(type: "TEXT", nullable: true),
                    TechnicianName = table.Column<string>(type: "TEXT", nullable: true),
                    LeaderName = table.Column<string>(type: "TEXT", nullable: true),
                    WorkOrder = table.Column<string>(type: "TEXT", nullable: true),
                    Product = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentTickets", x => x.TicketId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentTickets");
        }
    }
}

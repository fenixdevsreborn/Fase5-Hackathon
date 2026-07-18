using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConexaoSolidaria.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campaign_stats",
                columns: table => new
                {
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MetaFinanceira = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalArrecadado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DoacoesProcessadas = table.Column<int>(type: "integer", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_stats", x => x.CampaignId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_NextAttemptAtUtc",
                table: "outbox_messages",
                column: "NextAttemptAtUtc",
                filter: "\"PublishedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_donations_DoadorId",
                table: "donations",
                column: "DoadorId");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_Status_DataFim",
                table: "campaigns",
                columns: new[] { "Status", "DataFim" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_stats");

            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_NextAttemptAtUtc",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_donations_DoadorId",
                table: "donations");

            migrationBuilder.DropIndex(
                name: "IX_campaigns_Status_DataFim",
                table: "campaigns");
        }
    }
}

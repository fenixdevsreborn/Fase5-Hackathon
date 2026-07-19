using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConexaoSolidaria.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDonationStatusProcessadaEmIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_donations_Status_ProcessadaEm",
                table: "donations",
                columns: new[] { "Status", "ProcessadaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_donations_Status_ProcessadaEm",
                table: "donations");
        }
    }
}

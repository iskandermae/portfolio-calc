using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioCalc.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddValidationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "SecurityPrices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FxRates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "SecurityPrices");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FxRates");
        }
    }
}

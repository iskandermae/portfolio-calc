using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioCalc.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxBaseCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxBaseCurrency",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "GBP");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxBaseCurrency",
                table: "AppSettings");
        }
    }
}

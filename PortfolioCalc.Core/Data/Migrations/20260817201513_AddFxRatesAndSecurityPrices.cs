using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioCalc.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFxRatesAndSecurityPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FxRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromCurrency = table.Column<string>(type: "TEXT", nullable: false),
                    ToCurrency = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Rate = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SecurityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityPrices_Securities_SecurityId",
                        column: x => x.SecurityId,
                        principalTable: "Securities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FxRates_FromCurrency_ToCurrency_Date",
                table: "FxRates",
                columns: new[] { "FromCurrency", "ToCurrency", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityPrices_SecurityId_Date",
                table: "SecurityPrices",
                columns: new[] { "SecurityId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FxRates");

            migrationBuilder.DropTable(
                name: "SecurityPrices");
        }
    }
}

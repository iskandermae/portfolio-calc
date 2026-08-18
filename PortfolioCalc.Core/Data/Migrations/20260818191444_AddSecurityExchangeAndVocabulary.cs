using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PortfolioCalc.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityExchangeAndVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Exchange",
                table: "Securities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VocabularyEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VocabularyType = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyEntries", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "VocabularyEntries",
                columns: new[] { "Id", "Description", "Key", "Value", "VocabularyType" },
                values: new object[,]
                {
                    { 1, "US (NYSE Arca) — no suffix", "ARCA", "", "ExchangeYahooSuffix" },
                    { 2, "US (Nasdaq) — no suffix", "NASDAQ", "", "ExchangeYahooSuffix" },
                    { 3, "US (NYSE) — no suffix", "NYSE", "", "ExchangeYahooSuffix" },
                    { 4, "London Stock Exchange (ETFs)", "LSEETF", ".L", "ExchangeYahooSuffix" },
                    { 5, "London Stock Exchange", "LSE", ".L", "ExchangeYahooSuffix" },
                    { 6, "Xetra / Deutsche Börse", "IBIS", ".DE", "ExchangeYahooSuffix" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyEntries_VocabularyType_Key",
                table: "VocabularyEntries",
                columns: new[] { "VocabularyType", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VocabularyEntries");

            migrationBuilder.DropColumn(
                name: "Exchange",
                table: "Securities");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioCalc.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUiLayoutSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UiLayoutSettings",
                columns: table => new
                {
                    ScreenKey = table.Column<string>(type: "TEXT", nullable: false),
                    LayoutJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UiLayoutSettings", x => x.ScreenKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UiLayoutSettings");
        }
    }
}

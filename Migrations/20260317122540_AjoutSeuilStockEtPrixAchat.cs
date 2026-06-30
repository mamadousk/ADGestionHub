using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdGestionHub.Migrations
{
    /// <inheritdoc />
    public partial class AjoutSeuilStockEtPrixAchat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StockAlertThreshold",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockAlertThreshold",
                table: "Products");
        }
    }
}

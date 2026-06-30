using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdGestionHub.Migrations
{
    /// <inheritdoc />
    public partial class FixPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BoutiqueId",
                table: "SystemLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "StoreName",
                table: "StoreSettings",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiptFooterMessage",
                table: "StoreSettings",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "StoreSettings",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "StoreSettings",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "StoreSettings",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "BoutiqueId",
                table: "SaleItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BoutiqueId",
                table: "ErrorLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_BoutiqueId",
                table: "SystemLogs",
                column: "BoutiqueId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreSettings_BoutiqueId",
                table: "StoreSettings",
                column: "BoutiqueId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_BoutiqueId",
                table: "SaleItems",
                column: "BoutiqueId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BoutiqueId",
                table: "Products",
                column: "BoutiqueId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BoutiqueId",
                table: "Expenses",
                column: "BoutiqueId");

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_BoutiqueId",
                table: "ErrorLogs",
                column: "BoutiqueId");

            migrationBuilder.CreateIndex(
                name: "IX_Debts_BoutiqueId",
                table: "Debts",
                column: "BoutiqueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SystemLogs_BoutiqueId",
                table: "SystemLogs");

            migrationBuilder.DropIndex(
                name: "IX_StoreSettings_BoutiqueId",
                table: "StoreSettings");

            migrationBuilder.DropIndex(
                name: "IX_SaleItems_BoutiqueId",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_Products_BoutiqueId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BoutiqueId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_ErrorLogs_BoutiqueId",
                table: "ErrorLogs");

            migrationBuilder.DropIndex(
                name: "IX_Debts_BoutiqueId",
                table: "Debts");

            migrationBuilder.DropColumn(
                name: "BoutiqueId",
                table: "SystemLogs");

            migrationBuilder.DropColumn(
                name: "BoutiqueId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "BoutiqueId",
                table: "ErrorLogs");

            migrationBuilder.AlterColumn<string>(
                name: "StoreName",
                table: "StoreSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReceiptFooterMessage",
                table: "StoreSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "StoreSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "StoreSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "StoreSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}

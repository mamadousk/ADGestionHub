using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdGestionHub.Migrations
{
    /// <inheritdoc />
    public partial class SchemaAlignmentAndSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. ApplicationUser.FullName
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            // 2. SaleItem.UnitCost (coût d'achat historique)
            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "SaleItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // 3. StoreSettings : alignement des colonnes avec le modèle C#
            migrationBuilder.RenameColumn(
                name: "PhoneNumber",
                table: "StoreSettings",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "StoreSettings",
                newName: "ContactEmail");

            migrationBuilder.RenameColumn(
                name: "ReceiptFooterMessage",
                table: "StoreSettings",
                newName: "FooterMessage");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "StoreSettings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "StoreSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("UPDATE StoreSettings SET StoreName = N'Ma Boutique' WHERE StoreName IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "StoreName",
                table: "StoreSettings",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // 4. Debts.ClientPhone nullable
            migrationBuilder.AlterColumn<string>(
                name: "ClientPhone",
                table: "Debts",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // 5. ErrorLogs.BoutiqueId nullable (évite l'échec FK pour les visiteurs anonymes / SuperAdmin)
            migrationBuilder.DropForeignKey(
                name: "FK_ErrorLogs_Boutiques_BoutiqueId",
                table: "ErrorLogs");

            migrationBuilder.AlterColumn<int>(
                name: "BoutiqueId",
                table: "ErrorLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql("UPDATE ErrorLogs SET BoutiqueId = NULL WHERE BoutiqueId IS NOT NULL AND BoutiqueId NOT IN (SELECT Id FROM Boutiques);");

            migrationBuilder.AddForeignKey(
                name: "FK_ErrorLogs_Boutiques_BoutiqueId",
                table: "ErrorLogs",
                column: "BoutiqueId",
                principalTable: "Boutiques",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // 6. SystemLogs.BoutiqueId / StackTrace / UserEmail nullables
            migrationBuilder.DropForeignKey(
                name: "FK_SystemLogs_Boutiques_BoutiqueId",
                table: "SystemLogs");

            migrationBuilder.AlterColumn<int>(
                name: "BoutiqueId",
                table: "SystemLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "StackTrace",
                table: "SystemLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "UserEmail",
                table: "SystemLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.Sql("UPDATE SystemLogs SET BoutiqueId = NULL WHERE BoutiqueId IS NOT NULL AND BoutiqueId NOT IN (SELECT Id FROM Boutiques);");

            migrationBuilder.AddForeignKey(
                name: "FK_SystemLogs_Boutiques_BoutiqueId",
                table: "SystemLogs",
                column: "BoutiqueId",
                principalTable: "Boutiques",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // 7. Index composites de performance & unicité WaitingListPros.Email
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "WaitingListPros",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_WaitingListPros_Email",
                table: "WaitingListPros",
                column: "Email",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_Sales_BoutiqueId",
                table: "Sales");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BoutiqueId_SaleDate",
                table: "Sales",
                columns: new[] { "BoutiqueId", "SaleDate" });

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BoutiqueId",
                table: "Expenses");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BoutiqueId_Date",
                table: "Expenses",
                columns: new[] { "BoutiqueId", "Date" });

            migrationBuilder.DropIndex(
                name: "IX_Debts_BoutiqueId",
                table: "Debts");

            migrationBuilder.CreateIndex(
                name: "IX_Debts_BoutiqueId_IsArchived_IsPaid",
                table: "Debts",
                columns: new[] { "BoutiqueId", "IsArchived", "IsPaid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Debts_BoutiqueId_IsArchived_IsPaid",
                table: "Debts");

            migrationBuilder.CreateIndex(
                name: "IX_Debts_BoutiqueId",
                table: "Debts",
                column: "BoutiqueId");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BoutiqueId_Date",
                table: "Expenses");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BoutiqueId",
                table: "Expenses",
                column: "BoutiqueId");

            migrationBuilder.DropIndex(
                name: "IX_Sales_BoutiqueId_SaleDate",
                table: "Sales");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BoutiqueId",
                table: "Sales",
                column: "BoutiqueId");

            migrationBuilder.DropIndex(
                name: "IX_WaitingListPros_Email",
                table: "WaitingListPros");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemLogs_Boutiques_BoutiqueId",
                table: "SystemLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ErrorLogs_Boutiques_BoutiqueId",
                table: "ErrorLogs");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "StoreSettings");

            migrationBuilder.RenameColumn(
                name: "FooterMessage",
                table: "StoreSettings",
                newName: "ReceiptFooterMessage");

            migrationBuilder.RenameColumn(
                name: "ContactEmail",
                table: "StoreSettings",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "StoreSettings",
                newName: "PhoneNumber");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");
        }
    }
}

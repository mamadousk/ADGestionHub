using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdGestionHub.Migrations
{
    /// <inheritdoc />
    public partial class LocalizationFiscalQuotesAndClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- 1. StoreSettings : localisation, bi-monétarisme, statut formalisé, TVA et compteurs ---
            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "StoreSettings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "CD");

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "StoreSettings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "fr");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryCurrencyCode",
                table: "StoreSettings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<bool>(
                name: "SecondaryCurrencyEnabled",
                table: "StoreSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryCurrencyCode",
                table: "StoreSettings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SecondaryExchangeRate",
                table: "StoreSettings",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 2850m);

            migrationBuilder.AddColumn<string>(
                name: "PhoneDialCode",
                table: "StoreSettings",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "+243");

            migrationBuilder.AddColumn<string>(
                name: "DateFormat",
                table: "StoreSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "dd/MM/yyyy");

            migrationBuilder.AddColumn<bool>(
                name: "IsFormalized",
                table: "StoreSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TaxIdentificationNumber",
                table: "StoreSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradeRegisterNumber",
                table: "StoreSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVatEnabled",
                table: "StoreSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultVatRate",
                table: "StoreSettings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 16m);

            migrationBuilder.AddColumn<int>(
                name: "NextReceiptNumber",
                table: "StoreSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "NextInvoiceNumber",
                table: "StoreSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "NextQuoteNumber",
                table: "StoreSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // --- 2. Products : SKU et taux de TVA optionnel ---
            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "Products",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "Products",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            // --- 3. Clients (répertoire B2C / B2B) ---
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BoutiqueId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsBusinessClient = table.Column<bool>(type: "bit", nullable: false),
                    TaxIdentificationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_Boutiques_BoutiqueId",
                        column: x => x.BoutiqueId,
                        principalTable: "Boutiques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // --- 4. Sales : instantané documentaire, fiscal, devise et moteur de calcul uniforme ---
            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerTaxId",
                table: "Sales",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                table: "Sales",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "Sales",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Receipt");

            migrationBuilder.AddColumn<bool>(
                name: "IsFormalizedSnapshot",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVatEnabledSnapshot",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Sales",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencySymbol",
                table: "Sales",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "$");

            migrationBuilder.AddColumn<int>(
                name: "CurrencyDecimals",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryCurrencyCode",
                table: "Sales",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRateToSecondary",
                table: "Sales",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaidInCurrencyCode",
                table: "Sales",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreLegalTaxIdSnapshot",
                table: "Sales",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreTaxIdLabelSnapshot",
                table: "Sales",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreRccmSnapshot",
                table: "Sales",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreRccmLabelSnapshot",
                table: "Sales",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalNet",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "Sales",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Sales",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Sales",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            // --- 5. SaleItems : instantané de calcul par ligne ---
            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "SaleItems",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineNet",
                table: "SaleItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineTax",
                table: "SaleItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LineTotal",
                table: "SaleItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // --- 6. Expenses & Debts : isolation de devise historique ---
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Expenses",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencySymbol",
                table: "Expenses",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "$");

            migrationBuilder.AddColumn<int>(
                name: "CurrencyDecimals",
                table: "Expenses",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "Debts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientTaxId",
                table: "Debts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Debts",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "CurrencySymbol",
                table: "Debts",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "$");

            migrationBuilder.AddColumn<int>(
                name: "CurrencyDecimals",
                table: "Debts",
                type: "int",
                nullable: false,
                defaultValue: 2);

            // --- 7. Quotes & QuoteItems (Devis / Proformas en mode Formalisé) ---
            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BoutiqueId = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CustomerTaxId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConvertedSaleId = table.Column<int>(type: "int", nullable: true),
                    IsVatEnabledSnapshot = table.Column<bool>(type: "bit", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CurrencySymbol = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    CurrencyDecimals = table.Column<int>(type: "int", nullable: false),
                    SecondaryCurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ExchangeRateToSecondary = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    StoreLegalTaxIdSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StoreTaxIdLabelSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    StoreRccmSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StoreRccmLabelSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SubtotalNet = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotes_Boutiques_BoutiqueId",
                        column: x => x.BoutiqueId,
                        principalTable: "Boutiques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Quotes_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QuoteItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuoteId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LineNet = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BoutiqueId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteItems_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // --- 8. EInvoicingOptIns (Facturation Électronique Certifiée) ---
            migrationBuilder.CreateTable(
                name: "EInvoicingOptIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BoutiqueId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EInvoicingOptIns", x => x.Id);
                });

            // --- Index & Clés étrangères additionnelles ---
            migrationBuilder.CreateIndex(
                name: "IX_Clients_BoutiqueId_Name",
                table: "Clients",
                columns: new[] { "BoutiqueId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ClientId",
                table: "Sales",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Clients_ClientId",
                table: "Sales",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_BoutiqueId_CreatedAt",
                table: "Quotes",
                columns: new[] { "BoutiqueId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ClientId",
                table: "Quotes",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteItems_BoutiqueId",
                table: "QuoteItems",
                column: "BoutiqueId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteItems_QuoteId",
                table: "QuoteItems",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_EInvoicingOptIns_BoutiqueId_CountryCode",
                table: "EInvoicingOptIns",
                columns: new[] { "BoutiqueId", "CountryCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Sales_Clients_ClientId", table: "Sales");
            migrationBuilder.DropIndex(name: "IX_Sales_ClientId", table: "Sales");

            migrationBuilder.DropTable(name: "EInvoicingOptIns");
            migrationBuilder.DropTable(name: "QuoteItems");
            migrationBuilder.DropTable(name: "Quotes");
            migrationBuilder.DropTable(name: "Clients");

            migrationBuilder.DropColumn(name: "ClientId", table: "Debts");
            migrationBuilder.DropColumn(name: "ClientTaxId", table: "Debts");
            migrationBuilder.DropColumn(name: "CurrencyCode", table: "Debts");
            migrationBuilder.DropColumn(name: "CurrencySymbol", table: "Debts");
            migrationBuilder.DropColumn(name: "CurrencyDecimals", table: "Debts");

            migrationBuilder.DropColumn(name: "CurrencyCode", table: "Expenses");
            migrationBuilder.DropColumn(name: "CurrencySymbol", table: "Expenses");
            migrationBuilder.DropColumn(name: "CurrencyDecimals", table: "Expenses");

            migrationBuilder.DropColumn(name: "VatRate", table: "SaleItems");
            migrationBuilder.DropColumn(name: "LineNet", table: "SaleItems");
            migrationBuilder.DropColumn(name: "LineTax", table: "SaleItems");
            migrationBuilder.DropColumn(name: "LineTotal", table: "SaleItems");

            migrationBuilder.DropColumn(name: "ClientId", table: "Sales");
            migrationBuilder.DropColumn(name: "CustomerTaxId", table: "Sales");
            migrationBuilder.DropColumn(name: "DocumentNumber", table: "Sales");
            migrationBuilder.DropColumn(name: "DocumentType", table: "Sales");
            migrationBuilder.DropColumn(name: "IsFormalizedSnapshot", table: "Sales");
            migrationBuilder.DropColumn(name: "IsVatEnabledSnapshot", table: "Sales");
            migrationBuilder.DropColumn(name: "CurrencyCode", table: "Sales");
            migrationBuilder.DropColumn(name: "CurrencySymbol", table: "Sales");
            migrationBuilder.DropColumn(name: "CurrencyDecimals", table: "Sales");
            migrationBuilder.DropColumn(name: "SecondaryCurrencyCode", table: "Sales");
            migrationBuilder.DropColumn(name: "ExchangeRateToSecondary", table: "Sales");
            migrationBuilder.DropColumn(name: "PaidInCurrencyCode", table: "Sales");
            migrationBuilder.DropColumn(name: "StoreLegalTaxIdSnapshot", table: "Sales");
            migrationBuilder.DropColumn(name: "StoreTaxIdLabelSnapshot", table: "Sales");
            migrationBuilder.DropColumn(name: "StoreRccmSnapshot", table: "Sales");
            migrationBuilder.DropColumn(name: "StoreRccmLabelSnapshot", table: "Sales");
            migrationBuilder.DropColumn(name: "SubtotalNet", table: "Sales");
            migrationBuilder.DropColumn(name: "TaxAmount", table: "Sales");
            migrationBuilder.DropColumn(name: "IsCancelled", table: "Sales");
            migrationBuilder.DropColumn(name: "CancelledAt", table: "Sales");
            migrationBuilder.DropColumn(name: "CancellationReason", table: "Sales");

            migrationBuilder.DropColumn(name: "Sku", table: "Products");
            migrationBuilder.DropColumn(name: "VatRate", table: "Products");

            migrationBuilder.DropColumn(name: "CountryCode", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "PreferredLanguage", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "PrimaryCurrencyCode", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "SecondaryCurrencyEnabled", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "SecondaryCurrencyCode", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "SecondaryExchangeRate", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "PhoneDialCode", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "DateFormat", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "IsFormalized", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "TaxIdentificationNumber", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "TradeRegisterNumber", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "IsVatEnabled", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "DefaultVatRate", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "NextReceiptNumber", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "NextInvoiceNumber", table: "StoreSettings");
            migrationBuilder.DropColumn(name: "NextQuoteNumber", table: "StoreSettings");
        }
    }
}

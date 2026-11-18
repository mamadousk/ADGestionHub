using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdGestionHub.Migrations;

public partial class CommerceMvp : Migration
{
    protected override void Up(MigrationBuilder migration)
    {
        foreach (var table in new[] { "StoreSettings", "Sales", "Quotes", "Expenses" })
            migration.AddColumn<decimal>("CdfPerUsd", table, type: "decimal(18,6)", precision: 18, scale: 6, nullable: true);
        migration.AddColumn<string>("CustomPaymentMethods", "StoreSettings", type: "nvarchar(600)", maxLength: 600, nullable: true);
        migration.AddColumn<decimal>("Discount", "Sales", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
        migration.AddColumn<string>("PaymentMethod", "Expenses", type: "nvarchar(50)", maxLength: 50, nullable: true);
        migration.AddColumn<string>("Category", "Products", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migration.AddColumn<string>("Description", "Products", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migration.AddColumn<bool>("IsArchived", "Products", type: "bit", nullable: false, defaultValue: false);
        migration.AddUniqueConstraint("AK_Sales_Id_BoutiqueId", "Sales", new[] { "Id", "BoutiqueId" });
        migration.AddUniqueConstraint("AK_Debts_Id_BoutiqueId", "Debts", new[] { "Id", "BoutiqueId" });

        migration.CreateTable("Suppliers", columns: table => new
        {
            Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            BoutiqueId = table.Column<int>(type: "int", nullable: false),
            Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
            Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
            Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
            Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_Suppliers", x => x.Id);
            table.UniqueConstraint("AK_Suppliers_Id_BoutiqueId", x => new { x.Id, x.BoutiqueId });
            table.ForeignKey("FK_Suppliers_Boutiques_BoutiqueId", x => x.BoutiqueId, "Boutiques", "Id");
        });
        migration.CreateIndex("IX_Suppliers_BoutiqueId_Name", "Suppliers", new[] { "BoutiqueId", "Name" });

        migration.CreateTable("Purchases", columns: table => new
        {
            Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            BoutiqueId = table.Column<int>(type: "int", nullable: false),
            SupplierId = table.Column<int>(type: "int", nullable: false),
            SupplierName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
            SupplierReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
            CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
            CdfPerUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
            Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_Purchases", x => x.Id);
            table.UniqueConstraint("AK_Purchases_Id_BoutiqueId", x => new { x.Id, x.BoutiqueId });
            table.ForeignKey("FK_Purchases_Suppliers_SupplierId_BoutiqueId", x => new { x.SupplierId, x.BoutiqueId }, "Suppliers", new[] { "Id", "BoutiqueId" });
        });
        migration.CreateIndex("IX_Purchases_BoutiqueId_RecordedAt", "Purchases", new[] { "BoutiqueId", "RecordedAt" });
        migration.CreateIndex("IX_Purchases_SupplierId_BoutiqueId", "Purchases", new[] { "SupplierId", "BoutiqueId" });

        migration.CreateTable("PurchaseItems", columns: table => new
        {
            Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            PurchaseId = table.Column<int>(type: "int", nullable: false),
            ProductId = table.Column<int>(type: "int", nullable: false),
            ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
            Quantity = table.Column<int>(type: "int", nullable: false),
            UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_PurchaseItems", x => x.Id);
            table.ForeignKey("FK_PurchaseItems_Purchases_PurchaseId", x => x.PurchaseId, "Purchases", "Id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_PurchaseItems_Products_ProductId", x => x.ProductId, "Products", "Id");
        });
        migration.CreateIndex("IX_PurchaseItems_PurchaseId", "PurchaseItems", "PurchaseId");
        migration.CreateIndex("IX_PurchaseItems_ProductId", "PurchaseItems", "ProductId");

        migration.CreateTable("Payments", columns: table => new
        {
            Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            BoutiqueId = table.Column<int>(type: "int", nullable: false),
            SaleId = table.Column<int>(type: "int", nullable: true),
            PurchaseId = table.Column<int>(type: "int", nullable: true),
            DebtId = table.Column<int>(type: "int", nullable: true),
            Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
            Method = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
            CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
            AppliedCurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
            Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
            CdfPerUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
            RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_Payments", x => x.Id);
            table.CheckConstraint("CK_Payments_OneDocument", """
                (CASE WHEN SaleId IS NULL THEN 0 ELSE 1 END + CASE WHEN PurchaseId IS NULL THEN 0 ELSE 1 END +
                 CASE WHEN DebtId IS NULL THEN 0 ELSE 1 END) <= 1
                """);
            table.ForeignKey("FK_Payments_Boutiques_BoutiqueId", x => x.BoutiqueId, "Boutiques", "Id");
            table.ForeignKey("FK_Payments_Sales_SaleId_BoutiqueId", x => new { x.SaleId, x.BoutiqueId }, "Sales", new[] { "Id", "BoutiqueId" });
            table.ForeignKey("FK_Payments_Purchases_PurchaseId_BoutiqueId", x => new { x.PurchaseId, x.BoutiqueId }, "Purchases", new[] { "Id", "BoutiqueId" });
            table.ForeignKey("FK_Payments_Debts_DebtId_BoutiqueId", x => new { x.DebtId, x.BoutiqueId }, "Debts", new[] { "Id", "BoutiqueId" });
        });
        migration.CreateIndex("IX_Payments_BoutiqueId_RecordedAt", "Payments", new[] { "BoutiqueId", "RecordedAt" });
        migration.CreateIndex("IX_Payments_DebtId_BoutiqueId", "Payments", new[] { "DebtId", "BoutiqueId" });
        migration.CreateIndex("IX_Payments_PurchaseId_BoutiqueId", "Payments", new[] { "PurchaseId", "BoutiqueId" });
        migration.CreateIndex("IX_Payments_SaleId_BoutiqueId", "Payments", new[] { "SaleId", "BoutiqueId" });

        migration.CreateTable("StockMovements", columns: table => new
        {
            Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            BoutiqueId = table.Column<int>(type: "int", nullable: false),
            ProductId = table.Column<int>(type: "int", nullable: false),
            Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
            Reference = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
            Delta = table.Column<int>(type: "int", nullable: false),
            QuantityAfter = table.Column<int>(type: "int", nullable: false),
            RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_StockMovements", x => x.Id);
            table.CheckConstraint("CK_StockMovements_Quantity", "Delta <> 0 AND QuantityAfter >= 0");
            table.ForeignKey("FK_StockMovements_Boutiques_BoutiqueId", x => x.BoutiqueId, "Boutiques", "Id");
            table.ForeignKey("FK_StockMovements_Products_ProductId", x => x.ProductId, "Products", "Id");
        });
        migration.CreateIndex("IX_StockMovements_BoutiqueId_ProductId_RecordedAt", "StockMovements", new[] { "BoutiqueId", "ProductId", "RecordedAt" });
        migration.CreateIndex("IX_StockMovements_ProductId", "StockMovements", "ProductId");

        // Executed dynamically so an idempotent deployment script still parses against the pre-migration schema.
        migration.Sql("""
            EXEC(N'
              UPDATE [StoreSettings] SET [CdfPerUsd] =
                CASE WHEN [PrimaryCurrencyCode] = ''CDF'' THEN [SecondaryExchangeRate]
                  ELSE ROUND(1.0 / NULLIF([SecondaryExchangeRate], 0), 6) END
              WHERE [SecondaryCurrencyEnabled] = 1 AND [SecondaryExchangeRate] > 0
                AND (([PrimaryCurrencyCode] = ''CDF'' AND [SecondaryCurrencyCode] = ''USD'')
                     OR ([PrimaryCurrencyCode] = ''USD'' AND [SecondaryCurrencyCode] = ''CDF''));
              UPDATE [StoreSettings] SET [CdfPerUsd] = NULL WHERE [CdfPerUsd] <= 0 OR [CdfPerUsd] > 1000000000;

              UPDATE [Sales] SET [CdfPerUsd] =
                CASE WHEN [CurrencyCode] = ''CDF'' THEN [ExchangeRateToSecondary]
                  ELSE NULLIF(ROUND(1.0 / NULLIF([ExchangeRateToSecondary], 0), 6), 0) END
              WHERE [ExchangeRateToSecondary] > 0
                AND (([CurrencyCode] = ''CDF'' AND [SecondaryCurrencyCode] = ''USD'')
                     OR ([CurrencyCode] = ''USD'' AND [SecondaryCurrencyCode] = ''CDF''));

              UPDATE [Quotes] SET [CdfPerUsd] =
                CASE WHEN [CurrencyCode] = ''CDF'' THEN [ExchangeRateToSecondary]
                  ELSE NULLIF(ROUND(1.0 / NULLIF([ExchangeRateToSecondary], 0), 6), 0) END
              WHERE [ExchangeRateToSecondary] > 0
                AND (([CurrencyCode] = ''CDF'' AND [SecondaryCurrencyCode] = ''USD'')
                     OR ([CurrencyCode] = ''USD'' AND [SecondaryCurrencyCode] = ''CDF''));

              UPDATE [Products] SET [StockAlertThreshold] = [LowStockThreshold] WHERE [LowStockThreshold] > 0;

              INSERT INTO [StockMovements] ([BoutiqueId], [ProductId], [Kind], [Delta], [QuantityAfter], [RecordedAt])
                SELECT [BoutiqueId], [Id], N''Stock à la reprise'', [StockQuantity], [StockQuantity], SYSDATETIME()
                FROM [Products] WHERE [BoutiqueId] IS NOT NULL AND [StockQuantity] > 0;

              -- The old POS treated each non-void sale as fully settled but did not persist the amount tendered.
              -- Preserve that accounting basis rather than guessing the tendered amount from the current rate.
              INSERT INTO [Payments] ([BoutiqueId], [SaleId], [Description], [Method], [CurrencyCode], [AppliedCurrencyCode], [Amount], [AppliedAmount], [CdfPerUsd], [RecordedAt])
                SELECT [BoutiqueId], [Id], N''Reprise : vente historique considérée réglée en devise du document'',
                  COALESCE(NULLIF(LEFT([PaymentMethod], 50), ''''), N''Non précisé''), [CurrencyCode], [CurrencyCode], [FinalPrice], [FinalPrice],
                  [CdfPerUsd], [SaleDate]
                FROM [Sales] WHERE [IsCancelled] = 0 AND [FinalPrice] > 0;
            ');
            """);
    }

    protected override void Down(MigrationBuilder migration)
    {
        migration.DropTable("Payments");
        migration.DropTable("StockMovements");
        migration.DropTable("PurchaseItems");
        migration.DropTable("Purchases");
        migration.DropTable("Suppliers");
        migration.DropUniqueConstraint("AK_Sales_Id_BoutiqueId", "Sales");
        migration.DropUniqueConstraint("AK_Debts_Id_BoutiqueId", "Debts");
        foreach (var table in new[] { "StoreSettings", "Sales", "Quotes", "Expenses" }) migration.DropColumn("CdfPerUsd", table);
        migration.DropColumn("CustomPaymentMethods", "StoreSettings");
        migration.DropColumn("Discount", "Sales");
        migration.DropColumn("PaymentMethod", "Expenses");
        migration.DropColumn("Category", "Products");
        migration.DropColumn("Description", "Products");
        migration.DropColumn("IsArchived", "Products");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdGestionHub.Migrations;

public partial class OperationAttribution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ActorUserId", table: "SystemLogs", type: "nvarchar(450)", maxLength: 450, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ActorName", table: "SystemLogs", type: "nvarchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<int>(name: "Subject", table: "SystemLogs", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "SubjectId", table: "SystemLogs", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>(name: "CreatedByUserId", table: "Payments", type: "nvarchar(450)", maxLength: 450, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CreatedByUserId", table: "StockMovements", type: "nvarchar(450)", maxLength: 450, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_SystemLogs_BoutiqueId_ActorUserId_DateOccurred", table: "SystemLogs",
            columns: new[] { "BoutiqueId", "ActorUserId", "DateOccurred" });
        migrationBuilder.CreateIndex(name: "IX_SystemLogs_BoutiqueId_Subject_SubjectId", table: "SystemLogs",
            columns: new[] { "BoutiqueId", "Subject", "SubjectId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_SystemLogs_BoutiqueId_ActorUserId_DateOccurred", table: "SystemLogs");
        migrationBuilder.DropIndex(name: "IX_SystemLogs_BoutiqueId_Subject_SubjectId", table: "SystemLogs");
        migrationBuilder.DropColumn(name: "ActorUserId", table: "SystemLogs");
        migrationBuilder.DropColumn(name: "ActorName", table: "SystemLogs");
        migrationBuilder.DropColumn(name: "Subject", table: "SystemLogs");
        migrationBuilder.DropColumn(name: "SubjectId", table: "SystemLogs");
        migrationBuilder.DropColumn(name: "CreatedByUserId", table: "Payments");
        migrationBuilder.DropColumn(name: "CreatedByUserId", table: "StockMovements");
    }
}

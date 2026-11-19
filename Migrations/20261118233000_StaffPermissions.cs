using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdGestionHub.Migrations;

public partial class StaffPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "StaffAccessMask", table: "AspNetUsers", type: "int", nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CreatedByUserId", table: "Sales", type: "nvarchar(450)", maxLength: 450, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CreatedByUserId", table: "Sales");
        migrationBuilder.DropColumn(name: "StaffAccessMask", table: "AspNetUsers");
    }
}

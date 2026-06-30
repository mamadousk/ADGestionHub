using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdGestionHub.Migrations
{
    /// <inheritdoc />
    public partial class SetupMultiTenantSaaS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "UserFeedbacks",
                newName: "UserName");

            migrationBuilder.AddColumn<int>(
                name: "BoutiqueId",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoutiqueId",
                table: "Sales");

            migrationBuilder.RenameColumn(
                name: "UserName",
                table: "UserFeedbacks",
                newName: "UserId");
        }
    }
}

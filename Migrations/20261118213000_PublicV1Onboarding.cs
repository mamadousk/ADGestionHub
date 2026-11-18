using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdGestionHub.Migrations;

public partial class PublicV1Onboarding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "SetupPending", table: "Boutiques", type: "bit", nullable: false, defaultValue: false);

        migrationBuilder.CreateTable(
            name: "StoreLogos",
            columns: table => new
            {
                BoutiqueId = table.Column<int>(type: "int", nullable: false),
                Png = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreLogos", x => x.BoutiqueId);
                table.ForeignKey("FK_StoreLogos_Boutiques_BoutiqueId", x => x.BoutiqueId, "Boutiques", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TermsAcceptances",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TermsAcceptances", x => x.Id);
                table.ForeignKey("FK_TermsAcceptances_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_TermsAcceptances_UserId_Version", "TermsAcceptances", new[] { "UserId", "Version" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("StoreLogos");
        migrationBuilder.DropTable("TermsAcceptances");
        migrationBuilder.DropColumn("SetupPending", "Boutiques");
    }
}

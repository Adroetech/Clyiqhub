using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameAgreementNoToAgreementCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AgreementNo",
                schema: "dbo",
                table: "Agreements",
                newName: "AgreementCode");

            migrationBuilder.RenameIndex(
                name: "IX_Agreements_AgreementNo",
                schema: "dbo",
                table: "Agreements",
                newName: "IX_Agreements_AgreementCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AgreementCode",
                schema: "dbo",
                table: "Agreements",
                newName: "AgreementNo");

            migrationBuilder.RenameIndex(
                name: "IX_Agreements_AgreementCode",
                schema: "dbo",
                table: "Agreements",
                newName: "IX_Agreements_AgreementNo");
        }
    }
}

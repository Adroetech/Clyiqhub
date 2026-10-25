using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgreementContractNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractNumber",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // Backfill existing rows with a distinct placeholder (derived
            // from each row's own PK, so it's guaranteed unique) before the
            // column is made required and the unique index below is added —
            // a blanket default would collide on the unique constraint for
            // every row past the first.
            migrationBuilder.Sql(
                "UPDATE [dbo].[Agreements] SET [ContractNumber] = N'LEGACY-' + CONVERT(nvarchar(36), [AgreementId]) WHERE [ContractNumber] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "ContractNumber",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_ContractNumber",
                schema: "dbo",
                table: "Agreements",
                column: "ContractNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Agreements_ContractNumber",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "ContractNumber",
                schema: "dbo",
                table: "Agreements");
        }
    }
}

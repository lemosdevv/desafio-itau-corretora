using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItauCorretora.Desafio.Migrations
{
    /// <inheritdoc />
    public partial class MakeStockIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountsMovements_Stocks_StockId",
                table: "AccountsMovements");

            migrationBuilder.AlterColumn<int>(
                name: "StockId",
                table: "AccountsMovements",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountsMovements_Stocks_StockId",
                table: "AccountsMovements",
                column: "StockId",
                principalTable: "Stocks",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountsMovements_Stocks_StockId",
                table: "AccountsMovements");

            migrationBuilder.AlterColumn<int>(
                name: "StockId",
                table: "AccountsMovements",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountsMovements_Stocks_StockId",
                table: "AccountsMovements",
                column: "StockId",
                principalTable: "Stocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

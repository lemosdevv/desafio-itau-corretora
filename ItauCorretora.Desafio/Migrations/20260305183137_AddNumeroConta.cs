using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItauCorretora.Desafio.Migrations
{
    /// <inheritdoc />
    public partial class AddNumeroConta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValorMensal",
                table: "customers");

            migrationBuilder.RenameColumn(
                name: "DateRegister",
                table: "customers",
                newName: "SubscriptionDate");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "customers",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyValue",
                table: "customers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "Accounts",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Accounts",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "MonthlyValue",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Accounts");

            migrationBuilder.RenameColumn(
                name: "SubscriptionDate",
                table: "customers",
                newName: "DateRegister");

            migrationBuilder.AddColumn<decimal>(
                name: "ValorMensal",
                table: "customers",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}

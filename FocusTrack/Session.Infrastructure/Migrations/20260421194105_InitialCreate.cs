using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Session.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "MonthlyFocusProjection",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "MonthlyFocusProjection",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_MonthlyFocusProjection",
                table: "MonthlyFocusProjection",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_MonthlyFocusProjection",
                table: "MonthlyFocusProjection");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "MonthlyFocusProjection");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "MonthlyFocusProjection",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}

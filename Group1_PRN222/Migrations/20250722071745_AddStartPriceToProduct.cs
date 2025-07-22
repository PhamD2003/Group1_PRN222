using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Group1_PRN222.Migrations
{
    public partial class AddStartPriceToProduct : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "StartPrice",
                table: "Product",
                type: "decimal(10,2)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartPrice",
                table: "Product");
        }
    }
}

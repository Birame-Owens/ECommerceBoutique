using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceBoutique.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalApiPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalApiPassword",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalApiPassword",
                table: "AspNetUsers");
        }
    }
}

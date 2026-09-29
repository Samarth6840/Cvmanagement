using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuiltInAttributeFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBuiltIn",
                table: "AttributeDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDefinitions_IsBuiltIn",
                table: "AttributeDefinitions",
                column: "IsBuiltIn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttributeDefinitions_IsBuiltIn",
                table: "AttributeDefinitions");

            migrationBuilder.DropColumn(
                name: "IsBuiltIn",
                table: "AttributeDefinitions");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CvManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueCvPerPositionAndAttributeName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CvRecords_CandidateProfileId_PositionId",
                table: "CvRecords",
                columns: new[] { "CandidateProfileId", "PositionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttributeDefinitions_Name",
                table: "AttributeDefinitions",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CvRecords_CandidateProfileId_PositionId",
                table: "CvRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttributeDefinitions_Name",
                table: "AttributeDefinitions");
        }
    }
}

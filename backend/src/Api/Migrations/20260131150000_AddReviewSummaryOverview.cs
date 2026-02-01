using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewSummaryOverview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AfterState",
                table: "ReviewSummaries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BeforeState",
                table: "ReviewSummaries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OverallSummary",
                table: "ReviewSummaries",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AfterState",
                table: "ReviewSummaries");

            migrationBuilder.DropColumn(
                name: "BeforeState",
                table: "ReviewSummaries");

            migrationBuilder.DropColumn(
                name: "OverallSummary",
                table: "ReviewSummaries");
        }
    }
}

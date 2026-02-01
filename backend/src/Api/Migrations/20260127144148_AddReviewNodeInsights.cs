using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewNodeInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "ReviewNodes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawDiffText",
                table: "FileChanges",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReviewNodeBehaviourSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BehaviourChange = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    ReviewerFocus = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewNodeBehaviourSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewNodeBehaviourSummaries_ReviewNodes_ReviewNodeId",
                        column: x => x.ReviewNodeId,
                        principalTable: "ReviewNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewNodeChecklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Items = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewNodeChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewNodeChecklists_ReviewNodes_ReviewNodeId",
                        column: x => x.ReviewNodeId,
                        principalTable: "ReviewNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewNodeBehaviourSummaries_ReviewNodeId",
                table: "ReviewNodeBehaviourSummaries",
                column: "ReviewNodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewNodeChecklists_ReviewNodeId",
                table: "ReviewNodeChecklists",
                column: "ReviewNodeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewNodeBehaviourSummaries");

            migrationBuilder.DropTable(
                name: "ReviewNodeChecklists");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "ReviewNodes");

            migrationBuilder.DropColumn(
                name: "RawDiffText",
                table: "FileChanges");
        }
    }
}

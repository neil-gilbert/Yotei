using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewSessionModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PullRequestSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSessions_PullRequestSnapshots_PullRequestSnapshotId",
                        column: x => x.PullRequestSnapshotId,
                        principalTable: "PullRequestSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    NodeType = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    ChangeType = table.Column<string>(type: "text", nullable: false),
                    RiskTags = table.Column<List<string>>(type: "text[]", nullable: false),
                    Evidence = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewNodes_ReviewNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ReviewNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReviewNodes_ReviewSessions_ReviewSessionId",
                        column: x => x.ReviewSessionId,
                        principalTable: "ReviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedFilesCount = table.Column<int>(type: "integer", nullable: false),
                    NewFilesCount = table.Column<int>(type: "integer", nullable: false),
                    ModifiedFilesCount = table.Column<int>(type: "integer", nullable: false),
                    DeletedFilesCount = table.Column<int>(type: "integer", nullable: false),
                    EntryPoints = table.Column<List<string>>(type: "text[]", nullable: false),
                    SideEffects = table.Column<List<string>>(type: "text[]", nullable: false),
                    RiskTags = table.Column<List<string>>(type: "text[]", nullable: false),
                    TopPaths = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSummaries_ReviewSessions_ReviewSessionId",
                        column: x => x.ReviewSessionId,
                        principalTable: "ReviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewNodeExplanations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Response = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewNodeExplanations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewNodeExplanations_ReviewNodes_ReviewNodeId",
                        column: x => x.ReviewNodeId,
                        principalTable: "ReviewNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewNodeExplanations_ReviewNodeId",
                table: "ReviewNodeExplanations",
                column: "ReviewNodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewNodes_ParentId",
                table: "ReviewNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewNodes_ReviewSessionId",
                table: "ReviewNodes",
                column: "ReviewSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSessions_PullRequestSnapshotId",
                table: "ReviewSessions",
                column: "PullRequestSnapshotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSummaries_ReviewSessionId",
                table: "ReviewSummaries",
                column: "ReviewSessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewNodeExplanations");

            migrationBuilder.DropTable(
                name: "ReviewSummaries");

            migrationBuilder.DropTable(
                name: "ReviewNodes");

            migrationBuilder.DropTable(
                name: "ReviewSessions");
        }
    }
}

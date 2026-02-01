using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChangeTrees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PullRequestSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeTrees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeTrees_PullRequestSnapshots_PullRequestSnapshotId",
                        column: x => x.PullRequestSnapshotId,
                        principalTable: "PullRequestSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeTreeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    NodeType = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: true),
                    ChangeType = table.Column<string>(type: "text", nullable: true),
                    AddedLines = table.Column<int>(type: "integer", nullable: false),
                    DeletedLines = table.Column<int>(type: "integer", nullable: false),
                    RawDiffRef = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeNodes_ChangeNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ChangeNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChangeNodes_ChangeTrees_ChangeTreeId",
                        column: x => x.ChangeTreeId,
                        principalTable: "ChangeTrees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeNodes_ChangeTreeId",
                table: "ChangeNodes",
                column: "ChangeTreeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeNodes_ParentId",
                table: "ChangeNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeTrees_PullRequestSnapshotId",
                table: "ChangeTrees",
                column: "PullRequestSnapshotId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeNodes");

            migrationBuilder.DropTable(
                name: "ChangeTrees");
        }
    }
}

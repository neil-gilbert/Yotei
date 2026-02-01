using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRawDiffBlobs : Migration
    {
        /// <summary>
        /// Adds the RawDiffBlobs table for database-backed diff storage.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RawDiffBlobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PullRequestSnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    DiffText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawDiffBlobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawDiffBlobs_PullRequestSnapshots_PullRequestSnapshotId",
                        column: x => x.PullRequestSnapshotId,
                        principalTable: "PullRequestSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RawDiffBlobs_PullRequestSnapshotId_Path",
                table: "RawDiffBlobs",
                columns: new[] { "PullRequestSnapshotId", "Path" },
                unique: true);
        }

        /// <summary>
        /// Removes the RawDiffBlobs table introduced by this migration.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawDiffBlobs");
        }
    }
}

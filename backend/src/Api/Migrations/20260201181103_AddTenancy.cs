using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var defaultTenantId = new Guid("11111111-1111-1111-1111-111111111111");
            var defaultTenantToken = $"legacy-{Guid.NewGuid():N}";

            migrationBuilder.DropIndex(
                name: "IX_Repositories_Owner_Name",
                table: "Repositories");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ReviewSessions",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Repositories",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PullRequestSnapshots",
                type: "uuid",
                nullable: false,
                defaultValue: defaultTenantId);

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "Id", "Name", "Slug", "Token", "CreatedAt" },
                values: new object[] { defaultTenantId, "Legacy", "legacy", defaultTenantToken, DateTimeOffset.UtcNow });

            migrationBuilder.CreateTable(
                name: "GitHubInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<long>(type: "bigint", nullable: false),
                    AccountLogin = table.Column<string>(type: "text", nullable: false),
                    AccountType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitHubInstallations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSessions_TenantId",
                table: "ReviewSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_TenantId_Owner_Name",
                table: "Repositories",
                columns: new[] { "TenantId", "Owner", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestSnapshots_TenantId",
                table: "PullRequestSnapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubInstallations_InstallationId",
                table: "GitHubInstallations",
                column: "InstallationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubInstallations_TenantId",
                table: "GitHubInstallations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Token",
                table: "Tenants",
                column: "Token",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PullRequestSnapshots_Tenants_TenantId",
                table: "PullRequestSnapshots",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Repositories_Tenants_TenantId",
                table: "Repositories",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewSessions_Tenants_TenantId",
                table: "ReviewSessions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PullRequestSnapshots_Tenants_TenantId",
                table: "PullRequestSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_Repositories_Tenants_TenantId",
                table: "Repositories");

            migrationBuilder.DropForeignKey(
                name: "FK_ReviewSessions_Tenants_TenantId",
                table: "ReviewSessions");

            migrationBuilder.DropTable(
                name: "GitHubInstallations");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_ReviewSessions_TenantId",
                table: "ReviewSessions");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_TenantId_Owner_Name",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_PullRequestSnapshots_TenantId",
                table: "PullRequestSnapshots");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ReviewSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PullRequestSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Owner_Name",
                table: "Repositories",
                columns: new[] { "Owner", "Name" },
                unique: true);
        }
    }
}

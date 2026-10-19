using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ramai.Modules.Tenants.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenants");

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "tenants",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.TenantId);
                    table.CheckConstraint("CK_Tenants_Id", "\"TenantId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_Tenants_Name", "length(btrim(\"Name\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                schema: "tenants",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => new { x.TenantId, x.UserId });
                    table.CheckConstraint("CK_Memberships_Ids", "\"TenantId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"UserId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_Memberships_OwnerActive", "\"Role\" <> 2 OR \"Status\" = 0");
                    table.CheckConstraint("CK_Memberships_Role", "\"Role\" IN (0, 1, 2)");
                    table.CheckConstraint("CK_Memberships_Status", "\"Status\" IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_Memberships_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenants",
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId",
                schema: "tenants",
                table: "Memberships",
                column: "TenantId",
                unique: true,
                filter: "\"Role\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_UserId",
                schema: "tenants",
                table: "Memberships",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Memberships",
                schema: "tenants");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "tenants");
        }
    }
}

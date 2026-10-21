using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ramai.Modules.Tenants.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceProvisioningRequests",
                schema: "tenants",
                columns: table => new
                {
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceProvisioningRequests", x => new { x.ActorUserId, x.RequestId });
                    table.CheckConstraint("CK_Provisioning_Ids", "\"ActorUserId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RequestId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_WorkspaceProvisioningRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenants",
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceProvisioningRequests_TenantId",
                schema: "tenants",
                table: "WorkspaceProvisioningRequests",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Successful receipts cannot be discarded silently: it would permit duplicate workspaces on retry.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM tenants."WorkspaceProvisioningRequests") THEN
                        RAISE EXCEPTION 'Provisioning receipts require a reviewed recovery procedure before rollback'
                            USING ERRCODE = '55000';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "WorkspaceProvisioningRequests",
                schema: "tenants");
        }
    }
}

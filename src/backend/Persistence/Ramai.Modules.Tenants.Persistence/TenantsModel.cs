using Microsoft.EntityFrameworkCore;
using Ramai.Modules.Tenants.Domain;

namespace Ramai.Modules.Tenants.Persistence;

internal static class TenantsModel
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.HasDefaultSchema("tenants");
        builder.Entity<WorkspaceProvisioningRequest>(request =>
        {
            request.ToTable("WorkspaceProvisioningRequests", table => table.HasCheckConstraint("CK_Provisioning_Ids",
                "\"ActorUserId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RequestId\" <> '00000000-0000-0000-0000-000000000000'::uuid"));
            request.HasKey(x => new { x.ActorUserId, x.RequestId });
            request.Property(x => x.ActorUserId).ValueGeneratedNever();
            request.Property(x => x.RequestId).ValueGeneratedNever();
            request.Property(x => x.TenantId).ValueGeneratedNever();
            request.Property(x => x.RequestName).IsRequired().HasMaxLength(256);
            request.HasIndex(x => x.TenantId).IsUnique();
            request.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Tenant>(tenant =>
        {
            tenant.ToTable("Tenants", table =>
            {
                table.HasCheckConstraint("CK_Tenants_Id", "\"TenantId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                table.HasCheckConstraint("CK_Tenants_Name", "length(btrim(\"Name\")) > 0");
            });
            tenant.HasKey(x => x.TenantId);
            tenant.Property(x => x.TenantId).ValueGeneratedNever();
            tenant.Property(x => x.Name).IsRequired().HasMaxLength(256);
            tenant.HasMany(x => x.Memberships).WithOne().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            tenant.Navigation(x => x.Memberships).HasField("_memberships").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Entity<Membership>(membership =>
        {
            membership.ToTable("Memberships", table =>
            {
                table.HasCheckConstraint("CK_Memberships_Ids", "\"TenantId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"UserId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                table.HasCheckConstraint("CK_Memberships_Role", "\"Role\" IN (0, 1, 2)");
                table.HasCheckConstraint("CK_Memberships_Status", "\"Status\" IN (0, 1)");
                table.HasCheckConstraint("CK_Memberships_OwnerActive", "\"Role\" <> 2 OR \"Status\" = 0");
            });
            membership.HasKey(x => new { x.TenantId, x.UserId });
            membership.Property(x => x.TenantId).ValueGeneratedNever();
            membership.Property(x => x.UserId).ValueGeneratedNever();
            membership.Property(x => x.Role);
            membership.Property(x => x.Status);
            membership.HasIndex(x => x.UserId);
            membership.HasIndex(x => x.TenantId).IsUnique().HasFilter("\"Role\" = 2");
        });
    }
}

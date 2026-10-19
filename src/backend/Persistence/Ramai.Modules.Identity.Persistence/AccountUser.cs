using Microsoft.AspNetCore.Identity;

namespace Ramai.Modules.Identity.Persistence;

// The single persistent account/credential source of truth. UserAccount is a domain
// model, not a second table. Tenant roles never belong to this global identity.
public sealed class AccountUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; } = true;
}

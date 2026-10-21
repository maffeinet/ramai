using System.Reflection;
using System.Xml.Linq;

namespace Ramai.ArchitectureTests;

public sealed class DependencyBoundaryTests
{
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    [Fact]
    public void Shared_layers_have_only_allowed_project_references()
    {
        AssertReferences("Ramai.Domain", []);
        AssertReferences("Ramai.Application", ["Ramai.Domain"]);
        AssertReferences("Ramai.Infrastructure", ["Ramai.Application"]);
        // ADR-010: explicit host-only composition, never a cross-module dependency.
        AssertReferences("Ramai.Api", ["Ramai.Application", "Ramai.Infrastructure", "Ramai.Modules.Identity.Persistence", "Ramai.Modules.Tenants.Persistence"]);
    }

    [Fact]
    public void Domain_compiled_assembly_has_no_infrastructure_dependencies()
    {
        var forbidden = new[] { "Ramai.Api", "Ramai.Infrastructure", "Npgsql", "StackExchange.Redis" };
        Assert.DoesNotContain(Assembly.Load("Ramai.Domain").GetReferencedAssemblies(),
            reference => forbidden.Contains(reference.Name));
    }

    [Fact]
    public void All_seven_modules_have_no_direct_module_or_host_dependencies()
    {
        var projects = Directory.GetFiles(
            Path.Combine(RepositoryRoot, "src", "backend", "Modules"), "*.csproj", SearchOption.AllDirectories);
        Assert.Equal(7, projects.Length);
        foreach (var project in projects)
        {
            Assert.Empty(XDocument.Load(project).Descendants("ProjectReference"));
            Assert.DoesNotContain(Assembly.Load(Path.GetFileNameWithoutExtension(project)).GetReferencedAssemblies(),
                reference => reference.Name?.StartsWith("Ramai.", StringComparison.Ordinal) == true);
        }
    }

    private static void AssertReferences(string name, string[] expected)
    {
        var project = Path.Combine(RepositoryRoot, "src", "backend", name, name + ".csproj");
        var references = XDocument.Load(project).Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(
                element.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order().ToArray();
        Assert.Equal(expected.Order().ToArray(), references);
    }

    [Fact]
    public void Api_uses_identity_and_tenant_adapters_only_in_its_composition_root()
    {
        var root = Path.Combine(RepositoryRoot, "src", "backend", "Ramai.Api");
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(root, file).StartsWith("Composition" + Path.DirectorySeparatorChar)
                && !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
        {
            Assert.DoesNotContain("Ramai.Modules.Identity.Persistence", File.ReadAllText(file));
            Assert.DoesNotContain("Ramai.Modules.Tenants", File.ReadAllText(file));
        }
    }

    [Fact]
    public void Identity_and_tenant_models_have_no_persistence_or_identity_framework_dependencies()
    {
        var models = new[] { "Ramai.Modules.Identity", "Ramai.Modules.Tenants" };
        var forbiddenPrefixes = new[] { "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore.Identity", "Npgsql", "StackExchange.Redis" };
        foreach (var model in models)
        {
            Assert.DoesNotContain(Assembly.Load(model).GetReferencedAssemblies(),
                reference => forbiddenPrefixes.Any(prefix => reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true));
        }

        var user = Assembly.Load(models[0]).GetType("Ramai.Modules.Identity.Domain.UserAccount")!;
        var membership = Assembly.Load(models[1]).GetType("Ramai.Modules.Tenants.Domain.Membership")!;
        Assert.Null(user.GetProperty("TenantId"));
        Assert.Equal(typeof(Guid), membership.GetProperty("TenantId")!.PropertyType);
        Assert.Null(membership.GetProperty("TenantId")!.SetMethod);
        Assert.Null(membership.GetProperty("UserId")!.SetMethod);
    }

    [Fact]
    public void Application_contracts_do_not_expose_module_or_infrastructure_types()
    {
        var application = Assembly.Load("Ramai.Application");
        var contracts = application.GetExportedTypes()
            .Where(type => type.Namespace?.StartsWith("Ramai.Application.Contracts.", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.NotEmpty(contracts);
        foreach (var contract in contracts)
        {
            foreach (var property in contract.GetProperties())
            {
                AssertContractType(property.PropertyType, application);
            }

            if (!contract.IsInterface)
            {
                continue;
            }

            foreach (var method in contract.GetMethods())
            {
                AssertContractType(method.ReturnType, application);
                foreach (var parameter in method.GetParameters())
                {
                    AssertContractType(parameter.ParameterType, application);
                }
            }
        }
    }

    private static void AssertContractType(Type type, Assembly application)
    {
        Assert.True(type.Assembly == application ||
            type.Assembly.GetName().Name?.StartsWith("System.", StringComparison.Ordinal) == true,
            $"Unexpected contract type: {type.FullName}");
        foreach (var argument in type.GetGenericArguments())
        {
            AssertContractType(argument, application);
        }
    }

    [Fact]
    public void Global_tenant_context_is_restricted_to_reviewed_bootstrap_paths()
    {
        var root = Path.Combine(RepositoryRoot, "src", "backend");
        var allowed = new[] { "TenantsStoreContext.cs", "TenantsStoreFactory.cs", "TenantMembershipReader.cs", "WorkspaceProvisioningStore.cs" };
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                && !file.Contains(Path.DirectorySeparatorChar + "Migrations" + Path.DirectorySeparatorChar)))
        {
            // ADR-010 approved host-only DI registration, not a handler/global query path.
            if (Path.GetRelativePath(root, file) == Path.Combine("Ramai.Api", "Composition", "WebTenantsRegistration.cs")) { continue; }
            if (allowed.Contains(Path.GetFileName(file))) { continue; }
            Assert.DoesNotContain("TenantsStoreContext", File.ReadAllText(file));
        }
    }

    [Fact]
    public void Persistence_adapters_reference_only_their_own_model_and_shared_contracts()
    {
        var root = Path.Combine(RepositoryRoot, "src", "backend", "Persistence");
        var projects = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories);
        Assert.Equal(2, projects.Length);
        foreach (var project in projects)
        {
            var identity = Path.GetFileNameWithoutExtension(project) == "Ramai.Modules.Identity.Persistence";
            var allowed = identity ? new[] { "Ramai.Application" } : new[] { "Ramai.Application", "Ramai.Modules.Tenants" };
            var actual = XDocument.Load(project).Descendants("ProjectReference")
                .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")!.Value.Replace('\\', '/')));
            Assert.Equal(allowed.Order(), actual.Order());
            var assembly = Assembly.Load(Path.GetFileNameWithoutExtension(project));
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference =>
                reference.Name?.StartsWith("Ramai.", StringComparison.Ordinal) == true && !allowed.Contains(reference.Name));
        }
    }
}

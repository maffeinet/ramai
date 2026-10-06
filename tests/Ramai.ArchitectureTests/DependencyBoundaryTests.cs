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
        AssertReferences("Ramai.Api", ["Ramai.Application", "Ramai.Infrastructure"]);
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
}

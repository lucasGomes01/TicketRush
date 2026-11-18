using System.Reflection;
using NetArchTest.Rules;
using TicketRush.Modules.Events;
using TicketRush.Modules.Identity;
using TicketRush.Modules.Orders;
using TicketRush.Modules.Ticketing;
using TicketRush.Shared.Modules;

namespace TicketRush.ArchitectureTests;

/// <summary>
/// Garante as regras do monolito modular: um módulo só conversa com outro através do
/// projeto *.Contracts, e nada além da classe do módulo é público.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly Assembly[] ModuleAssemblies =
    [
        typeof(EventsModule).Assembly,
        typeof(TicketingModule).Assembly,
        typeof(OrdersModule).Assembly,
        typeof(IdentityModule).Assembly,
    ];

    public static TheoryData<string> Modules => new(ModuleAssemblies.Select(a => a.GetName().Name!));

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_should_not_reference_other_modules_implementation(string moduleName)
    {
        var assembly = ModuleAssemblies.Single(a => a.GetName().Name == moduleName);
        var otherModules = ModuleAssemblies.Where(a => a != assembly).Select(a => a.GetName().Name!).ToHashSet();

        var forbidden = assembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(otherModules.Contains)
            .ToList();

        Assert.True(forbidden.Count == 0, $"{moduleName} referencia diretamente: {string.Join(", ", forbidden)}. Use o projeto Contracts.");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Only_the_module_entry_point_should_be_public(string moduleName)
    {
        var assembly = ModuleAssemblies.Single(a => a.GetName().Name == moduleName);

        var result = Types.InAssembly(assembly)
            .That().ArePublic()
            .Should().ImplementInterface(typeof(IModule))
            .GetResult();

        Assert.True(result.IsSuccessful, $"Tipos públicos indevidos: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Orders_domain_should_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(typeof(OrdersModule).Assembly)
            .That().ResideInNamespace("TicketRush.Modules.Orders.Domain")
            .ShouldNot().HaveDependencyOnAny(
                "TicketRush.Modules.Orders.Infrastructure",
                "TicketRush.Modules.Orders.Features",
                "Npgsql",
                "Dapper",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, $"Domínio com dependências indevidas: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Each_module_should_ship_its_own_migrations(string moduleName)
    {
        var assembly = ModuleAssemblies.Single(a => a.GetName().Name == moduleName);
        var scripts = assembly.GetManifestResourceNames().Where(n => n.Contains(".Database.Scripts.") && n.EndsWith(".sql"));

        Assert.NotEmpty(scripts);
    }
}

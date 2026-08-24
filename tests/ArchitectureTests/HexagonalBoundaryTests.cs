using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ModularMonolith.ArchitectureTests;

/// <summary>
/// Executable architecture rules for ALL modules. Adding a module means adding
/// ONE entry to ModuleAssemblies below — every rule then applies automatically.
/// </summary>
public class HexagonalBoundaryTests
{
    private static readonly string[] ModuleNames =
    [
        "Identity", "Catalog", "Orders", "Customer", "Payment",
    ];

    private static Assembly AppAssembly(string m) => m switch
    {
        "Identity" => typeof(ModularMonolith.Modules.Identity.Application.DependencyInjection).Assembly,
        "Catalog" => typeof(ModularMonolith.Modules.Catalog.Application.DependencyInjection).Assembly,
        "Orders" => typeof(ModularMonolith.Modules.Orders.Application.DependencyInjection).Assembly,
        "Customer" => typeof(ModularMonolith.Modules.Customer.Application.DependencyInjection).Assembly,
        "Payment" => typeof(ModularMonolith.Modules.Payment.Application.DependencyInjection).Assembly,
        _ => throw new ArgumentOutOfRangeException(nameof(m)),
    };

    private static Assembly OutboundAdapterAssembly(string m) => m switch
    {
        "Identity" => typeof(ModularMonolith.Modules.Identity.Adapter.Outbound.DependencyInjection).Assembly,
        "Catalog" => typeof(ModularMonolith.Modules.Catalog.Adapter.Outbound.DependencyInjection).Assembly,
        "Orders" => typeof(ModularMonolith.Modules.Orders.Adapter.Outbound.DependencyInjection).Assembly,
        "Customer" => typeof(ModularMonolith.Modules.Customer.Adapter.Outbound.DependencyInjection).Assembly,
        "Payment" => typeof(ModularMonolith.Modules.Payment.Adapter.Outbound.DependencyInjection).Assembly,
        _ => throw new ArgumentOutOfRangeException(nameof(m)),
    };

    private static Assembly InboundRestAssembly(string m) => m switch
    {
        "Identity" => typeof(ModularMonolith.Modules.Identity.Adapter.Inbound.Rest.Endpoints.IEndpoint).Assembly,
        "Catalog" => typeof(ModularMonolith.Modules.Catalog.Adapter.Inbound.Rest.Endpoints.IEndpoint).Assembly,
        "Orders" => typeof(ModularMonolith.Modules.Orders.Adapter.Inbound.Rest.Endpoints.IEndpoint).Assembly,
        "Customer" => typeof(ModularMonolith.Modules.Customer.Adapter.Inbound.Rest.Endpoints.IEndpoint).Assembly,
        "Payment" => typeof(ModularMonolith.Modules.Payment.Adapter.Inbound.Rest.Endpoints.IEndpoint).Assembly,
        _ => throw new ArgumentOutOfRangeException(nameof(m)),
    };

    private static Assembly QueryAppAssembly(string m) => m switch
    {
        "Identity" => typeof(ModularMonolith.Modules.Identity.QueryApplication.DependencyInjection).Assembly,
        "Catalog" => typeof(ModularMonolith.Modules.Catalog.QueryApplication.DependencyInjection).Assembly,
        "Orders" => typeof(ModularMonolith.Modules.Orders.QueryApplication.DependencyInjection).Assembly,
        "Customer" => typeof(ModularMonolith.Modules.Customer.QueryApplication.DependencyInjection).Assembly,
        "Payment" => typeof(ModularMonolith.Modules.Payment.QueryApplication.DependencyInjection).Assembly,
        _ => throw new ArgumentOutOfRangeException(nameof(m)),
    };

    [Fact]
    public void Application_Should_Not_Depend_On_Adapters()
    {
        foreach (var m in ModuleNames)
        {
            var result = Types.InAssembly(AppAssembly(m))
                .ShouldNot().HaveDependencyOnAny(ModuleNames.Select(n => $"ModularMonolith.Modules.{n}.Adapter").ToArray())
                .GetResult();
            result.IsSuccessful.Should().BeTrue($"Application ({m}) must not depend on any Adapter.");
        }
    }

    [Fact]
    public void Application_Should_Not_Depend_On_EntityFrameworkCore()
    {
        foreach (var m in ModuleNames)
        {
            var result = Types.InAssembly(AppAssembly(m))
                .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult();
            result.IsSuccessful.Should().BeTrue($"Application ({m}) must not depend on EF Core — only Outbound adapters do.");
        }
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Frameworks()
    {
        foreach (var m in ModuleNames)
        {
            var result = Types.InAssembly(AppAssembly(m))
                .ShouldNot().HaveDependencyOnAny(
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.AspNetCore",
                    "Wolverine")
                .GetResult();
            result.IsSuccessful.Should().BeTrue($"Application ({m}), which contains Domain, must be pure.");
        }
    }

    [Fact]
    public void No_Module_Application_Can_Reference_Another_Module_Application()
    {
        foreach (var consumer in ModuleNames)
            foreach (var provider in ModuleNames.Where(n => n != consumer))
            {
                var result = Types.InAssembly(AppAssembly(consumer))
                    .ShouldNot().HaveDependencyOn($"ModularMonolith.Modules.{provider}")
                    .GetResult();
                result.IsSuccessful.Should().BeTrue(
                    $"{consumer}.Application must NOT depend on {provider}.* — module boundary violation. " +
                    "Cross-module communication only through Contracts + ACL ports.");
            }
    }

    /// <summary>
    /// The ONLY sanctioned cross-module Application references are the ACL gateway
    /// adapters: Orders→Catalog and Payment→Customer. Every other assembly
    /// (including all Applications and all other Adapters) is forbidden.
    /// </summary>
    [Theory]
    [InlineData("Catalog", "Orders")]
    [InlineData("Customer", "Payment")]
    public void Only_The_Sanctioned_Gateway_Adapter_May_Reference_Provider_Application(string provider, string consumer)
    {
        var forbiddenAssemblies = new List<Assembly>();
        foreach (var m in ModuleNames)
        {
            // Other modules' hexagons may never touch the provider.
            if (m != provider && m != consumer) forbiddenAssemblies.Add(AppAssembly(m));
            // The CONSUMER's REST adapter must go through its own application, not around it.
            if (m == consumer) forbiddenAssemblies.Add(InboundRestAssembly(m));
            // No query app may read another module's internals.
            if (m != provider) forbiddenAssemblies.Add(QueryAppAssembly(m));
            // NOTE: consumer's Adapter.Outbound is deliberately EXCLUDED — that is
            // the sanctioned ACL gateway. Provider's own assemblies self-reference legally.
        }

        var providerRoot = $"ModularMonolith.Modules.{provider}";
        foreach (var assembly in forbiddenAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot().HaveDependencyOn(providerRoot)
                .GetResult();
            result.IsSuccessful.Should().BeTrue(
                $"{assembly.GetName().Name} must not reference {providerRoot}.* — " +
                $"only {consumer}.Adapter.Outbound may (ACL gateway exception).");
        }
    }

    [Fact]
    public void Adapters_Must_Not_Depend_On_Other_Modules_Adapters()
    {
        foreach (var m in ModuleNames)
            foreach (var other in ModuleNames.Where(n => n != m))
            {
                var otherAdapterPrefix = $"ModularMonolith.Modules.{other}.Adapter";
                foreach (var adapter in new[] { OutboundAdapterAssembly(m), InboundRestAssembly(m) })
                {
                    var result = Types.InAssembly(adapter)
                        .ShouldNot().HaveDependencyOn(otherAdapterPrefix)
                        .GetResult();
                    result.IsSuccessful.Should().BeTrue(
                        $"{m} Adapter must not depend on {otherAdapterPrefix} — only on {other}.Application (gateway pattern).");
                }
            }
    }

    [Fact]
    public void No_Module_Should_Use_Wolverine()
    {
        foreach (var m in ModuleNames)
            foreach (var assembly in new[] { AppAssembly(m), OutboundAdapterAssembly(m), InboundRestAssembly(m), QueryAppAssembly(m) })
            {
                var result = Types.InAssembly(assembly)
                    .ShouldNot().HaveDependencyOn("Wolverine")
                    .GetResult();
                result.IsSuccessful.Should().BeTrue($"No Wolverine anywhere — {assembly.GetName().Name}.");
            }
    }

    [Fact]
    public void Inbound_Ports_Must_Be_Interfaces()
    {
        foreach (var m in ModuleNames)
        {
            var result = Types.InAssembly(AppAssembly(m))
                .That().ResideInNamespace($"ModularMonolith.Modules.{m}.Application.Ports.Inbound")
                .Should().BeInterfaces()
                .GetResult();
            result.IsSuccessful.Should().BeTrue($"Inbound ports must be interfaces ({m}).");
        }
    }

    [Fact]
    public void Outbound_Ports_Must_Be_Interfaces()
    {
        foreach (var m in ModuleNames)
        {
            var result = Types.InAssembly(AppAssembly(m))
                .That().ResideInNamespace($"ModularMonolith.Modules.{m}.Application.Ports.Outbound")
                .Should().BeInterfaces()
                .GetResult();
            result.IsSuccessful.Should().BeTrue($"Outbound ports must be interfaces ({m}).");
        }
    }

    [Fact]
    public void Services_Must_Be_Sealed()
    {
        foreach (var m in ModuleNames)
        {
            var result = Types.InAssembly(AppAssembly(m))
                .That().ResideInNamespace($"ModularMonolith.Modules.{m}.Application.Service")
                .Should().BeSealed()
                .GetResult();
            result.IsSuccessful.Should().BeTrue($"Service implementations must be sealed ({m}).");
        }
    }

    [Fact]
    public void QueryApplication_Must_Be_Isolated_From_Other_Modules_And_Infrastructure()
    {
        foreach (var m in ModuleNames)
        {
            var forbidden = ModuleNames.Where(n => n != m)
                .Select(n => $"ModularMonolith.Modules.{n}")
                .Append("ModularMonolith.Infrastructure.Persistence")
                .ToArray();

            var result = Types.InAssembly(QueryAppAssembly(m))
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();
            result.IsSuccessful.Should().BeTrue(
                $"{m}.QueryApplication must be isolated: no other modules, no shared Infrastructure.Persistence.");
        }
    }

    /// <summary>
    /// Read-only enforcement (mechanical): a QueryApplication may not touch ANY of
    /// its own module's write-side outbound ports — UnitOfWork OR repositories.
    /// Combined with Application-isolation rules, the only remaining write vector
    /// is a direct DbContext.SaveChangesAsync call, which NetArchTest cannot see;
    /// that vector is covered by convention + code review (see AGENTS.md pitfalls).
    /// </summary>
    [Fact]
    public void QueryApplication_Must_Not_Reference_Write_Side_Outbound_Ports()
    {
        foreach (var m in ModuleNames)
        {
            var result = Types.InAssembly(QueryAppAssembly(m))
                .ShouldNot().HaveDependencyOn($"ModularMonolith.Modules.{m}.Application.Ports.Outbound")
                .GetResult();
            result.IsSuccessful.Should().BeTrue(
                $"{m}.QueryApplication is read-only — no UnitOfWork or repository ports may be referenced.");
        }
    }
}

// ─── Reporting module (ADR-0006): dedicated cross-module READ composition context ───

public class ReportingBoundaryTests
{
    private static readonly string[] ModuleNames = ["Identity", "Catalog", "Orders", "Customer", "Payment"];

    private static Assembly ReportingApp =>
        typeof(ModularMonolith.Modules.Reporting.Application.DependencyInjection).Assembly;
    private static Assembly ReportingOutbound =>
        typeof(ModularMonolith.Modules.Reporting.Adapter.Outbound.DependencyInjection).Assembly;
    private static Assembly ReportingGraphQL =>
        typeof(ModularMonolith.Modules.Reporting.Adapter.Inbound.GraphQL.DependencyInjection).Assembly;

    /// <summary>
    /// THE read-only guarantee for the composition context: NO Reporting assembly may
    /// reference ANY write-side Application. Composition touches provider QueryApplications
    /// only (via Reporting's own outbound ports). Violations = report could mutate data.
    /// </summary>
    [Fact]
    public void Reporting_Must_Never_Reference_Write_Side_Applications()
    {
        var forbidden = ModuleNames.Select(m => $"ModularMonolith.Modules.{m}.Application").ToArray();
        foreach (var asm in new[] { ReportingApp, ReportingOutbound, ReportingGraphQL })
        {
            var result = Types.InAssembly(asm)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();
            result.IsSuccessful.Should().BeTrue(
                $"{asm.GetName().Name} must never reference write-side Applications — reporting is provably read-only.");
        }
    }

    /// <summary>Only the outbound ACL adapter may see provider read sides — GraphQL and app layer stay blind.</summary>
    [Fact]
    public void Only_Reporting_Adapter_Outbound_May_Reference_Provider_Read_Sides()
    {
        var providerReadSides = new[]
        {
            "ModularMonolith.Modules.Customer.QueryApplication",
            "ModularMonolith.Modules.Payment.QueryApplication",
        };

        foreach (var asm in new[] { ReportingApp, ReportingGraphQL })
        foreach (var providerReadSide in providerReadSides)
        {
            var result = Types.InAssembly(asm)
                .ShouldNot().HaveDependencyOn(providerReadSide)
                .GetResult();
            result.IsSuccessful.Should().BeTrue(
                $"{asm.GetName().Name} must not reference {providerReadSide} — only Reporting.Adapter.Outbound may.");
        }
    }

    /// <summary>Reporting must not reach provider ADAPTERS (DbContexts, gateways are off-limits).</summary>
    [Fact]
    public void Reporting_Must_Not_Depend_On_Provider_Adapters()
    {
        foreach (var m in ModuleNames)
        foreach (var asm in new[] { ReportingApp, ReportingOutbound, ReportingGraphQL })
        {
            var result = Types.InAssembly(asm)
                .ShouldNot().HaveDependencyOn($"ModularMonolith.Modules.{m}.Adapter")
                .GetResult();
            result.IsSuccessful.Should().BeTrue($"{asm.GetName().Name} must not depend on {m}.Adapter.");
        }
    }

    /// <summary>Reporting owns no data: no EF Core anywhere except… nowhere. Even the adapter is EF-free.</summary>
    [Fact]
    public void Reporting_Must_Not_Depend_On_EntityFrameworkCore()
    {
        foreach (var asm in new[] { ReportingApp, ReportingOutbound, ReportingGraphQL })
        {
            var result = Types.InAssembly(asm)
                .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult();
            result.IsSuccessful.Should().BeTrue(
                $"{asm.GetName().Name} is a composition context — it must not touch any database directly.");
        }
    }
}
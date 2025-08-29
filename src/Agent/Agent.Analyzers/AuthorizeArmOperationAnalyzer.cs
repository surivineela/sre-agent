using System.Collections.Immutable;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Agent.Analyzers;

/// <summary>
/// Flags ASP.NET Core controller action methods that expose external HTTP APIs
/// (methods annotated with HttpMethodAttribute on controllers) but do not declare
/// an AuthorizeArmOperation attribute.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AuthorizeArmOperationAnalyzer : DiagnosticAnalyzer
{
    private const string DiagnosticId = "CUSTOM004";
    private const string Title = "HTTP action must declare AuthorizeArmOperation";
    private static readonly string MessageFormat = "Controller HTTP action '{0}' must be decorated with [AuthorizeArmOperation]";
    private const string Description = "External HTTP APIs must require an ARM operation via [AuthorizeArmOperation].";
    private const string Category = "Validation";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var methodDecl = (MethodDeclarationSyntax)context.Node;

        // Quick exit: only public instance methods are considered controller actions
        if (!methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) ||
            methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            return;
        }

        var semanticModel = context.SemanticModel;
        if (semanticModel.GetDeclaredSymbol(methodDecl) is not IMethodSymbol methodSymbol)
        {
            return;
        }

        var containingType = methodSymbol.ContainingType;
        if (containingType is null)
        {
            return;
        }

        // Determine if containing type is a Controller (derives from ControllerBase) or is annotated with [ApiController]
        bool isController = InheritsFrom(containingType, "Microsoft.AspNetCore.Mvc.ControllerBase") ||
                            HasAttribute(containingType, a => SymbolEqualsName(a.AttributeClass, "Microsoft.AspNetCore.Mvc.ApiControllerAttribute") ||
                                                             SymbolEqualsName(a.AttributeClass, "ApiControllerAttribute"));

        if (!isController)
        {
            return; // Not a controller class; ignore
        }

        // Determine if method is an HTTP endpoint by checking for attributes derived from HttpMethodAttribute
        bool isHttpEndpoint = methodSymbol.GetAttributes().Any(ad => InheritsFrom(ad.AttributeClass, "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute"));

        if (!isHttpEndpoint)
        {
            return; // Not an external HTTP method
        }

        // Check for presence of AuthorizeArmOperationAttribute
        bool hasAuthorizeArmOperation = methodSymbol.GetAttributes().Any(ad =>
            SymbolEqualsName(ad.AttributeClass, "AuthorizeArmOperationAttribute"));

        if (!hasAuthorizeArmOperation)
        {
            var diagnostic = Diagnostic.Create(Rule, methodDecl.Identifier.GetLocation(), methodSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool HasAttribute(ISymbol symbol, Func<AttributeData, bool> predicate)
        => symbol.GetAttributes().Any(predicate);

    private static bool InheritsFrom(INamedTypeSymbol? symbol, string baseTypeMetadataName)
    {
        if (symbol is null)
        {
            return false;
        }

        // Walk base types
        for (var current = symbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualsName(current, baseTypeMetadataName))
            {
                return true;
            }
        }

        // Also check interfaces in case the metadata name refers to an interface
        foreach (var iface in symbol.AllInterfaces)
        {
            if (SymbolEqualsName(iface, baseTypeMetadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SymbolEqualsName(INamespaceOrTypeSymbol? symbol, string metadataOrSimpleName)
    {
        if (symbol is null)
        {
            return false;
        }

        // Compare by fully qualified display string
        var full = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (full.EndsWith(metadataOrSimpleName, StringComparison.Ordinal))
        {
            return true;
        }

        // Fallback to simple name
        return string.Equals(symbol.Name, metadataOrSimpleName, StringComparison.Ordinal);
    }
}

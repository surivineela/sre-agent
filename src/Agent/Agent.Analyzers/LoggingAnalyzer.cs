using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Agent.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class LoggingAnalyzer : DiagnosticAnalyzer
{
    private const string DiagnosticId = "CUSTOM002";
    private const string Title = "LoggerExtension methods must be used for logging";
    private static readonly string MessageFormat = $"All logging should be performed via the LoggerExtension methods.";
    private const string Description = "All logging should be performed via the LoggerExtension methods.";
    private const string Category = "Validation";


    private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var invocationExpression = (InvocationExpressionSyntax)context.Node;

        // Check if the method being called is "LogInformation"
        var memberAccess = invocationExpression.Expression as MemberAccessExpressionSyntax;
        if (memberAccess == null || (memberAccess.Name.Identifier.Text != "LogInformation" && memberAccess.Name.Identifier.Text != "LogWarning" && memberAccess.Name.Identifier.Text != "LogError"))
        {
            return;
        }

        // Get the symbol for the method being called
        var semanticModel = context.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

        if (methodSymbol == null || (methodSymbol.Name != "LogInformation" && methodSymbol.Name != "LogWarning" && methodSymbol.Name != "LogError"))
        {
            return;
        }

        // Check if the project is a first-party app project
        if (!IsFirstPartyProject(context.Compilation))
        {
            // Report a diagnostic if the project is not a first-party app project
            var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsFirstPartyProject(Compilation compilation)
    {
        // Example: Check if the assembly name starts with "FirstPartyAgent"
        var assemblyName = compilation.AssemblyName;
        return assemblyName != null && assemblyName.StartsWith("FirstPartyAgent");
    }
}

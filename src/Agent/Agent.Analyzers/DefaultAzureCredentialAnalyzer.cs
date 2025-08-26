using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Agent.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DefaultAzureCredentialAnalyzer : DiagnosticAnalyzer
{
    private const string DiagnosticId = "CUSTOM003";
    private const string Title = "DefaultAzureCredential use in Production";
    private static readonly string MessageFormat = "DefaultAzureCredential may only be used in non-production environments.Use IAuthenticationService for credentials";
    private const string Description = "Use IAuthenticationService for credentials instead of directly constructing DefaultAzureCredential.";
    private const string Category = "Validation";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
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

        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // Resolve constructed type via semantic model
        var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation);
        if (typeInfo.Type is not INamedTypeSymbol constructedType)
        {
            return;
        }

        if (constructedType.Name == "DefaultAzureCredential"
            && constructedType.ContainingNamespace?.ToDisplayString() == "Azure.Identity")
        {
            var diagnostic = Diagnostic.Create(Rule, objectCreation.Type.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}

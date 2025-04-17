using System.Collections.Immutable;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Agent.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DescriptionAttributeLengthAnalyzer : DiagnosticAnalyzer
{
    private const string DiagnosticId = "CUSTOM001";
    private const int MaxAllowedLength = 1024;
    private const string Title = "Description Attribute Length Exceeded";
    private static readonly string MessageFormat = $"The Description attribute exceeds the maximum allowed length of {MaxAllowedLength} characters. Current length: {{0}}.";
    private const string Description = "Description attribute can't be longer than 1024 characters.";
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

        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.Attribute);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var attributeSyntax = (AttributeSyntax)context.Node;
        if (!attributeSyntax.Name.ToString().Contains("Description"))
        {
            return;
        }

        // Check if this is a Foo attribute
        // Get the attribute arguments
        var argumentList = attributeSyntax.ArgumentList;
        if (argumentList == null || !argumentList.Arguments.Any())
            return; // No arguments to check

        // Check the first argument for a string literal
        var firstArgument = argumentList.Arguments[0];
        if (firstArgument.Expression is LiteralExpressionSyntax literalExpression &&
            literalExpression.Kind() == SyntaxKind.StringLiteralExpression)
        {
            // Get the string value
            string attributeValue = literalExpression.Token.ValueText;

            // Check if the string is too long
            if (attributeValue.Length > MaxAllowedLength)
            {
                // Report the diagnostic
                var diagnostic = Diagnostic.Create(
                    Rule,
                    literalExpression.GetLocation(),
                    attributeValue.Length,
                    MaxAllowedLength);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}

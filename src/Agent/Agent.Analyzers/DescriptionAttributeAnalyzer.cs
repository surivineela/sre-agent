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

    // Per test, gpt-4.1 and gpt-5 can at least support description length of 16384 now (before it was 1024).
    // There's no documentation about the length limit. Set to a reansonable high value to be a safe guard.
    private const int MaxAllowedLength = 16384;
    private const string Title = "Description Attribute Length Exceeded";
    private static readonly string MessageFormat = $"The Description attribute exceeds the maximum allowed length of {MaxAllowedLength} characters. Current length: {{0}}.";
    private const string Description = "Description attribute can't be longer than 16384 characters.";
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

        // Get the attribute arguments
        var argumentList = attributeSyntax.ArgumentList;
        if (argumentList == null || !argumentList.Arguments.Any())
            return; // No arguments to check

        // Check the first argument
        var firstArgument = argumentList.Arguments[0];
        var concatenatedString = ExtractConcatenatedString(firstArgument.Expression, context);

        if (concatenatedString != null && concatenatedString.Length > MaxAllowedLength)
        {
            // Report the diagnostic
            var diagnostic = Diagnostic.Create(
                Rule,
                firstArgument.Expression.GetLocation(),
                concatenatedString.Length);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static string? ExtractConcatenatedString(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
    {
        return expression switch
        {
            // Handle simple string literals
            LiteralExpressionSyntax literalExpression when literalExpression.Kind() == SyntaxKind.StringLiteralExpression
                => literalExpression.Token.ValueText,

            // Handle binary expressions (string concatenation with +)
            BinaryExpressionSyntax binaryExpression when binaryExpression.Kind() == SyntaxKind.AddExpression
                => ConcatenateBinaryExpression(binaryExpression, context),

            // Handle parenthesized expressions
            ParenthesizedExpressionSyntax parenthesizedExpression
                => ExtractConcatenatedString(parenthesizedExpression.Expression, context),

            // Handle identifier name expressions (const string variables)
            IdentifierNameSyntax identifierName
                => ExtractConstStringValue(identifierName, context),

            // Handle member access expressions (like ClassName.ConstString)
            MemberAccessExpressionSyntax memberAccess
                => ExtractConstStringFromMemberAccess(memberAccess, context),

            // Return null for unsupported expression types (method calls, etc.)
            _ => null
        };
    }

    private static string? ConcatenateBinaryExpression(BinaryExpressionSyntax binaryExpression, SyntaxNodeAnalysisContext context)
    {
        var leftString = ExtractConcatenatedString(binaryExpression.Left, context);
        var rightString = ExtractConcatenatedString(binaryExpression.Right, context);

        // If either side is not a computable string literal, we can't determine the length
        if (leftString == null || rightString == null)
            return null;

        return leftString + rightString;
    }

    private static string? ExtractConstStringValue(IdentifierNameSyntax identifierName, SyntaxNodeAnalysisContext context)
    {
        var symbolInfo = context.SemanticModel.GetSymbolInfo(identifierName);
        if (symbolInfo.Symbol is IFieldSymbol fieldSymbol &&
            fieldSymbol.IsConst &&
            fieldSymbol.Type.SpecialType == SpecialType.System_String &&
            fieldSymbol.ConstantValue is string constantValue)
        {
            return constantValue;
        }

        return null;
    }

    private static string? ExtractConstStringFromMemberAccess(MemberAccessExpressionSyntax memberAccess, SyntaxNodeAnalysisContext context)
    {
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
        if (symbolInfo.Symbol is IFieldSymbol fieldSymbol &&
            fieldSymbol.IsConst &&
            fieldSymbol.Type.SpecialType == SpecialType.System_String &&
            fieldSymbol.ConstantValue is string constantValue)
        {
            return constantValue;
        }

        return null;
    }
}

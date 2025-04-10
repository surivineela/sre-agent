
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator(LanguageNames.CSharp)]
public class DescriptionAttributeValidationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get all attribute syntax nodes
        var attributeSyntaxProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (syntaxNode, _) => syntaxNode is AttributeSyntax,
                transform: (syntaxContext, _) => (AttributeSyntax)syntaxContext.Node)
            .Where(attributeSyntax => attributeSyntax.Name.ToString().Contains("Description"));

        // Combine with compilation to resolve symbols
        var compilationAndAttributes = context.CompilationProvider.Combine(attributeSyntaxProvider.Collect());

        // Register the source output
        context.RegisterSourceOutput(compilationAndAttributes, (sourceProductionContext, tuple) =>
        {
            var (compilation, attributes) = tuple;

            // Get the DescriptionAttribute symbol
            var descriptionAttributeSymbol = compilation.GetTypeByMetadataName(typeof(System.ComponentModel.DescriptionAttribute).FullName);
            if (descriptionAttributeSymbol == null)
            {
                return;
            }

            // Analyze attributes
            foreach (var attribute in attributes)
            {
                var semanticModel = compilation.GetSemanticModel(attribute.SyntaxTree);
                var symbolInfo = semanticModel.GetSymbolInfo(attribute);
                if (symbolInfo.Symbol is not IMethodSymbol methodSymbol ||
                    !SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, descriptionAttributeSymbol))
                {
                    continue;
                }

                // Get the description argument
                var argumentList = attribute.ArgumentList;
                if (argumentList?.Arguments.Count > 0)
                {
                    var descriptionArgument = argumentList.Arguments[0];
                    var constantValue = semanticModel.GetConstantValue(descriptionArgument.Expression);
                    if (constantValue.HasValue && constantValue.Value is string descriptionValue)
                    {
                        // descriptionValue is the concatenated string
                        if (descriptionValue.Length > 1024)
                        {
                            // Report a diagnostic if the description exceeds 1024 characters
                            var diagnostic = Diagnostic.Create(
                                new DiagnosticDescriptor(
                                    id: "DESC001",
                                    title: "Description Attribute Length Exceeded",
                                    messageFormat: "The Description attribute exceeds the maximum allowed length of 1024 characters. Current length: " + descriptionValue.Length,
                                    category: "Validation",
                                    DiagnosticSeverity.Error,
                                    isEnabledByDefault: true),
                                descriptionArgument.Expression.GetLocation());

                            sourceProductionContext.ReportDiagnostic(diagnostic);
                        }
                    }
                    else
                    {
                        // Report a diagnostic if the description is not a string literal
                        var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                id: "DESC002",
                                title: "Invalid Description Attribute",
                                messageFormat: "The Description attribute must be a string literal.",
                                category: "Validation",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            descriptionArgument.Expression.GetLocation());

                        sourceProductionContext.ReportDiagnostic(diagnostic);
                    }
                }
            }
        });
    }
}

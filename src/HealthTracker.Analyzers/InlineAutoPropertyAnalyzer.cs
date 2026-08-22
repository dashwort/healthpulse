using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace HealthTracker.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class InlineAutoPropertyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "HP0001";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            "Inline simple auto-property",
            "Simple auto-property should be declared on one line",
            "Formatting",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Keep auto-properties containing only get/set or get/init accessors on one line.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
        }

        private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            var property = (PropertyDeclarationSyntax)context.Node;
            var accessors = property.AccessorList?.Accessors;

            if (accessors is null || accessors.Value.Count != 2 ||
                accessors.Value.Any(accessor => accessor.Body is not null || accessor.ExpressionBody is not null) ||
                !accessors.Value.Any(accessor => accessor.Kind() == SyntaxKind.GetAccessorDeclaration) ||
                !accessors.Value.Any(accessor => accessor.Kind() is SyntaxKind.SetAccessorDeclaration or SyntaxKind.InitAccessorDeclaration))
            {
                return;
            }

            var lineSpan = Location.Create(
                property.SyntaxTree,
                TextSpan.FromBounds(property.Type.SpanStart, property.AccessorList!.Span.End)).GetLineSpan();
            if (lineSpan.StartLinePosition.Line != lineSpan.EndLinePosition.Line)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, property.GetLocation()));
            }
        }
    }
}

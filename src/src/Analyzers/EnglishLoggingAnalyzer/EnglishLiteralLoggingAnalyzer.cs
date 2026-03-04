using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EnglishLoggingAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnglishLiteralLoggingAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ENG001";
        private static readonly LocalizableString Title = "Non-English literal in logging or exception";
        private static readonly LocalizableString MessageFormat = "Literal contains non-ASCII/Non-English characters: '{0}'";
        private static readonly LocalizableString Description = "Log messages, exception messages and Console.WriteLine should use English to avoid encoding/locale issues.";
        private const string Category = "Localization";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Get symbol to identify method
            var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol == null) return;

            var containing = symbol.ContainingType?.Name;
            var methodName = symbol.Name;

            // Interested in: System.Console.WriteLine, Serilog.Log.*, Microsoft.Extensions.Logging ILogger.LogXXX (skip), and any static Log class
            bool isConsole = containing == "Console" && methodName == "WriteLine";
            bool isSerilogLog = containing == "Log"; // Serilog static Log class or other Log classes

            if (!isConsole && !isSerilogLog)
            {
                // also check for 'throw new Exception("...")' elsewhere via object creation
                return;
            }

            // inspect arguments for string literals or interpolated strings
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                var expr = arg.Expression;
                CheckExpressionForNonEnglish(expr, context);
            }
        }

        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var creation = (ObjectCreationExpressionSyntax)context.Node;
            var typeSymbol = context.SemanticModel.GetSymbolInfo(creation.Type).Symbol as ITypeSymbol;
            if (typeSymbol == null) return;

            // target exception creation: System.Exception or types deriving from Exception
            var baseType = typeSymbol.BaseType;
            bool isException = false;
            var t = typeSymbol;
            while (t != null)
            {
                if (t.ToDisplayString() == "System.Exception") { isException = true; break; }
                t = t.BaseType;
            }

            if (!isException) return;

            // check constructor arguments
            foreach (var arg in creation.ArgumentList?.Arguments ?? default(SeparatedSyntaxList<ArgumentSyntax>))
            {
                CheckExpressionForNonEnglish(arg.Expression, context);
            }
        }

        private static void CheckExpressionForNonEnglish(ExpressionSyntax expr, SyntaxNodeAnalysisContext context)
        {
            if (expr is LiteralExpressionSyntax les && les.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var text = les.Token.ValueText;
                if (ContainsNonAsciiOrCJK(text))
                {
                    var diag = Diagnostic.Create(Rule, les.GetLocation(), text.Length > 40 ? text.Substring(0, 40) + "..." : text);
                    context.ReportDiagnostic(diag);
                }
            }
            else if (expr is InterpolatedStringExpressionSyntax ises)
            {
                var full = ises.Contents.ToString();
                if (ContainsNonAsciiOrCJK(full))
                {
                    var diag = Diagnostic.Create(Rule, ises.GetLocation(), full.Length > 40 ? full.Substring(0, 40) + "..." : full);
                    context.ReportDiagnostic(diag);
                }
            }
        }

        private static bool ContainsNonAsciiOrCJK(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (var ch in s)
            {
                if (ch > 127) return true; // non-ASCII
                // optionally detect CJK range
                if (ch >= 0x4E00 && ch <= 0x9FFF) return true;
            }
            return false;
        }
    }
}

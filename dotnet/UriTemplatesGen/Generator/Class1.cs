using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace UriTemplatesGen
{
    [Generator]
    public class MySourceGenerator : ISourceGenerator
    {
        public void Execute(SourceGeneratorContext context)
        {
            var syntaxReceiver = (SyntaxReceiver)context.SyntaxReceiver;
            //Debug.WriteLine("hell");
            //Debugger.Launch();
            // begin creating the source we'll inject into the users compilation
            var sourceBuilder = new StringBuilder(@"
using System;

namespace HelloWorldGenerated
{
    public static class HelloWorld
    {
        public static void SayHello(Action<string> writeLine) 
        {
            writeLine(""Hello from generated code!"");
            writeLine(""The following syntax trees existed in the compilation that created this program:"");
");
            sourceBuilder.AppendLine("writeLine(\"" + DateTime.Now + "\");");

            // using the context, get a list of syntax trees in the users compilation
            var syntaxTrees = context.Compilation.SyntaxTrees;

            // add the filepath of each tree to the class we're building
            foreach (SyntaxTree tree in syntaxTrees)
            {
                sourceBuilder.AppendLine($@"writeLine(@"" - {tree.FilePath}"");");
            }

            // finish creating the source to inject
            sourceBuilder.Append(@"
        }
    }
}");
            // inject the created source into the users compilation
            context.AddSource("helloWorldGenerator2", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
        }

        public void Initialize(InitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
            // No initialization required for this one
        }
    }

    public class SyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (!(syntaxNode is ClassDeclarationSyntax classDeclaration))
            {
                return;
            }

            if (classDeclaration.BaseList == null)
            {
                return;
            }

            if (!classDeclaration.BaseList.Types.Any())
            {
                return;
            }

            // Only add this class if it extends a type prefixed with "I".
            var targetType = "I" + classDeclaration.Identifier.Text;

            foreach (var baseType in classDeclaration.BaseList.Types)
            {
                if (baseType.Type is IdentifierNameSyntax parentName)
                {
                    if (parentName.Identifier.ValueText == targetType)
                    {
                        CandidateClasses.Add(classDeclaration);
                        return;
                    }
                }
            }
        }
    }

}
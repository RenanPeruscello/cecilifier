﻿using System;
using System.Collections.Generic;
using System.Linq;
using Cecilifier.Core.Extensions;
using Cecilifier.Core.Misc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil.Cil;

namespace Cecilifier.Core.AST
{
	class MethodDeclarationVisitor : SyntaxWalkerBase
	{
		public MethodDeclarationVisitor(IVisitorContext context) : base(context)
		{
		}

	    public override void VisitBlock(BlockSyntax node)
	    {
		    StatementVisitor.Visit(Context, ilVar, node);
	    }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
		{
			ProcessMethodDeclaration(node, node.Identifier.ValueText, MethodNameOf(node), ResolveType(node.ReturnType), _ => base.VisitMethodDeclaration(node));
		}

		public override void VisitParameter(ParameterSyntax node)
		{
			var declaringMethodName = ".ctor";
			if (node.Parent.Parent.IsKind(SyntaxKind.MethodDeclaration))
			{
				var declaringMethod = (MethodDeclarationSyntax) node.Parent.Parent;
				declaringMethodName = declaringMethod.Identifier.ValueText;
			}
			
			var paramVar = TempLocalVar(node.Identifier.ValueText); 
			
			Context.DefinitionVariables.Register(declaringMethodName, node.Identifier.ValueText, MemberKind.Parameter, paramVar);
			
			var exps = CecilDefinitionsFactory.Parameter(node, Context.SemanticModel, Context.DefinitionVariables.GetVariable(declaringMethodName, MemberKind.Method).VariableName, paramVar, ResolveType(node.Type));
			AddCecilExpressions(exps);
			
			base.VisitParameter(node);
		}

		public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
		{
			AddCecilExpression("[PropertyDeclaration] {0}", node);
			base.VisitPropertyDeclaration(node);
		}

	    protected void ProcessMethodDeclaration<T>(T node, string simpleName, string fqName, string returnType, Action<string> runWithCurrent) where T : BaseMethodDeclarationSyntax
		{
			var declaringTypeName = DeclaringTypeNameFor(node);

			var methodVar = MethodExtensions.LocalVariableNameFor(declaringTypeName, simpleName, node.MangleName(Context.SemanticModel));

			AddOrUpdateMethodDefinition(methodVar, fqName, node.Modifiers.MethodModifiersToCecil(ModifiersToCecil, GetSpecificModifiers(), DeclaredSymbolFor(node)), returnType);
			AddCecilExpression("{0}.Methods.Add({1});", Context.DefinitionVariables.GetLastOf(MemberKind.Type).VariableName, methodVar);

			var isAbstract = DeclaredSymbolFor(node).IsAbstract;
			if (!isAbstract)
			{
				ilVar = MethodExtensions.LocalVariableNameFor("il", declaringTypeName, simpleName, node.MangleName(Context.SemanticModel));
				AddCecilExpression(@"var {0} = {1}.Body.GetILProcessor();", ilVar, methodVar);
			}

			WithCurrentNode(node, methodVar, fqName, runWithCurrent);

			//TODO: Move this to default ctor handling and rely on VisitReturnStatement here instead
			if (!isAbstract && !node.DescendantNodes().Any(n => n.Kind() == SyntaxKind.ReturnStatement))
			{
				AddCilInstruction(ilVar, OpCodes.Ret);
			}
		}

		protected static string DeclaringTypeNameFor<T>(T node) where T : BaseMethodDeclarationSyntax
		{
			var declaringType = (TypeDeclarationSyntax) node.Parent;
			return declaringType.Identifier.ValueText;
		}

		private void AddOrUpdateMethodDefinition(string methodVar, string fqName, string methodModifiers, string returnType)
		{
			if (Context.Contains(methodVar))
			{
				AddCecilExpression("{0}.Attributes = {1};", methodVar, methodModifiers);
				AddCecilExpression("{0}.HasThis = !{0}.IsStatic;", methodVar);
			}
			else
			{
				AddMethodDefinition(Context, methodVar, fqName, methodModifiers, returnType);
			}
		}

		public static void AddMethodDefinition(IVisitorContext context, string methodVar, string fqName, string methodModifiers, string returnType)
		{
			context.WriteCecilExpression($"var {methodVar} = new MethodDefinition(\"{fqName}\", {methodModifiers}, {returnType});\r\n");
			context[methodVar] = "";
		}

		protected virtual string GetSpecificModifiers()
		{
			return null;
		}

		private string MethodNameOf(MethodDeclarationSyntax method)
		{
			return DeclaredSymbolFor(method).Name;
		}

	    protected string ilVar;
	}

    internal class IfStatementVisitor : SyntaxWalkerBase
    {
        private static string _ilVar;

	    private IfStatementVisitor(IVisitorContext ctx) : base(ctx)
        {
        }

        internal static void Visit(IVisitorContext context, string ilVar, IfStatementSyntax node)
        {
            _ilVar = ilVar;
            node.Accept(new IfStatementVisitor(context));
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            WriteCecilExpression(Context, $"// if ({node.Condition})");
            ExpressionVisitor.Visit(Context, _ilVar, node.Condition);

	        var elsePrologVarName = TempLocalVar("esp");
            WriteCecilExpression(Context, $"var {elsePrologVarName} = {_ilVar}.Create(OpCodes.Nop);");
            WriteCecilExpression(Context, $"{_ilVar}.Append({_ilVar}.Create(OpCodes.Brfalse, {elsePrologVarName}));");

            WriteCecilExpression(Context, "//if body");
            StatementVisitor.Visit(Context, _ilVar, node.Statement);

	        var elseEndTargetVarName = TempLocalVar("ese");
            WriteCecilExpression(Context, $"var {elseEndTargetVarName} = {_ilVar}.Create(OpCodes.Nop);");
            if (node.Else != null)
            {
                
                var branchToEndOfIfStatementVarName = LocalVariableNameForId(NextLocalVariableTypeId());
                WriteCecilExpression(Context, $"var {branchToEndOfIfStatementVarName} = {_ilVar}.Create(OpCodes.Br, {elseEndTargetVarName});");
                WriteCecilExpression(Context, $"{_ilVar}.Append({branchToEndOfIfStatementVarName});");

                WriteCecilExpression(Context, $"{_ilVar}.Append({elsePrologVarName});");
                ExpressionVisitor.Visit(Context, _ilVar, node.Else);
            }
            else
            {
                WriteCecilExpression(Context, $"{_ilVar}.Append({elsePrologVarName});");
            }

            WriteCecilExpression(Context, $"{_ilVar}.Append({elseEndTargetVarName});");
            WriteCecilExpression(Context, $"{Context.DefinitionVariables.GetLastOf(MemberKind.Method).VariableName}.Body.OptimizeMacros();");
        }
    }
	
	internal class StatementVisitor : SyntaxWalkerBase
	{
		internal StatementVisitor(IVisitorContext ctx) : base(ctx)
		{
		}
		
		internal static void Visit(IVisitorContext context, string ilVar, StatementSyntax node)
		{
			_ilVar = ilVar;
			node.Accept(new StatementVisitor(context));
		}

		public override void VisitBlock(BlockSyntax node)
		{
			using (Context.DefinitionVariables.EnterScope())
			{
				base.VisitBlock(node);
			}
		}
		
		public override void VisitReturnStatement(ReturnStatementSyntax node)
		{
			ExpressionVisitor.Visit(Context, _ilVar, node.Expression);
			AddCilInstruction(_ilVar, OpCodes.Ret);
		}

		public override void VisitExpressionStatement(ExpressionStatementSyntax node)
		{
			ExpressionVisitor.Visit(Context, _ilVar, node);
		}

		public override void VisitIfStatement(IfStatementSyntax node)
		{
			IfStatementVisitor.Visit(Context, _ilVar, node);
		}

		public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
		{
			var methodVar = Context.DefinitionVariables.GetLastOf(MemberKind.Method).VariableName;
			foreach(var localVar in node.Declaration.Variables)
			{
				AddLocalVariable(node.Declaration.Type, localVar, methodVar);
				ProcessVariableInitialization(localVar);
			}
		}

		private void AddLocalVariable(TypeSyntax type, VariableDeclaratorSyntax localVar, string methodVar)
		{
			string resolvedVarType = type.IsVar 
				? ResolveExpressionType(localVar.Initializer.Value)
				: ResolveType(type);

			var cecilVarDeclName = TempLocalVar($"lv_{localVar.Identifier.ValueText}");
			AddCecilExpression("var {0} = new VariableDefinition({1});", cecilVarDeclName, resolvedVarType);
			AddCecilExpression("{0}.Body.Variables.Add({1});", methodVar, cecilVarDeclName);

			Context.DefinitionVariables.Register(string.Empty, localVar.Identifier.ValueText, MemberKind.LocalVariable, cecilVarDeclName);
		}

		private void ProcessVariableInitialization(VariableDeclaratorSyntax localVar)
		{
			if (ExpressionVisitor.Visit(Context, _ilVar, localVar.Initializer)) return;

			var localVarDef = Context.DefinitionVariables.GetVariable(localVar.Identifier.ValueText, MemberKind.LocalVariable);
			AddCilInstruction(_ilVar, OpCodes.Stloc, localVarDef.VariableName);
		}

		private static string _ilVar;
	}
}

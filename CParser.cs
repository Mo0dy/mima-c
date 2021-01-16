﻿using mima_c.ast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mima_c
{
    using ASTList = List<dynamic>;

    abstract class Parser
    {
        private TokenStream TokenStream { get; }

        public Parser(TokenStream tokenStream)
        {
            TokenStream = tokenStream;
        }

        public abstract AST Parse();

        protected Token Eat(TokenType expectedToken)
        {
            return TokenStream.Eat(expectedToken);
        }
        protected string EatV(TokenType expectedToken)
        {
            return TokenStream.Eat(expectedToken).value;
        }

        protected bool PeekEatIf(TokenType expectedToken, int n = 0)
        {
            if (!PeekType(expectedToken))
                return false;
            Eat(expectedToken);
            return true;
        }

        protected Token Peek(int n = 0)
        {
            return TokenStream.Peek(n);
        }

        protected bool PeekType(TokenType expectedToken, int n = 0)
        {
            return Peek(n).tokenType == expectedToken;
        }
    }

    class TypeScope
    {
        TypeScope parent;

        HashSet<string> types;

        public TypeScope(TypeScope parent)
        {
            this.parent = parent;
            this.types = new HashSet<string>();
        }

        public bool Defined(string identifier)
        {
            return types.Contains(identifier) || (parent != null && parent.Defined(identifier));
        }

        public bool AddType(string type)
        {
            if (Defined(type))
                throw new ArgumentException("Type allready defined! Type: " + type);

            types.Add(type);
            return true;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("TypeScope:\n");

            foreach (var item in types)
            {
                builder.Append(item);
                builder.Append("\n");
            }

            return builder.ToString();
        }

        public static TypeScope GetBasicTypeScope()
        {
            TypeScope typeScope = new TypeScope(null);

            typeScope.AddType("int");
            typeScope.AddType("float");
            typeScope.AddType("double");
            typeScope.AddType("char");

            return typeScope;
        }
    }

    class CParser : Parser
    {
        public CParser(TokenStream tokenStream) : base(tokenStream)
        {
        }

        public override AST Parse()
        {
            TypeScope typeScope = TypeScope.GetBasicTypeScope();

            Eat(TokenType.BOS);
            AST node = program(typeScope);
            Eat(TokenType.EOS);
            return node;
        }

        private string type(TypeScope typeScope)
        {
            string type = EatV(TokenType.IDENTIFIER);

            if (!typeScope.Defined(type))
                throw new KeyNotFoundException("Type of " + type + " was not defined in the current Scope!");

            return type;
        }

        private AST program(TypeScope typeScope)
        {
            ASTList statements = new ASTList();

            while (!PeekType(TokenType.EOS))
                statements.Add(statement(typeScope));

            return new Program(statements);
        }

        private AST statement(TypeScope typeScope)
        {
            if (PeekType(TokenType.IDENTIFIER) && PeekType(TokenType.IDENTIFIER, 1))
            {
                if (PeekType(TokenType.LPAREN, 2))
                    return funcdecl(typeScope);
                else
                {
                    AST node = vardecl(typeScope);
                    Eat(TokenType.SEMICOLON);
                    return node;
                }
            }
            // unhandled error condition
            Eat(TokenType.UNDEFINED);
            return null;
        }

        private AST funcdecl(TypeScope typeScope)
        {
            // IDENTIFIER IDENTIFIER LPAREN (RPAREN | vardecl (COMMA vardecl)*)

            string returnType = type(typeScope);
            string identifier = EatV(TokenType.IDENTIFIER);

            Eat(TokenType.LPAREN);

            List<FuncDecl.Parameter> parameters = new List<FuncDecl.Parameter>();

            if (!PeekType(TokenType.RPAREN))
            {
                funcdeclprime(typeScope, parameters);

                while (PeekEatIf(TokenType.COMMA))
                    funcdeclprime(typeScope, parameters);
            }

            Eat(TokenType.RPAREN);

            ASTList statements = new ASTList();
            statements.Add(new FuncDecl(returnType, identifier, parameters));

            if (PeekType(TokenType.LBRACE))
                statements.Add(new FuncDef(returnType, identifier, parameters, block(typeScope) as BlockStatements));
            else
                Eat(TokenType.SEMICOLON);

            return new Statements(statements);
        }

        private void funcdeclprime(TypeScope typeScope, List<FuncDecl.Parameter> parameters)
        {
            string varType = type(typeScope);
            string varIdentifier = EatV(TokenType.IDENTIFIER);
            parameters.Add(new FuncDecl.Parameter(varType, varIdentifier));
        }

        private AST vardecl(TypeScope typeScope)
        {
            string varType = type(typeScope);

            ASTList statements = new ASTList();
            vardeclprime(typeScope, statements, varType);

            while (PeekEatIf(TokenType.COMMA))
                vardeclprime(typeScope, statements, varType);

            return new Statements(statements);
        }

        private void vardeclprime(TypeScope typeScope, ASTList statements, string varType)
        {
            string identifier;

            if (!PeekType(TokenType.STAR))
                statements.Add(vardeclprimeprime(typeScope, out identifier, varType));
            else
            {
                AST node = vardeclprimeprime(typeScope, out identifier, varType);
                while (PeekEatIf(TokenType.STAR))
                    node = new PointerDecl(node, EatV(TokenType.IDENTIFIER));
                statements.Add(node);
            }

            if (PeekEatIf(TokenType.ASSIGN))
                statements.Add(new VariableAssign(new Variable(identifier), expr(typeScope)));
        }

        private AST vardeclprimeprime(TypeScope typeScope, out string identifier, string varType)
        {
            identifier = EatV(TokenType.IDENTIFIER);

            if (PeekEatIf(TokenType.LBRACKET))
            {
                AST countExpr = null;
                if (!PeekEatIf(TokenType.RBRACKET))
                {
                    countExpr = expr(typeScope);
                    Eat(TokenType.RBRACKET);
                }
                return new ArrayDecl(varType, identifier, countExpr);
            }
            else
                return new VariableDecl(varType, identifier);
        }

        // one extra level of recursion so it's easy to extend expr
        private AST expr(TypeScope typeScope)
        {
            return assignment(typeScope);
        }

        private AST assignment(TypeScope typeScope)
        {
            AST node = ternary(typeScope);

            if (PeekEatIf(TokenType.ASSIGN))
                node = new VariableAssign(node, assignment(typeScope));
            else if (Peek().tokenType.In(TokenType.PLUSEQ, TokenType.MINUSEQ, TokenType.MODULOEQ, TokenType.STAREQ, TokenType.DIVIDEEQ))
            {
                TokenType op = Peek().tokenType;
                Eat(op);
                node = new OperationAssign(node, Operator.Parse(op), assignment(typeScope));
            }

            return node;
        }

        private AST ternary(TypeScope typeScope)
        {
            AST node = p9(typeScope);

            if (PeekEatIf(TokenType.QUESTIONMARK))
            {
                AST ifBlock = expr(typeScope);
                Eat(TokenType.COLON);
                AST elseBlock = expr(typeScope);
                node = new Ternary(node, ifBlock, elseBlock);
            }

            return node;
        }

        private AST p9(TypeScope typeScope)
        {
            return parseBinary(typeScope, p8, TokenType.AND);
        }

        private AST p8(TypeScope typeScope)
        {
            return parseBinary(typeScope, p7, TokenType.OR);
        }

        private AST p7(TypeScope typeScope)
        {
            return parseBinary(typeScope, p6, TokenType.EQ, TokenType.NEQ);
        }

        private AST p6(TypeScope typeScope)
        {
            return parseBinary(typeScope, p4, TokenType.LT, TokenType.GT, TokenType.LEQ, TokenType.GEQ);
        }

        private AST p4(TypeScope typeScope)
        {
            return parseBinary(typeScope, p3, TokenType.PLUS, TokenType.MINUS);
        }

        private AST p3(TypeScope typeScope)
        {
            return parseBinary(typeScope, unary, TokenType.STAR, TokenType.DIVIDE, TokenType.MODULO);
        }

        private AST unary(TypeScope typeScope)
        {
            if (PeekEatIf(TokenType.MINUSMINUS))
                return new UnaryArithm(Operator.Parse(TokenType.MINUSMINUS), postfix(typeScope));
            if (PeekEatIf(TokenType.PLUSPLUS))
                return new UnaryArithm(Operator.Parse(TokenType.PLUSPLUS), postfix(typeScope));

            if (PeekEatIf(TokenType.MINUS))
                return new UnaryArithm(Operator.Parse(TokenType.MINUS), postfix(typeScope));
            if (PeekEatIf(TokenType.PLUS))
                return new UnaryArithm(Operator.Parse(TokenType.PLUS), postfix(typeScope));


            if (PeekType(TokenType.LPAREN) && typeScope.Defined(Peek(1).value))
            {
                Eat(TokenType.LPAREN);
                string castType = type(typeScope);
                Eat(TokenType.RPAREN);
                return new Cast(castType, unary(typeScope));
            }

            if (PeekEatIf(TokenType.STAR))
                return new PointerAccess(unary(typeScope));
            if (PeekEatIf(TokenType.AMPERSAND))
                return new PointerLiteral(unary(typeScope));

            if (PeekEatIf(TokenType.NOT))
                return new UnaryArithm(Operator.Parse(TokenType.NOT), unary(typeScope));
            if (PeekEatIf(TokenType.LNOT))
                return new UnaryArithm(Operator.Parse(TokenType.LNOT), unary(typeScope));

            return postfix(typeScope);
        }

        private AST postfix(TypeScope typeScope)
        {
            AST node = value(typeScope);

            if (PeekEatIf(TokenType.MINUSMINUS))
                node = new PostfixArithm(Operator.Parse(TokenType.MINUSMINUS), node);
            if (PeekEatIf(TokenType.PLUSPLUS))
                node = new PostfixArithm(Operator.Parse(TokenType.PLUSPLUS), node);
            if (PeekEatIf(TokenType.DOT))
                node = new StructAccess(TokenType.DOT, node);
            if (PeekEatIf(TokenType.ARROW))
                node = new StructAccess(TokenType.ARROW, node);

            return node;
        }

        private AST value(TypeScope typeScope)
        {
            if (Literal.tokenToType.ContainsKey(Peek().tokenType))
            {
                Token token = Eat(Peek().tokenType);
                return new Literal(Literal.tokenToType[token.tokenType], token.value);
            }
            if (PeekType(TokenType.LBRACE))
            {
                return arrayLiteral(typeScope);
            }
            if (PeekType(TokenType.IDENTIFIER))
            {
                if (PeekType(TokenType.LPAREN, 1))
                    return functionCall(typeScope);
                else if (PeekType(TokenType.LBRACKET, 1))
                    return arrayAccess(typeScope);
                else
                    return new Variable(EatV(TokenType.IDENTIFIER));
            }

            Eat(TokenType.LPAREN);
            AST node = expr(typeScope);
            Eat(TokenType.RPAREN);
            return node;
        }

        private AST arrayLiteral(TypeScope typeScope)
        {
            Eat(TokenType.LBRACE);
            ASTList expressions = readExprList(typeScope, TokenType.RBRACE);
            Eat(TokenType.RBRACE);
            return new ArrayLiteral(expressions);
        }

        private AST functionCall(TypeScope typeScope)
        {
            string identifier = EatV(TokenType.IDENTIFIER);

            Eat(TokenType.LPAREN);
            ASTList arguments = readExprList(typeScope, TokenType.RPAREN);
            Eat(TokenType.RPAREN);

            return new FuncCall(identifier, arguments);
        }

        private AST arrayAccess(TypeScope typeScope)
        {
            string identifier = EatV(TokenType.IDENTIFIER);

            Eat(TokenType.LBRACKET);
            AST indexExpr = expr(typeScope);
            Eat(TokenType.RBRACKET);

            return new ArrayAccess(identifier, indexExpr);
        }

        private AST block(TypeScope typeScope)
        {
            TypeScope blockScope = new TypeScope(typeScope);
            Eat(TokenType.LBRACE);

            ASTList blockStatements = new ASTList();
            while (!PeekType(TokenType.RBRACE))
                blockStatements.Add(blockStatement(blockScope));

            Eat(TokenType.RBRACE);

            return new BlockStatements(blockStatements);
        }

        private AST blockStatement(TypeScope typeScope)
        {
            // NOTE: ONLY RETURN DIRECTLY IF NO SEMICOLON IS NEEDED

            if (PeekType(TokenType.LBRACE))
                return block(typeScope);
            if (PeekType(TokenType.FOR))
                return for_(typeScope);
            if (PeekType(TokenType.WHILE))
                return while_(typeScope);
            if (PeekType(TokenType.IF))
                return if_(typeScope);

            AST node;
            if (PeekType(TokenType.BREAK))
                node = break_();
            else if (PeekType(TokenType.CONTINUE))
                node = continue_();
            else if (PeekType(TokenType.RETURN))
                node = return_(typeScope);
            else if (PeekType(TokenType.INTRINSIC))
                node = intrinsic(typeScope);
            else if (PeekType(TokenType.IDENTIFIER) && PeekType(TokenType.IDENTIFIER, 1))
                node = vardecl(typeScope);
            else
                node = expr(typeScope);

            Eat(TokenType.SEMICOLON);
            return node;
        }

        private AST for_(TypeScope typeScope)
        {
            Eat(TokenType.FOR);
            Eat(TokenType.LPAREN);

            AST initialization;
            if (PeekType(TokenType.IDENTIFIER) && PeekType(TokenType.IDENTIFIER, 1))
                initialization = vardecl(typeScope);
            else
                initialization = expr(typeScope);
            Eat(TokenType.SEMICOLON);
            AST condition = expr(typeScope);
            Eat(TokenType.SEMICOLON);
            AST execution = expr(typeScope);
            Eat(TokenType.RPAREN);

            AST body = blockStatement(typeScope);

            return new For(initialization, condition, execution, body);
        }

        private AST while_(TypeScope typeScope)
        {
            Eat(TokenType.WHILE);
            Eat(TokenType.LPAREN);
            AST condition = expr(typeScope);
            Eat(TokenType.RPAREN);
            AST body = blockStatement(typeScope);

            return new While(condition, body);
        }

        private AST if_(TypeScope typeScope)
        {
            Eat(TokenType.IF);
            Eat(TokenType.LPAREN);
            AST condition = expr(typeScope);
            Eat(TokenType.RPAREN);
            AST ifBody = blockStatement(typeScope);

            AST elseBody;
            if (PeekType(TokenType.ELSE))
            {
                Eat(TokenType.ELSE);
                elseBody = blockStatement(typeScope);
            }
            else
                elseBody = new Statements(new ASTList());

            return new If(condition, ifBody, elseBody);
        }

        private AST break_()
        {
            Eat(TokenType.BREAK);
            return new Break();
        }

        private AST continue_()
        {
            Eat(TokenType.CONTINUE);
            return new Continue();
        }

        private AST return_(TypeScope typeScope)
        {
            Eat(TokenType.RETURN);
            return new Return(expr(typeScope));
        }

        private AST intrinsic(TypeScope typeScope)
        {
            string type = EatV(TokenType.INTRINSIC);

            Eat(TokenType.LPAREN);
            ASTList parameters = readExprList(typeScope, TokenType.RPAREN);
            Eat(TokenType.RPAREN);

            return new Intrinsic(parameters, type);
        }

        private AST parseBinary(TypeScope typeScope, Func<TypeScope, AST> nextNodeFunc, params TokenType[] types)
        {
            AST node1 = nextNodeFunc(typeScope);

            while (types.ToList().Contains(Peek().tokenType))
            {
                TokenType type = Peek().tokenType;
                Eat(type);
                AST node2 = nextNodeFunc(typeScope);
                node1 = new BinaryArithm(Operator.Parse(type), node1, node2);
            }

            return node1;
        }

        private ASTList readExprList(TypeScope typeScope, TokenType delimiter)
        {
            ASTList expressions = new ASTList();

            if (!PeekType(delimiter))
            {
                expressions.Add(expr(typeScope));
                while (PeekEatIf(TokenType.COMMA))
                    expressions.Add(expr(typeScope));
            }

            return expressions;
        }

    }
}

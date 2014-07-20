﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Mond.Compiler.Expressions
{
    class BlockExpression : Expression, IBlockExpression, IStatementExpression
    {
        public ReadOnlyCollection<Expression> Statements { get; private set; }

        public BlockExpression(Token token, IList<Expression> statements)
            : base(token.FileName, token.Line)
        {
            Statements = new ReadOnlyCollection<Expression>(statements);
        }

        public BlockExpression(IList<Expression> statements)
            : this(new Token(null, -1, TokenType.Eof, null), statements)
        {
            
        }

        public override void Print(int indent)
        {
            foreach (var statement in Statements)
            {
                statement.Print(indent);
            }
        }

        public override int Compile(FunctionContext context)
        {
            foreach (var statement in Statements)
            {
                var stack = statement.Compile(context);
                
                while (stack > 0)
                {
                    context.Drop();
                    stack--;
                }
            }

            return 0;
        }

        public override Expression Simplify()
        {
            Statements = Statements
                .Select(s => s.Simplify())
                .ToList()
                .AsReadOnly();

            return this;
        }

        public override void SetParent(Expression parent)
        {
            base.SetParent(parent);

            foreach (var statement in Statements)
            {
                statement.SetParent(this);
            }
        }
    }
}
//
//  Main.cs
//
//  Author:
//       Nikolay Orlyuk <virkony@gmail.com>
//
//  Copyright (c) 2012 (c) Nikolay Orlyuk
//
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
//
using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace neForth
{
    public class Compiler
    {
        private readonly LinkedList<ParameterExpression> inputs = new LinkedList<ParameterExpression>();
        private readonly LinkedList<Expression> words = new LinkedList<Expression>();
        private readonly LinkedList<ParameterExpression> interm = new LinkedList<ParameterExpression>();
        private readonly LinkedList<Expression> outputs = new LinkedList<Expression>();


        private Expression[] PopFrame(params Type[] types)
        {
            /*
            Contract.Requires(
                    Contract.ForAll(outputs.Zip(types, Tuple.Create),
                                x => x.Item2.IsEquivalentTo(x.Item1.Type))
            );
            Contract.Ensures(
                Contract.Result<Expression[]>().Length == types.Length);
            Contract.Ensures(
                Contract.Result<Expression[]>()
                    .Zip(types, Tuple.Create, (e, t) => t.IsEquivalentTo(e.Type))
                    .All());
            */
            Debug.Assert(types != null);

            Debug.Assert(
                outputs
                .Zip(types, (e, t) => t.IsAssignableFrom(e.Type))
                .All(x => x)
            );

            var frame = new Expression[types.Length];
            int i = 0;

            for (; i < types.Length; ++i)
            {
                var arg = outputs.First.Value;
                var type = types[i];

                // TODO: more sophisticated compile-time checks
                if (type != arg.Type && (type.IsValueType || arg.Type.IsValueType))
                {
                    arg = Expression.Convert(arg, type);
                }

                frame[i] = arg;
                outputs.RemoveFirst();
            }

            int j = inputs.Count;
            foreach (var type in types.Skip(i))
            {
                var param = Expression.Parameter(type, "i" +  (j++));
                inputs.AddLast(param);
                frame[i++] = param;
            }

            Debug.Assert(
                frame
                .Zip(types, (e, t) => t.IsEquivalentTo(e.Type))
                .All(x => x)
            );

            return frame;
        }


        public void Lit<T>(T value)
        {
            //Contract.Ensures(outputs == Contract.OldValue(outputs.Count) + 1);
            //Contract.Ensures(outputs.First.Value.Type == typeof(T));

            outputs.AddFirst(Expression.Constant(value, typeof(T)));
        }

        public void Compile<T>(Expression<Action<T>> action)
        {
            Contract.Requires(action != null);
            //Contract.Requires(typeof(T).IsEquivalentTo(outputs.First.Value.Type));

            words.AddLast(Expression.Invoke(action, PopFrame(typeof(T))));
        }

        public void Compile<TA,TB,TC>(Expression<Func<TA,TB,TC>> func)
        {
            Contract.Requires(func != null);
            //Contract.Ensures(outputs.First.Value.Type == typeof(TC));
            //Contract.Ensures(Contract.ValueAtReturn(out outputs.First).Value.Type == typeof(TC));

            var args = PopFrame(typeof(TA), typeof(TB));

            var result = Expression.Variable(typeof(TC), "o" + interm.Count);
            interm.AddLast(result);
            words.AddLast(Expression.Assign(result, Expression.Invoke(func, args)));

            outputs.AddFirst(result);
        }

        public Expression<TDelegate> ComposeLambda<TDelegate>()
        {
            //Contract.Ensures(Contract.Result<object>() != null);

            Console.WriteLine(string.Join("\n",words.Select(x => x.ToString())));

            var body = Expression.Block(interm, words).Reduce();

            // TODO: resolve outputs
            return Expression.Lambda<TDelegate>(body, inputs);
        }
    }

    class MainClass
    {
        public static void Main(string[] args)
        {
            Compiler t = new Compiler();
            t.Lit("Hello World");
            t.Lit(2);
            t.Lit(3);
            t.Compile((int x, int y) => x+y);
            t.Compile((object x) => Console.WriteLine(x));
            t.Compile((object x) => Console.WriteLine(x));
            t.ComposeLambda<Action>().Compile()();
        }
    }
}

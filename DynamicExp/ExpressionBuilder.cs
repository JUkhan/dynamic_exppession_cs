using Microsoft.EntityFrameworkCore;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;


namespace DynamicExp
{
    /// <summary>
    /// Dynamic expression builder from string query.
    /// </summary>
    public static class ExpressionBuilder
    {
        /// <summary>
        /// Filters a sequence of values based on a specified condition string.
        /// </summary>
        /// <typeparam name="T">The type of elements in the sequence.</typeparam>
        /// <param name="data">The sequence of values to filter.</param>
        /// <param name="str">The condition string to filter the sequence.</param>
        /// <param name="isSql">A flag indicating whether the condition string is SQL syntax. Default is false.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> that contains elements from the input sequence that satisfy the condition.</returns>
        public static IEnumerable<T> Where<T>(this IEnumerable<T> data, string str, bool isSql = false)
        {
            ExpressionBuilder.isSql = isSql;
            var predicate = GetExpression<T>(str).Compile();
            return from T value in data
                   where predicate(value)
                   select value;
        }

        /// <summary>
        /// Filters the elements of an <see cref="IQueryable{T}"/> based on a specified condition string.
        /// </summary>
        /// <typeparam name="T">The type of elements in the IQueryable.</typeparam>
        /// <param name="query">The IQueryable to filter.</param>
        /// <param name="str">The condition string to filter the IQueryable.</param>
        /// <param name="isSql">A flag indicating whether the condition string is SQL syntax. Default is false.</param>
        /// <returns>An IQueryable that contains elements from the input sequence that satisfy the condition.</returns>
        public static IQueryable<T> Where<T>(this IQueryable<T> query, string str, bool isSql = false)
        {
            ExpressionBuilder.isSql = isSql;
            return query.Where<T>(GetExpression<T>(str));
        }

        /// <summary>
        /// like operator for in memory operation.
        /// </summary>
        /// <param name="source">source.</param>
        /// <param name="find">find.</param>
        /// <param name="flag">flag</param>
        /// <returns>a bool value.</returns>
        public static bool Like(string source, string find, int flag)
        {

            if (flag == 1)
            {
                return source.Contains(find, StringComparison.CurrentCultureIgnoreCase);
            }
            else if (flag == 2)
            {
                return source.EndsWith(find, StringComparison.CurrentCultureIgnoreCase);
            }
            else if (flag == 3)
            {
                return source.StartsWith(find, StringComparison.CurrentCultureIgnoreCase);
            }

            return source.Equals(find, StringComparison.CurrentCultureIgnoreCase);
        }

        private static Expression<Func<T, bool>> GetExpression<T>(string str)
        {
            var paramExpression = Expression.Parameter(typeof(T));

            try
            {
                var tokens = MapToPostfix(GetTokens(str));

                var stack = new Stack<Expression>();

                int i = 0, len = tokens.Count;
                foreach (var item in tokens)
                {
                    switch (item)
                    {
                        case "not":
                            if (stack.Count > 0)
                            {
                                stack.Push(Expression.Not(stack.Pop()));
                            }

                            break;
                        case "or":
                        case "and":
                            if (stack.Count >= 2)
                            {
                                var right = stack.Pop();
                                var left = stack.Pop();
                                var exp = item == "and" ? Expression.And(left, right) : Expression.Or(left, right);
                                stack.Push(exp);
                            }

                            break;
                        case "=":
                        case "==":
                        case "<":
                        case ">":
                        case "<=":
                        case ">=":
                        case "in":
                        case "like":
                            var colName = tokens[i - 2].Replace("_", string.Empty).ToLower();
                            var exp1 = GetExpression<T>(colName, item.ToLower(), tokens[i - 1], paramExpression);
                            if (exp1 != null)
                            {
                                stack.Push(exp1);
                            }

                            break;
                    }

                    i++;
                }

                if (stack.Count == 0)
                {
                    stack.Push(Expression.Equal(Expression.Constant(1), Expression.Constant(1)));
                }

                var expression = stack.Pop();
                if (expression.CanReduce)
                {
                    expression = expression.ReduceAndCheck();
                }

                var res = Expression.Lambda<Func<T, bool>>(expression, paramExpression);

                return res;
            }
            catch (Exception)
            {
                return Expression.Lambda<Func<T, bool>>(Expression.Equal(Expression.Constant(1), Expression.Constant(1)), paramExpression);
            }
        }

        private static Expression GetExpression<T>(string colName, string @operator, string constraint, ParameterExpression paramExpression)
        {
            var type = typeof(T);
            var info = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(it => it.Name.ToLower() == colName);

            if (info != null)
            {
                return GetExpression(info, @operator, constraint, paramExpression);
            }
            else
            {
                var cols = colName.Split('.');
                if (cols.Length == 2)
                {
                    colName = cols[1];
                    info = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(it => it.Name.ToLower() == cols[0]);
                    if (info != null)
                    {
                        var exp = GetExpression(info, paramExpression, @operator, colName, constraint);
                        if (exp != null)
                        {
                            return exp;
                        }
                    }
                }
            }

            return null;
        }

        private static Expression GetExpression(PropertyInfo info, ParameterExpression paramExpression, string @operator, string colName, string constraint)
        {
            var property = Expression.Property(paramExpression, info.Name);

            var childInfo = info.PropertyType.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(it => it.Name.ToLower() == colName);
            if (childInfo == null)
            {
                return null;
            }

            BinaryExpression nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(object)));
            var childProperty = Expression.Property(property, childInfo);
            if (@operator == "in")
            {
                return Expression.AndAlso(nullCheck, GetInExp(childInfo, constraint, childProperty));
            }

            if (@operator == "like")
            {
                return Expression.AndAlso(nullCheck, !isSql ? GetLikeExp(constraint, childProperty) : GetSqlLikeExp(constraint, childProperty));
            }

            var constantExpression = GetConstantExpression(childInfo.PropertyType.Name, constraint);

            var expression = GetBinaryExpression(childProperty, @operator, constantExpression);

            return Expression.AndAlso(nullCheck, expression);
        }

        private static Expression GetExpression(PropertyInfo info, string @operator, string constraint, ParameterExpression paramExpression)
        {
            var property = Expression.Property(paramExpression, info.Name);

            if (@operator == "in")
            {
                return GetInExp(info, constraint, property);
            }

            if (@operator == "like")
            {
                return !isSql ? GetLikeExp(constraint, property) : GetSqlLikeExp(constraint, property);
            }

            var constantExpression = GetConstantExpression(info.PropertyType.Name, constraint);

            return GetBinaryExpression(property, @operator, constantExpression);
        }

        private static Expression GetSqlLikeExp(string constraint, MemberExpression property) => Expression.Call(
                      MethodLike,
                      Expression.Constant(EF.Functions),
                      property,
                      Expression.Constant(constraint));

        private static ConstantExpression GetConstantExpression(string typeName, string constraint) =>
               typeName switch
               {
                   "Int32" => Expression.Constant(int.Parse(constraint)),
                   "Int64" => Expression.Constant(long.Parse(constraint)),
                   "Single" => Expression.Constant(float.Parse(constraint)),
                   "Double" => Expression.Constant(double.Parse(constraint)),
                   "Decimal" => Expression.Constant(decimal.Parse(constraint)),
                   "Boolean" => Expression.Constant(bool.Parse(constraint)),
                   "DateTime" => Expression.Constant(DateTime.Parse(constraint)),
                   _ => Expression.Constant(constraint, typeof(string))
               };

        private static BinaryExpression GetBinaryExpression(MemberExpression property, string @operator, ConstantExpression constantExpression) =>
         @operator switch
         {
             "=" => Expression.Equal(property, constantExpression),
             "==" => Expression.Equal(property, constantExpression),
             "<" => Expression.LessThan(property, constantExpression),
             "<=" => Expression.LessThanOrEqual(property, constantExpression),
             ">" => Expression.GreaterThan(property, constantExpression),
             ">=" => Expression.GreaterThanOrEqual(property, constantExpression),
             _ => Expression.Equal(Expression.Constant(1), Expression.Constant(1)),
         };

        private static Expression GetInExp(PropertyInfo propInfo, string constraint, MemberExpression property)
        {
            var list = constraint.Split(',').Select(it => it.Trim());
            object val = list;
            switch (propInfo.PropertyType.Name)
            {
                case "Int32":
                    val = list.Select(it => int.Parse(it));
                    break;
                case "Int64":
                    val = list.Select(it => long.Parse(it));
                    break;
                case "Single":
                    val = list.Select(it => float.Parse(it));
                    break;
                case "Double":
                    val = list.Select(it => double.Parse(it));
                    break;
                case "Decimal":
                    val = list.Select(it => decimal.Parse(it));
                    break;
                case "DateTime":
                    val = list.Select(DateTime.Parse);
                    break;
            }

            var contains = MethodContains.MakeGenericMethod(propInfo.PropertyType);

            var exp = Expression.Call(
                      contains,
                      Expression.Constant(val),
                      property);
            return exp;
        }

        private static Expression GetLikeExp(string constraint, MemberExpression property)
        {
            int len = constraint.Length, flag = 0;
            string query = constraint;
            if (constraint[0] == '%' && constraint[len - 1] == '%')
            {
                len--;
                query = constraint[1..len];
                flag = 1;
            }
            else if (constraint[0] == '%')
            {
                query = constraint[1..];
                flag = 2;
            }
            else if (constraint[len - 1] == '%')
            {
                len--;
                query = constraint[0..len];
                flag = 3;
            }

            var like = typeof(ExpressionBuilder).GetMethod("Like")!;

            var exp = Expression.Call(
                      like,
                      property,
                      Expression.Constant(query),
                      Expression.Constant(flag)
                      );
            return exp;
        }

        private static int CheckOrEqual(string str, int curIndex, char ch, StringBuilder sb, List<string> tokens)
        {
            if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
                sb.Clear();
            }

            if (str[curIndex + 1] == '=')
            {
                tokens.Add(ch.ToString() + "=");
                curIndex++;
            }
            else
            {
                tokens.Add(ch.ToString());
            }

            return curIndex;
        }

        private static List<string> GetTokens(string str)
        {
            int len = str.Length, i = 0;
            var tokens = new List<string>();
            StringBuilder sb = new StringBuilder();

            while (i < len)
            {
                char ch = str[i];

                switch (ch)
                {
                    case '(':
                    case ')':
                        if (sb.Length > 0)
                        {
                            tokens.Add(sb.ToString());
                            sb.Clear();
                        }

                        tokens.Add(ch.ToString());
                        if (ch == '(' && tokens.Count > 2 && tokens[tokens.Count - 2].Equals("in", StringComparison.CurrentCultureIgnoreCase))
                        {
                            while (i + 1 < len && str[i + 1] != ')')
                            {
                                ch = str[i + 1];
                                if (!(ch == '\'' || ch == '"'))
                                {
                                    sb.Append(ch);
                                }

                                i++;
                            }

                            i++;
                            tokens[tokens.Count - 1] = sb.ToString();
                            sb.Clear();
                        }

                        break;
                    case '=':
                    case '<':
                    case '>':
                        i = CheckOrEqual(str, i, ch, sb, tokens);
                        break;

                    case ' ':
                        if (sb.Length > 0)
                        {
                            tokens.Add(sb.ToString());
                            sb.Clear();
                        }

                        while (i + 1 < len && str[i + 1] == ' ')
                        {
                            ch = str[i];
                            i++;
                        }

                        if (ch != ' ')
                        {
                            sb.Append(ch);
                        }

                        break;
                    case '"':
                    case '\'':
                        i++;
                        while (i < len && !(str[i] == '"' || str[i] == '\''))
                        {
                            sb.Append(str[i]);
                            i++;
                        }

                        tokens.Add(sb.ToString());
                        sb.Clear();
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }

                i++;
            }

            if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
            }

            return tokens;
        }

        private static List<string> MapToPostfix(List<string> tokens)
        {
            var res = new List<string>();
            var stack = new Stack<string>();
            foreach (var item in tokens)
            {
                switch (item)
                {
                    case "not":
                    case "NOT":
                    case "Not":
                    case "(":
                        stack.Push(item.ToLower());
                        break;
                    case "AND":
                    case "And":
                    case "and":
                    case "OR":
                    case "Or":
                    case "or":
                        if (stack.Count > 0)
                        {
                            res.Add(stack.Pop());
                        }
                        stack.Push(item.ToLower());
                        break;
                    case "=":
                    case "==":
                    case "<":
                    case ">":
                    case "<=":
                    case ">=":
                    case "IN":
                    case "In":
                    case "in":
                    case "LIKE":
                    case "Like":
                    case "like":
                        stack.Push(item.ToLower());
                        break;
                    case ")":
                        while (stack.Count > 0)
                        {
                            var operand = stack.Pop();
                            if (operand == "(") { break; }
                            res.Add(operand);
                        }

                        break;
                    default:
                        res.Add(item);
                        break;

                }
            }

            while (stack.Count > 0)
            {
                res.Add(stack.Pop());
            }

            return res;
        }

        private static readonly MethodInfo MethodContains = typeof(Enumerable).GetMethods(
                        BindingFlags.Static | BindingFlags.Public)
                        .Single(m => m.Name == nameof(Enumerable.Contains)
                            && m.GetParameters().Length == 2);

        private static readonly MethodInfo MethodLike = typeof(DbFunctionsExtensions).GetMethods().Single(m => m.Name == nameof(DbFunctionsExtensions.Like) && m.GetParameters().Length == 3);

        private static bool isSql = true;
    }
 
}

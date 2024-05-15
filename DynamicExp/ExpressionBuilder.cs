using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore;

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
        /// <param name="query">The condition string to filter the sequence.</param>
        /// <param name="isSql">A flag indicating whether the condition string is SQL syntax. Default is false.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> that contains elements from the input sequence that satisfy the condition.</returns>
        public static IEnumerable<T> Where<T>(this IEnumerable<T> data, string query, bool isSql = true)
        {
            var predicate = GetExpression<T>(query, isSql).Compile();
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
        public static IQueryable<T> Where<T>(this IQueryable<T> query, string str, bool isSql = true) => query.Where<T>(GetExpression<T>(str, isSql));

        /// <summary>
        /// Performs a case-insensitive "like" comparison between two strings based on the specified LikeMode.
        /// </summary>
        /// <param name="source">The source string to compare.</param>
        /// <param name="find">The string to find within the source string.</param>
        /// <param name="likeMode">The mode of comparison, specifying how the find string should match the source string.</param>
        /// <returns>True if the strings match based on the specified LikeMode; otherwise, false.</returns>
        public static bool Like(string source, string find, LikeMode likeMode)
        {
            if (likeMode == LikeMode.Contains)
            {
                return source.Contains(find, StringComparison.CurrentCultureIgnoreCase);
            }
            else if (likeMode == LikeMode.EndsWith)
            {
                return source.EndsWith(find, StringComparison.CurrentCultureIgnoreCase);
            }
            else if (likeMode == LikeMode.StartsWith)
            {
                return source.StartsWith(find, StringComparison.CurrentCultureIgnoreCase);
            }

            return source.Equals(find, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Creates a dynamic LINQ expression based on a string representing a conditional statement.
        /// </summary>
        /// <typeparam name="T">The type of elements for which the expression is generated.</typeparam>
        /// <param name="str">The string representing the conditional statement.</param>
        /// <param name="isSql">A flag indicating whether the condition string is SQL syntax. Default is false.</param>
        /// <returns>An expression representing the dynamic LINQ condition.</returns>
        public static Expression<Func<T, bool>> GetExpression<T>(string str, bool isSql)
        {
            ExpressionBuilder.isSql = isSql;
            var paramExpression = Expression.Parameter(typeof(T));

            try
            {
                var tokens = MapToPostfix(GetTokens(str));

                var stack = new Stack<Expression>();

                int i = 0;
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
                                var exp = item == "and" ? Expression.AndAlso(left, right) : Expression.OrElse(left, right);
                                stack.Push(exp);
                            }

                            break;
                        case "=":
                        case "<>":
                        case "<":
                        case ">":
                        case "<=":
                        case ">=":
                        case "in":
                        case "like":
                            var binaryExp = GetExpression<T>(tokens[i - 2].Replace("_", string.Empty).ToLower(), item, tokens[i - 1], paramExpression);
                            if (binaryExp != null)
                            {
                                stack.Push(binaryExp);
                            }

                            break;
                        case "between":
                            var betweenExp = GetBetweenExpression<T>(tokens[i - 3].Replace("_", string.Empty).ToLower(), tokens[i - 2], tokens[i - 1], paramExpression);
                            if (betweenExp != null)
                            {
                                stack.Push(betweenExp);
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
                        return GetExpression(info, paramExpression, @operator, colName, constraint);
                    }
                }
            }

            return null;
        }

        private static Expression BetweenOtherTypeHelper(PropertyInfo info, string start, string end, MemberExpression member)
        {
            switch (info.PropertyType.Name)
            {
                case "Int32":
                    return Expression.AndAlso(GetBinaryExpression(member, ">=", Expression.Constant(int.Parse(start))), GetBinaryExpression(member, "<=", Expression.Constant(int.Parse(end))));

                case "Int64":
                    return Expression.AndAlso(GetBinaryExpression(member, ">=", Expression.Constant(long.Parse(start))), GetBinaryExpression(member, "<=", Expression.Constant(long.Parse(end))));
                case "Single":
                    return Expression.AndAlso(GetBinaryExpression(member, ">=", Expression.Constant(float.Parse(start))), GetBinaryExpression(member, "<=", Expression.Constant(float.Parse(end))));
                case "Double":
                    return Expression.AndAlso(GetBinaryExpression(member, ">=", Expression.Constant(double.Parse(start))), GetBinaryExpression(member, "<=", Expression.Constant(double.Parse(end))));
                case "Decimal":
                    return Expression.AndAlso(GetBinaryExpression(member, ">=", Expression.Constant(decimal.Parse(start))), GetBinaryExpression(member, "<=", Expression.Constant(decimal.Parse(end))));
                case "DateTime":
                    return Expression.AndAlso(GetBinaryExpression(member, ">=", Expression.Constant(DateTime.Parse(start))), GetBinaryExpression(member, "<=", Expression.Constant(DateTime.Parse(end))));
            }

            return null;
        }

        private static Expression BetweenStringHelper(PropertyInfo info, string start, string end, MemberExpression property)
        {
            if (isSql)
            {
                return Expression.OrElse(GetSqlLikeExp(start + "%", property), GetSqlLikeExp(end + "%", property));
            }

            var startCon = GetConstantExpression(info.PropertyType.Name, start);
            var endCon = GetConstantExpression(info.PropertyType.Name, end);
            var ignoreCase = Expression.Constant(true);
            var zeroCon = Expression.Constant(0);

            var compare1 = Expression.Call(typeof(string), "Compare", null, startCon, property, ignoreCase);
            var compare2 = Expression.Call(typeof(string), "Compare", null, property, endCon, ignoreCase);

            var left = GetBinaryExpression(compare1, "<=", zeroCon);
            var right = GetBinaryExpression(compare2, "<=", zeroCon);

            return Expression.AndAlso(left, right);
        }

        private static Expression BetweenHelper(PropertyInfo info, string start, string end, MemberExpression property)
        {
            switch (info.PropertyType.Name)
            {
                case "String":
                    return BetweenStringHelper(info, start, end, property);
                default:
                    return BetweenOtherTypeHelper(info, start, end, property);
            }
        }

        private static Expression GetBetweenExpression<T>(string colName, string start, string end, ParameterExpression paramExpression)
        {
            var type = typeof(T);
            var info = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(it => it.Name.ToLower() == colName);

            if (info != null)
            {
                var property = Expression.Property(paramExpression, info.Name);
                return BetweenHelper(info, start, end, property);
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
                        var property = Expression.Property(paramExpression, info.Name);
                        BinaryExpression nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(object)));
                        info = info.PropertyType.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(it => it.Name.ToLower() == colName);
                        if (info == null)
                        {
                            return null;
                        }

                        var childProperty = Expression.Property(property, info);

                        return Expression.AndAlso(nullCheck, BetweenHelper(info, start, end, childProperty));
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
                constraint = constraint[1..(constraint.Length - 1)];
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
                constraint = constraint[1..(constraint.Length - 1)];
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
                   _ => Expression.Constant(constraint[1..(constraint.Length - 1)], typeof(string))
               };

        private static BinaryExpression GetBinaryExpression(Expression property, string @operator, ConstantExpression constantExpression) =>
         @operator switch
         {
             "=" => Expression.Equal(property, constantExpression),
             "<>" => Expression.NotEqual(property, constantExpression),
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
            int len = constraint.Length;
            LikeMode likeMode = LikeMode.Equal;
            string query = constraint;
            if (constraint[0] == '%' && constraint[len - 1] == '%')
            {
                len--;
                query = constraint[1..len];
                likeMode = LikeMode.Contains;
            }
            else if (constraint[0] == '%')
            {
                query = constraint[1..];
                likeMode = LikeMode.EndsWith;
            }
            else if (constraint[len - 1] == '%')
            {
                len--;
                query = constraint[0..len];
                likeMode = LikeMode.StartsWith;
            }

            var like = typeof(ExpressionBuilder).GetMethod("Like")!;

            var exp = Expression.Call(
                      like,
                      property,
                      Expression.Constant(query),
                      Expression.Constant(likeMode));
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
            else if (str[curIndex + 1] == '>')
            {
                tokens.Add(ch.ToString() + ">");
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
                                if (ch == '\'')
                                {
                                    i++;
                                    while (i + 1 < len && str[i + 1] != '\'')
                                    {
                                        sb.Append(str[i + 1]);
                                        i++;
                                    }
                                }
                                else
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

                    case '\'':
                        i++;
                        sb.Append('"');
                        while (i < len && str[i] != '\'')
                        {
                            sb.Append(str[i]);
                            i++;
                        }

                        sb.Append('"');
                        tokens.Add(sb.ToString());
                        sb.Clear();
                        break;
                    case '\r':
                    case '\n':
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
                var lowercaseItem = item.ToLower();
                switch (lowercaseItem)
                {
                    case "not":
                    case "(":
                        stack.Push(lowercaseItem);
                        break;

                    case "and":
                    case "or":

                        if (stack.Count > 0)
                        {
                            string between = string.Empty;
                            stack.TryPeek(out between!);
                            if (between.Equals("between"))
                            {
                                continue;
                            }

                            if (stack.Peek() == "(")
                            {
                                stack.Push(lowercaseItem);
                                continue;
                            }

                            while (stack.Count > 0 && (Priority(lowercaseItem) >= Priority(stack.Peek())))
                            {
                                res.Add(stack.Pop());
                            }
                        }

                        stack.Push(lowercaseItem);
                        break;
                    case "=":
                    case "==":
                    case "<":
                    case ">":
                    case "<=":
                    case ">=":
                    case "in":
                    case "<>":
                    case "between":
                        stack.Push(lowercaseItem);
                        break;
                    case "like":
                    case "ilike":
                        stack.Push("like");
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
        static int Priority(string @operator)
        {
            return @operator switch
            {
                "not" => 1,
                "and" => 2,
                "or" => 2,
                "(" => 4,
                _ => 0
            };
        }
        private static readonly MethodInfo MethodContains = typeof(Enumerable).GetMethods(
                        BindingFlags.Static | BindingFlags.Public)
                        .Single(m => m.Name == nameof(Enumerable.Contains)
                            && m.GetParameters().Length == 2);

        private static readonly MethodInfo MethodLike = typeof(DbFunctionsExtensions).GetMethods().Single(m => m.Name == nameof(DbFunctionsExtensions.Like) && m.GetParameters().Length == 3);

        private static bool isSql = false;

    }

    /// <summary>
    /// Specifies different modes for string comparison in a "like" operation.
    /// </summary>
    public enum LikeMode
    {
        /// <summary>
        /// The string must contain the specified value.
        /// </summary>
        Contains = 1,

        /// <summary>
        /// The string must start with the specified value.
        /// </summary>
        StartsWith,

        /// <summary>
        /// The string must end with the specified value.
        /// </summary>
        EndsWith,

        /// <summary>
        /// The string must be an exact match to the specified value.
        /// </summary>
        Equal,
    }
}

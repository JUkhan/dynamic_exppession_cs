using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;


namespace DynamicExp
{

    public static class ExpressionBuilder
    {
        public static IQueryable<T> Where<T>(this IQueryable<T> query, string str, params string[] relationalProps) => query.Where<T>(GetExpression<T>(str, relationalProps));

        public static Expression<Func<T, bool>> GetExpression<T>(string str, params string[] relationalProps)
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
                            var exp1 = GetExpression<T>(colName, item.ToLower(), tokens[i - 1], paramExpression, relationalProps);
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

                //Console.WriteLine(expression);
                var res = Expression.Lambda<Func<T, bool>>(expression, paramExpression);

                return res;
            }
            catch (Exception)
            {
                return Expression.Lambda<Func<T, bool>>(Expression.Equal(Expression.Constant(1), Expression.Constant(1)), paramExpression);
            }
        }
        private static readonly MethodInfo MethodContains = typeof(Enumerable).GetMethods(
                        BindingFlags.Static | BindingFlags.Public)
                        .Single(m => m.Name == nameof(Enumerable.Contains)
                            && m.GetParameters().Length == 2);

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
        
        private static Expression? GetExpression<T>(string colName, string @operator, string constraint, ParameterExpression paramExpression, params string[] props)
        {
            var type = typeof(T);
            var info = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(it => it.Name.ToLower() == colName);
            var exps = new List<Expression>();
            if (info != null)
            {
                exps.Add(GetExpression<T>(info, @operator, constraint, paramExpression));
            }
            foreach (var prop in props)
            {
                info = type.GetProperty(prop);
                if (info != null)
                {
                    var exp = GetExpression<T>(info, paramExpression, @operator, colName, constraint);
                    if (exp != null)
                    {
                        exps.Add(exp);
                    }
                }
            }
            if (exps.Count == 1) { return exps[0]; }
            else if (exps.Count > 1)
            {
                var falsyExp = Expression.Equal(Expression.Constant(false), Expression.Constant(true));
                return exps.Aggregate(falsyExp, (accu, exp) => Expression.Or(accu, exp));
            }
            return null;
        }

        private static Expression? GetExpression<T>(
            PropertyInfo info,
            ParameterExpression paramExpression,
            string @operator,
            string colName, string constraint)
        {
            var property = Expression.Property(paramExpression, info.Name);
            var childInfo = info.PropertyType.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(it => it.Name.ToLower() == colName);
            if (childInfo == null)
            {
                return null;
            }
            var childProperty = Expression.Property(property, childInfo);
            if (@operator == "in")
            {
                return GetInExp<T>(childInfo, constraint, childProperty);
            }
            if (@operator == "like")
            {
                return LikeOperatorMode == LikeOperatorMode.InMemory? GetLikeExp<T>(childInfo, constraint, childProperty): GetSqlLikeExp<T>(childInfo, constraint, childProperty);
            }
            var constantExpression = GetConstantExpression(childInfo.PropertyType.Name, constraint);

            var expression = GetBinaryExpression(childProperty, @operator, constantExpression);

            return expression;
        }
        private static Expression GetExpression<T>(PropertyInfo info, string @operator, string constraint, ParameterExpression paramExpression)
        {
            var property = Expression.Property(paramExpression, info.Name);

            if (@operator == "in")
            {
                return GetInExp<T>(info, constraint, property);
            }
            if (@operator == "like")
            {
                return LikeOperatorMode==LikeOperatorMode.InMemory? GetLikeExp<T>(info, constraint, property): GetSqlLikeExp<T>(info, constraint, property);
            }
            var constantExpression = GetConstantExpression(info.PropertyType.Name, constraint);

            return GetBinaryExpression(property, @operator, constantExpression);
        }
        private static ConstantExpression GetConstantExpression(string typeName, string constraint)
        {
            return typeName switch
            {
                "Int32" => Expression.Constant(Int32.Parse(constraint)),
                "Int64" => Expression.Constant(Int64.Parse(constraint)),
                "Single" => Expression.Constant(Single.Parse(constraint)),
                "Double" => Expression.Constant(Double.Parse(constraint)),
                "Decimal" => Expression.Constant(Decimal.Parse(constraint)),
                "Boolean" => Expression.Constant(Boolean.Parse(constraint)),
                "DateTime" => Expression.Constant(DateTime.Parse(constraint)),
                _ => Expression.Constant(constraint, typeof(string))
            };

        }
        private static BinaryExpression GetBinaryExpression(MemberExpression property, string @operator, ConstantExpression constantExpression)
        {
            return @operator switch
            {
                "=" => Expression.Equal(property, constantExpression),
                "==" => Expression.Equal(property, constantExpression),
                "<" => Expression.LessThan(property, constantExpression),
                "<=" => Expression.LessThanOrEqual(property, constantExpression),
                ">" => Expression.GreaterThan(property, constantExpression),
                ">=" => Expression.GreaterThanOrEqual(property, constantExpression),
                _ => Expression.Equal(Expression.Constant(1), Expression.Constant(1)),
            };
        }
        private static Expression GetInExp<T>(PropertyInfo propInfo, string constraint, MemberExpression property)
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
                    val = list.Select(it => DateTime.Parse(it));
                    break;
            }

            var contains = MethodContains.MakeGenericMethod(propInfo.PropertyType);

            var exp = Expression.Call(
                      contains,
                      Expression.Constant(val),
                      property);
            return exp;
        }
        
        private static readonly MethodInfo MethodLike = typeof(DbFunctionsExtensions).GetMethods().Single(m => m.Name == nameof(DbFunctionsExtensions.Like)
                                                                        && m.GetParameters().Length == 3);
        private static Expression GetSqlLikeExp<T>(PropertyInfo propInfo, string constraint, MemberExpression property) => Expression.Call(
                      MethodLike,
                      Expression.Constant(EF.Functions),
                      property,
                      Expression.Constant(constraint)
                      );

        private static Expression GetLikeExp<T>(PropertyInfo propInfo, string constraint, MemberExpression property)
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
            else tokens.Add(ch.ToString());
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
                                    sb.Append(ch);
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
                        i++;
                        while (i < len && str[i] != '"')
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
                    case "(":
                        stack.Push(item);
                        break;
                    case "AND":
                    case "And":
                    case "and":
                    case "OR":
                    case "Or":
                    case "or":
                        if (stack.Count > 0)
                            res.Add(stack.Pop());
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
                            if (operand == "(") break;
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
        public static LikeOperatorMode LikeOperatorMode = LikeOperatorMode.InMemory;
        
    }
    public enum LikeOperatorMode
    {
        Sql, InMemory
    }
}

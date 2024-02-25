# dynamic_expression_cs
dynamic linq expression from string literal

Supported Operators: `=` `<` `<=` `>` `>=` `In` `Like`, `And` `Or` and operators could be `all lower case`/`all upper case`/`title case` example: `in`/`IN`/`In`



It takes `Column Name`/ `Property Name` as case insensitive and also allowed snake case

## Example
```
firstName = jasim
first_name Like jasim%
name IN (jasim, ripon, "abdur rahman")
FIRST_NAME="jasim"
firstname="jasim khan" And age < 20
(firstname="jasim khan" And age < 20) OR (postal_code = 1270)

```
For single word `"` `"` is optional but must for multiple words like `"jasim khan"`

```c#


namespace DynamicExp;

record User(string firstName, int age, int postalCode, Address address );
record Address(string location, int postalCode);
record SDS(string content, string? segment = null, string? appName = null);

class Program
{

    public static void SampleExample()
    {
        var list = new List<User>
            {
               new User("Talha", 12, 1,  new Address("loc1", 101)),
               new User("jasim khan", 23, 2, new Address("loc2", 101)),
               new User("arif", 18, 3, new Address("loc2", 103)),

            };
        var listSDS = new List<SDS>
            {
                //new SDS("  first_name =  \"jasim khan\" "),
                //new SDS("age <= 20 AND first_name = arif"),
                //new SDS("age in ( 12, 18)"),
                //new SDS("first_name In(\"jasim\", arif)"),
                new SDS("postalCode IN (1, 103)"),
                //new SDS("postalCode=1")
                //new SDS("  first_name LIKE  tal% "),
            };

        var query = String.Join(" Or ", listSDS.Select(it => $"({it.content})"));
        //var exp = ExpressionBuilder.GetExpression<User>(query,"address");

        foreach (var item in list.Where(query))
        {
            Console.WriteLine(item);
        }


    }
    static void Main(string[] args)
    {
        SampleExample();
    }
}

```

Please look at the following  entities both has the  `postalCode` property and `address` prop in `User` has the instance of `Address`.
```c#
record User(string firstName, int age, int postalCode, Address address );
record Address(string location, int postalCode);
```
```c#
var list = new List<User>
            {
               new User("Talha", 12, 1,  new Address("loc1", 101)),
               new User("jasim khan", 23, 2, new Address("loc2", 101)),
               new User("arif", 18, 3, new Address("loc2", 103)),

            };
```
if you have query like `var query = "postalCode IN (1, 103)"` and run `list.Where(query)`, you have single result `User { firstName = Talha, age = 12, postalCode = 1, address = Address { location = loc1, postalCode = 101 } }`

What about `postalCode 103` which exist in `Address`(Fortunately `Where()` method takes two params `Where(string query, params string[] relationalProps)`)? Here `address` is a relational props. If you need to search any thing from relational entities, please pass them to the second params like `list.Where(query, "address")`. Now your result will have two items one from user itself and another one from address
```
User { firstName = Talha, age = 12, postalCode = 1, address = Address { location = loc1, postalCode = 101 } }
User { firstName = arif, age = 18, postalCode = 3, address = Address { location = loc2, postalCode = 103 } }
```
Keep in mind by default `Like` operator mode is `InMemory`, make it `Sql` for fetching data from DB.
```c#
 ExpressionBuilder.LikeOperatorMode = LikeOperatorMode.Sql;
```
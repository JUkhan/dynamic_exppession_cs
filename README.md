# dynamic_expression_cs
dynamic linq expression from string literal

Supported Operators: `=` `<` `<=` `>` `>=` `In` `Like`, `Between`, `And` `Or` `not` and operators could be `all lower case`/`all upper case`/`title case` example: `in`/`IN`/`In`



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


## Example
```c#

var query = "(firstName  like 'abdu%' and age<21) or address.postal_code between 1 and 5";
var user = new User(firstName: "abdulla", age: 21, postalCode: 2,
    address:new Address(location:"aasas", postalCode:5));
var predicate = ExpressionBuilder.GetExpression<User>(query, false).Compile();
Console.WriteLine(predicate(user));

var data = new List<User>
{
        new User(firstName: "abdulla", age: 21, postalCode: 2,address:new Address(location:"aasas", postalCode:3)),
        new User(firstName: "arif", age: 21, postalCode: 7,address:new Address(location:"aasas", postalCode:5))
};
foreach (var item in data.Where(query))
{
    Console.WriteLine(item);
}

```


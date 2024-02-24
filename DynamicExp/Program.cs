using System.Reflection;

namespace DynamicExp;

record User(string firstName, int age, int postalCode, Address? address = null);
record SDS(string content, string? segment = null, string? appName = null);
record Address(string location, int postalCode);
record Dummy(int a, long km, float b, double c, decimal d, string s, bool b2, DateTime dt);

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
                new SDS("  first_name =  \"jasim khan\" "),
                //new SDS("age <= 20 and first_name = arif"),
                //new SDS("age in ( 12, 18)"),
                //new SDS("first_name in(\"jasim\", arif)"),
                //new SDS("postalCode in(1, 103)"),
                //new SDS("postalCode=1")
                //new SDS("  first_name like  tal% "),
            };

        var str = String.Join(" or ", listSDS.Select(it => $"({it.content})"));
        //var exp = ExpressionBuilder.GetExpression<User>(str,"address");

        foreach (var item in list.Where(str, "address"))
        {
            Console.WriteLine(item);
        }


    }
    static void Main(string[] args)
    {
        SampleExample();
    }
}

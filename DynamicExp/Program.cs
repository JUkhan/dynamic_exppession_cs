﻿

using DynamicExp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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
                //new SDS("postalCode IN (1, 103)"),
                //new SDS("postalCode=1")
                new SDS("  first_name LIKE  tal% "),
            };

        var query = String.Join(" Or ", listSDS.Select(it => $"({it.content})"));
        var exp = ExpressionBuilder.GetExpression<User>(query,"address").Compile();

        foreach (var item in list.Where(exp))
        {
            Console.WriteLine(item);
        }


    }
    static void Main(string[] args)
    {
        SampleExample();
        //InitData();
        //QueryFromDatabase();
    }
    static void QueryFromDatabase()
    {
        ExpressionBuilder.LikeOperatorMode = LikeOperatorMode.Sql;
        using var db = new DataContext();

        var ds = db.Products.Where("name like lap%");

        foreach (var item in ds)
        {
            Console.WriteLine($"{item.Name} - {item.Price}");
        }
    }
    static void InitData()
    {
   
        using var db = new DataContext();
       
        
        db.Products.Add(new Models.Product { Name = "Laptop", Price = 12 });
        db.Products.Add(new Models.Product { Name = "Mobile", Price = 32 });
        db.Products.Add(new Models.Product { Name = "Karate Mats", Price = 19 });
        db.SaveChanges();
    }
}

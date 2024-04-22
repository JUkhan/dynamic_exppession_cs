

using DynamicExp.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace DynamicExp;

record User(string firstName, int age, int postalCode, Address? address = null);
record Address(string location, int postalCode);
record SDS(string query, Action action);
record Action(bool view, bool add, bool edit, bool delete);
public class HierarchyNodeName
{
    public string Name { get; set; }
}
public class HierarchyNodeType
{
    public string Name { get; set; }
}
public class RelationshipHierarchy
{
    public int Id { get; set; }
    public string Name { get; set; }
    public virtual HierarchyNodeName HierarchyNodeName { get; set; }
    public virtual HierarchyNodeType HierarchyNodeType { get; set; }
    public virtual Relationship Relationship { get; set; }
}
public class Relationship
{
    public string Name { get; set; }

    public virtual ICollection<RelationshipHierarchy> RelationshipHierarchies { get; set; }
}

class Program
{

    public static List<Relationship> GetRelations()
    {
        return new List<Relationship>
        {
            new Relationship
            {
                Name = "cnt hamas",
                RelationshipHierarchies= new List<RelationshipHierarchy>
                {
                    new RelationshipHierarchy
                    {
                        Id = 1,
                        HierarchyNodeName=new HierarchyNodeName{Name="Bangladesh"},
                        HierarchyNodeType=new HierarchyNodeType{Name="country"},
                        Relationship=new Relationship{Name="cnt hamas"}
                    },
                    new RelationshipHierarchy
                    {
                        Id = 2,
                        HierarchyNodeName=new HierarchyNodeName{Name="India"},
                        HierarchyNodeType=new HierarchyNodeType{Name="country"},
                        Relationship=new Relationship{Name="cnt hamas"}
                    },
                    new RelationshipHierarchy
                    {
                        Id = 3,
                        HierarchyNodeName=new HierarchyNodeName{Name="Dhaka"},
                        HierarchyNodeType=new HierarchyNodeType{Name="division"},
                        Relationship=new Relationship{Name="cnt hamas"}
                    },
                    new RelationshipHierarchy
                    {
                        Id = 4,
                        HierarchyNodeName=new HierarchyNodeName{Name="Rajshahi"},
                        HierarchyNodeType=new HierarchyNodeType{Name="division"},
                        Relationship=new Relationship{Name="cnt hamas"}
                    },
                    new RelationshipHierarchy
                    {
                        Id = 5,
                        HierarchyNodeName=new HierarchyNodeName{Name="Chittagong"},
                        HierarchyNodeType=new HierarchyNodeType{Name="division"},
                        Relationship=new Relationship{Name="cnt hamas"}
                    },
                }
            },
            new Relationship
            {
                Name = "ccc",
                RelationshipHierarchies= new List<RelationshipHierarchy>
                {
                    new RelationshipHierarchy
                    {
                        Id = 6,
                        HierarchyNodeName=new HierarchyNodeName{Name="US"},
                        HierarchyNodeType=new HierarchyNodeType{Name="country"}
                    },
                    new RelationshipHierarchy
                    {
                        Id = 7,
                        HierarchyNodeName=new HierarchyNodeName{Name="CA"},
                        HierarchyNodeType=new HierarchyNodeType{Name="country"}
                    },
                    new RelationshipHierarchy
                    {
                        Id = 8,
                        HierarchyNodeName=new HierarchyNodeName{Name="Dhaka"},
                        HierarchyNodeType=new HierarchyNodeType{Name="division"}
                    },
                    new RelationshipHierarchy
                    {
                        Id = 9,
                        HierarchyNodeName=new HierarchyNodeName{Name="Rajshahi"},
                        HierarchyNodeType=new HierarchyNodeType{Name="division"}
                    },
                    new RelationshipHierarchy
                    {
                        Id = 10,
                        HierarchyNodeName=new HierarchyNodeName{Name="Chittagong"},
                        HierarchyNodeType=new HierarchyNodeType{Name="division"}
                    },
                }
            },

        };
    }
    public static void SampleExample()
    {
        //var rgex = new Regex("__NodeName|__NodeType");
        //Console.WriteLine(rgex.Matches("__RelationShipName like cnt% and __NodeType = country").Count);
        var svc = new SDSPoliceService();
        Console.WriteLine(svc.GetQuery(SDSPoliceService.EntityName.DataMart, it => it.view));
        svc.CheckPermissionForRelationship(action => action.view && action.add);
        svc.CheckPermissionForRelationshipHierarchy(it => it.view && it.delete, it => it.Id == 5);
        var list = new List<User>
            {
               new User("Talha", 12, 1,  new Address("loc1", 101)),
               new User("jasim khan", 23, 2, new Address("loc2", 102)),
               new User("arif", 18, 3),

            };
        foreach (var it in list.Where("firstname not like tal%", false))
        {
            Console.WriteLine(it);
        }


        var data = GetRelations().AsQueryable();
        //data = data.Where(it => it.Name.Contains("cnt"));

        foreach (var item in svc.GetRelationships())
        {
            Console.WriteLine($"{item.Name}");

        }

        foreach (var h in svc.GetRelationshipHierarchies())
        {
            Console.WriteLine($"{h.HierarchyNodeType.Name}-{h.HierarchyNodeName.Name}");
        }


    }
    
    static void meeting()
    {
        var query = "(firstName  ilike 'abdu%' and age<21) or address.postal_code between 1 and 5";
        var user = new User(firstName: "abdulla", age: 21, postalCode: 2,
            address:new Address(location:"aasas", postalCode:5));
        var predicate = ExpressionBuilder.GetExpression<User>(query, false).Compile();
        Console.WriteLine(predicate(user));

        var data = new List<User>
        {
             new User(firstName: "abdulla", age: 21, postalCode: 2,address:new Address(location:"aasas", postalCode:3)),
             new User(firstName: "arif", age: 21, postalCode: 7,address:new Address(location:"aasas", postalCode:5))
        };
        foreach (var item in data.Where(query, false))
        {
            Console.WriteLine(item);
        }
       
    }
    
    static void Main(string[] args)
    {
        //SampleExample();
        //InitData();
        //QueryFromDatabase();
        meeting();
        
        
    }
    static List<string> GetSDS()
    {
        return new List<string>
        {
            "__productName in (Laptop) or __price<20",
            "__orderFirst_Name = abdur",
            "age in (20,30)"
        };
    }
    static void QueryFromDatabase()
    {
        
        using var db = new DataContext();
        //var sdsQuery = ExpressionBuilder.MapSDS(GetSDS(), "__product");
        //Console.WriteLine(sdsQuery);
        foreach (var item in db.Products.Where("id Between 3 and 5", true))
        {
            Console.WriteLine($"{item.Id} {item.Name} - {item.Price}");
        }
        //sdsQuery = ExpressionBuilder.MapSDS(GetSDS(), "__order");
        foreach (var item in db.Orders.Include(it=>it.Customer).Where("Customer.Id between 1 and 2"))
        {
            Console.WriteLine($"{item.Customer.Id} {item.Customer.FirstName} - {item.Customer.LastName}");
        }

    }
    static void InitData()
    {

        using var db = new DataContext();

        db.Products.Add(new Models.Product { Name = "Laptop", Price = 12 });
        db.Products.Add(new Models.Product { Name = "Mobile", Price = 32 });
        db.Products.Add(new Models.Product { Name = "Karate Mats", Price = 19 });

        db.Customers.Add(new Models.Customer { FirstName = "jasim", LastName = "khan", Address = "Tangail", Phone = "" });
        db.Customers.Add(new Models.Customer { FirstName = "abdur", LastName = "rahman", Address = "Dhaka", Phone = "" });
        db.SaveChanges();
        db.Orders.Add(new Models.Order { CustomerId = 1, OrderPlaced = DateTime.Now, OrderedFulfill = DateTime.Now });
        db.Orders.Add(new Models.Order { CustomerId = 2, OrderPlaced = DateTime.Now, OrderedFulfill = DateTime.Now });
        db.SaveChanges();
        db.OrderDetails.Add(new Models.OrderDetail { OrderId = 1, Quantity = 2, ProductId = 2 });
        db.OrderDetails.Add(new Models.OrderDetail { OrderId = 1, Quantity = 3, ProductId = 3 });

        db.SaveChanges();
    }
}

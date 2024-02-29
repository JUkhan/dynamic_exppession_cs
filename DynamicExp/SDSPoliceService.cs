using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DynamicExp
{
    
    class SDSPoliceService
    {
         
        public SDSPoliceService()
        {
            
            Polices = new List<SDS>
            {
               new SDS("STATE_CODE in ('NJ', 'NY','PA')", new Action(true, false, false, false)),
               new SDS("__RelationShipName like cnt%", new Action(false, true, false,false)),
               new SDS("__RelationShipName = ccc", new Action(true, true, false,false)),
               new SDS("__RelationShipName like cnt% and __NodeType = 'country'", new Action(true, true, true,false)),
               new SDS("__RelationShipName like cnt% and __NodeType = 'division' and __NodeName = Dhaka", new Action(true, true, false,true)),
               new SDS("__RelationShipName like cnt% and __NodeType = 'division' and __NodeName = Rajshahi", new Action(true, false, true,false)),
               new SDS("__RelationShipName like cnt% and __NodeType = 'division' and __NodeName = Chittagong", new Action(true, false, false,true)),
            };
        }
       
        public string GetQuery(EntityName entityName, Func<Action, bool> predicate)
        {
            Regex rgex = new Regex("__NodeName|__NodeType");
            Regex db_rgex = new Regex("__RelationShipName|__NodeName|__NodeType");
            IEnumerable<(string query, bool hasAction)> qa = null;
            var filter =Polices.Where(it => db_rgex.Matches(it.query).Count > 0);
            switch (entityName)
            {
                case EntityName.Relationship:
                    filter = filter.Where(it => rgex.Matches(it.query).Count == 0);
                    qa = filter
                    .Select(it => (it.query
                    .Replace("__RelationShipName", "Name")
                      , predicate(it.action))
                    );
                    break;
                case EntityName.RelationshipHierarchy:
                    filter = filter.Where(it => rgex.Matches(it.query).Count > 0);
                    qa = filter
                    .Select(it => (it.query
                    .Replace("__RelationShipName", "RelationShip.Name")
                    .Replace("__NodeType", "HierarchyNodeType.Name")
                    .Replace("__NodeName", "HierarchyNodeName.Name"), predicate(it.action))
                    );
                    break;
                default:
                    filter = Polices.Where(it => db_rgex.Matches(it.query).Count == 0);
                    qa = filter.Select(it => (it.query,predicate(it.action)));
                    break;
            }
           
            var pos = qa.Where(p => p.hasAction).Select(it => $"({it.query})").ToList();
            var neg = string.Join(" or ", qa.Where(p => !p.hasAction).Select(it => $"({it.query})"));
            if (!string.IsNullOrEmpty(neg))
            {
                pos.Add($"not({neg})");
            }
            var query = string.Join(" or ", pos);
            //Console.WriteLine(query);
            return query;
        }
       
        public void CheckPermissionForRelationship(
            Func<Action, bool> actionPredicate,
            Func<Relationship, bool> itemPredicate,
            string messege = "You have no permission")
        {
            var coll = GetRawRelationships().Where(GetQuery(EntityName.Relationship, actionPredicate));
            
            if (!coll.Any(itemPredicate))
            {
                throw new Exception(messege);
            }
        }

        public void CheckPermissionForRelationshipHierarchy(
            Func<Action, bool> actionPredicate,
            Func<RelationshipHierarchy, bool> itemPredicate,
            string messege = "You have no permission")
        {
            var coll = GetRawRelationshipHierarchies().Where(GetQuery(EntityName.RelationshipHierarchy, actionPredicate));

            if (!coll.Any(itemPredicate))
            {
                throw new Exception(messege);
            }
        }
        public IQueryable<Relationship> GetRelationships()
        {
            return GetRawRelationships().Where(GetQuery(EntityName.Relationship, action=>action.view));
        }
        public IQueryable<RelationshipHierarchy> GetRelationshipHierarchies()
        {
            return GetRawRelationshipHierarchies().Where(GetQuery(EntityName.RelationshipHierarchy, action=>action.view));
        }

        private IQueryable<Relationship> GetRawRelationships()
        {
            return Program.GetRelations().AsQueryable();
        }
        private IQueryable<RelationshipHierarchy> GetRawRelationshipHierarchies()
        {
            return Program.GetRelations()[0].RelationshipHierarchies.AsQueryable();
        }
        IEnumerable<SDS> Polices { get; set; }
        public enum EntityName { Relationship, RelationshipHierarchy, DataMart }
    }
   
}

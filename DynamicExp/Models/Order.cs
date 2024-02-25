using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicExp.Models
{
    public class Order
    {
        public int Id { get; set; }

        public DateTime OrderPlaced { get; set; }

        public DateTime? OrderedFulfill { get; set; }

        public int CustomerId { get; set; }

        public Customer Customer { get; set; } = default;

        public ICollection<OrderDetail> OrderDetails { get; set; }
    }
}

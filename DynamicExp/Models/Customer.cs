using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicExp.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required]
        public string FirstName { get; set; }=  string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        public string? Address { get; set; }

        public string? Phone { get; set; }

        public ICollection<Order>? Orders { get; set; }
    }
}

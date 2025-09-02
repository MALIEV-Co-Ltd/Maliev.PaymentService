using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.PaymentService.Data.Database.PaymentContext
{
    public class Account
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public required decimal Balance { get; set; }

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
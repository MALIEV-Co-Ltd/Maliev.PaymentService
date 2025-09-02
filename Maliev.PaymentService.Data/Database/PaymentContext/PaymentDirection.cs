using System.Collections.Generic;

namespace Maliev.PaymentService.Data.Database.PaymentContext
{
    public class PaymentDirection
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
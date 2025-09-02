using System.Collections.Generic;

namespace Maliev.PaymentService.Data.Database.PaymentContext
{
    public class PaymentMethod
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
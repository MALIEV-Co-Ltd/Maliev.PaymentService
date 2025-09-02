using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.PaymentService.Data.Database.PaymentContext
{
    public class Payment
    {
        public int Id { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public required decimal Amount { get; set; }
        public required DateTime Date { get; set; }
        public string? Description { get; set; }

        public int AccountId { get; set; }
        public Account? Account { get; set; }

        public int PaymentDirectionId { get; set; }
        public PaymentDirection? PaymentDirection { get; set; }

        public int PaymentMethodId { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }

        public int PaymentTypeId { get; set; }
        public PaymentType? PaymentType { get; set; }

        public ICollection<PaymentFile> PaymentFiles { get; set; } = new List<PaymentFile>();
    }
}
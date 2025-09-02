using System.ComponentModel.DataAnnotations;

namespace Maliev.PaymentService.Api.Models
{
    public class PaymentTypeDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }

    public class CreatePaymentTypeRequest
    {
        [Required]
        [StringLength(255)]
        public required string Name { get; set; }
    }

    public class UpdatePaymentTypeRequest
    {
        [Required]
        [StringLength(255)]
        public required string Name { get; set; }
    }
}
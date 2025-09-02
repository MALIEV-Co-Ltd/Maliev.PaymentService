using System.ComponentModel.DataAnnotations;

namespace Maliev.PaymentService.Api.Models
{
    public class PaymentMethodDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }

    public class CreatePaymentMethodRequest
    {
        [Required]
        [StringLength(255)]
        public required string Name { get; set; }
    }

    public class UpdatePaymentMethodRequest
    {
        [Required]
        [StringLength(255)]
        public required string Name { get; set; }
    }
}
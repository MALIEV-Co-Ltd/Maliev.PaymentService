using System.ComponentModel.DataAnnotations;

namespace Maliev.PaymentService.Api.Models
{
    public class PaymentDirectionDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }

    public class CreatePaymentDirectionRequest
    {
        [Required]
        [StringLength(255)]
        public required string Name { get; set; }
    }

    public class UpdatePaymentDirectionRequest
    {
        [Required]
        [StringLength(255)]
        public required string Name { get; set; }
    }
}
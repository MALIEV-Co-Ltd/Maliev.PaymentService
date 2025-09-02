using System.ComponentModel.DataAnnotations;

namespace Maliev.PaymentService.Api.Models
{
    public class AccountDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required decimal Balance { get; set; }
    }

    public class CreateAccountRequest
    {
        [Required]
        [StringLength(255)]
        public required string Name { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public required decimal Balance { get; set; }
    }

    public class UpdateAccountRequest
    {
        [Required]
        [StringLength(255)]
        public required string Name { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public required decimal Balance { get; set; }
    }
}
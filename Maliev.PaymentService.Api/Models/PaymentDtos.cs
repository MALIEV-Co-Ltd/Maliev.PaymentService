using System;
using System.ComponentModel.DataAnnotations;

namespace Maliev.PaymentService.Api.Models
{
    public class PaymentDto
    {
        public int Id { get; set; }
        public required decimal Amount { get; set; }
        public required DateTime Date { get; set; }
        public string? Description { get; set; }
        public int AccountId { get; set; }
        public int PaymentDirectionId { get; set; }
        public int PaymentMethodId { get; set; }
        public int PaymentTypeId { get; set; }
    }

    public class CreatePaymentRequest
    {
        [Required]
        [Range(0, double.MaxValue)]
        public required decimal Amount { get; set; }
        [Required]
        public required DateTime Date { get; set; }
        [StringLength(1000)]
        public string? Description { get; set; }
        [Required]
        public required int AccountId { get; set; }
        [Required]
        public required int PaymentDirectionId { get; set; }
        [Required]
        public required int PaymentMethodId { get; set; }
        [Required]
        public required int PaymentTypeId { get; set; }
    }

    public class UpdatePaymentRequest
    {
        [Required]
        [Range(0, double.MaxValue)]
        public required decimal Amount { get; set; }
        [Required]
        public required DateTime Date { get; set; }
        [StringLength(1000)]
        public string? Description { get; set; }
        [Required]
        public required int AccountId { get; set; }
        [Required]
        public required int PaymentDirectionId { get; set; }
        [Required]
        public required int PaymentMethodId { get; set; }
        [Required]
        public required int PaymentTypeId { get; set; }
    }
}
using System;
using System.ComponentModel.DataAnnotations;

namespace Maliev.PaymentService.Api.Models
{
    public class PaymentFileDto
    {
        public int Id { get; set; }
        public required string FileName { get; set; }
        public required string FilePath { get; set; }
        public required DateTime UploadDate { get; set; }
        public int PaymentId { get; set; }
    }

    public class CreatePaymentFileRequest
    {
        [Required]
        [StringLength(255)]
        public required string FileName { get; set; }
        [Required]
        [StringLength(1000)]
        public required string FilePath { get; set; }
        public required DateTime UploadDate { get; set; }
        public required int PaymentId { get; set; }
    }

    public class UpdatePaymentFileRequest
    {
        [Required]
        [StringLength(255)]
        public required string FileName { get; set; }
        [Required]
        [StringLength(1000)]
        public required string FilePath { get; set; }
        public required DateTime UploadDate { get; set; }
        public required int PaymentId { get; set; }
    }
}
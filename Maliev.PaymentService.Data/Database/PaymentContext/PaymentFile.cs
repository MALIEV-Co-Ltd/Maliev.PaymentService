using System;

namespace Maliev.PaymentService.Data.Database.PaymentContext
{
    public class PaymentFile
    {
        public int Id { get; set; }
        public required string FileName { get; set; }
        public required string FilePath { get; set; }
        public required DateTime UploadDate { get; set; }

        public int PaymentId { get; set; }
        public Payment? Payment { get; set; }
    }
}
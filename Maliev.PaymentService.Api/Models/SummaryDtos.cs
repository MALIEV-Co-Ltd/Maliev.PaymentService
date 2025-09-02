namespace Maliev.PaymentService.Api.Models
{
    public class FinancialSummaryDto
    {
        public required decimal TotalIncome { get; set; }
        public required decimal TotalExpense { get; set; }
        public required decimal NetBalance { get; set; }
    }

    public class SummaryDetailDto
    {
        public required string Category { get; set; }
        public required decimal Amount { get; set; }
    }
}
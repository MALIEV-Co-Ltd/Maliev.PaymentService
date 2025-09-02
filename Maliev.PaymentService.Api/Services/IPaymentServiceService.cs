using Maliev.PaymentService.Api.Models;
using Maliev.PaymentService.Common.Enumerations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maliev.PaymentService.Api.Services
{
    public interface IPaymentServiceService
    {
        // Account operations
        Task<IEnumerable<AccountDto>> GetAccountsAsync();
        Task<AccountDto?> GetAccountByIdAsync(int id);
        Task<AccountDto> CreateAccountAsync(CreateAccountRequest request);
        Task<AccountDto?> UpdateAccountAsync(int id, UpdateAccountRequest request);
        Task<bool> DeleteAccountAsync(int id);

        // Payment operations
        Task<IEnumerable<PaymentDto>> GetPaymentsAsync(PaymentSortType sortType = PaymentSortType.None);
        Task<PaymentDto?> GetPaymentByIdAsync(int id);
        Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request);
        Task<PaymentDto?> UpdatePaymentAsync(int id, UpdatePaymentRequest request);
        Task<bool> DeletePaymentAsync(int id);

        // PaymentDirection operations
        Task<IEnumerable<PaymentDirectionDto>> GetPaymentDirectionsAsync();
        Task<PaymentDirectionDto?> GetPaymentDirectionByIdAsync(int id);
        Task<PaymentDirectionDto> CreatePaymentDirectionAsync(CreatePaymentDirectionRequest request);
        Task<PaymentDirectionDto?> UpdatePaymentDirectionAsync(int id, UpdatePaymentDirectionRequest request);
        Task<bool> DeletePaymentDirectionAsync(int id);

        // PaymentFile operations
        Task<IEnumerable<PaymentFileDto>> GetPaymentFilesAsync();
        Task<PaymentFileDto?> GetPaymentFileByIdAsync(int id);
        Task<PaymentFileDto> CreatePaymentFileAsync(CreatePaymentFileRequest request);
        Task<PaymentFileDto?> UpdatePaymentFileAsync(int id, UpdatePaymentFileRequest request);
        Task<bool> DeletePaymentFileAsync(int id);

        // PaymentMethod operations
        Task<IEnumerable<PaymentMethodDto>> GetPaymentMethodsAsync();
        Task<PaymentMethodDto?> GetPaymentMethodByIdAsync(int id);
        Task<PaymentMethodDto> CreatePaymentMethodAsync(CreatePaymentMethodRequest request);
        Task<PaymentMethodDto?> UpdatePaymentMethodAsync(int id, UpdatePaymentMethodRequest request);
        Task<bool> DeletePaymentMethodAsync(int id);

        // PaymentType operations
        Task<IEnumerable<PaymentTypeDto>> GetPaymentTypesAsync();
        Task<PaymentTypeDto?> GetPaymentTypeByIdAsync(int id);
        Task<PaymentTypeDto> CreatePaymentTypeAsync(CreatePaymentTypeRequest request);
        Task<PaymentTypeDto?> UpdatePaymentTypeAsync(int id, UpdatePaymentTypeRequest request);
        Task<bool> DeletePaymentTypeAsync(int id);

        // Summary operations
        Task<FinancialSummaryDto> GetFinancialSummaryAsync();
        Task<IEnumerable<SummaryDetailDto>> GetSummaryDetailsAsync();
    }
}
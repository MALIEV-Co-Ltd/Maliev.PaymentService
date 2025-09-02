using Maliev.PaymentService.Api.Models;
using Maliev.PaymentService.Common.Enumerations;
using Maliev.PaymentService.Data.Database.PaymentContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maliev.PaymentService.Api.Services
{
    public class PaymentServiceService : IPaymentServiceService
    {
        private readonly PaymentContext _context;
        private readonly ILogger<PaymentServiceService> _logger;

        public PaymentServiceService(PaymentContext context, ILogger<PaymentServiceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Account operations
        public async Task<IEnumerable<AccountDto>> GetAccountsAsync()
        {
            _logger.LogInformation("Getting all accounts.");
            return await _context.Accounts
                .Select(a => new AccountDto { Id = a.Id, Name = a.Name, Balance = a.Balance })
                .ToListAsync();
        }

        public async Task<AccountDto?> GetAccountByIdAsync(int id)
        {
            _logger.LogInformation("Getting account by ID: {Id}", id);
            var account = await _context.Accounts.FindAsync(id);
            return account == null ? null : new AccountDto { Id = account.Id, Name = account.Name, Balance = account.Balance };
        }

        public async Task<AccountDto> CreateAccountAsync(CreateAccountRequest request)
        {
            _logger.LogInformation("Creating new account with name: {Name}", request.Name);
            var account = new Account { Name = request.Name, Balance = request.Balance };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
            return new AccountDto { Id = account.Id, Name = account.Name, Balance = account.Balance };
        }

        public async Task<AccountDto?> UpdateAccountAsync(int id, UpdateAccountRequest request)
        {
            _logger.LogInformation("Updating account with ID: {Id}", id);
            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
            {
                _logger.LogWarning("Account with ID {Id} not found for update.", id);
                return null;
            }
            account.Name = request.Name;
            account.Balance = request.Balance;
            await _context.SaveChangesAsync();
            return new AccountDto { Id = account.Id, Name = account.Name, Balance = account.Balance };
        }

        public async Task<bool> DeleteAccountAsync(int id)
        {
            _logger.LogInformation("Deleting account with ID: {Id}", id);
            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
            {
                _logger.LogWarning("Account with ID {Id} not found for deletion.", id);
                return false;
            }
            _context.Accounts.Remove(account);
            await _context.SaveChangesAsync();
            return true;
        }

        // Payment operations
        public async Task<IEnumerable<PaymentDto>> GetPaymentsAsync(PaymentSortType sortType = PaymentSortType.None)
        {
            _logger.LogInformation("Getting all payments with sort type: {SortType}", sortType);
            var query = _context.Payments.AsQueryable();

            query = sortType switch
            {
                PaymentSortType.DateAscending => query.OrderBy(p => p.Date),
                PaymentSortType.DateDescending => query.OrderByDescending(p => p.Date),
                PaymentSortType.AmountAscending => query.OrderBy(p => p.Amount),
                PaymentSortType.AmountDescending => query.OrderByDescending(p => p.Amount),
                _ => query
            };

            return await query
                .Select(p => new PaymentDto
                {
                    Id = p.Id,
                    Amount = p.Amount,
                    Date = p.Date,
                    Description = p.Description,
                    AccountId = p.AccountId,
                    PaymentDirectionId = p.PaymentDirectionId,
                    PaymentMethodId = p.PaymentMethodId,
                    PaymentTypeId = p.PaymentTypeId
                })
                .ToListAsync();
        }

        public async Task<PaymentDto?> GetPaymentByIdAsync(int id)
        {
            _logger.LogInformation("Getting payment by ID: {Id}", id);
            var payment = await _context.Payments.FindAsync(id);
            return payment == null ? null : new PaymentDto
            {
                Id = payment.Id,
                Amount = payment.Amount,
                Date = payment.Date,
                Description = payment.Description,
                AccountId = payment.AccountId,
                PaymentDirectionId = payment.PaymentDirectionId,
                PaymentMethodId = payment.PaymentMethodId,
                PaymentTypeId = payment.PaymentTypeId
            };
        }

        public async Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest request)
        {
            _logger.LogInformation("Creating new payment for account ID: {AccountId}", request.AccountId);
            var payment = new Payment
            {
                Amount = request.Amount,
                Date = request.Date,
                Description = request.Description,
                AccountId = request.AccountId,
                PaymentDirectionId = request.PaymentDirectionId,
                PaymentMethodId = request.PaymentMethodId,
                PaymentTypeId = request.PaymentTypeId
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return new PaymentDto
            {
                Id = payment.Id,
                Amount = payment.Amount,
                Date = payment.Date,
                Description = payment.Description,
                AccountId = payment.AccountId,
                PaymentDirectionId = payment.PaymentDirectionId,
                PaymentMethodId = payment.PaymentMethodId,
                PaymentTypeId = payment.PaymentTypeId
            };
        }

        public async Task<PaymentDto?> UpdatePaymentAsync(int id, UpdatePaymentRequest request)
        {
            _logger.LogInformation("Updating payment with ID: {Id}", id);
            var payment = await _context.Payments.FindAsync(id);
            if (payment == null)
            {
                _logger.LogWarning("Payment with ID {Id} not found for update.", id);
                return null;
            }
            payment.Amount = request.Amount;
            payment.Date = request.Date;
            payment.Description = request.Description;
            payment.AccountId = request.AccountId;
            payment.PaymentDirectionId = request.PaymentDirectionId;
            payment.PaymentMethodId = request.PaymentMethodId;
            payment.PaymentTypeId = request.PaymentTypeId;
            await _context.SaveChangesAsync();
            return new PaymentDto
            {
                Id = payment.Id,
                Amount = payment.Amount,
                Date = payment.Date,
                Description = payment.Description,
                AccountId = payment.AccountId,
                PaymentDirectionId = payment.PaymentDirectionId,
                PaymentMethodId = payment.PaymentMethodId,
                PaymentTypeId = payment.PaymentTypeId
            };
        }

        public async Task<bool> DeletePaymentAsync(int id)
        {
            _logger.LogInformation("Deleting payment with ID: {Id}", id);
            var payment = await _context.Payments.FindAsync(id);
            if (payment == null)
            {
                _logger.LogWarning("Payment with ID {Id} not found for deletion.", id);
                return false;
            }
            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();
            return true;
        }

        // PaymentDirection operations
        public async Task<IEnumerable<PaymentDirectionDto>> GetPaymentDirectionsAsync()
        {
            _logger.LogInformation("Getting all payment directions.");
            return await _context.PaymentDirections
                .Select(pd => new PaymentDirectionDto { Id = pd.Id, Name = pd.Name })
                .ToListAsync();
        }

        public async Task<PaymentDirectionDto?> GetPaymentDirectionByIdAsync(int id)
        {
            _logger.LogInformation("Getting payment direction by ID: {Id}", id);
            var paymentDirection = await _context.PaymentDirections.FindAsync(id);
            return paymentDirection == null ? null : new PaymentDirectionDto { Id = paymentDirection.Id, Name = paymentDirection.Name };
        }

        public async Task<PaymentDirectionDto> CreatePaymentDirectionAsync(CreatePaymentDirectionRequest request)
        {
            _logger.LogInformation("Creating new payment direction with name: {Name}", request.Name);
            var paymentDirection = new PaymentDirection { Name = request.Name };
            _context.PaymentDirections.Add(paymentDirection);
            await _context.SaveChangesAsync();
            return new PaymentDirectionDto { Id = paymentDirection.Id, Name = paymentDirection.Name };
        }

        public async Task<PaymentDirectionDto?> UpdatePaymentDirectionAsync(int id, UpdatePaymentDirectionRequest request)
        {
            _logger.LogInformation("Updating payment direction with ID: {Id}", id);
            var paymentDirection = await _context.PaymentDirections.FindAsync(id);
            if (paymentDirection == null)
            {
                _logger.LogWarning("Payment direction with ID {Id} not found for update.", id);
                return null;
            }
            paymentDirection.Name = request.Name;
            await _context.SaveChangesAsync();
            return new PaymentDirectionDto { Id = paymentDirection.Id, Name = paymentDirection.Name };
        }

        public async Task<bool> DeletePaymentDirectionAsync(int id)
        {
            _logger.LogInformation("Deleting payment direction with ID: {Id}", id);
            var paymentDirection = await _context.PaymentDirections.FindAsync(id);
            if (paymentDirection == null)
            {
                _logger.LogWarning("Payment direction with ID {Id} not found for deletion.", id);
                return false;
            }
            _context.PaymentDirections.Remove(paymentDirection);
            await _context.SaveChangesAsync();
            return true;
        }

        // PaymentFile operations
        public async Task<IEnumerable<PaymentFileDto>> GetPaymentFilesAsync()
        {
            _logger.LogInformation("Getting all payment files.");
            return await _context.PaymentFiles
                .Select(pf => new PaymentFileDto { Id = pf.Id, FileName = pf.FileName, FilePath = pf.FilePath, UploadDate = pf.UploadDate, PaymentId = pf.PaymentId })
                .ToListAsync();
        }

        public async Task<PaymentFileDto?> GetPaymentFileByIdAsync(int id)
        {
            _logger.LogInformation("Getting payment file by ID: {Id}", id);
            var paymentFile = await _context.PaymentFiles.FindAsync(id);
            return paymentFile == null ? null : new PaymentFileDto { Id = paymentFile.Id, FileName = paymentFile.FileName, FilePath = paymentFile.FilePath, UploadDate = paymentFile.UploadDate, PaymentId = paymentFile.PaymentId };
        }

        public async Task<PaymentFileDto> CreatePaymentFileAsync(CreatePaymentFileRequest request)
        {
            _logger.LogInformation("Creating new payment file with name: {FileName}", request.FileName);
            var paymentFile = new PaymentFile { FileName = request.FileName, FilePath = request.FilePath, UploadDate = request.UploadDate, PaymentId = request.PaymentId };
            _context.PaymentFiles.Add(paymentFile);
            await _context.SaveChangesAsync();
            return new PaymentFileDto { Id = paymentFile.Id, FileName = paymentFile.FileName, FilePath = paymentFile.FilePath, UploadDate = paymentFile.UploadDate, PaymentId = paymentFile.PaymentId };
        }

        public async Task<PaymentFileDto?> UpdatePaymentFileAsync(int id, UpdatePaymentFileRequest request)
        {
            _logger.LogInformation("Updating payment file with ID: {Id}", id);
            var paymentFile = await _context.PaymentFiles.FindAsync(id);
            if (paymentFile == null)
            {
                _logger.LogWarning("Payment file with ID {Id} not found for update.", id);
                return null;
            }
            paymentFile.FileName = request.FileName;
            paymentFile.FilePath = request.FilePath;
            paymentFile.UploadDate = request.UploadDate;
            paymentFile.PaymentId = request.PaymentId;
            await _context.SaveChangesAsync();
            return new PaymentFileDto { Id = paymentFile.Id, FileName = paymentFile.FileName, FilePath = paymentFile.FilePath, UploadDate = paymentFile.UploadDate, PaymentId = paymentFile.PaymentId };
        }

        public async Task<bool> DeletePaymentFileAsync(int id)
        {
            _logger.LogInformation("Deleting payment file with ID: {Id}", id);
            var paymentFile = await _context.PaymentFiles.FindAsync(id);
            if (paymentFile == null)
            {
                _logger.LogWarning("Payment file with ID {Id} not found for deletion.", id);
                return false;
            }
            _context.PaymentFiles.Remove(paymentFile);
            await _context.SaveChangesAsync();
            return true;
        }

        // PaymentMethod operations
        public async Task<IEnumerable<PaymentMethodDto>> GetPaymentMethodsAsync()
        {
            _logger.LogInformation("Getting all payment methods.");
            return await _context.PaymentMethods
                .Select(pm => new PaymentMethodDto { Id = pm.Id, Name = pm.Name })
                .ToListAsync();
        }

        public async Task<PaymentMethodDto?> GetPaymentMethodByIdAsync(int id)
        {
            _logger.LogInformation("Getting payment method by ID: {Id}", id);
            var paymentMethod = await _context.PaymentMethods.FindAsync(id);
            return paymentMethod == null ? null : new PaymentMethodDto { Id = paymentMethod.Id, Name = paymentMethod.Name };
        }

        public async Task<PaymentMethodDto> CreatePaymentMethodAsync(CreatePaymentMethodRequest request)
        {
            _logger.LogInformation("Creating new payment method with name: {Name}", request.Name);
            var paymentMethod = new PaymentMethod { Name = request.Name };
            _context.PaymentMethods.Add(paymentMethod);
            await _context.SaveChangesAsync();
            return new PaymentMethodDto { Id = paymentMethod.Id, Name = paymentMethod.Name };
        }

        public async Task<PaymentMethodDto?> UpdatePaymentMethodAsync(int id, UpdatePaymentMethodRequest request)
        {
            _logger.LogInformation("Updating payment method with ID: {Id}", id);
            var paymentMethod = await _context.PaymentMethods.FindAsync(id);
            if (paymentMethod == null)
            {
                _logger.LogWarning("Payment method with ID {Id} not found for update.", id);
                return null;
            }
            paymentMethod.Name = request.Name;
            await _context.SaveChangesAsync();
            return new PaymentMethodDto { Id = paymentMethod.Id, Name = paymentMethod.Name };
        }

        public async Task<bool> DeletePaymentMethodAsync(int id)
        {
            _logger.LogInformation("Deleting payment method with ID: {Id}", id);
            var paymentMethod = await _context.PaymentMethods.FindAsync(id);
            if (paymentMethod == null)
            {
                _logger.LogWarning("Payment method with ID {Id} not found for deletion.", id);
                return false;
            }
            _context.PaymentMethods.Remove(paymentMethod);
            await _context.SaveChangesAsync();
            return true;
        }

        // PaymentType operations
        public async Task<IEnumerable<PaymentTypeDto>> GetPaymentTypesAsync()
        {
            _logger.LogInformation("Getting all payment types.");
            return await _context.PaymentTypes
                .Select(pt => new PaymentTypeDto { Id = pt.Id, Name = pt.Name })
                .ToListAsync();
        }

        public async Task<PaymentTypeDto?> GetPaymentTypeByIdAsync(int id)
        {
            _logger.LogInformation("Getting payment type by ID: {Id}", id);
            var paymentType = await _context.PaymentTypes.FindAsync(id);
            return paymentType == null ? null : new PaymentTypeDto { Id = paymentType.Id, Name = paymentType.Name };
        }

        public async Task<PaymentTypeDto> CreatePaymentTypeAsync(CreatePaymentTypeRequest request)
        {
            _logger.LogInformation("Creating new payment type with name: {Name}", request.Name);
            var paymentType = new PaymentType { Name = request.Name };
            _context.PaymentTypes.Add(paymentType);
            await _context.SaveChangesAsync();
            return new PaymentTypeDto { Id = paymentType.Id, Name = paymentType.Name };
        }

        public async Task<PaymentTypeDto?> UpdatePaymentTypeAsync(int id, UpdatePaymentTypeRequest request)
        {
            _logger.LogInformation("Updating payment type with ID: {Id}", id);
            var paymentType = await _context.PaymentTypes.FindAsync(id);
            if (paymentType == null)
            {
                _logger.LogWarning("Payment type with ID {Id} not found for update.", id);
                return null;
            }
            paymentType.Name = request.Name;
            await _context.SaveChangesAsync();
            return new PaymentTypeDto { Id = paymentType.Id, Name = paymentType.Name };
        }

        public async Task<bool> DeletePaymentTypeAsync(int id)
        {
            _logger.LogInformation("Deleting payment type with ID: {Id}", id);
            var paymentType = await _context.PaymentTypes.FindAsync(id);
            if (paymentType == null)
            {
                _logger.LogWarning("Payment type with ID {Id} not found for deletion.", id);
                return false;
            }
            _context.PaymentTypes.Remove(paymentType);
            await _context.SaveChangesAsync();
            return true;
        }

        // Summary operations
        public async Task<FinancialSummaryDto> GetFinancialSummaryAsync()
        {
            _logger.LogInformation("Calculating financial summary.");
            var payments = await _context.Payments.ToListAsync();
            var totalIncome = payments.Where(p => p.PaymentDirectionId == 1).Sum(p => p.Amount); // Assuming 1 is Income direction
            var totalExpense = payments.Where(p => p.PaymentDirectionId == 2).Sum(p => p.Amount); // Assuming 2 is Expense direction
            var netBalance = totalIncome - totalExpense;

            return new FinancialSummaryDto
            {
                TotalIncome = totalIncome,
                TotalExpense = totalExpense,
                NetBalance = netBalance
            };
        }

        public async Task<IEnumerable<SummaryDetailDto>> GetSummaryDetailsAsync()
        {
            _logger.LogInformation("Getting summary details.");
            var summaryDetails = await _context.Payments
                .GroupBy(p => p.PaymentType!.Name) // Assuming PaymentType is loaded or can be joined
                .Select(g => new SummaryDetailDto
                {
                    Category = g.Key,
                    Amount = g.Sum(p => p.Amount)
                })
                .ToListAsync();
            return summaryDetails;
        }
    }
}
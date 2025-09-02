using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Maliev.PaymentService.Api.Services;
using Maliev.PaymentService.Data.Database.PaymentContext;
using Maliev.PaymentService.Api.Models;
using System;
using Maliev.PaymentService.Common.Enumerations;

namespace Maliev.PaymentService.Tests
{
    public class PaymentServiceServiceTests
    {
        private readonly PaymentContext _context;
        private readonly PaymentServiceService _service;
        private readonly ILogger<PaymentServiceService> _logger;

        public PaymentServiceServiceTests()
        {
            var options = new DbContextOptionsBuilder<PaymentContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new PaymentContext(options);
            _logger = Mock.Of<ILogger<PaymentServiceService>>();
            _service = new PaymentServiceService(_context, _logger);

            // Seed data for common entities
            _context.PaymentDirections.AddRange(
                new PaymentDirection { Id = 1, Name = "Income" },
                new PaymentDirection { Id = 2, Name = "Expense" }
            );
            _context.PaymentMethods.AddRange(
                new PaymentMethod { Id = 1, Name = "Cash" },
                new PaymentMethod { Id = 2, Name = "Credit Card" }
            );
            _context.PaymentTypes.AddRange(
                new PaymentType { Id = 1, Name = "Salary" },
                new PaymentType { Id = 2, Name = "Groceries" }
            );
            _context.SaveChanges();
        }

        [Fact]
        public async Task GetAccountsAsync_ReturnsAllAccounts()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Test Account 1", Balance = 100.00m });
            _context.Accounts.Add(new Account { Id = 2, Name = "Test Account 2", Balance = 200.00m });
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetAccountsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetAccountByIdAsync_ReturnsAccount_WhenAccountExists()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Test Account", Balance = 100.00m });
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetAccountByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Test Account", result.Name);
        }

        [Fact]
        public async Task GetAccountByIdAsync_ReturnsNull_WhenAccountDoesNotExist()
        {
            // Act
            var result = await _service.GetAccountByIdAsync(99);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateAccountAsync_CreatesAndReturnsAccount()
        {
            // Arrange
            var request = new CreateAccountRequest { Name = "New Account", Balance = 500.00m };

            // Act
            var result = await _service.CreateAccountAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal("New Account", result.Name);
            Assert.Equal(500.00m, result.Balance);
            Assert.NotNull(await _context.Accounts.FindAsync(result.Id));
        }

        [Fact]
        public async Task UpdateAccountAsync_UpdatesAndReturnsAccount_WhenAccountExists()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Original Account", Balance = 100.00m });
            await _context.SaveChangesAsync();
            var request = new UpdateAccountRequest { Name = "Updated Account", Balance = 150.00m };

            // Act
            var result = await _service.UpdateAccountAsync(1, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("Updated Account", result.Name);
            Assert.Equal(150.00m, result.Balance);
            var updatedAccount = await _context.Accounts.FindAsync(1);
            Assert.Equal("Updated Account", updatedAccount!.Name);
        }

        [Fact]
        public async Task UpdateAccountAsync_ReturnsNull_WhenAccountDoesNotExist()
        {
            // Arrange
            var request = new UpdateAccountRequest { Name = "Updated Account", Balance = 150.00m };

            // Act
            var result = await _service.UpdateAccountAsync(99, request);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteAccountAsync_DeletesAccount_WhenAccountExists()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Account to Delete", Balance = 100.00m });
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.DeleteAccountAsync(1);

            // Assert
            Assert.True(result);
            Assert.Null(await _context.Accounts.FindAsync(1));
        }

        [Fact]
        public async Task DeleteAccountAsync_ReturnsFalse_WhenAccountDoesNotExist()
        {
            // Act
            var result = await _service.DeleteAccountAsync(99);

            // Assert
            Assert.False(result);
        }

        // Payments Tests
        [Fact]
        public async Task GetPaymentsAsync_ReturnsAllPayments()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.Add(new Payment { Id = 1, Amount = 50, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 });
            _context.Payments.Add(new Payment { Id = 2, Amount = 75, Date = DateTime.Now.AddDays(-1), AccountId = 1, PaymentDirectionId = 2, PaymentMethodId = 2, PaymentTypeId = 2 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetPaymentsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Theory]
        [InlineData(PaymentSortType.DateAscending)]
        [InlineData(PaymentSortType.DateDescending)]
        [InlineData(PaymentSortType.AmountAscending)]
        [InlineData(PaymentSortType.AmountDescending)]
        [InlineData(PaymentSortType.None)]
        public async Task GetPaymentsAsync_ReturnsSortedPayments(PaymentSortType sortType)
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.Add(new Payment { Id = 1, Amount = 50, Date = new DateTime(2023, 1, 1), AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 });
            _context.Payments.Add(new Payment { Id = 2, Amount = 75, Date = new DateTime(2023, 1, 2), AccountId = 1, PaymentDirectionId = 2, PaymentMethodId = 2, PaymentTypeId = 2 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetPaymentsAsync(sortType);

            // Assert
            Assert.NotNull(result);
            var payments = result.ToList();

            switch (sortType)
            {
                case PaymentSortType.DateAscending:
                    Assert.True(payments[0].Date < payments[1].Date);
                    break;
                case PaymentSortType.DateDescending:
                    Assert.True(payments[0].Date > payments[1].Date);
                    break;
                case PaymentSortType.AmountAscending:
                    Assert.True(payments[0].Amount < payments[1].Amount);
                    break;
                case PaymentSortType.AmountDescending:
                    Assert.True(payments[0].Amount > payments[1].Amount);
                    break;
                case PaymentSortType.None:
                    // No specific order asserted for None
                    break;
            }
        }

        [Fact]
        public async Task GetPaymentByIdAsync_ReturnsPayment_WhenPaymentExists()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.Add(new Payment { Id = 1, Amount = 50, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetPaymentByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task GetPaymentByIdAsync_ReturnsNull_WhenPaymentDoesNotExist()
        {
            // Act
            var result = await _service.GetPaymentByIdAsync(99);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CreatePaymentAsync_CreatesAndReturnsPayment()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            await _context.SaveChangesAsync();
            var request = new CreatePaymentRequest
            {
                Amount = 100,
                Date = DateTime.Now,
                AccountId = 1,
                PaymentDirectionId = 1,
                PaymentMethodId = 1,
                PaymentTypeId = 1
            };

            // Act
            var result = await _service.CreatePaymentAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal(100, result.Amount);
            Assert.NotNull(await _context.Payments.FindAsync(result.Id));
        }

        [Fact]
        public async Task UpdatePaymentAsync_UpdatesAndReturnsPayment_WhenPaymentExists()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.Add(new Payment { Id = 1, Amount = 50, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 });
            await _context.SaveChangesAsync();
            var request = new UpdatePaymentRequest
            {
                Amount = 150,
                Date = DateTime.Now.AddDays(1),
                AccountId = 1,
                PaymentDirectionId = 2,
                PaymentMethodId = 2,
                PaymentTypeId = 2
            };

            // Act
            var result = await _service.UpdatePaymentAsync(1, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal(150, result.Amount);
            var updatedPayment = await _context.Payments.FindAsync(1);
            Assert.Equal(150, updatedPayment!.Amount);
        }

        [Fact]
        public async Task UpdatePaymentAsync_ReturnsNull_WhenPaymentDoesNotExist()
        {
            // Arrange
            var request = new UpdatePaymentRequest
            {
                Amount = 150,
                Date = DateTime.Now,
                AccountId = 1,
                PaymentDirectionId = 1,
                PaymentMethodId = 1,
                PaymentTypeId = 1
            };

            // Act
            var result = await _service.UpdatePaymentAsync(99, request);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task DeletePaymentAsync_DeletesPayment_WhenPaymentExists()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.Add(new Payment { Id = 1, Amount = 50, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.DeletePaymentAsync(1);

            // Assert
            Assert.True(result);
            Assert.Null(await _context.Payments.FindAsync(1));
        }

        [Fact]
        public async Task DeletePaymentAsync_ReturnsFalse_WhenPaymentDoesNotExist()
        {
            // Act
            var result = await _service.DeletePaymentAsync(99);

            // Assert
            Assert.False(result);
        }

        // PaymentDirection Tests
        [Fact]
        public async Task GetPaymentDirectionsAsync_ReturnsAllPaymentDirections()
        {
            // Act
            var result = await _service.GetPaymentDirectionsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetPaymentDirectionByIdAsync_ReturnsPaymentDirection_WhenExists()
        {
            // Act
            var result = await _service.GetPaymentDirectionByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task CreatePaymentDirectionAsync_CreatesAndReturnsPaymentDirection()
        {
            // Arrange
            var request = new CreatePaymentDirectionRequest { Name = "New Direction" };

            // Act
            var result = await _service.CreatePaymentDirectionAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal("New Direction", result.Name);
        }

        [Fact]
        public async Task UpdatePaymentDirectionAsync_UpdatesAndReturnsPaymentDirection()
        {
            // Arrange
            var request = new UpdatePaymentDirectionRequest { Name = "Updated Direction" };

            // Act
            var result = await _service.UpdatePaymentDirectionAsync(1, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated Direction", result.Name);
        }

        [Fact]
        public async Task DeletePaymentDirectionAsync_DeletesPaymentDirection()
        {
            // Act
            var result = await _service.DeletePaymentDirectionAsync(1);

            // Assert
            Assert.True(result);
        }

        // PaymentFile Tests (assuming Payment exists)
        [Fact]
        public async Task GetPaymentFilesAsync_ReturnsAllPaymentFiles()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.Add(new Payment { Id = 1, Amount = 50, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 });
            _context.PaymentFiles.Add(new PaymentFile { Id = 1, FileName = "File1.pdf", FilePath = "/path/to/file1.pdf", UploadDate = DateTime.Now, PaymentId = 1 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetPaymentFilesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task GetPaymentFileByIdAsync_ReturnsPaymentFile_WhenExists()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.Add(new Payment { Id = 1, Amount = 50, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 });
            _context.PaymentFiles.Add(new PaymentFile { Id = 1, FileName = "File1.pdf", FilePath = "/path/to/file1.pdf", UploadDate = DateTime.Now, PaymentId = 1 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetPaymentFileByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task CreatePaymentFileAsync_CreatesAndReturnsPaymentFile()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.Add(new Payment { Id = 1, Amount = 50, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 });
            await _context.SaveChangesAsync();
            var request = new CreatePaymentFileRequest { FileName = "NewFile.jpg", FilePath = "/new/path/NewFile.jpg", UploadDate = DateTime.Now, PaymentId = 1 };

            // Act
            var result = await _service.CreatePaymentFileAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal("NewFile.jpg", result.FileName);
        }

        [Fact]
        public async Task UpdatePaymentFileAsync_UpdatesAndReturnsPaymentFile()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.Add(new Payment { Id = 1, Amount = 50, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 });
            _context.PaymentFiles.Add(new PaymentFile { Id = 1, FileName = "Original.pdf", FilePath = "/path/Original.pdf", UploadDate = DateTime.Now, PaymentId = 1 });
            await _context.SaveChangesAsync();
            var request = new UpdatePaymentFileRequest { FileName = "Updated.pdf", FilePath = "/path/Updated.pdf", UploadDate = DateTime.Now, PaymentId = 1 };

            // Act
            var result = await _service.UpdatePaymentFileAsync(1, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated.pdf", result.FileName);
        }

        [Fact]
        public async Task DeletePaymentFileAsync_DeletesPaymentFile()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.Add(new Payment { Id = 1, Amount = 50, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 });
            _context.PaymentFiles.Add(new PaymentFile { Id = 1, FileName = "FileToDelete.pdf", FilePath = "/path/FileToDelete.pdf", UploadDate = DateTime.Now, PaymentId = 1 });
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.DeletePaymentFileAsync(1);

            // Assert
            Assert.True(result);
        }

        // PaymentMethod Tests
        [Fact]
        public async Task GetPaymentMethodsAsync_ReturnsAllPaymentMethods()
        {
            // Act
            var result = await _service.GetPaymentMethodsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetPaymentMethodByIdAsync_ReturnsPaymentMethod_WhenExists()
        {
            // Act
            var result = await _service.GetPaymentMethodByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task CreatePaymentMethodAsync_CreatesAndReturnsPaymentMethod()
        {
            // Arrange
            var request = new CreatePaymentMethodRequest { Name = "New Method" };

            // Act
            var result = await _service.CreatePaymentMethodAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal("New Method", result.Name);
        }

        [Fact]
        public async Task UpdatePaymentMethodAsync_UpdatesAndReturnsPaymentMethod()
        {
            // Arrange
            var request = new UpdatePaymentMethodRequest { Name = "Updated Method" };

            // Act
            var result = await _service.UpdatePaymentMethodAsync(1, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated Method", result.Name);
        }

        [Fact]
        public async Task DeletePaymentMethodAsync_DeletesPaymentMethod()
        {
            // Act
            var result = await _service.DeletePaymentMethodAsync(1);

            // Assert
            Assert.True(result);
        }

        // PaymentType Tests
        [Fact]
        public async Task GetPaymentTypesAsync_ReturnsAllPaymentTypes()
        {
            // Act
            var result = await _service.GetPaymentTypesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetPaymentTypeByIdAsync_ReturnsPaymentType_WhenExists()
        {
            // Act
            var result = await _service.GetPaymentTypeByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task CreatePaymentTypeAsync_CreatesAndReturnsPaymentType()
        {
            // Arrange
            var request = new CreatePaymentTypeRequest { Name = "New Type" };

            // Act
            var result = await _service.CreatePaymentTypeAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal("New Type", result.Name);
        }

        [Fact]
        public async Task UpdatePaymentTypeAsync_UpdatesAndReturnsPaymentType()
        {
            // Arrange
            var request = new UpdatePaymentTypeRequest { Name = "Updated Type" };

            // Act
            var result = await _service.UpdatePaymentTypeAsync(1, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated Type", result.Name);
        }

        [Fact]
        public async Task DeletePaymentTypeAsync_DeletesPaymentType()
        {
            // Act
            var result = await _service.DeletePaymentTypeAsync(1);

            // Assert
            Assert.True(result);
        }

        // Summary Tests
        [Fact]
        public async Task GetFinancialSummaryAsync_ReturnsCorrectSummary()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.AddRange(
                new Payment { Id = 1, Amount = 100, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1 }, // Income
                new Payment { Id = 2, Amount = 50, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 2, PaymentMethodId = 1, PaymentTypeId = 2 }  // Expense
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetFinancialSummaryAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result.TotalIncome);
            Assert.Equal(50, result.TotalExpense);
            Assert.Equal(50, result.NetBalance);
        }

        [Fact]
        public async Task GetSummaryDetailsAsync_ReturnsCorrectSummaryDetails()
        {
            // Arrange
            _context.Accounts.Add(new Account { Id = 1, Name = "Acc1", Balance = 100 });
            _context.Payments.AddRange(
                new Payment { Id = 1, Amount = 100, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 1, PaymentMethodId = 1, PaymentTypeId = 1, PaymentType = _context.PaymentTypes.Find(1) }, // Salary
                new Payment { Id = 2, Amount = 50, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 2, PaymentMethodId = 1, PaymentTypeId = 2, PaymentType = _context.PaymentTypes.Find(2) },  // Groceries
                new Payment { Id = 3, Amount = 25, Date = DateTime.Now, AccountId = 1, PaymentDirectionId = 2, PaymentMethodId = 1, PaymentTypeId = 2, PaymentType = _context.PaymentTypes.Find(2) }   // Groceries
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetSummaryDetailsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.Contains(result, d => d.Category == "Salary" && d.Amount == 100);
            Assert.Contains(result, d => d.Category == "Groceries" && d.Amount == 75);
        }
    }
}
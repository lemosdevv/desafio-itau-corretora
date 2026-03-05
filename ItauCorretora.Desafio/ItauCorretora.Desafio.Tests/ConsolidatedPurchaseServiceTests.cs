using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.Models;
using ItauCorretora.Desafio.Services.Implementations;
using ItauCorretora.Desafio.Services.Interfaces;
using ItauCorretora.Desafio.Kafka.Producers;
using Microsoft.Extensions.Logging;

namespace ItauCorretora.Desafio.Tests;

public class ConsolidatedPurchaseServiceTests
{
    private readonly AppDbContext _context;
    private readonly ConsolidatedPurchaseService _service;
    private readonly Mock<ILogger<ConsolidatedPurchaseService>> _loggerMock;
    private readonly Mock<IKafkaProducer> _kafkaProducerMock;

    public ConsolidatedPurchaseServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .EnableSensitiveDataLogging()
            .Options;
        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<ConsolidatedPurchaseService>>();
        _kafkaProducerMock = new Mock<IKafkaProducer>();
        _service = new ConsolidatedPurchaseService(_context, _loggerMock.Object, _kafkaProducerMock.Object);
    }

[Fact]
public async Task ExecutePurchaseAsync_WithActiveClientsAndActiveWallet_ShouldCreateOrdersAndDistributions()
{
    // Arrange
    SeedDatabase();

    // Act
    var result = await _service.ExecutePurchaseAsync(new DateTime(2026, 3, 5));

    // Assert
    Assert.True(result.Success, result.Message); // Mostra a mensagem se falhar
    Assert.Equal(2, result.TotalClients);
    Assert.Equal(3000m, result.TotalAmount); // 1000 + 2000
    Assert.NotEmpty(result.Orders);
    Assert.NotEmpty(result.Distributions);
    Assert.Equal(2, result.Distributions.Count);
    _kafkaProducerMock.Verify(p => p.ProduceAsync("ir-dedo-duro", It.IsAny<string>()), Times.AtLeastOnce);
}

private void SeedDatabase()
{
    var testDate = new DateTime(2026, 3, 5); // Data fixa para o teste

    // Clientes ativos
    var customer1 = new Customer
    {
        Id = 1,
        Name = "João",
        CPF = "12345678901",
        Email = "joao@email.com",
        MonthlyValue = 3000,
        Active = true,
        SubscriptionDate = DateTime.Now.AddMonths(-1),
        Account = new Account { Id = 1, Balance = 5000, Type = AccountType.Filhote, CustomerId = 1 }
    };
    var customer2 = new Customer
    {
        Id = 2,
        Name = "Maria",
        CPF = "09876543210",
        Email = "maria@email.com",
        MonthlyValue = 6000,
        Active = true,
        SubscriptionDate = DateTime.Now.AddMonths(-1),
        Account = new Account { Id = 2, Balance = 10000, Type = AccountType.Filhote, CustomerId = 2 }
    };
    _context.Customers.AddRange(customer1, customer2);

    // Conta master
    _context.Accounts.Add(new Account { Id = 99, Type = AccountType.Master, Balance = 0 });

    // Cesta ativa
    var wallet = new RecommendedWallet
    {
        Id = 1,
        Name = "Cesta Teste",
        Active = true,
        StartDate = DateTime.Now.AddMonths(-1)
    };
    _context.RecommendedWallets.Add(wallet);

    // Itens da cesta
    var stockPetr4 = new Stock { Id = 1, Code = "PETR4", Name = "Petrobras" };
    var stockVale3 = new Stock { Id = 2, Code = "VALE3", Name = "Vale" };
    var stockItub4 = new Stock { Id = 3, Code = "ITUB4", Name = "Itaú" };
    var stockBrdc4 = new Stock { Id = 4, Code = "BBDC4", Name = "Bradesco" };
    var stockWege3 = new Stock { Id = 5, Code = "WEGE3", Name = "WEG" };
    _context.Stocks.AddRange(stockPetr4, stockVale3, stockItub4, stockBrdc4, stockWege3);

    _context.WalletRecommendedItems.AddRange(
        new WalletRecommendedItem { Id = 1, RecommendedWalletId = 1, StockId = 1, Weight = 30 },
        new WalletRecommendedItem { Id = 2, RecommendedWalletId = 1, StockId = 2, Weight = 25 },
        new WalletRecommendedItem { Id = 3, RecommendedWalletId = 1, StockId = 3, Weight = 20 },
        new WalletRecommendedItem { Id = 4, RecommendedWalletId = 1, StockId = 4, Weight = 15 },
        new WalletRecommendedItem { Id = 5, RecommendedWalletId = 1, StockId = 5, Weight = 10 }
    );

    // Cotações para a data do teste
    _context.Quotes.AddRange(
        new Quote { StockId = 1, Date = testDate, ClosePrice = 35.80m },
        new Quote { StockId = 2, Date = testDate, ClosePrice = 62.30m },
        new Quote { StockId = 3, Date = testDate, ClosePrice = 30.10m },
        new Quote { StockId = 4, Date = testDate, ClosePrice = 15.20m },
        new Quote { StockId = 5, Date = testDate, ClosePrice = 40.20m }
    );

    _context.SaveChanges();
}
}
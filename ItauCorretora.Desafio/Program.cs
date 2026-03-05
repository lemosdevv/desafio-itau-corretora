using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.Kafka.Producers;
using ItauCorretora.Desafio.Models;
using ItauCorretora.Desafio.Services.Implementations;
using ItauCorretora.Desafio.Services.Interfaces;
using ItauCorretora.Desafio.Workers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Config DbContext with MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Register services and workers
builder.Services.AddScoped<IPurchaseEngineService, PurchaseEngineService>();
builder.Services.AddScoped<IRebalancementService, RebalancementService>();
builder.Services.AddScoped<IConsolidatedPurchaseService, ConsolidatedPurchaseService>();
builder.Services.AddScoped<IIncomeTaxService, IncomeTaxService>();
builder.Services.AddHostedService<PurchaseSchedulerWorker>();

// Kafka Producers
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
builder.Services.AddHostedService<RebalancementWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Check if a master account already exists.
    var masterAccount = context.Accounts.FirstOrDefault(a => a.Type == AccountType.Master);
    if (masterAccount == null)
    {
        masterAccount = new Account
        {
            Type = AccountType.Master,
            Balance = 0,
            CustomerId = null
        };
        context.Accounts.Add(masterAccount);
        context.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("Application configured, trying to start Kestrel....");

try
{
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Error starting the web server: {ex}");
}
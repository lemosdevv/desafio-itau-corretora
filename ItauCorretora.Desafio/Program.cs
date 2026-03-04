using ItauCorretora.Desafio.Data;
using ItauCorretora.Desafio.Kafka.Consumers;
using ItauCorretora.Desafio.Kafka.Producers;
using ItauCorretora.Desafio.Services.Implementations;
using ItauCorretora.Desafio.Services.Interfaces;
using ItauCorretora.Desafio.Workers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configurar DbContext com MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Registrar serviços e workers (já deve ter)
builder.Services.AddScoped<IPurchaseEngineService, PurchaseEngineService>();
builder.Services.AddScoped<IRebalancementService, RebalancementService>();
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
//builder.Services.AddHostedService<OrderExecutedConsumer>();
//builder.Services.AddHostedService<RebalancementWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("Aplicação configurada, tentando iniciar Kestrel...");

try
{
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Erro ao iniciar o servidor web: {ex}");
}
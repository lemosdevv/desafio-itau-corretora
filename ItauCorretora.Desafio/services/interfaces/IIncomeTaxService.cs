namespace ItauCorretora.Desafio.Services.Interfaces;

public interface IIncomeTaxService
{
    Task CalculateMonthlyTaxAsync(int year, int month);
    Task<IncomeTaxResult> CalculateCustomerTaxAsync(int customerId, int year, int month);
}

public class IncomeTaxResult
{
    public int CustomerId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalLoss { get; set; }
    public decimal NetProfit { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxDue { get; set; }
}
namespace ItauCorretora.Desafio.Services.Interfaces;

public interface ICotahistParserService
{
    Task<int> ParseAndUpdateQuotesAsync(string filePath);
}
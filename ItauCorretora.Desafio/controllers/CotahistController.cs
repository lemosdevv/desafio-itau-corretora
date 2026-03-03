using ItauCorretora.Desafio.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ItauCorretora.Desafio.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CotahistController : ControllerBase
{
    private readonly ICotahistParserService _parserService;

    public CotahistController(ICotahistParserService parserService)
    {
        _parserService = parserService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No files uploaded..");

        // Save temporarily
        var tempPath = Path.GetTempFileName();
        using (var stream = System.IO.File.Create(tempPath))
        {
            await file.CopyToAsync(stream);
        }

        try
        {
            var count = await _parserService.ParseAndUpdateQuotesAsync(tempPath);
            return Ok(new { Processed = count, Message = "File processed successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error processing file: {ex.Message}");
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }
}
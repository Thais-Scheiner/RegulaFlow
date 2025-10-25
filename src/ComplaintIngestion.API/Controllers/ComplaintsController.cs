using ComplaintIngestion.API.Models;
using ComplaintIngestion.API.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace ComplaintIngestion.API.Controllers;

[ApiController]
[Route("api/[controller]")] // Rota base ser� /api/Complaints
public class ComplaintsController : ControllerBase
{
    private readonly SqsPublisherService _sqsPublisher;
    private readonly ILogger<ComplaintsController> _logger;

    // Injetamos o servi�o SQS e o logger
    public ComplaintsController(SqsPublisherService sqsPublisher, ILogger<ComplaintsController> logger)
    {
        _sqsPublisher = sqsPublisher;
        _logger = logger;
    }

    [HttpPost] // Mapeia para requisi��es POST
    [ProducesResponseType(StatusCodes.Status202Accepted)] // Resposta de sucesso
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // Erro de valida��o
    [ProducesResponseType(StatusCodes.Status500InternalServerError)] // Erro interno (ex: falha ao enviar SQS)
    public async Task<IActionResult> SubmitComplaint([FromBody] ComplaintRequest request)
    {
        // A valida��o do modelo (DataAnnotations) � feita automaticamente pelo ASP.NET Core
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Recebida requisi��o inv�lida para submeter reclama��o.");
            return BadRequest(ModelState); // Retorna 400 com os detalhes do erro
        }

        try
        {
            _logger.LogInformation("Recebida nova reclama��o de {CustomerEmail}", request.CustomerEmail);
            await _sqsPublisher.PublishComplaintAsync(request);

            // Retornamos 202 Accepted, indicando que a requisi��o foi aceita
            // para processamento ass�ncrono, mas ainda n�o foi conclu�da.
            _logger.LogInformation("Reclama��o de {CustomerEmail} enviada para processamento.", request.CustomerEmail);
            return Accepted();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar a submiss�o da reclama��o de {CustomerEmail}", request.CustomerEmail);
            // Retorna um erro gen�rico 500 para n�o expor detalhes internos
            return StatusCode(StatusCodes.Status500InternalServerError, "Ocorreu um erro ao processar sua solicita��o.");
        }
    }
}
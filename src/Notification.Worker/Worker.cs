using Amazon.SQS;
using Amazon.SQS.Model;
using Notification.Worker.Events;
using System.Text.Json;
using System.Text.Json.Serialization; // Adicionar este using

namespace Notification.Worker;

// DTO para deserializar a mensagem externa que vem do SNS
// Adicionamos o [JsonPropertyName] para garantir o mapeamento correto, independentemente do case.
public record SnsMessage(
    [property: JsonPropertyName("Message")] string Message
);

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IAmazonSQS _sqsClient;
    private readonly string _queueUrl;

    public Worker(ILogger<Worker> logger, IAmazonSQS sqsClient, IConfiguration configuration)
    {
        _logger = logger;
        _sqsClient = sqsClient;
        _queueUrl = configuration["Aws:SqsQueueUrl"] ?? throw new ArgumentNullException("Aws:SqsQueueUrl");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de Notifica��o iniciado em: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Verificando a fila de notifica��es por novas mensagens...");

            var receiveRequest = new ReceiveMessageRequest
            {
                QueueUrl = _queueUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 20
            };

            var response = await _sqsClient.ReceiveMessageAsync(receiveRequest, stoppingToken);

            // --- CORRE��O 1: VERIFICA��O DEFENSIVA ---
            // Garante que a resposta e a lista de mensagens n�o s�o nulas antes de continuar.
            if (response?.Messages?.Any() != true)
            {
                _logger.LogInformation("Nenhuma mensagem na fila.");
                continue; // Volta para o in�cio do loop
            }

            _logger.LogInformation("{count} mensagens de notifica��o recebidas.", response.Messages.Count);

            foreach (var message in response.Messages)
            {
                try
                {
                    // --- CORRE��O 2: VERIFICA��O DE MENSAGEM VAZIA ---
                    if (string.IsNullOrWhiteSpace(message.Body))
                    {
                        _logger.LogWarning("Recebida mensagem com corpo vazio ou nulo. MessageId: {id}", message.MessageId);
                        // Apaga a mensagem "ruim" para n�o travar a fila
                        await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                        continue;
                    }

                    var snsMessage = JsonSerializer.Deserialize<SnsMessage>(message.Body);

                    // --- CORRE��O 3: VERIFICA��O AP�S DESERIALIZA��O ---
                    if (snsMessage is null || string.IsNullOrWhiteSpace(snsMessage.Message))
                    {
                        _logger.LogWarning("N�o foi poss�vel deserializar o envelope SNS ou a mensagem interna est� vazia. Body: {body}", message.Body);
                        await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                        continue;
                    }

                    var processedEvent = JsonSerializer.Deserialize<ComplaintProcessedEvent>(snsMessage.Message);
                    if (processedEvent is null)
                    {
                        _logger.LogWarning("N�o foi poss�vel deserializar o evento ComplaintProcessedEvent. Message: {message}", snsMessage.Message);
                        await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                        continue;
                    }

                    _logger.LogInformation("Evento de reclama��o processada recebido para o ID: {ComplaintId}", processedEvent.ComplaintId);

                    _logger.LogInformation(">>> SIMULANDO ENVIO DE E-MAIL para {CustomerEmail}: Sua reclama��o sobre '{ComplaintType}' foi recebida e est� sendo processada.",
                        processedEvent.CustomerEmail, processedEvent.ComplaintType);

                    await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                    _logger.LogInformation("Mensagem de notifica��o apagada da fila.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro fatal ao processar a mensagem de notifica��o {messageId}. A mensagem n�o ser� apagada para an�lise.", message.MessageId);
                }
            }
        }
    }
}
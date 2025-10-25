using System.ComponentModel.DataAnnotations;

namespace ComplaintIngestion.API.Models;

// Usando Records do C# para DTOs imut�veis e concisos
public record ComplaintRequest(
    [Required] string CustomerName,
    [Required][EmailAddress] string CustomerEmail,
    [Required] string ComplaintType, // Ex: "Cobran�a Indevida", "Atendimento", "Produto Defeituoso"
    [Required][StringLength(1000, MinimumLength = 10)] string Description
);
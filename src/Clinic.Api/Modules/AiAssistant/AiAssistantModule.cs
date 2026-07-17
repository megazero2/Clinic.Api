using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Modules.AiAssistant;

public static class AiAssistantModule
{
    public static IServiceCollection AddAiAssistantModule(this IServiceCollection services)
    {
        services.AddScoped<AiAssistantService>();
        return services;
    }
}

public sealed record GenerateMessageRequest(Guid? ClientId, [Required] string Purpose, [Required] string Channel, [Required] string Tone);
public sealed record AiResponse(string Content, string Model, DateTimeOffset CreatedUtc);

public sealed class AiAssistantService
{
    public AiResponse GenerateMessage(GenerateMessageRequest request)
    {
        var message = request.Purpose.Trim().ToLowerInvariant() switch
        {
            "appointment-reminder" => request.Channel.Equals("sms", StringComparison.OrdinalIgnoreCase)
                ? "Hola, te recordamos tu cita. Responde CONFIRMAR para apartar tu lugar."
                : "Hola, te recordamos tu proxima cita. Si necesitas cambiar el horario, responde a este mensaje.",
            "follow-up" => "Hola, queremos dar seguimiento a tu ultima visita y resolver cualquier duda.",
            _ => $"Hola, te contactamos sobre: {request.Purpose}."
        };

        return new AiResponse(message, "rule-based-dev-assistant", DateTimeOffset.UtcNow);
    }
}

[ApiController]
[Route("api/ai")]
public sealed class AiController(AiAssistantService ai) : ControllerBase
{
    [HttpPost("generate-message")]
    public ActionResult<AiResponse> GenerateMessage(GenerateMessageRequest request) => Ok(ai.GenerateMessage(request));
}

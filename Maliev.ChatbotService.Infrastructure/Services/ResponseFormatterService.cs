using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Infrastructure.Services;

/// <summary>
/// Service for formatting chatbot responses with suggested actions.
/// </summary>
public class ResponseFormatterService : IResponseFormatterService
{
    /// <inheritdoc/>
    public (string FormattedContent, List<SuggestedActionDto> SuggestedActions) FormatResponse(string content, Language language)
    {
        var suggestedActions = new List<SuggestedActionDto>();

        // Add suggested actions based on language
        if (language == Language.Thai)
        {
            suggestedActions.Add(new SuggestedActionDto
            {
                Text = "ดูบริการทั้งหมด",
                Type = "view_services",
                Data = "all"
            });
            suggestedActions.Add(new SuggestedActionDto
            {
                Text = "ติดต่อเรา",
                Type = "contact",
                Data = "general"
            });
            suggestedActions.Add(new SuggestedActionDto
            {
                Text = "ขอใบเสนอราคา",
                Type = "request_quote",
                Data = null
            });
        }
        else
        {
            suggestedActions.Add(new SuggestedActionDto
            {
                Text = "View All Services",
                Type = "view_services",
                Data = "all"
            });
            suggestedActions.Add(new SuggestedActionDto
            {
                Text = "Contact Us",
                Type = "contact",
                Data = "general"
            });
            suggestedActions.Add(new SuggestedActionDto
            {
                Text = "Request a Quote",
                Type = "request_quote",
                Data = null
            });
        }

        return (content, suggestedActions);
    }

    /// <inheritdoc/>
    public string GetWelcomeMessage(Language language)
    {
        return language == Language.Thai
            ? "สวัสดีค่ะ! ยินดีต้อนรับสู่ Maliev Manufacturing Services ฉันสามารถช่วยคุณเกี่ยวกับบริการผลิตของเราได้อย่างไร?"
            : "Hello! Welcome to Maliev Manufacturing Services. How can I help you with our manufacturing services today?";
    }
}

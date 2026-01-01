using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Domain.Entities;

/// <summary>
/// Represents a unified user identity across platforms.
/// </summary>
public class UserProfile
{
    /// <summary>
    /// Gets or sets the unique identifier for the user profile.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the internal user ID if linked to internal employee/customer ID.
    /// </summary>
    public string? InternalUserId { get; set; }

    /// <summary>
    /// Gets or sets the LINE user ID.
    /// </summary>
    public string? LineUserId { get; set; }

    /// <summary>
    /// Gets or sets the Facebook user ID.
    /// </summary>
    public string? FacebookId { get; set; }

    /// <summary>
    /// Gets or sets the Instagram user ID.
    /// </summary>
    public string? InstagramId { get; set; }

    /// <summary>
    /// Gets or sets the WhatsApp user ID.
    /// </summary>
    public string? WhatsAppId { get; set; }

    /// <summary>
    /// Gets or sets the role of the user.
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the profile was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last user activity.
    /// </summary>
    public DateTimeOffset LastActiveAt { get; set; }

    /// <summary>
    /// Gets or sets the collection of conversation sessions for this user.
    /// </summary>
    public ICollection<ConversationSession> Sessions { get; set; } = new List<ConversationSession>();

    /// <summary>
    /// Gets or sets the collection of user memories for this user.
    /// </summary>
    public ICollection<UserMemory> Memories { get; set; } = new List<UserMemory>();

    /// <summary>
    /// Gets or sets the collection of identity links for this user.
    /// </summary>
    public ICollection<IdentityLink> IdentityLinks { get; set; } = new List<IdentityLink>();

    /// <summary>
    /// Gets or sets the collection of operation logs for this user.
    /// </summary>
    public ICollection<OperationLog> OperationLogs { get; set; } = new List<OperationLog>();
}

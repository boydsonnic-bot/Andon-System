namespace SharedLib.Contracts.Requests;

public class OpenTicketRequest
{
    public string MachineName { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Khuyến nghị cho phân tích timeline/ca
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? ShiftCode { get; set; }

    // Chống duplicate khi retry offline
    public string? IdempotencyKey { get; set; }
}
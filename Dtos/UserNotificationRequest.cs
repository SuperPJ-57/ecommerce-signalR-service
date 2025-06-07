namespace EcommerceSignalrService.Dtos;

public class UserNotificationRequest
{
    public int Id { get; set; }
    public string Username { get; set; }
    public int OrderId { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string Type { get; set; }
    public DateTime CreatedAt { get; set; }
}

namespace EcommerceSignalrService.Hubs;

using EcommerceSignalrService.Dtos;
using EcommerceSignalrService.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Text.Json;



[Authorize(Policy = "UserPolicy")]
public class NotificationHub : Hub
{
    private readonly ConnectedUserManager _connectedUserManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationHub> _logger;
    private readonly string API_URL;

    public NotificationHub(ConnectedUserManager connectedUserManager, IHttpClientFactory httpClientFactory, ILogger<NotificationHub> logger, IOptions<ApiSettings> apiSettings)
    {
        _connectedUserManager = connectedUserManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        API_URL = $"{apiSettings.Value.BaseUrl}/api/notification/trigger-retry";
    }

    public override async Task OnConnectedAsync()
    {
        const string functionName = nameof(OnConnectedAsync);
        var username = GetUsername();

        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("{Function}: User number missing during connection.", functionName);
            await base.OnConnectedAsync();
            return;
        }

        _connectedUserManager.AddUser(username, Context.ConnectionId, GetUserRoles());
        _logger.LogInformation("{Function}: Added user {User} with connection ID {ConnectionId}.", functionName, username, Context.ConnectionId);

        
        try
        {
            using var httpClient = _httpClientFactory.CreateClient("KutumbaLogistics");
            var json = JsonSerializer.Serialize(new { username });
            if (!GetUserRoles().Contains("User"))
            {
                json = JsonSerializer.Serialize(new { username = "" });
            }
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("{Function}: Notifying backend({API}) about user connection: {User}", functionName, API_URL, username);
            var response = await httpClient.PostAsync(API_URL, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("{Function}: Informed backend about user connection: {User}", functionName, username);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("{Function}: Failed to inform backend. Status: {StatusCode}. Error: {Error}", functionName, response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Function}: Exception while notifying backend for user: {User}", functionName, username);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        const string functionName = nameof(OnDisconnectedAsync);
        var username = GetUsername();

        if (!string.IsNullOrWhiteSpace(username))
        {
            _connectedUserManager.RemoveUser(username, Context.ConnectionId);
            _logger.LogInformation("{Function}: Removed user {User} with connection ID {ConnectionId}.", functionName, username, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string? GetUsername()
    {
        return Context.User?.Identity?.Name;
    }
    private List<string> GetUserRoles()
    {
        return Context.User?.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList() ?? new List<string>();
    }

    public async Task SendNotificationToUser(UserNotificationRequest request)
    {
        const string functionName = nameof(SendNotificationToUser);
        var connections = _connectedUserManager.GetConnections(request.Username);

        if (connections == null || connections.Count == 0)
        {
            _logger.LogWarning("{Function}: No active connections for user {User}.", functionName, request.Username);
            return;
        }

        foreach (var connectionId in connections)
        {
            _logger.LogInformation("{Function}: Sending notification to {User} via connection ID {ConnectionId}.", functionName, request.Username, connectionId);
            await Clients.Client(connectionId).SendAsync("ReceiveNotification", request);
        }
    }
    public async Task SendNotificationToAdmin(UserNotificationRequest request)
    {
        const string functionName = nameof(SendNotificationToAdmin);
        var connections = _connectedUserManager.GetAdminConnections(request.Username);

        if (connections == null || connections.Count == 0)
        {
            _logger.LogWarning("{Function}: No active connections for admin {Admin}.", functionName, request.Username);
            return;
        }

        foreach (var connectionId in connections)
        {
            _logger.LogInformation("{Function}: Sending notification to {Admin} via connection ID {ConnectionId}.", functionName, request.Username, connectionId);
            await Clients.Client(connectionId).SendAsync("ReceiveNotification", request);
        }
    }
    public async Task SendNotificationToAll(string message)
    {
        const string functionName = nameof(SendNotificationToAll);
        _logger.LogInformation("{Function}: Broadcasting message to all clients: {Message}", functionName, message);
        await Clients.All.SendAsync("ReceiveNotification", message);
    }
}

namespace EcommerceSignalrService.Controllers;

using EcommerceSignalrService.Dtos;
using EcommerceSignalrService.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Text.Json;


[ApiController]
[Route("api/signalr")]
[Authorize(Policy = "ServicePolicy")]
public class NotificationController : ControllerBase
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ConnectedUserManager _connectedUserManager;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        IHubContext<NotificationHub> hubContext,
        ConnectedUserManager connectedUserManager,
        ILogger<NotificationController> logger)
    {
        _hubContext = hubContext;
        _connectedUserManager = connectedUserManager;
        _logger = logger;
    }

    [HttpPost("send-to-user")]
    public async Task<IActionResult> SendToUser([FromBody] UserNotificationRequest request)
    {
        const string functionName = nameof(SendToUser);
        _logger.LogInformation("{Function}: Payload - {Payload}", functionName, JsonSerializer.Serialize(request));

        var connections = _connectedUserManager.GetConnections(request.Username);
        if (!connections.Any())
            return NotFound("User not connected.");

        foreach (var connectionId in connections)
        {
            await _hubContext.Clients.Client(connectionId)
                .SendAsync("ReceiveNotification", request);
        }

        return Ok();
    }

    [HttpPost("send-to-admin")]
    public async Task<IActionResult> SendToAdmin([FromBody] UserNotificationRequest request)
    {
        const string functionName = nameof(SendToAdmin);
        _logger.LogInformation("{Function}: Payload - {Payload}", functionName, JsonSerializer.Serialize(request));

        var connections = _connectedUserManager.GetAdminConnections();
        if (!connections.Any())
            return NotFound("Admin not connected.");

        foreach (var connectionId in connections)
        {
            await _hubContext.Clients.Client(connectionId)
                .SendAsync("ReceiveNotification", request);
        }

        return Ok();
    }

    //[HttpPost("send-to-all")]
    //public async Task<IActionResult> SendToAll([FromBody] BroadcastNotificationRequest request)
    //{
    //    const string functionName = nameof(SendToAll);
    //    _logger.LogInformation("{Function}: Payload - {Payload}", functionName, JsonSerializer.Serialize(request));

    //    await _hubContext.Clients.All.SendAsync("ReceiveNotification", request);
    //    return Ok();
    //}
}

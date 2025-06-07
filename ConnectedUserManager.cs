namespace EcommerceSignalrService;

public class ConnectedUserManager
{
    private readonly Dictionary<string, HashSet<string>> _userConnections = new();
    private readonly Dictionary<string, HashSet<string>> _adminConnections = new();
    private readonly object _lock = new();
    private readonly ILogger<ConnectedUserManager> _logger;

    public ConnectedUserManager(ILogger<ConnectedUserManager> logger)
    {
        _logger = logger;
    }

    public void AddUser(string username, string connectionId, List<string> roles = null)
    {
        lock (_lock)
        {
            if (roles?.Count > 0)
            {
                if (roles.Contains("Admin"))
                {
                    if (!_adminConnections.ContainsKey(username))
                        _adminConnections[username] = new HashSet<string>();

                    _adminConnections[username].Add(connectionId);

                    _logger.BeginScope("Admin Connection: {User} with connection ID {ConnectionId}", username, connectionId);
                }
                if (roles.Contains("User"))
                {
                    if (!_userConnections.ContainsKey(username))
                        _userConnections[username] = new HashSet<string>();

                    _userConnections[username].Add(connectionId);

                    _logger.BeginScope("User Connection: {User} with connection ID {ConnectionId}", username, connectionId);
                }
            }
            else
            {
                _logger.LogWarning("No roles provided for user {User}.", username);
            }

        }
    }

    public void RemoveUser(string username, string connectionId)
    {
        lock (_lock)
        {
            if (_userConnections.ContainsKey(username))
            {
                _userConnections[username].Remove(connectionId);
                if (_userConnections[username].Count == 0)
                {
                    _userConnections.Remove(username);

                    _logger.BeginScope("User Disconnection: {User} with connection ID {ConnectionId}", username, connectionId);
                }
            }
            if (_adminConnections.ContainsKey(username))
            {
                _adminConnections[username].Remove(connectionId);
                if (_adminConnections[username].Count == 0)
                {
                    _adminConnections.Remove(username);

                    _logger.BeginScope("Admin Disconnection: {User} with connection ID {ConnectionId}", username, connectionId);
                }
            }
        }
    }

    public List<string> GetConnections(string username)
    {
        lock (_lock)
        {
            return _userConnections.ContainsKey(username)
                ? _userConnections[username].ToList()
                : new List<string>();
        }
    }
    public List<string> GetAdminConnections(string username = null)
    {
        lock (_lock)
        {
            if (username == null)
            {
                return _adminConnections.SelectMany(x => x.Value).ToList();
            }
            return _adminConnections.ContainsKey(username)
                ? _adminConnections[username].ToList()
                : new List<string>();
        }
    }
}


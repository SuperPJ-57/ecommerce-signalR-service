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

    public void AddUser(string email, string connectionId, List<string> roles = null)
    {
        lock (_lock)
        {
            if (roles?.Count > 0)
            {
                if (roles.Contains("Admin"))
                {
                    if (!_adminConnections.ContainsKey(email))
                        _adminConnections[email] = new HashSet<string>();

                    _adminConnections[email].Add(connectionId);

                    _logger.BeginScope("Admin Connection: {User} with connection ID {ConnectionId}", email, connectionId);
                }
                if (roles.Contains("User"))
                {
                    if (!_userConnections.ContainsKey(email))
                        _userConnections[email] = new HashSet<string>();

                    _userConnections[email].Add(connectionId);

                    _logger.BeginScope("User Connection: {User} with connection ID {ConnectionId}", email, connectionId);
                }
            }
            else
            {
                _logger.LogWarning("No roles provided for user {User}.", email);
            }

        }
    }

    public void RemoveUser(string email, string connectionId)
    {
        lock (_lock)
        {
            if (_userConnections.ContainsKey(email))
            {
                _userConnections[email].Remove(connectionId);
                if (_userConnections[email].Count == 0)
                {
                    _userConnections.Remove(email);

                    _logger.BeginScope("User Disconnection: {User} with connection ID {ConnectionId}", email, connectionId);
                }
            }
            if (_adminConnections.ContainsKey(email))
            {
                _adminConnections[email].Remove(connectionId);
                if (_adminConnections[email].Count == 0)
                {
                    _adminConnections.Remove(email);

                    _logger.BeginScope("Admin Disconnection: {User} with connection ID {ConnectionId}", email, connectionId);
                }
            }
        }
    }

    public List<string> GetConnections(string email)
    {
        lock (_lock)
        {
            return _userConnections.ContainsKey(email)
                ? _userConnections[email].ToList()
                : new List<string>();
        }
    }
    public List<string> GetAdminConnections(string email = null)
    {
        lock (_lock)
        {
            if (email == null)
            {
                return _adminConnections.SelectMany(x => x.Value).ToList();
            }
            return _adminConnections.ContainsKey(email)
                ? _adminConnections[email].ToList()
                : new List<string>();
        }
    }
}


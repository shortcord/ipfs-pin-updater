namespace ipfs_pin_util;

public class IPFSConfig
{
    public bool UseBasicAuth
        => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

    public string Username { get; set; }
    public string Password { get; set; }
    public string Api { get; set; }
    public string UnixDomainSocketLocation { get; set; }
    public IReadOnlyList<string> Pins { get; set; } = new List<string>();
}
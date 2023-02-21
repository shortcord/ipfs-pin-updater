namespace ipfs_pin_util;

public class PinsConfig
{
    public IReadOnlyList<string> IPNS { get; set; } = new List<string>();
    public IReadOnlyList<string> IPFS { get; set; } = new List<string>();
}
namespace ipfs_pin_util;

public class PinItem
{
    public int ID { get; set; }
    public string? CID { get; set; }
    public string? OldCID { get; set; }
    public string? RawName { get; set; }
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
    public bool Delete { get; set; } = false;

    public override string ToString()
    {
        return $"ID: {ID} | Created: {Created} | Updated: {Updated} | CID: {CID} | Old CID: {OldCID} | IpnsName: {RawName}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is PinItem o)
        {
            return ID == o.ID;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return ID.GetHashCode();
    }
}
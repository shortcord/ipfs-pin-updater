using Microsoft.Extensions.Configuration;
using Ipfs.Http;
using LiteDB;

namespace ipfs_pin_util;
class Program
{

    public enum PinItemType: int { Unknown, IPFS, IPNS }

    public class PinItem
    {
        public int ID { get; set; }
        public string? CID { get; set; }
        public string? OldCID { get; set; }
        public string? IpnsName { get; set; }
        public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
        public PinItemType PinItemType { get; set; } = PinItemType.Unknown;
        public bool Delete { get; set; } = false;

        public override string ToString()
        {
            return $"ID: {ID} | Created: {Created} | Updated: {Updated} | PinItemType: {PinItemType} | CID: {CID} | Old CID: {OldCID} | IpnsName: {IpnsName}";
        }
    }

    static async Task Main()
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        PinsConfig pinsConfig = new PinsConfig();
        config.Bind(nameof(PinsConfig), pinsConfig);

        var ipfsClient = new IpfsClient(config.GetConnectionString("IPFS"));
        var dbContext = new LiteDatabase(config.GetConnectionString("LiteDB"));
        var pins = dbContext.GetCollection<PinItem>();

        {
            var allPinsDelete = pins
                .FindAll()
                .ToArray();
            
            foreach (var pin in allPinsDelete)
            {
                pin.Delete = true;
                pins.Update(pin);
            }
        }

        Console.WriteLine($"LiteDB Connection String: {config.GetConnectionString("LiteDB")}");
        Console.WriteLine($"IPFS API Connection String: {config.GetConnectionString($"IPFS")}");

        foreach (var ipnsPin in pinsConfig.IPNS)
        {
            string ipnsName = "";
            if (ipnsPin.StartsWith("/ipns/"))
            {
                ipnsName = ipnsPin;
            }
            else
            {
                ipnsName = $"/ipns/{ipnsPin}";
            }

            string resolvedIpns = await ipfsClient.ResolveAsync(ipnsName);

            Console.WriteLine($"IPNS Pin: {ipnsName} == {resolvedIpns}");

            if (!pins.Exists(_ => _.IpnsName == ipnsName))
            {
                Console.WriteLine($"Adding {resolvedIpns}");
                var pin = new PinItem()
                {
                    CID = resolvedIpns,
                    IpnsName = ipnsName,
                    PinItemType = PinItemType.IPNS
                };
                pins.Insert(pin);
            } else
            {
                var existingPin = pins.FindOne(_ => _.IpnsName == ipnsName);
                existingPin.Delete = false;

                if (existingPin.CID != resolvedIpns)
                {
                    Console.WriteLine($"IPNS Pin Updated from \"{existingPin.CID}\" to \"{resolvedIpns}\"");
                    // Update the CID to point to the new one
                    existingPin.OldCID = existingPin.CID;
                    existingPin.CID = resolvedIpns;
                    existingPin.Updated = DateTimeOffset.UtcNow;
                    pins.Update(existingPin);
                }
            }
        }

        foreach (var ipfsPin in pinsConfig.IPFS)
        {
            var ipfsResolved = await ipfsClient.ResolveAsync(ipfsPin);

            Console.WriteLine($"IPFS Pin: {ipfsResolved}");

            if (!pins.Exists(_ => _.CID == ipfsResolved))
            {
                Console.WriteLine($"Adding {ipfsResolved}");
                var pin = new PinItem()
                {
                    CID = ipfsResolved,
                    PinItemType = PinItemType.IPFS
                };
                pins.Insert(pin);
            } else
            {
                var existingPin = pins.FindOne(_ => _.CID == ipfsResolved);
                existingPin.Delete = false;
            }
        }

        var allPins = pins
            .FindAll()
            .ToArray();

        for (int i = 0; i < allPins.Length; i++)
        {
            var pin = allPins[i];
            if (!pin.Delete)
            {
                if (pin.PinItemType == PinItemType.IPFS)
                {
                    Console.WriteLine($"Pinning: {pin.CID}");
                    await ipfsClient.Pin.AddAsync(pin.CID, recursive: true);
                    pin.Updated = DateTimeOffset.UtcNow;
                    pins.Update(pin);
                }
                else if (pin.PinItemType == PinItemType.IPNS)
                {
                    // If we are updating the pin
                    if (!string.IsNullOrWhiteSpace(pin.OldCID))
                    {
                        Console.WriteLine($"Updating pin: {pin.IpnsName}");
                        await ipfsClient.DoCommandAsync("pin/update", CancellationToken.None, pin.OldCID, new string[] { $"arg={pin.CID}", "unpin=true" });
                        pin.OldCID = "";
                    } else
                    {
                        Console.WriteLine($"Pinning: {pin.IpnsName}");
                        await ipfsClient.Pin.AddAsync(pin.CID, recursive: true);
                    }

                    pin.Updated = DateTimeOffset.UtcNow;
                    pins.Update(pin);
                }
            } else
            {
                string toRemove = pin.CID.Remove(0, "/ipfs/".Length);
                
                Console.WriteLine($"Removing pin: {pin.CID}");
                await ipfsClient.Pin.RemoveAsync(toRemove, recursive: true);
                pins.Delete(pin.ID);
            }
        }
    }
}

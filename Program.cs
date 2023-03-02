using Microsoft.Extensions.Configuration;
using Ipfs.Http;
using LiteDB;

namespace ipfs_pin_util;
class Program
{
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
    }

    static async Task Main()
    {
        IConfiguration config = new ConfigurationBuilder()
#if PKG_BUILD
            .SetBasePath("/etc/ipfs-pin-updater", optional: false)
#else
            .SetBasePath(Directory.GetCurrentDirectory())
#endif
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.custom.json", optional: true)
            .AddEnvironmentVariables("SC_IPFS")
            .Build();

        PinsConfig pinsConfig = new PinsConfig();
        config.Bind(nameof(PinsConfig), pinsConfig);

        var ipfsClient = new IpfsClient(config.GetConnectionString("IPFS"));
        var dbContext = new LiteDatabase(config.GetConnectionString("LiteDB"));

        Console.WriteLine($"LiteDB Connection String: {config.GetConnectionString("LiteDB")}");
        Console.WriteLine($"IPFS API Connection String: {config.GetConnectionString($"IPFS")}");

        var pinDbContext = dbContext.GetCollection<PinItem>();

        {
            var allPinsDelete = pinDbContext
                .FindAll()
                .ToArray();

            foreach (var pin in allPinsDelete)
            {
                pin.Delete = true;
                pinDbContext.Update(pin);
            }
        }

        foreach (var pinRaw in pinsConfig.Pins)
        {
            if (!pinRaw.StartsWith("/ipns/") &&
                !pinRaw.StartsWith("/ipfs/"))
            {
                Console.WriteLine($"{pinRaw} is not formated correctly, must start with /ipfs/ or /ipns/");
                continue;
            }

            string resolvedCID = pinRaw;

            if (pinRaw.StartsWith("/ipns/"))
            {
                Console.WriteLine("Resolving IPNS to CID");
                resolvedCID = await ipfsClient.ResolveAsync(resolvedCID);
                Console.WriteLine($"{pinRaw} == {resolvedCID}");
            };

            // Remove /ipfs/ and /ipns/ from the start, they are the same length
            string cidOnly = resolvedCID.Remove(0, "/ipfs/".Length);

            if (!pinDbContext.Exists(_ => _.RawName == pinRaw))
            {
                Console.WriteLine("Pin doesn't exist in DB; Creating...");

                var pin = new PinItem()
                {
                    CID = cidOnly,
                    RawName = pinRaw
                };

                pinDbContext.Insert(pin);
            }

            var existingPin = pinDbContext.FindOne(_ => _.RawName == resolvedCID);

            if (existingPin != null)
            {
                existingPin.Delete = false;

                if (existingPin.CID != cidOnly)
                {
                    existingPin.OldCID = existingPin.CID;
                    existingPin.CID = cidOnly;
                }

                pinDbContext.Update(existingPin);
            }
        }

        {
            var pinsToDelete = pinDbContext
                .Find(_ => _.Delete)
                .ToArray();

            for (var i = 0; i < pinsToDelete.Length; i++)
            {
                var pinToDelete = pinsToDelete[i];

                Console.WriteLine($"Deleting pin {pinToDelete.CID}");

                await ipfsClient.Pin.RemoveAsync(pinToDelete.CID, recursive: true);

                pinDbContext.Delete(pinToDelete.ID);
            }
        }

        {
            var pinsToPin = pinDbContext
                .Find(_ => !_.Delete)
                .ToArray();

            for (var i = 0; i < pinsToPin.Length; i++)
            {
                var pinToPin = pinsToPin[i];

                if ((!string.IsNullOrWhiteSpace(pinToPin.OldCID)) &&
                    (pinToPin.CID != pinToPin.OldCID))
                {
                    Console.WriteLine($"Updating {pinToPin.OldCID} to {pinToPin.CID}");
                    await ipfsClient.DoCommandAsync("pin/update", CancellationToken.None, pinToPin.OldCID, new string[] { $"arg={pinToPin.CID}", "unpin=true" });
                    pinToPin.OldCID = "";
                    continue;
                }

                Console.WriteLine($"Pining {pinToPin.CID}");
                await ipfsClient.Pin.AddAsync(pinToPin.CID, recursive: true);
            }
        }
    }
}

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
        public string? rawName { get; set; }
        public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
        public bool Delete { get; set; } = false;

        public override string ToString()
        {
            return $"ID: {ID} | Created: {Created} | Updated: {Updated} | CID: {CID} | Old CID: {OldCID} | IpnsName: {rawName}";
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
        var pinDbContext = dbContext.GetCollection<PinItem>();

        Console.WriteLine($"LiteDB Connection String: {config.GetConnectionString("LiteDB")}");
        Console.WriteLine($"IPFS API Connection String: {config.GetConnectionString($"IPFS")}");

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
            }

            if (!pinDbContext.Exists(_ => _.rawName == pinRaw))
            {
                Console.WriteLine("Pin doesn't exist in DB; Creating...");

                var pin = new PinItem()
                {
                    CID = resolvedCID,
                    rawName = pinRaw
                };

                pinDbContext.Insert(pin);
            }

            var existingPin = pinDbContext.FindOne(_ => _.rawName == resolvedCID);

            if (existingPin != null)
            {
                existingPin.Delete = false;

                if (existingPin.CID != resolvedCID)
                {
                    existingPin.OldCID = existingPin.CID;
                    existingPin.CID = resolvedCID;
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

                if (pinToPin.rawName.StartsWith("/ipfs/"))
                {
                    if ((!string.IsNullOrWhiteSpace(pinToPin.OldCID)) &&
                        (pinToPin.CID != pinToPin.OldCID))
                    {
                        Console.WriteLine($"Updating {pinToPin.OldCID} to {pinToPin.CID}");
                        await ipfsClient.DoCommandAsync("pin/update", CancellationToken.None, pinToPin.OldCID, new string[] { $"arg={pinToPin.CID}", "unpin=true" });
                        pinToPin.OldCID = "";
                        continue;
                    }
                }

                Console.WriteLine($"Pining {pinToPin.CID}");
                await ipfsClient.Pin.AddAsync(pinToPin.CID, recursive: true);
            }
        }
    }
}

using Microsoft.Extensions.Configuration;
using Ipfs.Http;
using LiteDB;

namespace ipfs_pin_util;
class Program
{

    public class PinItem
    {
        public int id { get; set; }
        public string? cid { get; set; }
        public string? oldCid { get; set; }

        public override string ToString()
        {
            return $"ID: {id} | CID: {cid} | Old CID: {oldCid}";
        }
    }

    static async Task Main()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        IConfiguration config = builder.Build();

        PinsConfig pinsConfig = new PinsConfig();
        config.Bind(nameof(PinsConfig), pinsConfig);

        var ipfsClient = new IpfsClient(config.GetConnectionString("IPFS"));
        var dbContext = new LiteDatabase(config.GetConnectionString("LiteDB"));
        var pins = dbContext.GetCollection<PinItem>();

        Console.WriteLine($"LiteDB Connection String: {config.GetConnectionString("LiteDB")}");
        Console.WriteLine($"IPFS API Connection String: {config.GetConnectionString($"IPFS")}");

        foreach (var ipnsPin in pinsConfig.IPNS)
        {
            string resolvedIpns = "";
            if (ipnsPin.StartsWith("/ipns/"))
            {
                resolvedIpns = await ipfsClient.ResolveAsync(ipnsPin);
            }
            else
            {
                resolvedIpns = await ipfsClient.ResolveAsync($"/ipns/{ipnsPin}");
            }

            Console.WriteLine($"IPNS Pin: {resolvedIpns}");

            if (!pins.Exists(_ => _.cid == resolvedIpns))
            {
                Console.WriteLine($"Adding {resolvedIpns}");
                var pin = new PinItem()
                {
                    cid = resolvedIpns
                };
                pins.Insert(pin);
            }
        }

        foreach (var ipfsPin in pinsConfig.IPFS)
        {
            var ipfsResolved = await ipfsClient.ResolveAsync(ipfsPin);

            Console.WriteLine($"IPFS Pin: {ipfsResolved}");

            if (!pins.Exists(_ => _.cid == ipfsResolved))
            {
                Console.WriteLine($"Adding {ipfsResolved}");
                var pin = new PinItem()
                {
                    cid = ipfsResolved
                };
                pins.Insert(pin);
            }
        }

        foreach (var pin in pins.FindAll())
        {
            Console.WriteLine(pin);
        }
    }
}

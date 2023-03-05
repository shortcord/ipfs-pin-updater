using Microsoft.Extensions.Configuration;
using Ipfs.Http;
using LiteDB;
using System.Net.Sockets;

namespace ipfs_pin_util;
class Program
{
    static async Task<int> Main()
    {
        IConfiguration config = new ConfigurationBuilder()
#if PKG_BUILD
            .SetBasePath("/etc/ipfs-pin-updater")
#else
            .SetBasePath(Directory.GetCurrentDirectory())
#endif
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.custom.json", optional: true)
            .AddEnvironmentVariables("SC_IPFS")
            .Build();

        IPFSConfig ipfsConfig = new IPFSConfig();
        config.Bind(nameof(IPFSConfig), ipfsConfig);

        var dbContext = new LiteDatabase(config.GetConnectionString("LiteDB"));

        IpfsClient? ipfsClient = null;

        if (!string.IsNullOrWhiteSpace(ipfsConfig.UnixDomainSocketLocation))
        {
            if (File.Exists(ipfsConfig.UnixDomainSocketLocation))
            {
                Console.WriteLine("IPFS API Connection String: {0}", ipfsConfig.UnixDomainSocketLocation);
                ipfsClient = new IpfsClient()
                {
                    HttpMessageHandler = new SocketsHttpHandler
                    {
                        ConnectCallback = async (context, token) =>
                        {
                            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                            var endpoint = new UnixDomainSocketEndPoint(ipfsConfig.UnixDomainSocketLocation);
                            await socket.ConnectAsync(endpoint);
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                    }
                };
            } else
            {
                Console.WriteLine($"{ipfsConfig.UnixDomainSocketLocation} doesn't exist.");
                return 1;
            }
        } else
        {
            Console.WriteLine("IPFS API Connection String: {0}", ipfsConfig.Api);
            ipfsClient = new IpfsClient(ipfsConfig.Api);
            if (ipfsConfig.UseBasicAuth)
            {
                ipfsClient.HttpMessageHandler = new BasicAuthHttpClientHandler(ipfsConfig.Username, ipfsConfig.Password);
            }
        }

        Console.WriteLine($"LiteDB Connection String: {config.GetConnectionString("LiteDB")}");

        Console.WriteLine($"{await ipfsClient.IdAsync()}");
        return 0;

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

        foreach (var pinRaw in ipfsConfig.Pins)
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

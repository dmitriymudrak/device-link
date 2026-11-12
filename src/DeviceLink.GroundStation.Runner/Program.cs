using System.Net;

namespace DeviceLink.GroundStation.Runner
{
    internal class Program
    {
        static async Task Main(string[] args)
        {


            using (var cts = new CancellationTokenSource())
            {
                int port = args.Length > 0 ? int.Parse(args[0]) : 5000;
                string stationId = args.Length > 2 ? args[2] : "station-1";
                string apiAddress = args.Length > 1 ? args[1] : "http://localhost:5100";
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                var forwarder = new ApiForwarder(apiAddress, stationId, () => auth.GetAccessTokenAsync());

                var listener = new DroneListener(
                    new IPEndPoint(IPAddress.Loopback, port),
                    (seq, t) => 
                    {
                        Console.WriteLine($"{DateTime.Now:HH:mm:ss} seq={seq,3} lat={t.Latitude:F7} lon={t.Longitude:F7}");
                        forwarder.Enqueue(seq, t);
                    });

                Console.WriteLine($"Станція {stationId}: дрон на порту {port}, Api {apiAddress}. Ctrl+C — зупинка.");


                try
                {
                    Task[] tasks = [listener.RunAsync(cts.Token), forwarder.RunAsync(cts.Token)];
                    await await Task.WhenAny(tasks);
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    Console.WriteLine("Станцію зупинено.");
                }
            }

        }
    }
}
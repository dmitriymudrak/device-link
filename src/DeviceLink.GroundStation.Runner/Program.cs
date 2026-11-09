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
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                var listener = new DroneListener(
                    new IPEndPoint(IPAddress.Loopback, port),
                    (seq, t) => Console.WriteLine(
                        $"{DateTime.Now:HH:mm:ss} seq={seq,3} lat={t.Latitude:F7} lon={t.Longitude:F7}"));

                Console.WriteLine($"Станція слухає порт {port}. Ctrl+C — зупинка.");

                try
                {
                    await listener.RunAsync(cts.Token);
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    Console.WriteLine("Станцію зупинено.");
                }
            }

        }
    }
}
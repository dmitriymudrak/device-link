using DeviceLink.Protocol;
using System.Net.Sockets;

namespace DeviceLink.Simulator
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            

            using (var cts = new CancellationTokenSource()) 
            {
                string host = args.Length > 0 ? args[0] : "127.0.0.1";
                int port = args.Length > 1 ? int.Parse(args[1]) : 5000;

                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                var route = new Route(
                [(50.4501, 30.5234), (50.4700, 30.5600), (50.4350, 30.5700), (50.4250, 30.5150)],
                stepsPerSegment: 60);

                var sender = new TelemetrySender(route, TimeSpan.FromSeconds(1));
                try
                {
                    using (var client = new TcpClient())
                    {
                        await client.ConnectAsync(host, port, cts.Token);
                        Console.WriteLine($"Підключено до {host}:{port}");
                        await sender.RunAsync(client.GetStream(), cts.Token);

                    }
                }
                catch(IOException ex)
                {
                    Console.WriteLine($"З'єднання розірвано: {ex.Message}");
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    Console.WriteLine("Зупинено.");
                }
                catch(SocketException ex)
                {
                    Console.WriteLine($"Немає з'єднання з {host}:{port}: {ex.Message}");
                }

            }

        }
    }
}

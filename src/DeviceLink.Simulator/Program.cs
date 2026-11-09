using DeviceLink.Protocol;
using System.IO.Pipelines;
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
                bool destroyed = false;
                void OnCommand(Command command)
                {
                    Console.WriteLine($"Команда #{command.CommandId}: {command.Code}");
                    switch (command.Code)
                    {
                        case CommandCode.Hold: sender.Hold(); break;
                        case CommandCode.Resume: sender.Resume(); break;
                        case CommandCode.Destroy: destroyed = true; cts.Cancel(); break;
                        default: Console.WriteLine("Невідома команда, пропускаю"); break;
                    }
                }

                try
                {
                    using (var client = new TcpClient())
                    {
                        await client.ConnectAsync(host, port, cts.Token);
                        Console.WriteLine($"Підключено до {host}:{port}");
                        
                        var stream = client.GetStream();
                        var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
                        Task[] tasks = [sender.RunAsync(client.GetStream(), cts.Token), CommandReader.ReadAsync(reader, OnCommand, cts.Token)];

                        await await Task.WhenAny(tasks);
                        Console.WriteLine("Станція закрила з'єднання.");
                    }
                }
                catch(IOException ex)
                {
                    Console.WriteLine($"З'єднання розірвано: {ex.Message}");
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    Console.WriteLine(destroyed ? "Дрон знищено командою станції." : "Зупинено.");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Немає з'єднання з {host}:{port}: {ex.Message}");
                }

            }

        }
    }
}

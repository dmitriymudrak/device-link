using System.ComponentModel.DataAnnotations;

namespace DeviceLink.Simulator
{
    /// <summary>Simulator settings ("Simulator" section): station address and telemetry period.</summary>
    public sealed class SimulatorOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string Section = "Simulator";

        /// <summary>Ground station host.</summary>
        [Required]
        public string Host { get; init; } = "127.0.0.1";

        /// <summary>Ground station TCP port.</summary>
        [Range(1, 65535)]
        public int Port { get; init; } = 5000;

        /// <summary>Telemetry send period.</summary>
        [Range(typeof(TimeSpan), "00:00:00.010", "01:00:00")]
        public TimeSpan Period { get; init; } = TimeSpan.FromSeconds(1);
    }
}

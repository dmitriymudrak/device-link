using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DeviceLink.GroundStation.Desktop.Auth
{
    public static class TokenStore
    {
        static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Station", "session.bin");

        public static void Save(string refreshToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(FilePath, ProtectedData.Protect(Encoding.UTF8.GetBytes(refreshToken), null, DataProtectionScope.CurrentUser));
        }

        public static string? Load() => File.Exists(FilePath) ?
            Encoding.UTF8.GetString(ProtectedData.Unprotect(
                File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser)) : null;

        public static void Clear()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}

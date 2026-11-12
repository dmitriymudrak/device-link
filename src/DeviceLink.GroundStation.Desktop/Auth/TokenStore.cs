using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DeviceLink.GroundStation.Desktop.Auth
{
    /// <summary>Stores the refresh token in a file encrypted with DPAPI for the current user.</summary>
    public sealed class TokenStore(ILogger<TokenStore> logger)
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Station", "session.bin");

        /// <summary>Encrypts and writes the refresh token; errors are only logged.</summary>
        public void Save(string refreshToken)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllBytes(FilePath,
                    ProtectedData.Protect(Encoding.UTF8.GetBytes(refreshToken), null, DataProtectionScope.CurrentUser));
                logger.LogDebug("Refresh token saved to {Path}", FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                logger.LogWarning(ex, "Failed to save refresh token to {Path}", FilePath);
            }
        }

        /// <summary>Reads the saved token; null if the file is missing or cannot be decrypted.</summary>
        public string? Load()
        {
            if (!File.Exists(FilePath))
            {
                logger.LogDebug("No saved session at {Path}", FilePath);
                return null;
            }

            try
            {
                return Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
            {
                logger.LogWarning(ex, "Saved session at {Path} is unreadable, clearing it", FilePath);
                Clear();
                return null;
            }
        }

        /// <summary>Deletes the saved session file if it exists.</summary>
        public void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                    logger.LogDebug("Saved session cleared");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Failed to delete saved session at {Path}", FilePath);
            }
        }
    }
}

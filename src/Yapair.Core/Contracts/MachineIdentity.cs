using System.Security.Cryptography;
using System.Text;

namespace Yapair.Core.Contracts;

/// <summary>
/// Pseudonymous machine identity for the case store. The computer name is only a
/// human-readable label; the stable key is a one-way hash of the hardware
/// identifier. Raw HWID/serial values are never persisted.
/// </summary>
public sealed record MachineIdentity(string Key, string Label)
{
    public static MachineIdentity Create(string? computerName, string? hardwareId)
    {
        var label = string.IsNullOrWhiteSpace(computerName) ? "unknown-machine" : computerName.Trim();
        var stableInput = string.IsNullOrWhiteSpace(hardwareId) ? $"name:{label}" : $"hwid:{hardwareId.Trim()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"yapair.machine.v1|{stableInput}"));
        return new MachineIdentity(Convert.ToHexString(hash).ToLowerInvariant(), label);
    }

    public string ShortKey => Key.Length <= 12 ? Key : Key[..12];
}

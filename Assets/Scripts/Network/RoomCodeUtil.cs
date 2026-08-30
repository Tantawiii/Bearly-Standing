using System;
using System.Net;
using System.Net.Sockets;

namespace BearlyStanding
{
    /// <summary>
    /// Turns a host's IPv4 + port into a short shareable "room code" and back. 6 bytes
    /// (4 address octets + 2 port bytes) → 10 chars of Crockford base32 (no 0/O/1/I ambiguity).
    /// Also accepts a raw "ip" or "ip:port" string so friends can paste either.
    /// </summary>
    public static class RoomCodeUtil
    {
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford base32
        public const ushort DefaultPort = 7777;

        public static string Encode(string ipv4, ushort port)
        {
            var bytes = new byte[6];
            var parts = (ipv4 ?? "").Split('.');
            for (int i = 0; i < 4; i++)
                bytes[i] = (i < parts.Length && byte.TryParse(parts[i], out var b)) ? b : (byte)0;
            bytes[4] = (byte)(port >> 8);
            bytes[5] = (byte)(port & 0xFF);
            return Base32Encode(bytes);
        }

        /// <summary>Accepts a 10-char room code, or "ip", or "ip:port". Falls back to DefaultPort.</summary>
        public static bool TryResolve(string input, out string ip, out ushort port)
        {
            ip = null;
            port = DefaultPort;
            if (string.IsNullOrWhiteSpace(input)) return false;
            input = input.Trim();

            if (input.Contains('.'))
            {
                var hostPort = input.Split(':');
                ip = hostPort[0].Trim();
                if (hostPort.Length > 1 && ushort.TryParse(hostPort[1].Trim(), out var p)) port = p;
                return IPAddress.TryParse(ip, out _);
            }

            var normalized = input.ToUpperInvariant().Replace("O", "0").Replace("I", "1").Replace("L", "1");
            if (!TryBase32Decode(normalized, out var bytes) || bytes.Length != 6) return false;
            ip = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
            port = (ushort)((bytes[4] << 8) | bytes[5]);
            return true;
        }

        /// <summary>Best-effort private LAN IPv4 of this machine.</summary>
        public static string LocalIPv4()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint) return endPoint.Address.ToString();
            }
            catch { /* offline / no route — fall through */ }

            try
            {
                foreach (var addr in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (addr.AddressFamily == AddressFamily.InterNetwork) return addr.ToString();
            }
            catch { /* ignore */ }

            return "127.0.0.1";
        }

        private static string Base32Encode(byte[] data)
        {
            var chars = new char[(data.Length * 8 + 4) / 5];
            int buffer = data[0], bitsLeft = 8, next = 0;
            for (int i = 1; next < chars.Length;)
            {
                if (bitsLeft < 5)
                {
                    if (i < data.Length)
                    {
                        buffer = (buffer << 8) | (data[i++] & 0xFF);
                        bitsLeft += 8;
                    }
                    else
                    {
                        buffer <<= (5 - bitsLeft);
                        bitsLeft = 5;
                    }
                }
                int index = (buffer >> (bitsLeft - 5)) & 0x1F;
                bitsLeft -= 5;
                chars[next++] = Alphabet[index];
            }
            return new string(chars);
        }

        private static bool TryBase32Decode(string input, out byte[] result)
        {
            result = Array.Empty<byte>();
            if (string.IsNullOrEmpty(input)) return false;

            int outputLength = input.Length * 5 / 8;
            var bytes = new byte[outputLength];
            int buffer = 0, bitsLeft = 0, next = 0;

            foreach (char c in input)
            {
                int val = Alphabet.IndexOf(char.ToUpperInvariant(c));
                if (val < 0) return false;
                buffer = (buffer << 5) | val;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    bytes[next++] = (byte)((buffer >> (bitsLeft - 8)) & 0xFF);
                    bitsLeft -= 8;
                }
            }
            result = bytes;
            return true;
        }
    }
}

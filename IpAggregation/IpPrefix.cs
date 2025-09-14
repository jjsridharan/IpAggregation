using System.Net;
using System.Net.Sockets;

namespace IpAggregation
{
    /// <summary>
    /// Represents an IP prefix, consisting of an IP address and a mask length (CIDR notation).
    /// Provides methods to parse and work with IP prefixes.
    /// </summary>
    public class IPPrefix
    {
        /// <summary>
        /// Gets the normalized IP address for the prefix.
        /// </summary>
        public IPAddress Address { get; }

        /// <summary>
        /// Gets the mask length (CIDR notation) for the prefix.
        /// </summary>
        public int MaskLength { get; }

        private bool computedHash = false;

        private int cachedHashCode = 0;

        private bool computedBits = false;

        private bool[] cachedBits;


        /// <summary>
        /// Initializes a new instance of the <see cref="IPPrefix"/> class with the specified IP address and mask length.
        /// </summary>
        /// <param name="address">The IP address for the prefix.</param>
        /// <param name="maskLength">The mask length (CIDR notation) for the prefix.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="address"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maskLength"/> is not valid for the address family.</exception>
        public IPPrefix(IPAddress address, int maskLength)
        {
            if (address == null)

            {
                throw new ArgumentNullException(nameof(address));
            }

            int maxMask = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;

            if (maskLength < 0 || maskLength > maxMask)
            {
                throw new ArgumentOutOfRangeException(nameof(maskLength), $"Mask length must be between 0 and {maxMask} for {address.AddressFamily}");
            }

            MaskLength = maskLength;

            Address = Normalize(address, maskLength);
            cachedBits = new bool[maxMask];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IPPrefix"/> class with the specified cidr notation string.
        /// </summary>
        /// <param name="cidr"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public IPPrefix(string cidr)
        {
            if (string.IsNullOrWhiteSpace(cidr))
            {
                throw new ArgumentException("Prefix cannot be null or empty.", nameof(cidr));
            }

            var parts = cidr.Split('/');

            if (!IPAddress.TryParse(parts[0], out IPAddress ip))
            {
                throw new FormatException("Invalid IP address.");
            }

            int maxMask = ip.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
            int mask = maxMask;
            if (parts.Length == 2)
            {
                if (!int.TryParse(parts[1], out mask))
                {
                    throw new FormatException("Invalid mask length.");
                }
            }

            if (mask < 0 || mask > maxMask)
            {
                throw new ArgumentOutOfRangeException(nameof(mask), $"Mask length must be between 0 and {maxMask}");
            }

            MaskLength = mask;
            Address = Normalize(ip, mask);
            cachedBits = new bool[maxMask];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IPPrefix"/> class from raw bytes and mask length.
        /// </summary>
        /// <param name="bytes">Byte array representing the IP address (length 4 for IPv4, 16 for IPv6).</param>
        /// <param name="maskLength">The prefix mask length.</param>
        public IPPrefix(byte[] bytes, int maskLength)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length != 4 && bytes.Length != 16)
            {
                throw new ArgumentException("Byte array must be length 4 (IPv4) or 16 (IPv6).", nameof(bytes));
            }

            int maxMask = bytes.Length * 8;
            if (maskLength <= 0 || maskLength > maxMask)
            {
                throw new ArgumentOutOfRangeException(nameof(maskLength), $"Mask length must be between 1 and {maxMask}.");
            }

            byte[] addressBytes = new byte[bytes.Length];
            Array.Copy(bytes, addressBytes, bytes.Length);

            // Zero out bits beyond the mask length
            int fullBytes = maskLength / 8;
            int remainingBits = maskLength % 8;

            if (remainingBits != 0 && fullBytes < bytes.Length)
            {
                byte mask = (byte)(0xFF << (8 - remainingBits));
                addressBytes[fullBytes] &= mask;
                fullBytes++;
            }

            for (int i = fullBytes; i < bytes.Length; i++)
            {
                addressBytes[i] = 0;
            }

            Address = new IPAddress(addressBytes);
            MaskLength = maskLength;
            cachedBits = new bool[maxMask];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IPPrefix"/> class from a bool array and mask length.
        /// Supports both IPv4 and IPv6.
        /// </summary>
        /// <param name="bits">Bool array representing the bits of the IP address (MSB-first).</param>
        /// <param name="maskLength">The prefix mask length.</param>
        public IPPrefix(bool[] bits, int maskLength)
        {
            if (bits == null || bits.Length == 0)
            {
                throw new ArgumentException("Bits cannot be null or empty.", nameof(bits));
            }

            if (maskLength <= 0 || maskLength > bits.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(maskLength), "Mask length must be positive and within the bounds of the bit array.");
            }

            MaskLength = maskLength;

            int byteLength;
            if (bits.Length <= 32)
            {
                byteLength = 4; // IPv4
            }
            else if (bits.Length <= 128)
            {
                byteLength = 16; // IPv6
            }
            else
            {
                throw new ArgumentException("Bits length cannot exceed 128 for IPv6.");
            }

            byte[] addressBytes = new byte[byteLength];

            for (int i = 0; i < maskLength; i++)
            {
                if (bits[i])
                {
                    addressBytes[i / 8] |= (byte)(1 << (7 - (i % 8)));
                }
            }

            // Zero out remaining bits in the last byte if maskLength is not a multiple of 8
            int remainingBits = maskLength % 8;
            if (remainingBits != 0)
            {
                int lastByteIndex = maskLength / 8;
                byte mask = (byte)(0xFF << (8 - remainingBits));
                addressBytes[lastByteIndex] &= mask;
            }

            Address = new IPAddress(addressBytes);
            cachedBits = new bool[bits.Length];
        }

        private static IPAddress Normalize(IPAddress address, int maskLength)
        {
            byte[] bytes = address.GetAddressBytes();
            int fullBytes = maskLength / 8;
            int remainingBits = maskLength % 8;

            // Zero out host bits
            if (fullBytes < bytes.Length)
            {
                // Mask partial byte
                if (remainingBits > 0)
                {
                    byte mask = (byte)(0xFF << (8 - remainingBits));
                    bytes[fullBytes] &= mask;
                    fullBytes++;
                }

                // Zero out remaining bytes
                for (int i = fullBytes; i < bytes.Length; i++)
                {
                    bytes[i] = 0;
                }
            }

            return new IPAddress(bytes);
        }

        // NEW: virtual factory so derived classes can ensure children are same concrete type.
        public virtual IPPrefix CreateSubPrefix(bool[] fullBits, int newMaskLength)
            => new IPPrefix(fullBits, newMaskLength);

        /// <summary>
        /// Gets the bits of the IP address as a boolean array.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public bool[] GetBitsForAddress()
        {
            if (computedBits)
            {
                return this.cachedBits;
            }

            byte[] bytes = Address.GetAddressBytes();
            int totalBits = bytes.Length * 8;
            bool[] bits = new bool[totalBits];

            if (bytes.Length == 4) // IPv4
            {
                uint addr = ((uint)bytes[0] << 24) |
                            ((uint)bytes[1] << 16) |
                            ((uint)bytes[2] << 8) |
                            ((uint)bytes[3]);

                for (int i = 0; i < 32; i++)
                {
                    bits[i] = (addr & (1u << (31 - i))) != 0;
                }
            }
            else if (bytes.Length == 16) // IPv6
            {
                // Use two ulongs for faster bit extraction
                ulong high = BitConverter.ToUInt64(bytes, 0);
                ulong low = BitConverter.ToUInt64(bytes, 8);

                // Convert from little-endian to big-endian if needed
                if (BitConverter.IsLittleEndian)
                {
                    high = ReverseBytes(high);
                    low = ReverseBytes(low);
                }

                for (int i = 0; i < 64; i++)
                {
                    bits[i] = (high & (1UL << (63 - i))) != 0;
                    bits[i + 64] = (low & (1UL << (63 - i))) != 0;
                }
            }
            else
            {
                throw new InvalidOperationException("Unsupported address family.");
            }

            return this.cachedBits = bits;
        }

        // Helper to reverse ulong bytes for big-endian representation
        private static ulong ReverseBytes(ulong value)
        {
            return ((value & 0x00000000000000FFUL) << 56) |
                   ((value & 0x000000000000FF00UL) << 40) |
                   ((value & 0x0000000000FF0000UL) << 24) |
                   ((value & 0x00000000FF000000UL) << 8) |
                   ((value & 0x000000FF00000000UL) >> 8) |
                   ((value & 0x0000FF0000000000UL) >> 24) |
                   ((value & 0x00FF000000000000UL) >> 40) |
                   ((value & 0xFF00000000000000UL) >> 56);
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Address}/{MaskLength}";

        /// <inheritdoc/>
        public override bool Equals(object? obj)
            => obj is IPPrefix other &&
               Address.Equals(other.Address) &&
               MaskLength == other.MaskLength;

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            if (this.computedHash)
            {
                return cachedHashCode;
            }

            this.cachedHashCode = HashCode.Combine(Address, MaskLength);
            this.computedHash = true;
            return this.cachedHashCode;
        }
    }
}

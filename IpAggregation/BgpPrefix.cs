using System.Net;

namespace IpAggregation
{
    public class BgpPrefix : IPPrefix
    {
        public List<string> Community { get; }
        public int? Med { get; internal set; }
        public string? Origin { get; internal set; }
        public bool? AtomicAggregate { get; internal set; }

        private bool computedHash = false;

        private int cachedHashCode = 0;

        // Construct from string (CIDR form)
        public BgpPrefix(string cidr, List<string>? community = null)
            : base(cidr)
        {
            Community = community ?? new List<string>();
        }

        public BgpPrefix(IPAddress address, int maskLength, List<string>? community = null)
            : base(address, maskLength)
        {
            Community = community ?? new List<string>();
        }

        public BgpPrefix(bool[] bits, int maskLength, List<string>? community = null)
            : base(bits, maskLength)
        {
            Community = community ?? new List<string>();
        }

        // Propagate BGP metadata (choose what to carry forward; here we reuse same attributes)
        public override IPPrefix CreateSubPrefix(bool[] fullBits, int newMaskLength)
            => new BgpPrefix(fullBits, newMaskLength, Community);

        public override string ToString()
            => base.ToString();

        /// <inheritdoc/>
        public override bool Equals(object? obj)
            => obj is IPPrefix other &&
               Address.Equals(other.Address) &&
               MaskLength == other.MaskLength &&
               Community.SequenceEqual((other as BgpPrefix)?.Community ?? new List<string>()) &&
               Med == (other as BgpPrefix)?.Med &&
               Origin == (other as BgpPrefix)?.Origin &&
               AtomicAggregate == (other as BgpPrefix)?.AtomicAggregate;/// <inheritdoc/>
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
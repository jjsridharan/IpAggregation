using IpAggregation;
using System.Net.Sockets;

namespace Bechmark.IpAggregation
{
    public class IpPrefixComparer : IComparer<IPPrefix>
    {
        /// <summary>
        /// The compare method which compares two prefixes and returns appropriates value.
        /// </summary>
        /// <param name="first">First BGP Prefix</param>
        /// <param name="second">Second BGP Prefix.</param>
        /// <returns>0 if both prefixes are same. 1 if first is greater than second else -1</returns>
        public int Compare(IPPrefix? first, IPPrefix? second)
        {
            if (first == null && second == null)
            {
                return 0;
            }

            if (first == null)
            {
                return -1;
            }

            if (second == null)
            {
                return 1;
            }

            var cp = first.MaskLength.CompareTo(second.MaskLength);
            if (cp == 0)
            {
                var firstAddress = first.GetBitsForAddress();
                var secondAddress = second.GetBitsForAddress();

                for (int i = 0; i < firstAddress.Length; i++)
                {
                    if (firstAddress[i] == secondAddress[i]) continue;

                    if (firstAddress[i] && secondAddress[i] == false)
                    {
                        return 1;
                    }

                    return -1;
                }
            }

            return cp;

        }
    }
}
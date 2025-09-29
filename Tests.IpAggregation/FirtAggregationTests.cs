using Bechmark.IpAggregation;
using IpAggregation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPAggregatorTests
{
    /// <summary>
    /// Class for testing validation of FIRT(Full Internet Routing Table) aggregation.
    /// </summary>
    [TestClass]
    public class FirtAggregationTests
    {
        /// <summary>
        /// Test method to validate FIRT IPv4 aggregation.
        /// </summary>
        [TestMethod]
        public void TestV4FirtAggregation()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            var prefixes = Program.ReadPrefixes("Files\\bgptablev4_cleaned.txt");
            trie.PerformOperations(prefixes, new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);

            Assert.AreEqual(88602, aggregatesAdded.Count);

            var exportedLines = File.ReadAllLines("Files\\exported_v4_trie.txt");

            var exportedPrefixes = aggregatesAdded.Select(p => p.ToString()).ToHashSet();

            Assert.IsTrue(exportedLines.All(prefix => exportedPrefixes.Contains(prefix)));
        }

        /// <summary>
        /// Test method to validate FIRT IPv6 aggregation.
        /// </summary>
        [TestMethod]
        public void TestV6FirtAggregation()
        {
            var trie = new TrieNode(new IPPrefix("::/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            var prefixes = Program.ReadPrefixes("Files\\bgptablev6_cleaned.txt");
            trie.PerformOperations(prefixes, new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);

            Assert.AreEqual(24006, aggregatesAdded.Count);

            var exportedLines = File.ReadAllLines("Files\\exported_v6_trie.txt");

            var exportedPrefixes = aggregatesAdded.Select(p => p.ToString()).ToHashSet();

            Assert.IsTrue(exportedLines.All(prefix => exportedPrefixes.Contains(prefix)));
        }
    }
}

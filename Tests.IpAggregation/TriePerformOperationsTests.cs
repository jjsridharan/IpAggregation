    using IpAggregation;

namespace IPAggregatorTests
{
    [TestClass]
    public class TriePerformOperationsTests
    {
        [TestMethod]
        public void AddTwoAdjacentPrefixes_ShouldAggregateTo_OnePrefix()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.1.0/24"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")), "Expected aggregate /23 was not added");
            Assert.AreEqual(1, aggregatesAdded.Count, "Unexpected number of aggregates added");
            Assert.AreEqual(0, withdrawnPrefixes.Count, "No withdrawals expected at this stage");
        }

        [TestMethod]
        public void RemoveOnePrefix_ShouldWithdrawAggregate()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Add two /24s -> aggregate to /23
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.1.0/24"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")), "Setup failed: /23 aggregate not created");

            // Clear sets and remove one of the children
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix>(),
                new List<IPPrefix> { new IPPrefix("10.0.1.0/24") },
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/23")), "Expected /23 to be withdrawn");
            Assert.AreEqual(1, withdrawnPrefixes.Count);
            Assert.AreEqual(0, aggregatesAdded.Count, "No new aggregates expected when a /24 remains");
        }

        [TestMethod]
        public void NonContiguousPrefixes_ShouldNotFormAggregate()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.2.0/24"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.AreEqual(0, aggregatesAdded.Count, "Non-contiguous /24s should not aggregate");
            Assert.AreEqual(0, withdrawnPrefixes.Count);
        }

        [TestMethod]
        public void AddingAggregateThenSpecific_ShouldKeepOnlyAggregate()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Add /23 first
            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("10.0.0.0/23") },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            // Clear sets and add one of its /24s
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("10.0.0.0/24"), new IPPrefix("10.0.1.0/24") },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            // Aggregate should remain, leaf should not be exported
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")));
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count);
        }

        [TestMethod]
        public void MultiLevelNesting_ShouldCollapseIntoHigherAggregate()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Step 1: two /24s -> expect /23
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.1.0/24"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")));
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count);

            // Step 2: add next two /24s -> expect /22, withdraw /23
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.2.0/24"),
                    new IPPrefix("10.0.3.0/24"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/22")));
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/23")));
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(1, withdrawnPrefixes.Count);
        }

        [TestMethod]
        public void CollapseAndExpand_OnDelete_ShouldWithdrawAggregateAndReExportHalf()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Step 1: Add 4 /24s -> /22
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.1.0/24"),
                    new IPPrefix("10.0.2.0/24"),
                    new IPPrefix("10.0.3.0/24"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/22")));
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count);

            // Step 2: Remove right half -> withdraw /22, export left /23
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix>(),
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.2.0/24"),
                    new IPPrefix("10.0.3.0/24"),
                },
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/22")));
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")));
            Assert.IsFalse(aggregatesAdded.Contains(new IPPrefix("10.0.2.0/23")));
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(1, withdrawnPrefixes.Count);
        }

        [TestMethod]
        public void CollapseAndExpand_LeftHalfRemoved_ShouldWithdrawAggregateAndReExportRightHalf()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Step 1: Add 4 /24s -> /22
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.1.0/24"),
                    new IPPrefix("10.0.2.0/24"),
                    new IPPrefix("10.0.3.0/24"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/22")));
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count);

            // Step 2: Remove left half -> withdraw /22, export right /23
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix>(),
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.1.0/24"),
                },
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/22")));
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.2.0/23")));
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(1, withdrawnPrefixes.Count);
        }

        [TestMethod]
        public void InterleavedAddDelete_ShouldUpdateAggregatesCorrectly()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Step 1: Add three /24s -> expect first two make /23
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.1.0/24"),
                    new IPPrefix("10.0.2.0/24"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")));

            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            // Step 2: Add one more to complete /22
            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("10.0.3.0/24") },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/22")));
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/23")));

            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            // Step 3: Delete one from middle -> withdraw /22, right pair becomes /23
            trie.PerformOperations(
                new List<IPPrefix>(),
                new List<IPPrefix> { new IPPrefix("10.0.1.0/24") },
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/22")));
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.2.0/23")));
        }
        [TestMethod]
        public void InterleavedAddDelete_ShouldUpdateAggregatesCorrectly2()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Step 1: Add three /24s -> expect first two make /23
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.1.0/24"),
                    new IPPrefix("10.0.2.0/24"),
                    new IPPrefix("10.0.4.0/24"),
                    new IPPrefix("10.0.5.0/24"),
                    new IPPrefix("10.0.6.0/24"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")));
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.4.0/23")));
            Assert.AreEqual(2, aggregatesAdded.Count, "Expecting only two /23s");

            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            // Step 2: Add one more to complete /22
            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("10.0.3.0/24"), new IPPrefix("10.0.7.0/24") },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/21")));
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/23")));
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.4.0/23")));
            Assert.AreEqual(1, aggregatesAdded.Count, "Expecting only /21");
            Assert.AreEqual(2, withdrawnPrefixes.Count, "Expecting only two /23s");

            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();
        }


        [TestMethod]
        public void InterleavedAddDelete_ShouldUpdateAggregatesCorrectly3()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Step 1: Add three /24s -> expect first two make /23
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.1.0/24"),
                    new IPPrefix("10.0.2.0/24"),
                    new IPPrefix("10.0.4.0/24"),
                    new IPPrefix("10.0.5.0/24"),
                    new IPPrefix("10.0.6.0/24"),
                    new IPPrefix("10.0.7.0/24"),
                    new IPPrefix("10.0.8.0/24"),
                    new IPPrefix("10.0.9.0/24"),
                    new IPPrefix("10.0.10.0/24"),
                    new IPPrefix("10.0.11.0/24"),
                    new IPPrefix("10.0.12.0/24"),
                    new IPPrefix("10.0.13.0/24"),
                    new IPPrefix("10.0.14.0/24"),
                    new IPPrefix("10.0.15.0/24"),
                    new IPPrefix("10.0.16.0/24"),
                    new IPPrefix("10.0.17.0/24"),
                    new IPPrefix("10.0.18.0/24"),
                    new IPPrefix("10.0.19.0/24"),
                    new IPPrefix("10.0.20.0/24"),
                    new IPPrefix("10.0.21.0/24"),
                    new IPPrefix("10.0.22.0/24"),
                    new IPPrefix("10.0.23.0/24"),
                    new IPPrefix("10.0.24.0/24"),
                    new IPPrefix("10.0.25.0/24"),
                    new IPPrefix("10.0.26.0/24"),
                    new IPPrefix("10.0.27.0/24"),
                    new IPPrefix("10.0.28.0/24"),
                    new IPPrefix("10.0.29.0/24"),
                    new IPPrefix("10.0.30.0/24"),
                    new IPPrefix("10.0.31.0/24")
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.8.0/21")));
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")));
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.4.0/22")));
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.16.0/20")));
            Assert.AreEqual(4, aggregatesAdded.Count, "Expecting 4 responses.");

            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            // Step 2: Add one more to complete /19
            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("10.0.3.0/24"), new IPPrefix("10.0.7.0/24") },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/19")));
            Assert.AreEqual(1, aggregatesAdded.Count, "Expecting only /19");
            Assert.AreEqual(4, withdrawnPrefixes.Count, "Expecting only two /23s");

            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            // Step 2: Add one more to complete /19
            trie.PerformOperations(
                new List<IPPrefix>(),
                new List<IPPrefix>() { new IPPrefix("10.0.15.0/24") },
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.16.0/20")));
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.12.0/23")));
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.8.0/22")));
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/21")));
            Assert.AreEqual(4, aggregatesAdded.Count, "Expecting 4 aggregates");
            Assert.AreEqual(1, withdrawnPrefixes.Count, "Expecting only one /23");
        }


        [TestMethod]
        public void IPv6_AdjacentPrefixes_ShouldAggregate()
        {
            var trie = new TrieNode(new IPPrefix("::/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("2001:db8::/64"),
                    new IPPrefix("2001:db8:0:1::/64"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("2001:db8::/63")));
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count);
        }
    }
}

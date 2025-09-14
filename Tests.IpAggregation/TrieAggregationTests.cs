using IpAggregation;

namespace IPAggregatorTests
{
    [TestClass]
    public class TrieAggregationTests
    {
        [TestMethod]
        public void AddTwoAdjacentPrefixes_ShouldAggregateTo_OnePrefix()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            // Arrange
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            var prefixes = new List<IPPrefix>
            {
                new IPPrefix("10.0.0.0/24"),
                new IPPrefix("10.0.1.0/24")
            };

            // Act
            trie.PerformOperations(prefixes, new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);

            // Assert
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")),
                "Expected aggregate /23 was not added");
            Assert.AreEqual(1, aggregatesAdded.Count, "Unexpected number of aggregates added");
            Assert.AreEqual(0, withdrawnPrefixes.Count, "No withdrawals expected at this stage");
        }

        [TestMethod]
        public void RemoveOnePrefix_ShouldBreakAggregate_AndReannounceRemaining()
        {
            // Arrange
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Step 1: Add two /24s → aggregate to /23
            var prefixesToAdd = new List<IPPrefix>
            {
                new IPPrefix("10.0.0.0/24"),
                new IPPrefix("10.0.1.0/24")
            };
            trie.PerformOperations(prefixesToAdd, new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")),
                "Setup failed: /23 aggregate not created");

            // Step 2: Now remove one prefix → expect /23 withdrawn, and surviving /24 reannounced
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            var prefixesToRemove = new List<IPPrefix>
            {
                new IPPrefix("10.0.1.0/24")
            };
            trie.PerformOperations(new List<IPPrefix>(), prefixesToRemove, aggregatesAdded, withdrawnPrefixes);

            // Assert
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/23")),
                "Expected /23 to be withdrawn");
            Assert.AreEqual(1, withdrawnPrefixes.Count, "Unexpected withdrawals count");
        }

        [TestMethod]
        public void NonContiguousPrefixes_ShouldNotFormAggregate()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Add non-contiguous prefixes
            var prefixesToAdd = new List<IPPrefix>
    {
        new IPPrefix("10.0.0.0/24"),
        new IPPrefix("10.0.2.0/24")
    };
            trie.PerformOperations(prefixesToAdd, new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);
            Assert.AreEqual(0, aggregatesAdded.Count, "No /24s should be present");
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

            // Now add one of its children /24
            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("10.0.0.0/24"), new IPPrefix("10.0.1.0/24") },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")));
            Assert.AreEqual(1, aggregatesAdded.Count, "No /24s should be present");
        }

        [TestMethod]
        public void MultiLevelNesting_ShouldCollapseIntoHigherAggregate()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));

            // --- First step: Add two /24s → expect /23
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            trie.PerformOperations(
                new List<IPPrefix>
                {
            new IPPrefix("10.0.0.0/24"),
            new IPPrefix("10.0.1.0/24")
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")), "Expected /23 aggregate to be exported");
            Assert.IsFalse(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/24")), "Leaf /24 should not be exported");
            Assert.AreEqual(0, withdrawnPrefixes.Count);

            // reset sets for next batch
            aggregatesAdded = new List<IPPrefix>();
            withdrawnPrefixes = new List<IPPrefix>();

            // --- Second step: Add next two /24s → expect collapse into /22
            trie.PerformOperations(
                new List<IPPrefix>
                {
            new IPPrefix("10.0.2.0/24"),
            new IPPrefix("10.0.3.0/24")
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/22")), "Expected /22 aggregate to be exported");
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/23")), "Intermediate /23 should be withdrawn");
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(1, withdrawnPrefixes.Count);
        }

        [TestMethod]
        public void CollapseAndExpand_OnDelete_ShouldWithdrawAggregateAndReExportChildren()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));

            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // --- Step 1: Add 4 /24s -> collapse into /22
            trie.PerformOperations(
                new List<IPPrefix>
                {
            new IPPrefix("10.0.0.0/24"),
            new IPPrefix("10.0.1.0/24"),
            new IPPrefix("10.0.2.0/24"),
            new IPPrefix("10.0.3.0/24")
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/22")), "Expected /22 aggregate to be exported after full coverage");
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count, "Nothing withdrawn in first insert");

            // reset sets for next call
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            // --- Step 2: Remove 2 /24s (break the /22)
            trie.PerformOperations(
                new List<IPPrefix>(),
                new List<IPPrefix>
                {
            new IPPrefix("10.0.2.0/24"),
            new IPPrefix("10.0.3.0/24")
                },
                aggregatesAdded,
                withdrawnPrefixes);

            // /22 must be withdrawn
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/22")), "Collapsed /22 should be withdrawn when coverage is broken");

            // Left half should be re-exported
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")), "Left-side /23 must be re-exported after collapse breaks");

            // Right half is gone entirely, so no re-aggregate
            Assert.IsFalse(aggregatesAdded.Contains(new IPPrefix("10.0.2.0/23")), "Right-side /23 should not exist since its /24s were removed");

            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(1, withdrawnPrefixes.Count);
        }

        [TestMethod]
        public void CollapseAndExpand_LeftHalfRemoved_ShouldWithdrawAggregateAndReExportRightHalf()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));

            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // --- Step 1: Add 4 /24s -> collapse into /22
            trie.PerformOperations(
                new List<IPPrefix>
                {
            new IPPrefix("10.0.0.0/24"),
            new IPPrefix("10.0.1.0/24"),
            new IPPrefix("10.0.2.0/24"),
            new IPPrefix("10.0.3.0/24")
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/22")), "Expected /22 aggregate to be exported after full coverage");
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count, "Nothing withdrawn in first insert");

            // reset sets for next call
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            // --- Step 2: Remove left half /24s (10.0.0.0/24 and 10.0.1.0/24)
            trie.PerformOperations(
                new List<IPPrefix>(),
                new List<IPPrefix>
                {
            new IPPrefix("10.0.0.0/24"),
            new IPPrefix("10.0.1.0/24")
                },
                aggregatesAdded,
                withdrawnPrefixes);

            // /22 must be withdrawn
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/22")), "Collapsed /22 should be withdrawn when coverage is broken");

            // Right half should be re-exported
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.2.0/23")), "Right-side /23 must be re-exported after collapse breaks");

            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(1, withdrawnPrefixes.Count);
        }

        [TestMethod]
        public void InterleavedAddDelete_ShouldUpdateAggregatesCorrectly()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // --- Step 1: Add some /24 prefixes
            trie.PerformOperations(
                new List<IPPrefix>
                {
            new IPPrefix("10.0.0.0/24"),
            new IPPrefix("10.0.1.0/24"),
            new IPPrefix("10.0.2.0/24")
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")), "Expected /23 aggregate from first two /24s");

            // reset for next operation
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            // --- Step 2: Add another /24 to complete /22
            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("10.0.3.0/24") },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            // /22 should replace previous /23 aggregates
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/22")), "Full /22 should now be exported");
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/23")), "Previous /23 should be withdrawn");

            // reset
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            // --- Step 3: Delete one /24 from the middle
            trie.PerformOperations(
                new List<IPPrefix>(),
                new List<IPPrefix> { new IPPrefix("10.0.1.0/24") },
                aggregatesAdded,
                withdrawnPrefixes);

            // /22 should be withdrawn
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/22")), "/22 should be withdrawn due to coverage break");

            // remaining /24s may form smaller aggregates
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.2.0/23")), "Right two /24s should form /23 aggregate");
        }


    }
}

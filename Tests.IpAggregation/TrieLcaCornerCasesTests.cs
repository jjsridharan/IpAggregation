using IpAggregation;

namespace IPAggregatorTests
{
    [TestClass]
    public class TrieLcaCornerCasesTests
    {
        [TestMethod]
        [TestCategory("Unit")]
        public void LCA_Root_WithDistinctPrefixes_NoAggregation()
        {
            // Arrange
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Act: prefixes that live far apart -> common ancestor is close to root
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.1.0/24"),
                    new IPPrefix("172.16.0.0/24"),
                    new IPPrefix("172.16.1.0/24"),
                    new IPPrefix("192.168.1.0/24"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            // Assert: we expect only aggregates for locally contiguous pairs
            Assert.AreEqual(2, aggregatesAdded.Count);
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")), "Expected /23 after two /24s");
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("172.16.0.0/23")), "Expected /23 after two /24s");
            Assert.AreEqual(0, withdrawnPrefixes.Count);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void LCA_Deep_MultiLevelAggregation_FromFour_QuarterBlocks()
        {
            // Arrange
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Step 1: Two /26 -> expect /25
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/26"),
                    new IPPrefix("10.0.0.64/26"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/25")), "Expected /25 after first two /26s");
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count);

            // Step 2: Add the other two /26 -> expect /24, withdraw the /25
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.128/26"),
                    new IPPrefix("10.0.0.192/26"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/24")), "Expected /24 after all four /26s");
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/25")), "Intermediate /25 should be withdrawn");
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(1, withdrawnPrefixes.Count);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void LCA_AddAndRemove_InSingleCall_DifferentBranches()
        {
            // Arrange: start with a collapsed /22
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

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

            // Act: remove one that breaks /22 and add a disjoint /24 in same call
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("10.0.4.0/24") },
                new List<IPPrefix> { new IPPrefix("10.0.3.0/24") },
                aggregatesAdded,
                withdrawnPrefixes);

            // Assert: /22 withdrawn and left half /23 re-exported
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/22")), "Collapsed /22 should be withdrawn");
            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")), "Left-side /23 should be exported");
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void LCA_DisjointAdd_WithRemoval_OnlyAffectsImpactedSubtree()
        {
            // Arrange: start with /23 aggregate
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

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")));

            // Act: add a disjoint /24 and remove one child of the /23 in the same call
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("172.16.0.0/24") },
                new List<IPPrefix> { new IPPrefix("10.0.1.0/24") },
                aggregatesAdded,
                withdrawnPrefixes);

            // Assert: /23 must be withdrawn; disjoint add does not immediately produce aggregates
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/23")));
            Assert.AreEqual(0, aggregatesAdded.Count);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void LCA_IPv6_MultiLevelAggregation()
        {
            // Arrange
            var trie = new TrieNode(new IPPrefix("::/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Step 1: two adjacent /65 under 2001:db8::/64 -> expect /64
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("2001:db8::/65"),
                    new IPPrefix("2001:db8:0:0:8000::/65"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("2001:db8::/64")));
            Assert.AreEqual(1, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count);

            // Step 2: add two adjacent /65 under 2001:db8:0:1::/64 -> expect /63 and withdraw /64
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("2001:db8:0:1::/65"),
                    new IPPrefix("2001:db8:0:1:8000::/65"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("2001:db8::/63")));
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("2001:db8::/64")));
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void LCA_RemoveAllChildren_ShouldWithdrawParentAggregate()
        {
            // Arrange: start with /23 aggregate
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

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/23")));

            // Act: remove both children -> parent aggregate must be withdrawn
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix>(),
                new List<IPPrefix> { new IPPrefix("10.0.0.0/24"), new IPPrefix("10.0.1.0/24") },
                aggregatesAdded,
                withdrawnPrefixes);

            // Assert
            Assert.IsTrue(withdrawnPrefixes.Contains(new IPPrefix("10.0.0.0/23")));
            Assert.AreEqual(0, aggregatesAdded.Count);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Recompute_NoAnnouncements_WhenCoveragePreservedByMoreSpecifics()
        {
            // Arrange: start with /22 exported from four /24s
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

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

            // Act: replace 10.0.0.0/24 with two /25s in a single call; coverage of /22 remains
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("10.0.0.0/25"), new IPPrefix("10.0.0.128/25") },
                new List<IPPrefix> { new IPPrefix("10.0.0.0/24") },
                aggregatesAdded,
                withdrawnPrefixes);

            // Assert: parent /22 remains exported; no announcements should change
            Assert.AreEqual(0, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Recompute_NoChildExport_WhenParentAlreadyExported()
        {
            // Arrange: build /21 exported (eight /24s)
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            trie.PerformOperations(
                new List<IPPrefix>
                {
                    new IPPrefix("10.0.0.0/24"),
                    new IPPrefix("10.0.1.0/24"),
                    new IPPrefix("10.0.2.0/24"),
                    new IPPrefix("10.0.3.0/24"),
                    new IPPrefix("10.0.4.0/24"),
                    new IPPrefix("10.0.5.0/24"),
                    new IPPrefix("10.0.6.0/24"),
                    new IPPrefix("10.0.7.0/24"),
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.IsTrue(aggregatesAdded.Contains(new IPPrefix("10.0.0.0/21")), "Expected /21 to be exported");

            // Act: add deeper specifics under an already-covered subtree
            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("10.0.0.0/25"), new IPPrefix("10.0.0.128/25") },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            // Assert: since parent /21 is exported, no child aggregates should be exported
            Assert.AreEqual(0, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Recompute_NoOp_WhenNoChanges()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            trie.PerformOperations(new List<IPPrefix>(), new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);

            Assert.AreEqual(0, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Recompute_AddAndRemoveSamePrefix_NoAnnouncements()
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            trie.PerformOperations(
                new List<IPPrefix> { new IPPrefix("10.1.0.0/24") },
                new List<IPPrefix> { new IPPrefix("10.1.0.0/24") },
                aggregatesAdded,
                withdrawnPrefixes);

            Assert.AreEqual(0, aggregatesAdded.Count);
            Assert.AreEqual(0, withdrawnPrefixes.Count);
        }
    }
}

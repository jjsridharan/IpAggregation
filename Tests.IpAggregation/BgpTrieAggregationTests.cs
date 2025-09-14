using IpAggregation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace IPAggregatorTests
{
    [TestClass]
    public class BgpTrieAggregationTests
    {
        private static BgpPrefix Make(string cidr,
                                      IEnumerable<string>? communities = null,
                                      int? med = null,
                                      string? origin = null,
                                      bool? atomicAgg = null)
        {
            var p = new BgpPrefix(cidr, communities?.ToList());
            p.Med = med;
            p.Origin = origin;
            p.AtomicAggregate = atomicAgg;
            return p;
        }

        [TestMethod]
        public void Bgp_AdjacentPrefixes_ShouldAggregate_WithCommunityUnion()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));

            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            var left = Make("10.0.0.0/24", new[] { "A", "B" }, med: 100, origin: "Igp", atomicAgg: false);
            var right = Make("10.0.1.0/24", new[] { "B", "C" }, med: 100, origin: "Igp", atomicAgg: false);

            trie.PerformOperations(new List<IPPrefix> { left, right }, new List<IPPrefix>(), adds, withdraws);

            // Expect single /23 aggregate (children not exported)
            var agg = adds.SingleOrDefault(p => p.MaskLength == 23 && p.Address.Equals(IPAddress.Parse("10.0.0.0")));
            Assert.IsNotNull(agg, "Expected /23 aggregate not exported");
            Assert.IsInstanceOfType(agg, typeof(BgpPrefix));

            var bgpAgg = (BgpPrefix)agg;
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, bgpAgg.Community.ToList(), "Community union incorrect");
            Assert.AreEqual(100, bgpAgg.Med, "MED should be preserved");
            Assert.AreEqual("Igp", bgpAgg.Origin, "Origin should remain Igp");
            Assert.IsFalse(bgpAgg.AtomicAggregate ?? true, "AtomicAggregate should be false");
            Assert.AreEqual(1, adds.Count, "Only aggregate should be exported");
            Assert.AreEqual(0, withdraws.Count, "No withdrawals expected");
        }

        [TestMethod]
        public void Bgp_OriginPrecedence_EgpBeatsIgp()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));
            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            var p1 = Make("10.0.0.0/24", new[] { "X" }, med: 10, origin: "Igp");
            var p2 = Make("10.0.1.0/24", new[] { "Y" }, med: 10, origin: "Egp");

            trie.PerformOperations(new List<IPPrefix> { p1, p2 }, new List<IPPrefix>(), adds, withdraws);

            var agg = (BgpPrefix)adds.Single(p => p.MaskLength == 23);
            Assert.AreEqual("Egp", agg.Origin, "Egp should dominate Igp");
        }

        [TestMethod]
        public void Bgp_OriginPrecedence_IncompleteBeatsEgp()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));
            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            var p1 = Make("10.0.0.0/24", new[] { "L1" }, med: 20, origin: "Incomplete");
            var p2 = Make("10.0.1.0/24", new[] { "L2" }, med: 20, origin: "Egp");

            trie.PerformOperations(new List<IPPrefix> { p1, p2 }, new List<IPPrefix>(), adds, withdraws);

            var agg = (BgpPrefix)adds.Single(p => p.MaskLength == 23);
            Assert.AreEqual("Incomplete", agg.Origin, "Incomplete should dominate Egp");
        }

        [TestMethod]
        public void Bgp_AtomicAggregate_TrueIfAnyChildSetsTrue()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));
            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            var left = Make("10.0.0.0/24", new[] { "AA" }, med: 5, origin: "Igp", atomicAgg: true);
            var right = Make("10.0.1.0/24", new[] { "BB" }, med: 5, origin: "Igp", atomicAgg: false);

            trie.PerformOperations(new List<IPPrefix> { left, right }, new List<IPPrefix>(), adds, withdraws);

            var agg = (BgpPrefix)adds.Single(p => p.MaskLength == 23);
            Assert.IsTrue(agg.AtomicAggregate.HasValue && agg.AtomicAggregate.Value, "AtomicAggregate should be true");
        }

        [TestMethod]
        public void Bgp_MedMismatch_ShouldPreventAggregation()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));
            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            var p1 = Make("10.0.0.0/24", new[] { "L" }, med: 10, origin: "Igp");
            var p2 = Make("10.0.1.0/24", new[] { "R" }, med: 20, origin: "Igp");

            trie.PerformOperations(new List<IPPrefix> { p1, p2 }, new List<IPPrefix>(), adds, withdraws);

            Assert.AreEqual(0, adds.Count, "No aggregate should be exported when MED differs");
        }

        [TestMethod]
        public void Bgp_MultiLevelAggregation_FourPrefixesCollapseAndWithdrawIntermediate()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));

            // Step 1: first pair
            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    Make("10.0.0.0/24", new[] { "A" }, med: 30, origin: "Igp"),
                    Make("10.0.1.0/24", new[] { "B" }, med: 30, origin: "Igp")
                },
                new List<IPPrefix>(),
                adds,
                withdraws);

            Assert.IsTrue(adds.Any(p => p.MaskLength == 23));
            adds.Clear(); withdraws.Clear();

            // Step 2: second pair triggers /22
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    Make("10.0.2.0/24", new[] { "C" }, med: 30, origin: "Igp"),
                    Make("10.0.3.0/24", new[] { "D" }, med: 30, origin: "Igp")
                },
                new List<IPPrefix>(),
                adds,
                withdraws);

            var agg22 = (BgpPrefix)adds.Single(p => p.MaskLength == 22 && p.Address.Equals(IPAddress.Parse("10.0.0.0")));
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C", "D" }, agg22.Community.ToList(), "Community union across multi-level aggregate incorrect");
            Assert.IsTrue(withdraws.Any(p => p.MaskLength == 23), "Intermediate /23 should be withdrawn");
        }

        [TestMethod]
        public void Bgp_DeleteHalf_ShouldWithdrawHigherAggregate_AndReExportRemainingSubAggregate()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));

            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            // Build /22
            trie.PerformOperations(
                new List<IPPrefix>
                {
                    Make("10.0.0.0/24", new[] { "L0" }, med: 40, origin: "Igp"),
                    Make("10.0.1.0/24", new[] { "L1" }, med: 40, origin: "Igp"),
                    Make("10.0.2.0/24", new[] { "R0" }, med: 40, origin: "Igp"),
                    Make("10.0.3.0/24", new[] { "R1" }, med: 40, origin: "Igp")
                },
                new List<IPPrefix>(),
                adds,
                withdraws);

            Assert.IsTrue(adds.Any(p => p.MaskLength == 22), "Setup failed: /22 not exported");
            adds.Clear(); withdraws.Clear();

            // Remove right half
            trie.PerformOperations(
                new List<IPPrefix>(),
                new List<IPPrefix>
                {
                    new BgpPrefix("10.0.2.0/24"),
                    new BgpPrefix("10.0.3.0/24")
                },
                adds,
                withdraws);

            Assert.IsTrue(withdraws.Any(p => p.MaskLength == 22), "/22 should be withdrawn");
            var newAgg = adds.SingleOrDefault(p => p.MaskLength == 23 && p.Address.Equals(IPAddress.Parse("10.0.0.0")));
            Assert.IsNotNull(newAgg, "Left-side /23 should re-appear");
            var bgpAgg = (BgpPrefix)newAgg;
            CollectionAssert.AreEquivalent(new[] { "L0", "L1" }, bgpAgg.Community.ToList(), "Remaining aggregate should only carry left communities");
        }

        [TestMethod]
        public void Bgp_DeleteOneChildOfPair_ShouldWithdrawAggregateAndLeaveNoExport()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));
            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            var a = Make("10.0.0.0/24", new[] { "A" }, med: 55, origin: "Igp");
            var b = Make("10.0.1.0/24", new[] { "B" }, med: 55, origin: "Igp");

            trie.PerformOperations(new List<IPPrefix> { a, b }, new List<IPPrefix>(), adds, withdraws);
            Assert.IsTrue(adds.Any(p => p.MaskLength == 23), "Setup failed");
            adds.Clear(); withdraws.Clear();

            trie.PerformOperations(new List<IPPrefix>(), new List<IPPrefix> { b }, adds, withdraws);

            Assert.IsTrue(withdraws.Any(p => p.MaskLength == 23), "Aggregate /23 should be withdrawn");
            Assert.AreEqual(0, adds.Count, "No new exports expected (single /24 leaf not exported)");
        }

        [TestMethod]
        public void Bgp_BothMedNull_ShouldAggregate_WithNullMed()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));
            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            var a = Make("10.20.0.0/24", new[] { "A" }, med: null, origin: "Igp");
            var b = Make("10.20.1.0/24", new[] { "B" }, med: null, origin: "Igp");

            trie.PerformOperations(new List<IPPrefix> { a, b }, new List<IPPrefix>(), adds, withdraws);

            var agg = (BgpPrefix)adds.Single(p => p.MaskLength == 23);
            Assert.IsNull(agg.Med, "MED should remain null when both contributors have null");
        }

        [TestMethod]
        public void Bgp_OneMedNullOneSet_ShouldNotAggregate()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));
            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            var a = Make("10.30.0.0/24", new[] { "A" }, med: null, origin: "Igp");
            var b = Make("10.30.1.0/24", new[] { "B" }, med: 200, origin: "Igp");

            trie.PerformOperations(new List<IPPrefix> { a, b }, new List<IPPrefix>(), adds, withdraws);

            Assert.AreEqual(0, adds.Count, "Aggregate should not form when one MED is null and the other set");
        }

        [TestMethod]
        public void Bgp_ReAddLeafWithNewCommunity_ShouldUpdateFutureAggregate()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));

            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            // Initial pair
            var a1 = Make("10.40.0.0/24", new[] { "A" }, med: 10, origin: "Igp");
            var b1 = Make("10.40.1.0/24", new[] { "B" }, med: 10, origin: "Igp");
            trie.PerformOperations(new List<IPPrefix> { a1, b1 }, new List<IPPrefix>(), adds, withdraws);
            var firstAgg = (BgpPrefix)adds.Single(p => p.MaskLength == 23);
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, firstAgg.Community.ToList());
            adds.Clear(); withdraws.Clear();

            // Re-add left leaf with additional community C (simulate attribute change)
            var a2 = Make("10.40.0.0/24", new[] { "A", "C" }, med: 10, origin: "Igp");
            trie.PerformOperations(new List<IPPrefix> { a2 }, new List<IPPrefix>(), adds, withdraws);

            // Expect no new aggregate export (already exported) or a withdrawal/re-add pair depending on logic,
            // but internal aggregate prefix should now include C.
            // Force a recompute by adding a sibling pair to escalate (optional trick): add /24s to form /22
            adds.Clear(); withdraws.Clear();
            var c = Make("10.40.2.0/24", new[] { "X" }, med: 10, origin: "Igp");
            var d = Make("10.40.3.0/24", new[] { "Y" }, med: 10, origin: "Igp");
            trie.PerformOperations(new List<IPPrefix> { c, d }, new List<IPPrefix>(), adds, withdraws);

            var agg22 = (BgpPrefix)adds.Single(p => p.MaskLength == 22);
            // Communities should contain all: A,B,C,X,Y
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C", "X", "Y" }, agg22.Community.ToList());
        }

        [TestMethod]
        public void Bgp_AtomicAggregateBecomesTrueAfterLeafChange()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));
            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            var left = Make("10.50.0.0/24", new[] { "L" }, med: 15, origin: "Igp", atomicAgg: false);
            var right = Make("10.50.1.0/24", new[] { "R" }, med: 15, origin: "Igp", atomicAgg: false);
            trie.PerformOperations(new List<IPPrefix> { left, right }, new List<IPPrefix>(), adds, withdraws);
            var agg = (BgpPrefix)adds.Single(p => p.MaskLength == 23);
            Assert.IsFalse(agg.AtomicAggregate ?? true);
            adds.Clear(); withdraws.Clear();

            // Re-add right with atomic=true
            var right2 = Make("10.50.1.0/24", new[] { "R" }, med: 15, origin: "Igp", atomicAgg: true);
            trie.PerformOperations(new List<IPPrefix> { right2 }, new List<IPPrefix>(), adds, withdraws);

            // Trigger recompute upward by adding another unrelated prefix pair forming higher aggregate
            adds.Clear(); withdraws.Clear();
            var c = Make("10.50.2.0/24", new[] { "X" }, med: 15, origin: "Igp");
            var d = Make("10.50.3.0/24", new[] { "Y" }, med: 15, origin: "Igp");
            trie.PerformOperations(new List<IPPrefix> { c, d }, new List<IPPrefix>(), adds, withdraws);

            var top = (BgpPrefix)adds.Single(p => p.MaskLength == 22);
            Assert.IsTrue(top.AtomicAggregate.HasValue && top.AtomicAggregate.Value,
                "AtomicAggregate should propagate true once any contributing subtree sets it");
        }

        [TestMethod]
        public void Bgp_GapFill_AddingMissingSiblingLaterTriggersAggregation()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));
            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            var only = Make("10.60.0.0/24", new[] { "A" }, med: 5, origin: "Igp");
            trie.PerformOperations(new List<IPPrefix> { only }, new List<IPPrefix>(), adds, withdraws);
            Assert.AreEqual(0, adds.Count, "Single leaf should not export aggregate");
            adds.Clear();

            var sibling = Make("10.60.1.0/24", new[] { "B" }, med: 5, origin: "Igp");
            trie.PerformOperations(new List<IPPrefix> { sibling }, new List<IPPrefix>(), adds, withdraws);

            Assert.IsTrue(adds.Any(p => p.MaskLength == 23 && p.Address.Equals(IPAddress.Parse("10.60.0.0"))),
                "Aggregate should appear after gap filled");
        }

        [TestMethod]
        public void Bgp_MedMismatchThenFix_ShouldAggregateAfterCorrection()
        {
            var trie = new TrieNode(new BgpPrefix("0.0.0.0/0"));
            var adds = new List<IPPrefix>();
            var withdraws = new List<IPPrefix>();

            var aBad = Make("10.70.0.0/24", new[] { "A" }, med: 10, origin: "Igp");
            var bBad = Make("10.70.1.0/24", new[] { "B" }, med: 20, origin: "Igp");
            trie.PerformOperations(new List<IPPrefix> { aBad, bBad }, new List<IPPrefix>(), adds, withdraws);
            Assert.AreEqual(0, adds.Count, "No aggregate due to MED mismatch");
            adds.Clear();

            // Re-add second with matching MED
            var bGood = Make("10.70.1.0/24", new[] { "B" }, med: 10, origin: "Igp");
            trie.PerformOperations(new List<IPPrefix> { bGood }, new List<IPPrefix>(), adds, withdraws);

            Assert.IsTrue(adds.Any(p => p.MaskLength == 23 && p.Address.Equals(IPAddress.Parse("10.70.0.0"))),
                "Aggregate should appear after MED corrected");
        }
    }
}
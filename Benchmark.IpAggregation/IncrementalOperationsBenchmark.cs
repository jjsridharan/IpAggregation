using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using IpAggregation;
using System.Collections.Generic;
using System.Linq;

namespace Bechmark.IpAggregation
{
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 1, invocationCount: 1)]
    [MaxIterationCount(16)] // limit iterations for faster runs
    public class IncrementalOperationsBenchmark
    {
        private List<IPPrefix> _prefixes = null!;     // All prefixes to choose from
        private List<IPPrefix> _toRemove = null!;    // Prefixes to delete each invocation
        private TrieNode _trie = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var allPrefixes = Program.ReadPrefixes();
            _prefixes = allPrefixes.Take(100).ToList(); // choose a small subset
            _toRemove = _prefixes;                       // delete same subset each time
        }

        // Runs before every benchmark invocation
        [IterationSetup(Target = nameof(Remove100NodesIncrementally))]
        public void BuildTrieForEachInvocation()
        {
            var allPrefixes = Program.ReadPrefixes();
            _trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Populate trie fully
            _trie.PerformOperations(allPrefixes, new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);
        }

        [Benchmark]
        public void Remove100NodesIncrementally()
        {
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();

            // Only measure deletions
            _trie.PerformOperations(new List<IPPrefix>(), _toRemove, aggregatesAdded, withdrawnPrefixes);
        }
    }
}

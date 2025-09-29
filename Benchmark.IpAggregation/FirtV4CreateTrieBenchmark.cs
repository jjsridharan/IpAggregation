using BenchmarkDotNet.Attributes;
using IpAggregation;
namespace Bechmark.IpAggregation;

[MemoryDiagnoser]
public class FirtV4CreateTrieBenchmark
{
    private List<IPPrefix> _prefixes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _prefixes = Program.ReadPrefixes("Files\\bgptablev4_cleaned.txt");
    }

    /// <summary>
    /// Full Internet Routing Table (FIRT) IPv4 trie creation benchmark.
    /// </summary>
    [Benchmark]
    public void CreateV4FirtTrie()
    {
        var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
        var aggregatesAdded = new List<IPPrefix>();
        var withdrawnPrefixes = new List<IPPrefix>();

        trie.PerformOperations(_prefixes, new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);
    }
}

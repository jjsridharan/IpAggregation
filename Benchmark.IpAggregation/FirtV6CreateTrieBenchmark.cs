using BenchmarkDotNet.Attributes;
using IpAggregation;
namespace Bechmark.IpAggregation;

[MemoryDiagnoser]
public class FirtCreateTrieBenchmark
{
    private List<IPPrefix> _prefixes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _prefixes = Program.ReadPrefixes("Files\\bgptablev6_cleaned.txt");
    }

    [Benchmark]
    public void CreateV6FirtTrie()
    {
        var trie = new TrieNode(new IPPrefix("::/0"));
        var aggregatesAdded = new List<IPPrefix>();
        var withdrawnPrefixes = new List<IPPrefix>();

        trie.PerformOperations(_prefixes, new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);
    }
}

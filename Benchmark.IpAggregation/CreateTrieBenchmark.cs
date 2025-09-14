using BenchmarkDotNet.Attributes;
using IpAggregation;
namespace Bechmark.IpAggregation;

[MemoryDiagnoser]
public class CreateTrieBenchmark
{
    private List<IPPrefix> _prefixes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _prefixes = Program.ReadPrefixes();
    }

    [Benchmark]
    public void CreateTrieWith2MillionNodes()
    {
        var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
        var aggregatesAdded = new List<IPPrefix>();
        var withdrawnPrefixes = new List<IPPrefix>();

        trie.PerformOperations(_prefixes, new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);
    }
}

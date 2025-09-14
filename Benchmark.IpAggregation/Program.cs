using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using IpAggregation;
using System.Diagnostics;

namespace Bechmark.IpAggregation
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // ConstructTrie();

            //VisualizeTrie();

            var config = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(Job.Default.WithRuntime(CoreRuntime.Core80).WithArguments(new[] { new MsBuildArgument("/nowarn:CS1591") }));
            BenchmarkSwitcher
                .FromAssemblies(new[] { typeof(Program).Assembly })
                .RunAll(config, args);
        }

        public static void VisualizeTrie()
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
                    new IPPrefix("10.0.3.0/24"),
                    new IPPrefix("10.0.4.0/24"),
                    new IPPrefix("10.0.5.0/24"),
                    new IPPrefix("10.0.6.0/24"),
                    new IPPrefix("10.0.7.0/24"),
                    new IPPrefix("10.0.8.0/24"),
                    new IPPrefix("10.0.9.0/24")
                },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.ToDotGraphFile("trie_step1.dot");

            // Step 2: Add one more to complete /19
            trie.PerformOperations(
                new List<IPPrefix>(),
                new List<IPPrefix>() { new IPPrefix("10.0.4.0/24") },
                aggregatesAdded,
                withdrawnPrefixes);

            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            trie.ToDotGraphFile("trie_step2.dot");

            // Step 2
            trie.PerformOperations(
                new List<IPPrefix>() { new IPPrefix("10.0.10.0/24"), new IPPrefix("10.0.11.0/24") },
                new List<IPPrefix>(),
                aggregatesAdded,
                withdrawnPrefixes);

            trie.ToDotGraphFile("trie_step3.dot");
        }

        private static void ConstructTrie()
        {
            List<IPPrefix> prefixes = ReadPrefixes();
            TrieOperations(prefixes);
        }

        private static void TrieOperations(List<IPPrefix> prefixes)
        {
            var trie = new TrieNode(new IPPrefix("0.0.0.0/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();
            // first batch.

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            trie.PerformOperations(prefixes, new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);
            stopwatch.Stop();
            PrintPrefixes(aggregatesAdded, withdrawnPrefixes);
            Console.WriteLine($"Time taken to process first batch: {stopwatch.ElapsedMilliseconds} ms");


            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            var prefixes2 = prefixes.Take(100).ToList();
            stopwatch.Restart();
            trie.PerformOperations(new List<IPPrefix>(), prefixes2, aggregatesAdded, withdrawnPrefixes);
            stopwatch.Stop();
            PrintPrefixes(aggregatesAdded, withdrawnPrefixes);
            Console.WriteLine($"Time taken to process first batch: {stopwatch.ElapsedMilliseconds} ms");
        }

        internal static List<IPPrefix> ReadPrefixes()
        {
            var prefixes = new List<IPPrefix>();
            File.ReadAllLines("ipv4.txt")
                .ToList()
                .ForEach(line =>
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    {
                        return; // Skip empty lines and comments
                    }
                    var parts = line.Split(' ');
                    if (parts.Length < 3)
                    {
                        Console.WriteLine($"Invalid line format: {line}");
                        return; // Skip invalid lines
                    }
                    prefixes.Add(new IPPrefix(parts[0]));
                });
            return prefixes;
        }

        private static void PrintPrefixes(List<IPPrefix> aggregatesAdded, List<IPPrefix> withdrawnPrefixes)
        {
            Console.WriteLine("Added prefixes:");
            foreach (IPPrefix prefix in aggregatesAdded)
            {
                Console.WriteLine(prefix);
            }

            Console.WriteLine("Withdrawn prefixes:");
            foreach (IPPrefix prefix in withdrawnPrefixes)
            {
                Console.WriteLine(prefix);
            }
        }
    }
}

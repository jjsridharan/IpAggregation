using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using IpAggregation;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("Tests.IpAggregation")]
namespace Bechmark.IpAggregation
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // CleanRawBgpTable("Files\\bgptablev4_raw.txt", "Files\\bgptablev4_cleaned.txt");
            // CleanRawBgpTable("Files\\bgptablev6_raw.txt", "Files\\bgptablev6_cleaned.txt");
            // AggregateLinear();
            // ConstructTrie();
            // VisualizeTrie();

            var config = ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(Job.Default.WithRuntime(CoreRuntime.Core80).WithArguments(new[] { new MsBuildArgument("/nowarn:CS1591") }));
            BenchmarkSwitcher
                .FromAssemblies(new[] { typeof(Program).Assembly })
                .RunAll(config, args);
        }


        /// <summary>
        /// Cleans the raw BGP table file to extract prefixes, next hops, and AS paths.
        /// https://bgp.potaroo.net/as2.0/bgp-active.html - V4 BGP table.
        /// https://bgp.potaroo.net/v6/as2.0/index.html - V6 BGP table.
        /// </summary>
        /// <param name="rawFile"></param>
        /// <param name="newFile"></param>
        internal static void CleanRawBgpTable(string rawFile, string newFile)
        {
            var fileContent = File.ReadAllLines(rawFile);

            var bgpRouteRegex = new Regex(@"\*>?\s+(\S+)\s+(\S+)\s+0\s+(.*) (?:i|e|\?)", RegexOptions.Compiled);
            var lineRegex = new Regex(@"\s+0 (.*) (?:i|e|\?)", RegexOptions.Compiled);

            int lineCount = 0;

            var cleanedLines = new List<string>();

            for (int i = 0; i < fileContent.Length; i++)
            {
                var line = fileContent[i];

                var lineMatch = lineRegex.Match(line);
                if (lineMatch.Success)
                {
                    cleanedLines.Add(line);
                    lineCount++;
                }
                else
                {
                    // there are lines where prefix and nexthop is split.
                    while (i + 1 < fileContent.Length)
                    {
                        line = line + fileContent[i + 1];
                        i++;

                        lineMatch = lineRegex.Match(line);
                        if (lineMatch.Success)
                        {
                            cleanedLines.Add(line);
                            lineCount++;
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"No match for line: {line}");
                        }
                    }
                }
            }


            Console.WriteLine($"Total lines matching '0 ... i': {lineCount}");

            var bgpPrefixes = new List<string>();

            foreach (var cleanedLine in cleanedLines)
            {
                var match = bgpRouteRegex.Match(cleanedLine);
                if (match.Success)
                {
                    var prefix = match.Groups[1].Value;
                    var nextHop = match.Groups[2].Value;
                    var asPath = match.Groups[3].Value;

                    bgpPrefixes.Add(prefix + "," + nextHop + "," + asPath);
                }
            }

            File.WriteAllLines(newFile, bgpPrefixes);

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
            List<IPPrefix> prefixes = ReadPrefixes("Files\\bgptablev6_cleaned.txt");
            TrieOperations(prefixes);
        }

        private static void TrieOperations(List<IPPrefix> prefixes)
        {
            var trie = new TrieNode(new IPPrefix("::/0"));
            var aggregatesAdded = new List<IPPrefix>();
            var withdrawnPrefixes = new List<IPPrefix>();
            // first batch.

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            trie.PerformOperations(prefixes, new List<IPPrefix>(), aggregatesAdded, withdrawnPrefixes);
            stopwatch.Stop();
            Console.WriteLine($"Time taken to process first batch: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Aggregates Added {aggregatesAdded.Count}");
            // PrintPrefixes(aggregatesAdded, withdrawnPrefixes);


            var exported = new List<IPPrefix>();
            foreach (var prefix in aggregatesAdded)
            {
                exported.Add(prefix);
            }

            exported.Sort(new IpPrefixComparer());
            
            var exportedStr = new List<string>();
            foreach (var prefix in exported)
            {
                exportedStr.Add(prefix.ToString());
            }

            File.WriteAllLines("Files\\exported_v6.txt", exportedStr);


            aggregatesAdded.Clear();
            withdrawnPrefixes.Clear();

            var prefixes2 = prefixes.Take(100).ToList();
            stopwatch.Restart();
            trie.PerformOperations(new List<IPPrefix>(), prefixes2, aggregatesAdded, withdrawnPrefixes);
            stopwatch.Stop();
            // PrintPrefixes(aggregatesAdded, withdrawnPrefixes);
            Console.WriteLine($"Time taken to process first batch: {stopwatch.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// Performs linear aggregation of prefixes without using a trie.
        /// </summary>
        internal static void AggregateLinear()
        {
            List<IPPrefix> prefixes = ReadPrefixes();
            var aggPrefixes = new SortedList<IPPrefix, Tuple<IPPrefix, bool>>(new IpPrefixComparer());
            Console.WriteLine("Length" + prefixes.Count);

            foreach (var prefix in prefixes)
            {
                if (aggPrefixes.ContainsKey(prefix))
                {
                    continue;
                }
                aggPrefixes.Add(prefix, Tuple.Create(prefix, false));
            }
            Console.WriteLine("Length" + aggPrefixes.Count);

            var changed = true;
            while (changed)
            {
                changed = false;
                int index = 1;
                for (; index < aggPrefixes.Count; ++index)
                {
                    var currPrefix = aggPrefixes.ElementAt(index);
                    var adjPrefix = GetAdjacentPrefix(currPrefix.Key);

                    // Check if the previous entry is same as what should be in adjPrefix.
                    var prevSumEntry = aggPrefixes.ElementAt(index - 1);
                    if (!prevSumEntry.Key.Equals(adjPrefix))
                    {
                        continue;
                    }

                    aggPrefixes.RemoveAt(index);
                    aggPrefixes.RemoveAt(index - 1);

                    var newPrefix = new IPPrefix(currPrefix.Key.Address, currPrefix.Key.MaskLength - 1);

                    aggPrefixes[newPrefix] = Tuple.Create(newPrefix, true);

                    changed = true;
                    --index;
                }

                Console.WriteLine("Length " + aggPrefixes.Count);
            }


            Console.WriteLine("After Aggregation");

            int count = 0;
            var exported = new List<IPPrefix>();
            var exportedHash = new HashSet<IPPrefix>();
            foreach (var prefix in aggPrefixes)
            {
                if (prefix.Value.Item2)
                {
                    count++;
                  // Console.WriteLine(prefix);

                    exported.Add(prefix.Key);
                    exportedHash.Add(prefix.Key);
                }
            }

            var exportedStr = new List<string>();
            for (int i = 0; i < exported.Count; i++)
            {
                bool isSuperNet = false;
                for(int j = 0; j < exported.Count; j++)
                {
                    if (i == j) continue;

                    isSuperNet = IsSupernet(exported[i].Address, exported[i].MaskLength, exported[j].Address, exported[j].MaskLength);

                    if (isSuperNet)
                    {
                        if (exportedHash.Contains(exported[j]))
                        {
                            exportedHash.Remove(exported[j]);
                        }
                    }
                }
            }

            foreach (var prefix in exportedHash)
            {
                exportedStr.Add(prefix.ToString());
            }


            Console.WriteLine("Length " + aggPrefixes.Count);
            Console.WriteLine("Aggregated " + exportedStr.Count);

            File.WriteAllLines("Files\\linear_exported.txt", exportedStr);
        }

        public static bool IsSupernet(IPAddress networkA, int prefixA, IPAddress networkB, int prefixB)
        {
            // A is supernet of B if A is less specific AND shares A's prefix bits
            return prefixA <= prefixB && PrefixMatch(networkA, networkB, prefixA);
        }

        private static bool PrefixMatch(IPAddress a, IPAddress b, int prefixLength)
        {
            if (a.AddressFamily != b.AddressFamily)
                return false;

            byte[] bytesA = a.GetAddressBytes();
            byte[] bytesB = b.GetAddressBytes();

            int fullBytes = prefixLength / 8;
            int remainingBits = prefixLength % 8;

            // Compare full bytes
            for (int i = 0; i < fullBytes; i++)
            {
                if (bytesA[i] != bytesB[i])
                    return false;
            }

            if (remainingBits > 0)
            {
                byte mask = (byte)(~(0xFF >> remainingBits));
                if ((bytesA[fullBytes] & mask) != (bytesB[fullBytes] & mask))
                    return false;
            }

            return true;
        }

        public static IPPrefix GetAdjacentPrefix(
            IPPrefix prefix)
        {
            var ar = prefix.Address.GetAddressBytes();

            var byteIndex = (prefix.MaskLength - 1) / 8;
            var bitIndex = 7 - ((prefix.MaskLength - 1) % 8);

            ar[byteIndex] = (byte)(ar[byteIndex] ^ (1 << bitIndex));

            // Return the adjacent prefix.
            return
                new IPPrefix(
                    new IPAddress(ar),
                    (ushort)prefix.MaskLength);
        }

        internal static List<IPPrefix> ReadPrefixes(string fileName = "Files\\bgptablev4_cleaned.txt")
        {
            var prefixes = new List<IPPrefix>();
            File.ReadAllLines(fileName)
                .ToList()
                .ForEach(line =>
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    {
                        return; // Skip empty lines and comments
                    }
                    var parts = line.Split(new char[] { ',', ' ' });
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

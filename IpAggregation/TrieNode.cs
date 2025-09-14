using System.Runtime.CompilerServices;
using System.Text;
[assembly: InternalsVisibleTo("Tests.IpAggregation")]
[assembly: InternalsVisibleTo("Benchmark.IpAggregation")]

namespace IpAggregation
{
    internal class TrieNode
    {
        public IPPrefix Prefix { get; private set; }

        public bool IsLeaf { get; private set; } = false;

        public bool IsAggregate { get; private set; } = false;

        public bool IsExported { get; private set; } = false;

        private TrieNode? Parent { get; set; }

        private TrieNode?[] children { get; set; } = new TrieNode[2];

        /// <summary>
        /// Property to indicate if the subtree rooted at this node has changed after add or remove operations.
        /// This  used to determine if we need to re-compute aggregates.
        /// </summary>
        private bool IsSubTreeChanged { get; set; } = false;

        public TrieNode(IPPrefix prefix, TrieNode? parent = null)
        {
            this.Prefix = prefix;
            this.Parent = parent;
        }

        public void PerformOperations(List<IPPrefix> prefixesToAdd, List<IPPrefix> prefixesToRemove, List<IPPrefix> aggregatesAdded, List<IPPrefix> aggregatesRemoved)
        {
            foreach (IPPrefix prefix in prefixesToAdd)
            {
                AddPrefix(prefix);
            }

            foreach (IPPrefix prefix in prefixesToRemove)
            {
                RemovePrefix(prefix, aggregatesRemoved);
            }

            ReComputeAffectedNodes(aggregatesAdded, aggregatesRemoved);
        }

        private void AddPrefix(IPPrefix prefix)
        {
            bool[] bitArray = prefix.GetBitsForAddress();

            TrieNode curr = this;
            curr.IsSubTreeChanged = true;
            for (int i = 0; i < prefix.MaskLength; i++)
            {
                var childIndex = bitArray[i] ? 1 : 0;
                if (curr.children[childIndex] == null)
                {
                    // Use virtual factory off current node's prefix to keep concrete type consistent.
                    IPPrefix childPrefix = curr.Prefix.CreateSubPrefix(bitArray, i + 1);
                    // If the child does not exist, create a new TrieNode with the prefix up to this point.
                    curr.children[childIndex] = new TrieNode(childPrefix, curr);
                }

                curr = curr.children[childIndex];
                curr.IsSubTreeChanged = true; // Mark this node as changed
            }

            curr.Prefix = prefix; // Update to the exact prefix (in case of BgpPrefix with metadata)

            var wasLeaf = curr.IsLeaf;

            curr.IsLeaf = true;

            curr = curr.Parent;

            var isBgpPrefix = prefix is BgpPrefix;

            // The goal here is to mark the parent nodes as aggregates if both children are leaves or aggregates.
            // We will exit the loop when we reach a node that does not have both children or if it is already an aggregate.
            while (curr != null)
            {
                if (curr.children[0] != null && curr.children[1] != null)
                {
                    if (curr.IsAggregate && (wasLeaf == false || isBgpPrefix == false))
                    {
                        // If this node is already an aggregate and there is no change in attributes, we can stop
                        break;
                    }

                    var previousPrefix = curr.Prefix;

                    curr.IsAggregate = CanThisNodeBeAggregated(curr);

                    var newPrefix = curr.Prefix;

                    // If the prefix has changed due to aggregation, we need to update it. (BGP implicit update)
                    if (isBgpPrefix && curr.IsAggregate && previousPrefix.Equals(newPrefix) == false)
                    {
                        // If the prefix has changed, we need to mark it as not exported so that it can be re-evaluated.
                        curr.IsExported = false;
                    }
                }
                else
                {
                    break; // Stop if we reach a node that does not have both children
                }

                curr = curr.Parent; // Move up to the parent node
            }
        }

        private bool CanThisNodeBeAggregated(TrieNode curr)
        {
            var canAggregate = (curr.children[0].IsLeaf || curr.children[0].IsAggregate) &&
                (curr.children[1].IsLeaf || curr.children[1].IsAggregate);

            // If we can aggregate, and the prefix type is BgpPrefix, we will generate the aggregate prefix.
            if (canAggregate && curr.Prefix is BgpPrefix)
            {
                canAggregate = BgpHelper.TryGenerateAggregate(
                    (BgpPrefix)curr.children[0].Prefix,
                    (BgpPrefix)curr.children[1].Prefix,
                    out BgpPrefix aggregateRoute);

                if (canAggregate)
                {
                    curr.Prefix = aggregateRoute;
                }
            }

            // If both children are leaves or aggregates, mark this node as aggregate
            return canAggregate;
        }

        private void RemovePrefix(IPPrefix prefix, List<IPPrefix> withdrawnPrefixes)
        {
            bool[] bitArray = prefix.GetBitsForAddress();

            TrieNode curr = this;
            curr.IsSubTreeChanged = true;
            int childIndex = bitArray[0] ? 1 : 0;
            for (int i = 0; i < prefix.MaskLength; i++)
            {
                childIndex = bitArray[i] ? 1 : 0;
                if (curr.children[childIndex] == null)
                {
                    throw new ArgumentException($"{prefix} The prefix to remove does not exist in the trie.");
                }

                curr = curr.children[childIndex];
                curr.IsSubTreeChanged = true; // Mark this node as changed
            }

            if (curr.IsLeaf == false)
            {
                throw new ArgumentException($"{prefix} The prefix to remove does not exist in the trie as leaf node.");
            }

            // If we reach here, we have found the leaf node to remove.
            curr.IsLeaf = false;

            // The goal here is to remove the nodes which are no longer needed in the parent stack.
            // Along the way,
            //  1)  we will add the prefix to withdrawn prefixes if it was exported and getting removed.
            //  2)  we will check if the node needs to be marked as aggregate or not.
            curr = curr.Parent;
            while (curr != null)
            {
                RemoveChildIfNoGrandChildren(curr, withdrawnPrefixes);

                // If both children are leaves or aggregates, mark this node as aggregate
                if (curr.children[0] != null && curr.children[1] != null)
                {
                    curr.IsAggregate = (curr.children[0].IsLeaf || curr.children[0].IsAggregate) &&
                        (curr.children[1].IsLeaf || curr.children[1].IsAggregate);
                }
                else
                {
                    curr.IsAggregate = false; // Mark as not aggregate
                }

                curr = curr.Parent;
            }

        }

        private static void RemoveChildIfNoGrandChildren(TrieNode curr, List<IPPrefix> withdrawnPrefixes)
        {
            for (int i = 0; i < 2; i++)
            {
                // If the child is not null and it is not a leaf, and it has no children, we can remove it.
                if (curr.children[i] != null && curr.children[i].IsLeaf == false && curr.children[i].children[0] == null && curr.children[i].children[1] == null)
                {
                    // If this child was exported, we need to add it to the withdrawn prefixes as we are removing it.
                    if (curr.children[i].IsExported)
                    {
                        withdrawnPrefixes.Add(curr.children[i].Prefix);
                    }

                    curr.children[i] = null;
                }
            }
        }

        private void ReComputeAffectedNodes(List<IPPrefix> aggregatesAdded, List<IPPrefix> withdrawnPrefixes)
        {
            TrieNode root = this;

            // Now start going down the trie from root and add aggregates and withdrawn prefixes based on the current state of the trie.
            var stack = new Stack<Tuple<TrieNode, bool>>();
            stack.Push(new Tuple<TrieNode, bool>(root, root.IsAggregate));

            while (stack.Count > 0)
            {
                (TrieNode curr, bool isParentAggregate) = stack.Pop();

                // If the current node is marked as aggregate and not exported and none of its parent nodes are aggregates, we will mark it as exported.
                if (curr.IsAggregate && curr.IsExported == false && isParentAggregate == false)
                {
                    curr.IsExported = true; // Mark as exported
                    aggregatesAdded.Add(curr.Prefix);
                }

                // If the current node is an aggregate and exported, but its parent is also an aggregate, we will mark it as not exported.
                // it is not a top-level aggregate. (parent is already covering this subtree)
                if (curr.IsAggregate && curr.IsExported && isParentAggregate)
                {
                    curr.IsExported = false; // Mark as not exported
                    withdrawnPrefixes.Add(curr.Prefix);
                }

                // If the current node is not an aggregate and is exported, we will mark it as not exported.
                if (curr.IsAggregate == false && curr.IsExported)
                {
                    withdrawnPrefixes.Add(curr.Prefix);
                    curr.IsExported = false; // Mark as not exported
                }

                // We need to push the children to the stack only if there is a change in subtree.
                if (curr.IsSubTreeChanged)
                {
                    isParentAggregate = curr.IsAggregate || isParentAggregate; // Update the parent aggregate status to be passed down to children.

                    if (curr.children[0] != null)
                    {
                        stack.Push(new Tuple<TrieNode, bool>(curr.children[0], isParentAggregate));
                    }

                    if (curr.children[1] != null)
                    {
                        stack.Push(new Tuple<TrieNode, bool>(curr.children[1], isParentAggregate));
                    }

                    curr.IsSubTreeChanged = false; // Reset the flag for this node
                }
            }
        }

        public void CollectAggregates(List<IPPrefix> result)
        {
            if (this.IsAggregate || this.IsLeaf)
            {
                result.Add(this.Prefix);
                return; // stop descending
            }

            this.children[0]?.CollectAggregates(result);

            this.children[1]?.CollectAggregates(result);
        }

        public void ToDotGraphFile(string fileName)
        {
            var content = ToDotGraph();
            File.WriteAllText(fileName, content);
        }

        public string ToDotGraph()
        {
            var sb = new StringBuilder();
            sb.AppendLine("digraph IPAggregation {");
            sb.AppendLine("node [shape=box];");
            sb.AppendLine("edge [arrowhead=normal];");

            var stack = new Stack<TrieNode>();
            stack.Push(this);
            var visited = new HashSet<TrieNode>();
            sb.AppendLine($"\"{this.Prefix}\" [label=\"{this.Prefix.ToString()}\" rank=0];");
            visited.Add(this);
            while (stack.Count > 0)
            {
                TrieNode curr = stack.Pop();
                string nodeLabel = curr.Prefix.ToString();
                string color = "white";
                if (curr.IsLeaf)
                {
                    nodeLabel += " (L)";
                    color = "lightgreen";
                }
                else if (curr.IsAggregate)
                {
                    nodeLabel += " (A)";
                    color = "orange";
                }
                if (curr.IsExported)
                {
                    nodeLabel += " (E)";
                    color = "lightblue";
                }

                
                TrieNode parent = visited.Contains(curr.Parent) ? curr.Parent : this;
                sb.AppendLine($"\"{curr.Prefix}\" [label=\"{nodeLabel}\" style=filled fillcolor={color} rank={curr.Prefix.MaskLength}];");
                sb.AppendLine($"\"{parent.Prefix}\" -> \"{curr.Prefix}\";");
                visited.Add(curr);

                for (int i = 1; i >= 0; i--)
                {
                    if (curr.children[i] != null)
                    {
                        stack.Push(curr.children[i]);
                    }
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
            => this.Prefix.GetHashCode();
    }
}

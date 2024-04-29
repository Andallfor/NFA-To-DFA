using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace nfaToDfa {
    public class Program {
        public static bool debug = false;
        public static void Main(string[] args) {
            Node nfa = construct();

            test(nfa, "aaaaaaaa"); // pass
            test(nfa, "b"); // fail
            test(nfa, "a"); // pass
            test(nfa, "bba"); // pass
            test(nfa, "baaaaba"); // pass
            test(nfa, "bbab"); // fail

            Node dfa = toDfa(nfa);

            Console.WriteLine("=====");

            test(dfa, "aaaaaaaa"); // pass
            test(dfa, "b"); // fail
            test(dfa, "a"); // pass
            test(dfa, "bba"); // pass
            test(dfa, "baaaaba"); // pass
            test(dfa, "bbab"); // fail  

            prettyPrint(nfa, "NFA");
            prettyPrint(dfa, "DFA");
        }

        public static void prettyPrint(Node head, string name) {
            List<Node> flattened = flatten(head);
            Console.WriteLine("\n\n" + name + "\n=====");
            Console.WriteLine("Total of " + flattened.Count + " states");

            Dictionary<Node, int> nts = new();
            for (int i = 0; i < flattened.Count; i++) nts.Add(flattened[i], i);
            
            int stateId = 0;
            Console.Write("(Entry) ");
            foreach (Node n in flattened) {
                if (n.isAccept) Console.Write("(Accept) ");
                if (n.to.Count == 0) Console.WriteLine("State " + stateId + " is void");    
                else {
                    StringBuilder sb = new StringBuilder();
                    foreach (Path p in n.to) sb.Append($"[{nts[p.to]} {p.flag}] ");
                    Console.WriteLine("State " + stateId + " connects to " + sb.ToString());
                }

                stateId++;
            }
        }

        public static Node toDfa(Node nfa) {
            Node empty = new Node();
            List<Node> flattened = flatten(nfa);
            Dictionary<NodeSequenceHash, NodeSequence> nodeSequences = new();

            int srcHashOffset = nfa.id;

            List<List<Node>> space = combinations(flattened).Skip(1).ToList(); // skip first element (null/no node)
            foreach (List<Node> sequence in space) {
                NodeSequence ns = new NodeSequence(sequence, srcHashOffset);
                nodeSequences.Add(ns.hash, ns);
            }

            // detect what characters are actually being used
            HashSet<Symbol> symbols = new();
            foreach (Node n in flattened) {
                foreach (Path p in n.to) {
                    if (p.flag != Symbol.epsilon) symbols.Add(p.flag);
                }
            }

            // get the new connections for each node
            foreach (NodeSequence ns in nodeSequences.Values) {
                // get each where each path connects to
                foreach (Symbol symbol in symbols) {
                    List<Node> accessible = new();
                    foreach (Node n in ns.nodes) accessible.AddRange(findAllConnecting(n, symbol));

                    if (accessible.Count == 0) ns.representation.registerTo(symbol, empty);
                    else {
                        NodeSequenceHash h = new NodeSequenceHash(accessible, srcHashOffset);
                        ns.representation.registerTo(symbol, nodeSequences[h].representation);
                    }
                }
            }

            // find the head
            List<Node> nodes = findAllConnecting(nfa, Symbol.epsilon);
            nodes.Add(nfa);
            NodeSequenceHash headHash = new NodeSequenceHash(nodes, srcHashOffset);
            Node head = nodeSequences[headHash].representation;

            return head;
        }

        public static IEnumerable<List<Node>> combinations(List<Node> flattened) {
            // https://stackoverflow.com/questions/64998630/get-all-combinations-of-liststring-where-order-doesnt-matter-and-minimum-of-2
            for (var i = 0; i < (1 << flattened.Count); i++) {
                yield return flattened.Where((t, j) => (i & (1 << j)) != 0).ToList();
            }
        }

        public static List<Node> findAllConnecting(Node n, Symbol s) {
            List<Node> accessible = new();

            foreach (Path p in n.to) {  // TODO: possible infinite loop
                if (p.flag == s) accessible.Add(p.to); // add next
                if (p.flag == Symbol.epsilon) accessible.AddRange(findAllConnecting(p.to, s)); // add query to frontier
            }

            return accessible;
        }

        public static List<Node> flatten(Node entry) {
            HashSet<Node> nodes = new();
            HashSet<Path> paths = new();

            Queue<Node> frontier = new();
            frontier.Enqueue(entry);

            while (frontier.Count > 0) {
                Node n = frontier.Dequeue();
                nodes.Add(n);

                foreach (Path p in n.to) {
                    if (!paths.Contains(p)) {
                        frontier.Enqueue(p.to);
                        paths.Add(p);
                    }
                }
            }

            // first element in list is head
            return nodes.ToList();
        }

        public static bool test(Node start, string s) {
            bool valid = test(start, processString(s));
            Console.WriteLine($"{s} is {(valid ? "valid" : "invalid")}");
            return valid;
        }

        public static bool test(Node start, List<Symbol> l) {
            // dont do recursion because we want breadth first approach (in case of nfa)
            Queue<State> frontier = new();
            frontier.Enqueue(new State(start, l));

            int i = 0;
            while (true) {
                if (frontier.Count == 0) return false;

                State state = frontier.Dequeue();

                if (state.l.Count == 0) {
                    if (state.n.isAccept) return true;
                    continue;
                }

                Symbol s = state.get();
                List<Symbol> next = state.process();

                foreach (Path p in state.n.to) {
                    if (p.flag == Symbol.epsilon || p.flag == s) {  // TODO: possible infinite loop
                        List<Symbol> n = (p.flag == Symbol.epsilon) ? state.l : next;
                        frontier.Enqueue(new State(p.to, n));
                        if (debug) Console.WriteLine(i + " Connecting to from " + p.from.id + " to " + p.to.id);
                    }
                }

                i++;
            }
        }

        public static List<Symbol> processString(string s) {
            List<Symbol> l = new();
            Dictionary<char, Symbol> key = new Dictionary<char, Symbol>() {
                {'a', Symbol.a},
                {'b', Symbol.b},
                {'c', Symbol.c},
                {'d', Symbol.d},
                {'e', Symbol.epsilon},
            };

            foreach (char c in s) l.Add(key[c]);
            return l;
        }

        public static Node construct() {
            /*
            Node one = new Node(true);
            Node two = new Node();
            Node three = new Node();

            one.registerTo(Symbol.b, two);
            one.registerTo(Symbol.epsilon, three);

            two.registerTo(Symbol.a, two);
            two.registerTo(Symbol.a, three);
            two.registerTo(Symbol.b, three);

            three.registerTo(Symbol.a, one);

            return one;
            */

            // third from last char is "a"
            Node zero = new Node();
            Node one = new Node();
            Node two = new Node();
            Node three = new Node(true);

            zero.registerTo(Symbol.a, zero);
            zero.registerTo(Symbol.b, zero);
            zero.registerTo(Symbol.a, one);

            one.registerTo(Symbol.a, two);
            one.registerTo(Symbol.b, two);

            two.registerTo(Symbol.a, three);
            two.registerTo(Symbol.b, three);

            return zero;
        }
    }

    public class Node { // tree but no
        public List<Path> to = new();
        public List<Path> from = new();
        public readonly int id;
        public readonly bool isAccept;
        private static int idIndex = 1;

        public Node(bool isAccept = false) {
            this.id = idIndex++;
            this.isAccept = isAccept;
        }

        public void registerTo(Symbol flag, Node n) { 
            Path p = new Path(this, n, flag);
            to.Add(p); 
            n.from.Add(p);
        }

        public override int GetHashCode() => id;
        public override bool Equals(object? obj) {
            if (obj is Node) return ((Node) obj).GetHashCode() == GetHashCode();
            return false;
        }
    }

    public struct Path {
        public Symbol flag;
        public Node from;
        public Node to;
        public Path(Node from, Node to, Symbol flag) {
            this.from = from;
            this.to = to;
            this.flag = flag;
        }

        public override int GetHashCode() => ((byte) flag << 16) | (from.GetHashCode() << 8) | to.GetHashCode();
        public override bool Equals([NotNullWhen(true)] object? obj) {
            if (obj is Path) return ((Path) obj).GetHashCode() == GetHashCode();
            return false;
        }
    }

    public struct State {
        public Node n;
        public List<Symbol> l;

        public State(Node n, List<Symbol> l) {
            this.n = n;
            this.l = new(); // deep copy value
            foreach (Symbol s in l) this.l.Add(s);
        }

        public Symbol get() => l[0];
        public List<Symbol> process() => l.Skip(1).ToList();

        public override int GetHashCode() => n.GetHashCode() ^ l.GetHashCode();
        public override bool Equals([NotNullWhen(true)] object? obj) {
            if (obj is State) return ((State) obj).GetHashCode() == GetHashCode();
            return false;
        }
    }

    public class NodeSequence {
        public List<Node> nodes;
        public Node representation;
        public bool isAccept;
        public NodeSequenceHash hash;

        private int hashOffset;

        public NodeSequence(List<Node> nodes, int hashOffset) {
            this.nodes = nodes;
            isAccept = nodes.Any((n) => n.isAccept);
            this.hashOffset = hashOffset;

            this.representation = new Node(isAccept);
            this.hash = new NodeSequenceHash(nodes, hashOffset);
        }

        public List<Path> getTo() => getPath(true);
        public List<Path> getFrom() => getPath(false);

        public List<Path> getPath(bool to) {
            List<Path> p = new();
            foreach (Node n in nodes) p.AddRange(to ? n.to : n.from);

            return p;
        }

        public override int GetHashCode() => hash.GetHashCode();
        public override bool Equals(object? obj) {
            if (obj is NodeSequenceHash) return obj.GetHashCode() == GetHashCode();
            return false;
        }
    }

    public struct NodeSequenceHash {
        public int hashOffset;
        public List<Node> nodes;

        public NodeSequenceHash(List<Node> nodes, int hashOffset) {
            this.nodes = nodes;
            this.hashOffset = hashOffset;
        }

        public override int GetHashCode() {
            // note that this only supports up to 32 unique nodes at a time and expects the nodes to be passed in order
            int h = 0;
            foreach (Node n in nodes) h |= 1 << (n.id - hashOffset);

            return h;
        }

        public override bool Equals([NotNullWhen(true)] object? obj) {
            if (obj is NodeSequenceHash) return obj.GetHashCode() == GetHashCode();
            return false;
        }
    }


    public enum Symbol : byte { // NOTE: does not support bit flags bc effort bad
        epsilon, a, b, c, d
    }
}
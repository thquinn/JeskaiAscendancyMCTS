using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    using ChanceEvent = ValueTuple<int, float>;
    using MCTSChild = ValueTuple<int, MCTSNode>;

    // TODO: If a node has only one child (or if it's a chance node that ends in a guaranteed win or loss), collapse it.

    public class MCTS {
        static float MIN_EXPANSION_PROBABILITY = 0;

        MCTSChoiceNode rootNode;
        State rootState;
        float[] rewards;

        public MCTS(State rootState, float[] rewards) {
            rootNode = new MCTSChoiceNode(null, rootState.GetMoves());
            this.rootState = rootState;
            this.rewards = rewards;
        }

        public void Rollout(int n) {
            while (rootNode.rollouts < n) {
                Rollout();
            }
        }
        public void Rollout() {
            Console.Clear();
            // Selection.
            MCTSNode current = rootNode;
            State state = new State(rootState);
            int eventID = 0;
            float probability = 1;
            while (probability >= MIN_EXPANSION_PROBABILITY) {
                Console.WriteLine(state);
                if (eventID == 0) {
                    MCTSChild child = current.GetChild();
                    if (child.Item2 == null) break;
                    Console.WriteLine(child.Item1);
                    Console.WriteLine(state.MoveToString(child.Item1));
                    ChanceEvent chanceEvent = state.ExecuteMove(child.Item1);
                    eventID = chanceEvent.Item1;
                    probability *= chanceEvent.Item2;
                    current = child.Item2;
                } else {
                    MCTSChild child = current.GetChild(eventID, state);
                    eventID = 0;
                }
            }
            // Expansion.
            current = current.Expand(state);
            // Simulation.
            while (!state.IsWon() && !state.IsLost()) {
                int[] moves = state.GetMoves();
                int i;
                lock (Program.random) {
                    i = Program.random.Next(moves.Length);
                }
                state.ExecuteMove(moves[i]);
                if (state.turn > rewards.Length) break;
            }
            float reward = state.IsWon() ? rewards[state.turn] : 0;
            // Backpropagation.
            while (current != null) {
                current.IncrementReward(reward);
                current = current.parent;
            }
        }
    }

    public class MCTSChoiceNode : MCTSNode {
        static float EXPLORATION = (float)Math.Sqrt(2);

        int[] moves;
        MCTSNode[] children;
        int expandedChildrenCount;

        public MCTSChoiceNode(MCTSNode parent, int[] moves) {
            this.parent = parent;
            this.moves = moves;
            children = new MCTSNode[moves.Length].Shuffle();
        }

        public override MCTSChild GetChild() {
            if (expandedChildrenCount < children.Length) return new MCTSChild();
            double highestUCT = double.MinValue;
            int highestIndex = -1;
            double lnSimulations = Math.Log(rollouts);
            for (int i = 0; i < expandedChildrenCount; i++) {
                MCTSNode child = children[i];
                double uct = child.totalReward / (double)child.rollouts + EXPLORATION * Math.Sqrt(lnSimulations / child.rollouts);
                if (uct > highestUCT) {
                    highestUCT = uct;
                    highestIndex = i;
                }
            }
            return new MCTSChild(moves[highestIndex], children[highestIndex]);
        }
        public override MCTSChild GetChild(int eventID, State state) {
            throw new Exception("Called GetChild(eventID, state) on a choice node.");
        }

        public override MCTSNode Expand(State state) {
            if (children.Length == 0) return this;
            ChanceEvent chanceEvent = state.ExecuteMove(moves[expandedChildrenCount]);
            MCTSNode child;
            if (chanceEvent.Item1 == 0) {
                Debug.Assert(chanceEvent.Item2 == 1, "No eventID on a < 1 probability event.");
                child = new MCTSChoiceNode(this, state.GetMoves());
                children[expandedChildrenCount++] = child;
            } else {
                child = new MCTSChanceNode(this);
                children[expandedChildrenCount++] = child;
            }
            return child;
        }
    }
    public class MCTSChanceNode : MCTSNode {
        SortedList<int, MCTSNode> children;

        public MCTSChanceNode(MCTSNode parent) {
            this.parent = parent;
            children = new SortedList<int, MCTSNode>();
        }

        // TODO: If one child is a loss, the whole node is guaranteed to be a loss. The only way to lose is to deck out or take too many turns, and these are deterministic.
        // The same goes for a win, but losses take precedent.
        
        public override MCTSChild GetChild(int eventID, State state) {
            MCTSNode child;
            if (children.TryGetValue(eventID, out child)) {
                return new MCTSChild(0, child);
            }
            child = new MCTSChoiceNode(this, state.GetMoves());
            children.Add(eventID, child);
            return new MCTSChild(0, child);
        }
        public override MCTSChild GetChild() {
            throw new Exception("Called GetChild() on a chance node.");
        }
        public override MCTSNode Expand(State state) {
            throw new Exception("Called Expand() on a chance node.");
        }
    }
    public abstract class MCTSNode {
        public MCTSNode parent;
        public float totalReward;
        public int rollouts;

        public abstract MCTSChild GetChild();
        public abstract MCTSChild GetChild(int eventID, State state);
        public abstract MCTSNode Expand(State state);
        public void IncrementReward(float r) {
            totalReward += r;
            rollouts++;
        }
    }
}

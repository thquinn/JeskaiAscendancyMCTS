using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    using ChanceEvent = ValueTuple<int, float>;
    using MCTSChild = ValueTuple<int, MCTSNode>;

    public class MCTS {
        static float MIN_EXPANSION_PROBABILITY = 0;

        MCTSChoiceNode rootNode;
        public State rootState;
        float[] rewards;

        public MCTS(State rootState, float[] rewards) {
            rootNode = new MCTSChoiceNode(null, rootState.GetMoves());
            this.rootState = rootState;
            this.rewards = rewards;
        }

        public void Rollout(int n) {
            while (rootNode.rollouts < n) {
                Rollout();
                if (rootNode.moves.Length == 1) {
                    // SIMPLIFICATION: Preventing rollouts in one-move situations will cause us to always mulligan no-land, no-Gitaxian-Probe hands. That's probably fine.
                    return;
                }
            }
        }
        public void Rollout() {
            // Selection.
            MCTSNode current = rootNode;
            State state = new State(rootState);
            float probability = 1;
            // Experiments have indicated that this is best at 0. Surprising, but who am I to tell the machine otherwise?
            while (probability >= MIN_EXPANSION_PROBABILITY) {
                MCTSChild child = current.GetChild();
                if (child.Item2 == null) break;
                current = child.Item2;
                ChanceEvent chanceEvent = state.ExecuteMove(child.Item1);
                // Choice node.
                if (chanceEvent.Item1 == 0) continue;
                // Chance node.
                probability *= chanceEvent.Item2;
                child = current.GetChild(chanceEvent.Item1, state);
                current = child.Item2;
            }
            // Expansion.
            if (probability >= MIN_EXPANSION_PROBABILITY) {
                current = current.Expand(state);
            }
            // Simulation.
            while (!state.IsWon() && !state.IsLost()) {
                int[] moves = state.GetMoves();
                int i;
                if (moves.Length == 1) i = 0;
                else if (moves[0] == State.SPECIAL_MOVE_END_TURN) i = StaticRandom.Next(1, moves.Length); // Avoid ending the turn in simulations.
                else i = StaticRandom.Next(moves.Length);
                state.ExecuteMove(moves[i]);
                if (state.turn >= rewards.Length) {
                    break;
                }
            }
            Debug.Assert(state.IsWon() || state.IsLost() || state.turn >= rewards.Length, "Simulation ended prematurely.");
            float reward = state.IsWon() ? rewards[state.turn] : 0;
            // Backpropagation.
            while (current != null) {
                current.IncrementReward(reward);
                current = current.parent;
            }
        }
        public float ExpectedRewardOfBestChild() {
            (int, MCTSNode) best = rootNode.GetBestChild();
            return best.Item2.totalReward / best.Item2.rollouts;
        }
        public void Advance(List<string> moveStrings = null) {
            // Replace the current root node with the best choice node child.
            (int, MCTSNode) best = rootNode.GetBestChild();
            if (moveStrings != null) {
                moveStrings.Add(rootState.MoveToString(best.Item1));
            }
            ChanceEvent chanceEvent = rootState.ExecuteMove(best.Item1);
            rootNode = (chanceEvent.Item1 == 0 ? best.Item2 : best.Item2.GetChild(chanceEvent.Item1, rootState).Item2) as MCTSChoiceNode;
            // While the new root node has only one move, make it.
            while (rootNode.moves.Length == 1) {
                if (moveStrings != null) {
                    moveStrings.Add(rootState.MoveToString(rootNode.moves[0]));
                }
                rootState.ExecuteMove(rootNode.moves[0]);
                // We could go through the proper chance node and find our subtree, but it's not likely to have many rollouts. Let's just start fresh. Whatever.
                rootNode = new MCTSChoiceNode(null, rootState.GetMoves());
            }
        }
    }

    public class MCTSChoiceNode : MCTSNode {
        public readonly static double EXPLORATION = .85;

        public int[] moves;
        MCTSNode[] children;
        int expandedChildrenCount;

        public MCTSChoiceNode(MCTSNode parent, int[] moves) {
            this.parent = parent;
            this.moves = moves.Shuffle();
            children = new MCTSNode[moves.Length];
        }

        public override MCTSChild GetChild() {
            if (children.Length == 0 || expandedChildrenCount < children.Length) return new MCTSChild();
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

        public (int, MCTSNode) GetBestChild() {
            int mostRollouts = -1;
            int mostIndex = -1;
            for (int i = 0; i < expandedChildrenCount; i++) {
                if (children[i].rollouts > mostRollouts) {
                    mostRollouts = children[i].rollouts;
                    mostIndex = i;
                }
            }
            return (moves[mostIndex], children[mostIndex]);
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

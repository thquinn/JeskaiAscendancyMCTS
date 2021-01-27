using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    public static class Simulation {
        public static float[] REWARDS = new float[] { 0, 0, 0, 0, 1, .75f, .5f, .25f };
        static int MULLIGAN_ROLLOUTS = 10000;
        // These thresholds are tied to the number of mulligan rollouts, the reward table, and the decklist. If any change, the thresholds should be updated.
        static Dictionary<int, float[]> MULLIGAN_THRESHOLDS = new Dictionary<int, float[]>() {
            { 10000, new float[] { -1, -1, -1, -1, -1, // no mulling to 3 or less
                                    0.06825269f,    // average 4-card best-child expected reward
                                    0.1398965f,     // average 5-card best-child expected reward
                                    0.1779792f } }, // average 6-card best-child expected reward
            { 100000, new float[] { -1, -1, -1, -1, -1, // no mulling to 3 or less
                                    0.07973079f,    // average 4-card best-child expected reward
                                    0.1521534f,     // average 5-card best-child expected reward
                                    0.1982474f } }, // average 6-card best-child expected reward
        };
        // At times, we seem to get a better average when we keep hands that are slightly worse than the average mulligan,
        // likely because the expected-reward-of-best-child metric is unreliable and it's better to not make close
        // mulligan decisions based on a relatively small # of rollouts. Plus, fewer mulligans means faster games.
        // This seems to vary greatly depending on both the number of mulligan and game rollouts.
        // With the current 1K/10K config, it seems like acting according to expected values is optimal (which we like!)
        static float MULLIGAN_AVERSION = 1.0f;

        public static int RunGame(Dictionary<Card, int> decklist, int rollouts, bool toConsole = false) {
            MCTS mcts = null;
            State state = null;
            for (int n = 7; n > 0; n--) {
                state = new State(decklist, n);
                mcts = new MCTS(state, REWARDS);
                // Mulligan decision.
                // TODO: More advanced mulligan logic.
                if (toConsole) Console.WriteLine("Opening hand:\n" + state);
                float threshold = MULLIGAN_THRESHOLDS[MULLIGAN_ROLLOUTS][n];
                if (threshold == -1) {
                    if (toConsole) Console.WriteLine("No known threshold, keep by default.");
                    break;
                }
                threshold *= MULLIGAN_AVERSION;
                mcts.Rollout(MULLIGAN_ROLLOUTS);
                if (mcts.ExpectedRewardOfBestChild() >= threshold) {
                    if (toConsole) Console.WriteLine("Keep.");
                    if (toConsole) Console.WriteLine("Move: " + mcts.rootState.MoveToString(mcts.GetBestMove()));
                    mcts.Advance();
                    if (toConsole) Console.WriteLine(mcts.rootState);
                    break;
                }
                if (toConsole) Console.WriteLine("Took a mulligan to {0}.", n - 1);
            }
            // Play the game.
            while (!mcts.rootState.IsWon() && !mcts.rootState.IsLost() && mcts.rootState.turn < REWARDS.Length) {
                mcts.Rollout(rollouts);
                if (toConsole) Console.WriteLine("Move: " + mcts.rootState.MoveToString(mcts.GetBestMove()));
                mcts.Advance();
                if (toConsole) Console.WriteLine(mcts.rootState);
                mcts.rootState.SanityCheck();
            }
            if (state.IsWon()) {
                if (toConsole) Console.WriteLine("Won on turn " + mcts.rootState.turn);
                return mcts.rootState.turn;
            } else {
                if (toConsole) Console.WriteLine("Failed to win before turn 8.");
                return -1;
            }
        }

        public static float RunMulligan(Dictionary<Card, int> decklist, int n) {
            State state = new State(decklist, n);
            MCTS mcts = new MCTS(state, REWARDS);
            mcts.Rollout(MULLIGAN_ROLLOUTS);
            return mcts.ExpectedRewardOfBestChild();
        }
    }
}

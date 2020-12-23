using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    public static class Simulation {
        public static float[] REWARDS = new float[] { 0, 0, 0, 0, 1, .75f, .5f, .25f };
        static int MULLIGAN_ROLLOUTS = 100000;
        // These thresholds are tied to the reward table, the decklist, and the number of mulligan rollouts. If any change, the thresholds should be updated.
        static float[] MULLIGAN_THRESHOLDS = new float[] { -1, -1, -1, -1, -1,
                                                          0.07973079f,  // average 4-card best-child expected reward
                                                          0.1521534f,   // average 5-card best-child expected reward
                                                          0.1982474f }; // average 6-card best-child expected reward
        // We seem to get a better average when we keep hands that are slightly worse than the average mulligan,
        // likely because the expected-reward-of-best-child metric is unreliable and it's better to not make close
        // mulligan decisions based on a bad 100K rollouts.
        static float MULLIGAN_AVERSION = 1;

        public static int RunGame(Dictionary<Card, int> decklist, int rollouts) {
            MCTS mcts = null;
            State state = null;
            for (int n = 7; n > 0; n--) {
                state = new State(decklist, n);
                mcts = new MCTS(state, REWARDS);
                // Mulligan decision.
                // TODO: More advanced mulligan logic.
                float threshold = MULLIGAN_THRESHOLDS[n];
                if (threshold == -1) {
                    break;
                }
                threshold *= MULLIGAN_AVERSION;
                mcts.Rollout(MULLIGAN_ROLLOUTS);
                if (mcts.ExpectedRewardOfBestChild() >= threshold) {
                    break;
                }
                Console.WriteLine("Took a mulligan to {0}.", n - 1);
            }
            // Play the game.
            while (!mcts.rootState.IsWon() && !mcts.rootState.IsLost() && mcts.rootState.turn < REWARDS.Length) {
                mcts.Rollout(rollouts);
                mcts.Advance();
                mcts.rootState.SanityCheck();
            }
            if (state.IsWon()) {
                return mcts.rootState.turn;
            } else {
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

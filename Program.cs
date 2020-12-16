using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    class Program {
        static void Main(string[] args) {
            Tuple<bool> l = new Tuple<bool>(false);
            float totalReward = 0;
            int winTurnTotal = 0;
            int wins = 0;
            int trials = 0;
            float[] rewards = new float[] { 0, 0, 0, 0, 1, .9f, .75f, .5f, .4f, .3f, .2f, .1f };

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Parallel.For(0, 1000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => {
                int turns = RunGame();
                lock (l) {
                    trials++;
                    if (turns > 0) {
                        totalReward += rewards[turns];
                        winTurnTotal += turns;
                        wins++;
                    } else {
                    }
                    Console.WriteLine("Average reward: {1} over {2} trials. Win rate before turn {3}: {4}%. Average win turn: {5}.", 0, (totalReward / trials).ToString("N2"), trials, rewards.Length, ((float)wins / trials * 100).ToString("N1"), (winTurnTotal / (float)wins).ToString("N2"));
                }
            });

            stopwatch.Stop();
            Console.WriteLine("Done in {0} ms.", stopwatch.ElapsedMilliseconds);
            Console.WriteLine("Average reward: {1} over {2} trials. Win rate before turn {3}: {4}%. Average win turn: {5}.", 0, (totalReward / trials).ToString("N2"), trials, rewards.Length, ((float)wins / trials * 100).ToString("N1"), (winTurnTotal / (float)wins).ToString("N2"));
            Console.ReadLine();
        }

        static int RunGame() {
            float[] rewards = new float[] { 0, 0, 0, 0, 1, .9f, .75f, .5f, .4f, .3f, .2f, .1f };

            State state = new State(new Dictionary<Card, int>() {
                { Card.Plains, 1 },
                { Card.Island, 12 },
                { Card.Mountain, 1 },
                { Card.MysticMonastery, 4 },
                { Card.EvolvingWilds, 4 },
                { Card.Brainstorm, 4 },
                { Card.CeruleanWisps, 2 },
                { Card.Fatestitcher, 4 },
                { Card.FranticInventory, 4 },
                { Card.FranticSearch, 4 },
                { Card.GitaxianProbe, 4 },
                { Card.JeskaiAscendancy, 4 },
                { Card.ObsessiveSearch, 2 },
                { Card.Opt, 4 },
                { Card.Ponder, 4 },
                { Card.TreasureCruise, 2 },
            }, 7);
            MCTS mcts = new MCTS(state, rewards);
            while (!mcts.rootState.IsWon() && !mcts.rootState.IsLost() && mcts.rootState.turn < rewards.Length) {
                mcts.Rollout(10000);
                mcts.Advance();
            }
            if (state.IsWon()) {
                return state.turn;
            } else {
                return -1;
            }
        }
    }
}
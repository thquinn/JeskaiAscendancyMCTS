﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    public static class Util {
        public static int CombineMultiplicativeIndices(int x, int y, int b) {
            if (y > x) {
                int temp = x;
                x = y;
                y = temp;
            }
            int yTemp = y;
            while (yTemp > 0) {
                yTemp /= b;
                x *= b;
            }
            return x + y;
        }
    }

    public static class ArrayExtensions {
        public static T[] Shuffle<T>(this T[] array) {
            int n = array.Length;
            for (int i = 0; i < n; i++) {
                int r = i + StaticRandom.Next(n - i);
                T t = array[r];
                array[r] = array[i];
                array[i] = t;
            }
            return array;
        }
    }
}
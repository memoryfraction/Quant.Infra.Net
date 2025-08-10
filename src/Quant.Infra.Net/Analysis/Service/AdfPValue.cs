using System;

namespace Quant.Infra.Net.Analysis.Service
{
    public static class AdfPValue
    {
        /// <summary>
        /// Wrapper for compatibility with previous name.
        /// regression: "c" (constant), "nc" (no constant), "ct" (constant+trend), "ctt" (constant+trend+trend^2)
        /// N: number of series believed I(1). For ADF use N=1.
        /// </summary>
        public static double ApproximateAdfPValue(double adfStat, string regression = "c", int N = 1)
        {
            return MackinnonPValue(adfStat, regression, N);
        }

        /// <summary>
        /// Port of statsmodels.tsa.adfvalues.mackinnonp
        /// Returns MacKinnon's approximate p-value for teststat.
        /// </summary>
        public static double MackinnonPValue(double teststat, string regression = "c", int N = 1)
        {
            // 先处理非法输入
            if (double.IsNaN(teststat) || double.IsInfinity(teststat))
            {
                return 1.0; // 返回p=1，表示不拒绝单位根原假设（非平稳）
            }

            // tables & scalings translated from statsmodels.tsa.adfvalues
            double[] tau_star_nc = { -1.04, -1.53, -2.68, -3.09, -3.07, -3.77 };
            double[] tau_min_nc = { -19.04, -19.62, -21.21, -23.25, -21.63, -25.74 };
            double[] tau_max_nc = { double.PositiveInfinity, 1.51, 0.86, 0.88, 1.05, 1.24 };

            double[] tau_star_c = { -1.61, -2.62, -3.13, -3.47, -3.78, -3.93 };
            double[] tau_min_c = { -18.83, -18.86, -23.48, -28.07, -25.96, -23.27 };
            double[] tau_max_c = { 2.74, 0.92, 0.55, 0.61, 0.79, 1.0 };

            double[] tau_star_ct = { -2.89, -3.19, -3.50, -3.65, -3.80, -4.36 };
            double[] tau_min_ct = { -16.18, -21.15, -25.37, -26.63, -26.53, -26.18 };
            double[] tau_max_ct = { 0.7, 0.63, 0.71, 0.93, 1.19, 1.42 };

            double[] tau_star_ctt = { -3.21, -3.51, -3.81, -3.83, -4.12, -4.63 };
            double[] tau_min_ctt = { -17.17, -21.1, -24.33, -24.03, -24.33, -28.22 };
            double[] tau_max_ctt = { 0.54, 0.79, 1.08, 1.43, 3.49, 1.92 };

            double[][] tau_nc_smallp = {
                new double[] {0.6344, 1.2378, 3.2496},
                new double[] {1.9129, 1.3857, 3.5322},
                new double[] {2.7648, 1.4502, 3.4186},
                new double[] {3.4336, 1.4835, 3.19},
                new double[] {4.0999, 1.5533, 3.59},
                new double[] {4.5388, 1.5344, 2.9807}
            };

            double[][] tau_c_smallp = {
                new double[] {2.1659, 1.4412, 3.8269},
                new double[] {2.92,   1.5012, 3.9796},
                new double[] {3.4699, 1.4856, 3.164},
                new double[] {3.9673, 1.4777, 2.6315},
                new double[] {4.5509, 1.5338, 2.9545},
                new double[] {5.1399, 1.6036, 3.4445}
            };

            double[][] tau_ct_smallp = {
                new double[] {3.2512, 1.6047, 4.9588},
                new double[] {3.6646, 1.5419, 3.6448},
                new double[] {4.0983, 1.5173, 2.9898},
                new double[] {4.5844, 1.5338, 2.8796},
                new double[] {5.0722, 1.5634, 2.9472},
                new double[] {5.53,   1.5914, 3.0392}
            };

            double[][] tau_ctt_smallp = {
                new double[] {4.0003, 1.658, 4.8288},
                new double[] {4.3534, 1.6016, 3.7947},
                new double[] {4.7343, 1.5768, 3.2396},
                new double[] {5.214,  1.6077, 3.3449},
                new double[] {5.6481, 1.6274, 3.3455},
                new double[] {5.9296, 1.5929, 2.8223}
            };

            double[][] tau_nc_largep = {
                new double[] {0.4797, 9.3557, -0.6999, 3.3066},
                new double[] {1.5578, 8.5580, -2.0830, -3.3549},
                new double[] {2.2268, 6.8093, -3.2362, -5.4448},
                new double[] {2.7654, 6.4502, -3.0811, -4.4946},
                new double[] {3.2684, 6.8051, -2.6778, -3.4972},
                new double[] {3.7268, 7.1670, -2.3648, -2.8288}
            };

            double[][] tau_c_largep = {
                new double[] {1.7339, 9.3202, -1.2745, -1.0368},
                new double[] {2.1945, 6.4695, -2.9198, -4.2377},
                new double[] {2.5893, 4.5168, -3.6529, -5.0074},
                new double[] {3.0387, 4.5452, -3.3666, -4.1921},
                new double[] {3.5049, 5.2098, -2.9158, -3.3468},
                new double[] {3.9489, 5.8933, -2.5359, -2.721}
            };

            double[][] tau_ct_largep = {
                new double[] {2.5261, 6.1654, -3.7956, -6.0285},
                new double[] {2.85,   5.2720, -3.6622, -5.1695},
                new double[] {3.221,  5.2550, -3.2685, -4.1501},
                new double[] {3.652,  5.9758, -2.7483, -3.2081},
                new double[] {4.0712, 6.6428, -2.3464, -2.5460},
                new double[] {4.4735, 7.1757, -2.0681, -2.1196}
            };

            double[][] tau_ctt_largep = {
                new double[] {3.0778, 4.9529, -4.1477, -5.9359},
                new double[] {3.4713, 5.9670, -3.2507, -4.2286},
                new double[] {3.8637, 6.7852, -2.6286, -3.1381},
                new double[] {4.2736, 7.6199, -2.1534, -2.4026},
                new double[] {4.6679, 8.2618, -1.8220, -1.9147},
                new double[] {5.0009, 8.3735, -1.6994, -1.6928}
            };

            double[] small_scaling = { 1.0, 1.0, 1e-2 };
            double[] large_scaling = { 1.0, 1e-1, 1e-1, 1e-2 };

            MultiplyColumnsInPlace(tau_nc_smallp, small_scaling);
            MultiplyColumnsInPlace(tau_c_smallp, small_scaling);
            MultiplyColumnsInPlace(tau_ct_smallp, small_scaling);
            MultiplyColumnsInPlace(tau_ctt_smallp, small_scaling);

            MultiplyColumnsInPlace(tau_nc_largep, large_scaling);
            MultiplyColumnsInPlace(tau_c_largep, large_scaling);
            MultiplyColumnsInPlace(tau_ct_largep, large_scaling);
            MultiplyColumnsInPlace(tau_ctt_largep, large_scaling);

            double[] maxstat, minstat, starstat;
            double[][] smallp, largep;
            switch (regression)
            {
                case "nc":
                    maxstat = tau_max_nc; minstat = tau_min_nc; starstat = tau_star_nc;
                    smallp = tau_nc_smallp; largep = tau_nc_largep;
                    break;
                case "c":
                    maxstat = tau_max_c; minstat = tau_min_c; starstat = tau_star_c;
                    smallp = tau_c_smallp; largep = tau_c_largep;
                    break;
                case "ct":
                    maxstat = tau_max_ct; minstat = tau_min_ct; starstat = tau_star_ct;
                    smallp = tau_ct_smallp; largep = tau_ct_largep;
                    break;
                case "ctt":
                    maxstat = tau_max_ctt; minstat = tau_min_ctt; starstat = tau_star_ctt;
                    smallp = tau_ctt_smallp; largep = tau_ctt_largep;
                    break;
                default:
                    throw new ArgumentException("Unsupported regression type: " + regression);
            }

            if (N < 1) N = 1;
            if (N > 6) N = 6;
            int idx = N - 1;

            if (teststat > maxstat[idx])
                return 1.0;
            if (teststat < minstat[idx])
                return 0.0;

            double[] tau_coef;
            if (teststat <= starstat[idx])
            {
                tau_coef = smallp[idx];
            }
            else
            {
                tau_coef = largep[idx];
            }

            double[] polyCoef = Reverse(tau_coef);
            double polyVal = PolyVal(polyCoef, teststat);

            // 防护 NaN 和 Infinity
            if (double.IsNaN(polyVal) || double.IsInfinity(polyVal))
            {
                return 1.0;
            }

            return NormalCdf(polyVal);
        }

        private static void MultiplyColumnsInPlace(double[][] matrix, double[] scaling)
        {
            if (matrix == null) return;
            int cols = scaling.Length;
            for (int i = 0; i < matrix.Length; i++)
            {
                for (int j = 0; j < cols && j < matrix[i].Length; j++)
                {
                    matrix[i][j] *= scaling[j];
                }
            }
        }

        private static double[] Reverse(double[] arr)
        {
            double[] r = new double[arr.Length];
            for (int i = 0; i < arr.Length; i++) r[i] = arr[arr.Length - 1 - i];
            return r;
        }

        private static double PolyVal(double[] polyCoef, double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
                return double.NaN;

            double result = 0.0;
            for (int i = 0; i < polyCoef.Length; i++)
            {
                result = result * x + polyCoef[i];
                if (double.IsNaN(result) || double.IsInfinity(result))
                    return double.NaN;
            }
            return result;
        }

        private static double NormalCdf(double z)
        {
            return 0.5 * (1.0 + Erf(z / Math.Sqrt(2.0)));
        }

        private static double Erf(double x)
        {
            double sign = Math.Sign(x);
            x = Math.Abs(x);

            double a1 = 0.254829592;
            double a2 = -0.284496736;
            double a3 = 1.421413741;
            double a4 = -1.453152027;
            double a5 = 1.061405429;
            double p = 0.3275911;

            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
            return sign * y;
        }
    }
}

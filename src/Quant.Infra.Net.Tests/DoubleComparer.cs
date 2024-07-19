namespace Quant.Infra.Net.Tests
{
    public class DoubleComparer : System.Collections.IComparer
    {
        private const double Tolerance = 1e-9;

        public int Compare(object x, object y)
        {
            double dx = (double)x;
            double dy = (double)y;

            if (Math.Abs(dx - dy) < Tolerance)
                return 0;
            return dx.CompareTo(dy);
        }
    }
}

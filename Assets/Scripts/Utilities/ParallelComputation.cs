using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace ModelViewer.Utilities
{
    public static class ParallelComputation
    {
        public static void ParallelFor<T>(IList<T> source, Action<T> body)
        {
            Parallel.For(0, source.Count, i => body(source[i]));
        }

        public static void ParallelFor<T>(IList<T> source, Action<T, int> body)
        {
            Parallel.For(0, source.Count, i => body(source[i], i));
        }

        public static List<TResult> ParallelSelect<TSource, TResult>(IList<TSource> source, Func<TSource, TResult> selector)
        {
            var results = new TResult[source.Count];
            Parallel.For(0, source.Count, i => results[i] = selector(source[i]));
            return new List<TResult>(results);
        }

        public static List<TResult> ParallelSelect<TSource, TResult>(IList<TSource> source, Func<TSource, int, TResult> selector)
        {
            var results = new TResult[source.Count];
            Parallel.For(0, source.Count, i => results[i] = selector(source[i], i));
            return new List<TResult>(results);
        }

        public static float ParallelSum(IList<float> source)
        {
            return source.AsParallel().Sum();
        }

        public static double ParallelSum(IList<double> source)
        {
            return source.AsParallel().Sum();
        }

        public static float ParallelAverage(IList<float> source)
        {
            return source.AsParallel().Average();
        }

        public static double ParallelAverage(IList<double> source)
        {
            return source.AsParallel().Average();
        }
    }
}
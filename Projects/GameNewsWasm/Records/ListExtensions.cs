using System;
using System.Collections.Generic;
using GameNewsWasm.Records;

namespace ListPaginated
{
    public static class ListExtensions
    {
        public static IEnumerable<List<GameRecord>> ChunkBy(this List<GameRecord> source, int chunkSize)
        {
            for (int i = 0; i < source.Count; i += chunkSize)
            {
                yield return source.GetRange(i, Math.Min(chunkSize, source.Count - i));
            }
        }
    }
}

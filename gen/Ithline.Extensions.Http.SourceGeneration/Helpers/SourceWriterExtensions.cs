namespace Ithline.Extensions.Http.SourceGeneration;

internal static class SourceWriterExtensions
{
    /// <summary>
    /// Starts a block of source code.
    /// </summary>
    /// <param name="source">Source to write after the open brace.</param>
    public static IDisposable EmitStartBlock(this SourceWriter writer, string? source = null)
    {
        if (source is not null)
        {
            writer.WriteLine(source);
        }

        writer.WriteLine("{");
        writer.Indentation++;

        return new BlockDisposable(writer);
    }

    private sealed class BlockDisposable : IDisposable
    {
        private SourceWriter? _writer;

        public BlockDisposable(SourceWriter writer)
        {
            _writer = writer;
        }

        public void Dispose()
        {
            var writer = Interlocked.Exchange(ref _writer, null);
            if (writer is not null)
            {
                writer.Indentation--;
                writer.WriteLine("}");
            }
        }
    }
}

using System.CodeDom.Compiler;

namespace Ithline.Extensions.Http.SourceGeneration;

internal sealed class CodeWriter : IndentedTextWriter
{
    public CodeWriter(StringWriter stringWriter, int baseIndent) : base(stringWriter)
    {
        Indent = baseIndent;
    }

    public void StartBlock()
    {
        this.WriteLine("{");
        Indent++;
    }

    public void EndBlock()
    {
        Indent--;
        this.WriteLine("}");
    }

    public void EndBlockWithComma()
    {
        Indent--;
        this.WriteLine("},");
    }

    public void EndBlockWithSemicolon()
    {
        Indent--;
        this.WriteLine("};");
    }

    // The IndentedTextWriter adds the indentation
    // _after_ writing the first line of text. This
    // method can be used ot initialize indentation
    // when an emit method might only emit one line
    // of code or when the code writer is emitting
    // indented code as part of a larger string.
    public void InitializeIndent()
    {
        for (var i = 0; i < Indent; i++)
        {
            this.Write(DefaultTabString);
        }
    }
}

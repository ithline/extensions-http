using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.Extensions.ObjectPool;

namespace Ithline.Extensions.Http;

/// <summary>
/// Internal API used by source generated code.
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedRouteHelper
{
    /// <summary>
    /// Internal API used by source generated code.
    /// </summary>
    public static readonly ObjectPool<StringBuilder> StringBuilderPool = ObjectPool.Create(new StringBuilderPooledObjectPolicy());

    /// <summary>
    /// Internal API used by source generated code.
    /// </summary>
    public static void EncodeValue(StringBuilder sb, object? value, bool lowercase, bool encodeSlashes)
    {
        if (value is null)
        {
            return;
        }

        var converted = Convert.ToString(value, CultureInfo.InvariantCulture);
        ReadOnlySpan<char> s = lowercase ? converted?.ToLowerInvariant() : converted;
        if (s.IsEmpty)
        {
            return;
        }

        OperationStatus status;
        var encoder = UrlEncoder.Default;
        Span<char> buffer = stackalloc char[256];

        int i;
        while (!encodeSlashes && (i = s.IndexOf('/')) >= 0)
        {
            var segment = s[0..i];
            do
            {
                status = encoder.Encode(segment, buffer, out var consumed, out var written, isFinalBlock: s.IsEmpty);

                segment = segment[consumed..];
                sb.Append(buffer[0..written]);
            }
            while (status != OperationStatus.Done);
            sb.Append('/');

            s = s.Slice(i + 1);
        }

        do
        {
            status = encoder.Encode(s, buffer, out var consumed, out var written, isFinalBlock: s.IsEmpty);

            s = s[consumed..];
            sb.Append(buffer[0..written]);
        }
        while (status != OperationStatus.Done);
    }
}

using System.Buffers.Binary;

namespace Xm6Control.Protocol;

public static class TandemFrameCodec
{
    public const byte Start = 0x3E;
    public const byte End = 0x3C;
    public const byte Escape = 0x3D;

    public static byte[] Encode(byte sequence, TandemDataType dataType, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > 16 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload is too large.");
        }

        var body = new byte[7 + payload.Length];
        body[0] = (byte)dataType;
        body[1] = sequence;
        BinaryPrimitives.WriteInt32BigEndian(body.AsSpan(2, 4), payload.Length);
        payload.CopyTo(body.AsSpan(6));

        var sum = 0;
        for (var index = 0; index < body.Length - 1; index++)
        {
            sum += body[index];
        }

        body[^1] = (byte)(sum & 0xff);
        var escaped = EscapeBytes(body);
        var frame = new byte[escaped.Length + 2];
        frame[0] = Start;
        escaped.CopyTo(frame.AsSpan(1));
        frame[^1] = End;
        return frame;
    }

    public static byte[] EncodeAck(byte sequence)
    {
        return
        [
            Start,
            (byte)TandemDataType.Ack,
            sequence,
            0,
            0,
            0,
            0,
            (byte)(((byte)TandemDataType.Ack + sequence) & 0xff),
            End,
        ];
    }

    public static bool TryDecode(ReadOnlySpan<byte> frame, out TandemFrame? result, out string? error)
    {
        result = null;
        error = null;

        if (frame.Length < 9)
        {
            error = "Frame is shorter than the minimum 9 bytes.";
            return false;
        }

        if (frame[0] != Start || frame[^1] != End)
        {
            error = "Frame must start with 0x3e and end with 0x3c.";
            return false;
        }

        if (!TryUnescape(frame[1..^1], out var body, out error))
        {
            return false;
        }

        if (body.Length < 7)
        {
            error = "Unescaped body is shorter than 7 bytes.";
            return false;
        }

        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(body.AsSpan(2, 4));
        if (payloadLength < 0 || payloadLength != body.Length - 7)
        {
            error = $"Length field {payloadLength} does not match body length {body.Length - 7}.";
            return false;
        }

        var sum = 0;
        for (var index = 0; index < body.Length - 1; index++)
        {
            sum += body[index];
        }

        var checksum = body[^1];
        if ((byte)(sum & 0xff) != checksum)
        {
            error = $"Checksum mismatch: expected 0x{(byte)(sum & 0xff):x2}, got 0x{checksum:x2}.";
            return false;
        }

        var payload = body.AsSpan(6, payloadLength).ToArray();
        result = new TandemFrame(
            body[1],
            (TandemDataType)body[0],
            payload,
            checksum);
        return true;
    }

    public static IReadOnlyList<TandemFrame> DecodeAll(ReadOnlySpan<byte> stream)
    {
        var frames = new List<TandemFrame>();
        var offset = 0;

        while (offset < stream.Length)
        {
            var start = stream[offset..].IndexOf(Start);
            if (start < 0)
            {
                break;
            }

            offset += start;
            var end = stream[offset..].IndexOf(End);
            if (end < 0)
            {
                break;
            }

            var candidate = stream.Slice(offset, end + 1);
            if (TryDecode(candidate, out var frame, out _) && frame is not null)
            {
                frames.Add(frame);
                offset += end + 1;
            }
            else
            {
                offset++;
            }
        }

        return frames;
    }

    private static byte[] EscapeBytes(ReadOnlySpan<byte> data)
    {
        var output = new List<byte>(data.Length + 8);
        foreach (var value in data)
        {
            switch (value)
            {
                case End:
                    output.Add(Escape);
                    output.Add(0x2C);
                    break;
                case Escape:
                    output.Add(Escape);
                    output.Add(0x2D);
                    break;
                case Start:
                    output.Add(Escape);
                    output.Add(0x2E);
                    break;
                default:
                    output.Add(value);
                    break;
            }
        }

        return output.ToArray();
    }

    private static bool TryUnescape(ReadOnlySpan<byte> data, out byte[] result, out string? error)
    {
        var output = new List<byte>(data.Length);
        error = null;

        for (var index = 0; index < data.Length; index++)
        {
            var value = data[index];
            if (value != Escape)
            {
                output.Add(value);
                continue;
            }

            if (index + 1 >= data.Length)
            {
                result = [];
                error = "Escape byte at the end of the frame.";
                return false;
            }

            var escaped = data[++index];
            if (escaped is not (0x2C or 0x2D or 0x2E))
            {
                result = [];
                error = $"Invalid escape sequence 0x3d 0x{escaped:x2}.";
                return false;
            }

            output.Add((byte)(escaped | 0x10));
        }

        result = output.ToArray();
        return true;
    }
}




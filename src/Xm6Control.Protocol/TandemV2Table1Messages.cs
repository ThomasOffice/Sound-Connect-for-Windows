using System.Buffers.Binary;
using System.Text;

namespace Xm6Control.Protocol;

public static class TandemV2Table1Messages
{
    public static byte[] ConnectGetProtocolInfo() =>
        [(byte)TandemV2Table1Command.ConnectGetProtocolInfo, (byte)TandemConnectInquiredType.FixedValue];

    public static byte[] ConnectGetCapabilityInfo() =>
        [(byte)TandemV2Table1Command.ConnectGetCapabilityInfo, (byte)TandemConnectInquiredType.FixedValue];

    public static byte[] ConnectGetDeviceInfo(TandemDeviceInfoType type) =>
        [(byte)TandemV2Table1Command.ConnectGetDeviceInfo, (byte)type];

    public static byte[] ConnectGetSupportFunction() =>
        [(byte)TandemV2Table1Command.ConnectGetSupportFunction, (byte)TandemConnectInquiredType.FixedValue];

    public static byte[] PowerGetCapability(TandemPowerInquiredType type) =>
        [(byte)TandemV2Table1Command.PowerGetCapability, (byte)type];

    public static byte[] PowerGetStatus(TandemPowerInquiredType type) =>
        [(byte)TandemV2Table1Command.PowerGetStatus, (byte)type];

    public static byte[] PowerGetParam(TandemPowerInquiredType type) =>
        [(byte)TandemV2Table1Command.PowerGetParam, (byte)type];

    public static byte[] PowerSetAutoPowerOff(
        TandemAutoPowerOffElement first,
        TandemAutoPowerOffElement second,
        TandemPowerInquiredType type = TandemPowerInquiredType.AutoPowerOff) =>
    [
        (byte)TandemV2Table1Command.PowerSetParam,
        (byte)type,
        (byte)first,
        (byte)second,
    ];

    public static byte[] PowerSetOnOff(TandemPowerInquiredType type, bool on) =>
    [
        (byte)TandemV2Table1Command.PowerSetParam,
        (byte)type,
        (byte)(on ? TandemOnOffSettingValue.On : TandemOnOffSettingValue.Off),
    ];

    public static byte[] AudioGetCapability(TandemAudioInquiredType type) =>
        [(byte)TandemV2Table1Command.AudioGetCapability, (byte)type];

    public static byte[] AudioGetParam(TandemAudioInquiredType type) =>
        [(byte)TandemV2Table1Command.AudioGetParam, (byte)type];

    public static byte[] AudioSetBgm(
        bool on,
        TandemRoomSize roomSize,
        TandemAudioInquiredType type = TandemAudioInquiredType.BgmModeAndErrorCode) =>
    [
        (byte)TandemV2Table1Command.AudioSetParam,
        (byte)type,
        (byte)(on ? TandemOnOffSettingValue.On : TandemOnOffSettingValue.Off),
        (byte)roomSize,
    ];

    public static byte[] AudioSetCinema(bool on) =>
    [
        (byte)TandemV2Table1Command.AudioSetParam,
        (byte)TandemAudioInquiredType.UpmixCinema,
        (byte)(on ? TandemOnOffSettingValue.On : TandemOnOffSettingValue.Off),
    ];

    public static byte[] AudioSetUpmixSeries(TandemUpmixItemId item) =>
    [
        (byte)TandemV2Table1Command.AudioSetParam,
        (byte)TandemAudioInquiredType.UpmixSeries,
        (byte)item,
    ];

    public static bool TryParseBgmState(
        ReadOnlySpan<byte> payload,
        out TandemBgmState state)
    {
        state = null!;

        var type = (TandemAudioInquiredType)payload[1];
        if (payload.Length != 4
            || (payload[0] != (byte)TandemV2Table1Command.AudioRetParam
                && payload[0] != (byte)TandemV2Table1Command.AudioNtfyParam)
            || (type != TandemAudioInquiredType.BgmMode
                && type != TandemAudioInquiredType.BgmModeAndErrorCode))
        {
            return false;
        }

        var onOff = (TandemOnOffSettingValue)payload[2];
        var roomSize = (TandemRoomSize)payload[3];
        if (onOff == TandemOnOffSettingValue.OutOfRange
            || roomSize == TandemRoomSize.OutOfRange)
        {
            return false;
        }

        state = new TandemBgmState(onOff, roomSize);
        return true;
    }

    public static bool TryParseCinemaState(
        ReadOnlySpan<byte> payload,
        out TandemCinemaState state)
    {
        state = null!;

        if (payload.Length != 3
            || (payload[0] != (byte)TandemV2Table1Command.AudioRetParam
                && payload[0] != (byte)TandemV2Table1Command.AudioNtfyParam)
            || payload[1] != (byte)TandemAudioInquiredType.UpmixCinema)
        {
            return false;
        }

        var onOff = (TandemOnOffSettingValue)payload[2];
        if (onOff == TandemOnOffSettingValue.OutOfRange)
        {
            return false;
        }

        state = new TandemCinemaState(onOff);
        return true;
    }

    public static bool TryParseUpmixSeriesState(
        ReadOnlySpan<byte> payload,
        out TandemUpmixItemId item)
    {
        item = TandemUpmixItemId.OutOfRange;

        if (payload.Length != 3
            || (payload[0] != (byte)TandemV2Table1Command.AudioRetParam
                && payload[0] != (byte)TandemV2Table1Command.AudioNtfyParam)
            || payload[1] != (byte)TandemAudioInquiredType.UpmixSeries)
        {
            return false;
        }

        item = (TandemUpmixItemId)payload[2];
        return item != TandemUpmixItemId.OutOfRange;
    }

    public static byte[] NcAsmGetCapability(TandemNcAsmInquiredType type) =>
        [(byte)TandemV2Table1Command.NcAsmGetCapability, (byte)type];

    public static byte[] NcAsmGetStatus(TandemNcAsmInquiredType type) =>
        [(byte)TandemV2Table1Command.NcAsmGetStatus, (byte)type];

    public static byte[] NcAsmGetParam(TandemNcAsmInquiredType type) =>
        [(byte)TandemV2Table1Command.NcAsmGetParam, (byte)type];

    public static byte[] NcAsmSetNcOnOff(bool on, bool? second = null)
    {
        var value = (byte)(on ? TandemNcAsmOnOffValue.On : TandemNcAsmOnOffValue.Off);
        var secondValue = (byte)((second ?? on) ? TandemNcAsmOnOffValue.On : TandemNcAsmOnOffValue.Off);
        return
        [
            (byte)TandemV2Table1Command.NcAsmSetParam,
            (byte)TandemNcAsmInquiredType.NcOnOff,
            (byte)TandemValueChangeStatus.Changed,
            value,
            secondValue,
        ];
    }

    public static byte[] EqEbbGetCapability(TandemEqEbbInquiredType type) =>
        [(byte)TandemV2Table1Command.EqEbbGetCapability, (byte)type];

    public static byte[] EqEbbGetStatus(TandemEqEbbInquiredType type) =>
        [(byte)TandemV2Table1Command.EqEbbGetStatus, (byte)type];

    public static byte[] EqEbbGetParam(TandemEqEbbInquiredType type) =>
        [(byte)TandemV2Table1Command.EqEbbGetParam, (byte)type];

    public static byte[] EqEbbSetPreset(
        TandemEqEbbInquiredType type,
        TandemEqPresetId preset,
        IReadOnlyList<byte> bands)
    {
        if (bands.Count > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(bands));
        }

        var payload = new byte[4 + bands.Count];
        payload[0] = (byte)TandemV2Table1Command.EqEbbSetParam;
        payload[1] = (byte)type;
        payload[2] = (byte)preset;
        payload[3] = (byte)bands.Count;
        for (var index = 0; index < bands.Count; index++)
        {
            payload[4 + index] = bands[index];
        }

        return payload;
    }

    public static byte[] EqEbbSetPresetOnly(
        TandemEqEbbInquiredType type,
        TandemEqPresetId preset) =>
    [
        (byte)TandemV2Table1Command.EqEbbSetParam,
        (byte)type,
        (byte)preset,
        0,
    ];

    public static byte[] EqEbbSetCustom(IReadOnlyList<byte> bands)
    {
        if (bands.Count > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(bands));
        }

        var payload = new byte[3 + bands.Count];
        payload[0] = (byte)TandemV2Table1Command.EqEbbSetParam;
        payload[1] = (byte)TandemEqEbbInquiredType.CustomEq;
        payload[2] = (byte)bands.Count;
        for (var index = 0; index < bands.Count; index++)
        {
            payload[3 + index] = bands[index];
        }

        return payload;
    }

    public static byte[] GeneralSettingGetCapability(TandemGsInquiredType type) =>
        [(byte)TandemV2Table1Command.GeneralSettingGetCapability, (byte)type];

    public static byte[] GeneralSettingGetStatus(TandemGsInquiredType type) =>
        [(byte)TandemV2Table1Command.GeneralSettingGetStatus, (byte)type];

    public static byte[] GeneralSettingGetParam(TandemGsInquiredType type) =>
        [(byte)TandemV2Table1Command.GeneralSettingGetParam, (byte)type];

    public static byte[] GeneralSettingSetBool(TandemGsInquiredType type, bool on) =>
    [
        (byte)TandemV2Table1Command.GeneralSettingSetParam,
        (byte)type,
        (byte)TandemGsSettingType.BooleanType,
        (byte)(on ? TandemGsSettingValue.On : TandemGsSettingValue.Off),
    ];

    public static byte[] GeneralSettingSetList(TandemGsInquiredType type, byte value) =>
    [
        (byte)TandemV2Table1Command.GeneralSettingSetParam,
        (byte)type,
        (byte)TandemGsSettingType.ListType,
        value,
    ];

    public static byte[] SystemGetStatus(TandemSystemInquiredType type) =>
        [(byte)TandemV2Table1Command.SystemGetStatus, (byte)type];

    public static byte[] SystemGetParam(TandemSystemInquiredType type) =>
        [(byte)TandemV2Table1Command.SystemGetParam, (byte)type];

    public static byte[] SystemSetStatus(TandemSystemInquiredType type, IReadOnlyList<byte> value) =>
        BuildWithValue(TandemV2Table1Command.SystemSetStatus, (byte)type, value);

    public static byte[] SystemSetParam(TandemSystemInquiredType type, IReadOnlyList<byte> value) =>
        BuildWithValue(TandemV2Table1Command.SystemSetParam, (byte)type, value);

    public static bool TryParseProtocolInfo(
        ReadOnlySpan<byte> payload,
        out int protocolVersion,
        out byte tableSetFlag,
        out byte secondFlag)
    {
        protocolVersion = 0;
        tableSetFlag = 0;
        secondFlag = 0;

        if (payload.Length != 8
            || payload[0] != (byte)TandemV2Table1Command.ConnectRetProtocolInfo
            || payload[1] != (byte)TandemConnectInquiredType.FixedValue)
        {
            return false;
        }

        protocolVersion = BinaryPrimitives.ReadInt32BigEndian(payload[2..6]);
        tableSetFlag = payload[6];
        secondFlag = payload[7];
        return true;
    }

    public static bool TryParseDeviceInfoText(
        ReadOnlySpan<byte> payload,
        out TandemDeviceInfoType type,
        out string text)
    {
        type = TandemDeviceInfoType.OutOfRange;
        text = string.Empty;

        if (payload.Length < 4
            || payload[0] != (byte)TandemV2Table1Command.ConnectRetDeviceInfo)
        {
            return false;
        }

        type = (TandemDeviceInfoType)payload[1];
        var length = payload[2];
        if (length > payload.Length - 3)
        {
            return false;
        }

        text = Encoding.UTF8.GetString(payload.Slice(3, length));
        return true;
    }

    public static bool TryParseSupportFunctions(
        ReadOnlySpan<byte> payload,
        out IReadOnlyList<(byte FunctionType, byte TableNumber)> functions)
    {
        functions = [];

        if (payload.Length < 3
            || payload[0] != (byte)TandemV2Table1Command.ConnectRetSupportFunction
            || payload[1] != (byte)TandemConnectInquiredType.FixedValue)
        {
            return false;
        }

        var count = payload[2];
        if (payload.Length != 3 + count * 2)
        {
            return false;
        }

        var result = new List<(byte, byte)>(count);
        for (var index = 0; index < count; index++)
        {
            result.Add((payload[3 + index * 2], payload[4 + index * 2]));
        }

        functions = result;
        return true;
    }

    public static bool TryParseGsCapability(
        ReadOnlySpan<byte> payload,
        out TandemGsCapability capability)
    {
        capability = null!;

        if (payload.Length < 6
            || payload[0] != (byte)TandemV2Table1Command.GeneralSettingRetCapability)
        {
            return false;
        }

        var inquiredType = (TandemGsInquiredType)payload[1];
        var settingType = (TandemGsSettingType)payload[2];
        if (inquiredType == TandemGsInquiredType.OutOfRange
            || settingType == TandemGsSettingType.OutOfRange)
        {
            return false;
        }

        var offset = 3;
        if (!TryReadGsString(payload, ref offset, out var titleFormat, out var title, out var summary))
        {
            return false;
        }

        var elements = new List<string>();
        if (settingType == TandemGsSettingType.ListType)
        {
            if (offset >= payload.Length)
            {
                return false;
            }

            var count = payload[offset++];
            for (var index = 0; index < count; index++)
            {
                if (!TryReadGsString(payload, ref offset, out _, out var elementTitle, out _))
                {
                    return false;
                }

                elements.Add(elementTitle);
            }
        }

        if (offset != payload.Length)
        {
            return false;
        }

        capability = new TandemGsCapability(
            inquiredType,
            settingType,
            titleFormat,
            title,
            summary,
            elements);
        return true;
    }

    public static bool TryParseGsState(
        ReadOnlySpan<byte> payload,
        out TandemGsState state)
    {
        state = null!;

        if (payload.Length < 4
            || payload[0] != (byte)TandemV2Table1Command.GeneralSettingRetParam)
        {
            return false;
        }

        var inquiredType = (TandemGsInquiredType)payload[1];
        var settingType = (TandemGsSettingType)payload[2];
        if (inquiredType == TandemGsInquiredType.OutOfRange
            || settingType == TandemGsSettingType.OutOfRange)
        {
            return false;
        }

        if (settingType == TandemGsSettingType.BooleanType)
        {
            var value = (TandemGsSettingValue)payload[3];
            if (value == TandemGsSettingValue.OutOfRange || payload.Length != 4)
            {
                return false;
            }

            state = new TandemGsState(inquiredType, settingType, value, null);
            return true;
        }

        if (payload.Length != 4)
        {
            return false;
        }

        state = new TandemGsState(
            inquiredType,
            settingType,
            TandemGsSettingValue.OutOfRange,
            payload[3]);
        return true;
    }

    public static bool TryParseGsStatus(
        ReadOnlySpan<byte> payload,
        out TandemGsInquiredType inquiredType,
        out bool enabled)
    {
        inquiredType = TandemGsInquiredType.OutOfRange;
        enabled = false;

        if (payload.Length != 3
            || payload[0] != (byte)TandemV2Table1Command.GeneralSettingRetStatus
            || payload[2] > 1)
        {
            return false;
        }

        inquiredType = (TandemGsInquiredType)payload[1];
        if (inquiredType == TandemGsInquiredType.OutOfRange)
        {
            return false;
        }

        enabled = payload[2] == 0;
        return true;
    }

    public static bool TryParseAutoPowerOffCapability(
        ReadOnlySpan<byte> payload,
        out TandemAutoPowerOffCapability capability)
    {
        capability = null!;

        if (payload.Length < 3
            || payload[0] != (byte)TandemV2Table1Command.PowerRetCapability
            || !IsAutoPowerOffType(payload[1]))
        {
            return false;
        }

        var count = payload[2];
        if (payload.Length != 3 + count)
        {
            return false;
        }

        var elements = new List<TandemAutoPowerOffElement>(count);
        for (var index = 0; index < count; index++)
        {
            var element = (TandemAutoPowerOffElement)payload[3 + index];
            if (element == TandemAutoPowerOffElement.OutOfRange)
            {
                return false;
            }

            elements.Add(element);
        }

        capability = new TandemAutoPowerOffCapability(elements);
        return true;
    }

    public static bool TryParseAutoPowerOffState(
        ReadOnlySpan<byte> payload,
        out TandemAutoPowerOffState state)
    {
        state = null!;

        if (payload.Length != 4
            || (payload[0] != (byte)TandemV2Table1Command.PowerRetParam
                && payload[0] != (byte)TandemV2Table1Command.PowerNtfyParam)
            || !IsAutoPowerOffType(payload[1]))
        {
            return false;
        }

        var first = (TandemAutoPowerOffElement)payload[2];
        var second = (TandemAutoPowerOffElement)payload[3];
        if (first == TandemAutoPowerOffElement.OutOfRange
            || second == TandemAutoPowerOffElement.OutOfRange)
        {
            return false;
        }

        state = new TandemAutoPowerOffState(first, second);
        return true;
    }

    private static bool TryReadGsString(
        ReadOnlySpan<byte> payload,
        ref int offset,
        out TandemGsStringFormat format,
        out string title,
        out string summary)
    {
        format = TandemGsStringFormat.OutOfRange;
        title = string.Empty;
        summary = string.Empty;

        if (offset + 2 > payload.Length)
        {
            return false;
        }

        format = (TandemGsStringFormat)payload[offset];
        var titleLength = payload[offset + 1];
        var titleStart = offset + 2;
        var summaryLengthOffset = titleStart + titleLength;
        if (summaryLengthOffset >= payload.Length)
        {
            return false;
        }

        var summaryLength = payload[summaryLengthOffset];
        var summaryStart = summaryLengthOffset + 1;
        var end = summaryStart + summaryLength;
        if (end > payload.Length)
        {
            return false;
        }

        title = Encoding.UTF8.GetString(payload.Slice(titleStart, titleLength));
        summary = Encoding.UTF8.GetString(payload.Slice(summaryStart, summaryLength));
        offset = end;
        return true;
    }

    public static byte[] NcAsmSetMode(
        TandemNcNcssAsmMode mode,
        TandemAmbientSoundMode ambientMode,
        byte level,
        TandemNoiseAdaptiveSensitivity sensitivity = TandemNoiseAdaptiveSensitivity.Standard,
        bool enabled = true,
        bool noiseAdaptive = true)
    {
        return
        [
            (byte)TandemV2Table1Command.NcAsmSetParam,
            (byte)TandemNcAsmInquiredType.ModeNcAsmDualNcModeSwitchAndAsmSeamlessNa,
            (byte)TandemValueChangeStatus.Changed,
            (byte)(enabled ? TandemNcAsmOnOffValue.On : TandemNcAsmOnOffValue.Off),
            (byte)mode,
            (byte)ambientMode,
            level,
            (byte)(noiseAdaptive ? TandemNcAsmOnOffValue.On : TandemNcAsmOnOffValue.Off),
            (byte)sensitivity,
        ];
    }

    public static bool TryParseNcAsmState(
        ReadOnlySpan<byte> payload,
        out TandemNcAsmState state)
    {
        state = null!;
        if (payload.Length != 9
            || payload[1] != (byte)TandemNcAsmInquiredType.ModeNcAsmDualNcModeSwitchAndAsmSeamlessNa)
        {
            return false;
        }

        var mode = (TandemNcNcssAsmMode)payload[4];
        var ambient = (TandemAmbientSoundMode)payload[5];
        var onOff = (TandemNcAsmOnOffValue)payload[3];
        var noiseAdaptive = (TandemNcAsmOnOffValue)payload[7];
        var sensitivity = (TandemNoiseAdaptiveSensitivity)payload[8];
        if (mode == TandemNcNcssAsmMode.OutOfRange
            || ambient == TandemAmbientSoundMode.OutOfRange
            || onOff == TandemNcAsmOnOffValue.OutOfRange
            || noiseAdaptive == TandemNcAsmOnOffValue.OutOfRange
            || sensitivity == TandemNoiseAdaptiveSensitivity.OutOfRange)
        {
            return false;
        }

        state = new TandemNcAsmState(
            mode,
            ambient,
            payload[6],
            onOff,
            noiseAdaptive,
            sensitivity,
            (TandemValueChangeStatus)payload[2]);
        return true;
    }
    public static bool TryParseEqParam(
        ReadOnlySpan<byte> payload,
        out TandemEqPresetId preset,
        out byte[] bands)
    {
        preset = TandemEqPresetId.Unspecified;
        bands = [];

        if (payload.Length < 4
            || payload[0] != (byte)TandemV2Table1Command.EqEbbRetParam)
        {
            return false;
        }

        var count = payload[3];
        if (payload.Length != 4 + count)
        {
            return false;
        }

        preset = (TandemEqPresetId)payload[2];
        bands = payload.Slice(4, count).ToArray();
        return true;
    }
    public static string? DescribeFeature(ReadOnlySpan<byte> payload)
    {
        if (payload.Length == 9
            && (payload[0] == (byte)TandemV2Table1Command.NcAsmRetParam
                || payload[0] == (byte)TandemV2Table1Command.NcAsmNtfyParam)
            && TryParseNcAsmState(payload, out var ncAsmState))
        {
            return $"ncasm: onOff={ncAsmState.OnOff} mode={ncAsmState.Mode} "
                + $"ambient={ncAsmState.AmbientMode} level={ncAsmState.Level} "
                + $"noiseAdaptive={ncAsmState.NoiseAdaptiveOnOff} "
                + $"sensitivity={ncAsmState.Sensitivity}";
        }

        if (payload.Length >= 3 && payload[0] == (byte)TandemV2Table1Command.NcAsmRetCapability)
        {
            var count = payload[2];
            if (payload.Length != 3 + count * 4)
            {
                return null;
            }

            var parts = new List<string>(count);
            for (var index = 0; index < count; index++)
            {
                var offset = 3 + index * 4;
                var mode = (TandemAmbientSoundMode)payload[offset];
                var min = payload[offset + 1];
                var max = payload[offset + 2];
                var step = payload[offset + 3];
                parts.Add($"{mode} level {min}..{max} step {step}");
            }

            return $"ambient modes: {string.Join("; ", parts)}";
        }

        if (payload.Length >= 5 && payload[0] == (byte)TandemV2Table1Command.EqEbbRetCapability)
        {
            var presetCount = payload[4];
            var presets = new List<string>(presetCount);
            var offset = 5;
            while (offset + 1 < payload.Length && presets.Count < presetCount)
            {
                var length = payload[offset + 1];
                if (offset + 2 + length > payload.Length)
                {
                    return null;
                }

                if (length == 0)
                {
                    var preset = (TandemEqPresetId)payload[offset];
                    presets.Add(preset.ToString());
                }

                offset += 2 + length;
            }

            if (presets.Count == presetCount)
            {
                return $"presets: {string.Join(", ", presets)}";
            }
        }

        if (payload.Length >= 4 && payload[0] == (byte)TandemV2Table1Command.EqEbbRetParam)
        {
            var preset = (TandemEqPresetId)payload[2];
            var bandCount = payload[3];
            if (payload.Length == 4 + bandCount)
            {
                var bands = payload.Slice(4, bandCount).ToArray();
                return $"preset: {preset}; bands: {string.Join(", ", bands)}";
            }
        }

        if (payload.Length >= 4 && payload[0] == (byte)TandemV2Table1Command.EqEbbRetStatus
            && payload[1] == (byte)TandemEqEbbInquiredType.PresetEqAndErrorCode)
        {
            var errorCount = payload[3];
            return errorCount == 0 ? "preset error codes: none" : $"preset error codes: {errorCount}";
        }

        if (payload.Length >= 4 && payload[0] == (byte)TandemV2Table1Command.PowerRetStatus
            && payload[1] == (byte)TandemPowerInquiredType.Battery)
        {
            return $"battery: {payload[2]}% charging=0x{payload[3]:x2}";
        }

        if (payload.Length >= 3 && payload[0] == (byte)TandemV2Table1Command.GeneralSettingRetCapability
            && TryParseGsCapability(payload, out var gsCapability))
        {
            var elements = gsCapability.Elements.Count == 0
                ? string.Empty
                : $"; elements: {string.Join(", ", gsCapability.Elements)}";
            return $"gs {gsCapability.InquiredType}: {gsCapability.SettingType} title={gsCapability.Title}{elements}";
        }

        if (payload.Length == 3 && payload[0] == (byte)TandemV2Table1Command.GeneralSettingRetStatus
            && TryParseGsStatus(payload, out var gsStatusType, out var gsEnabled))
        {
            return $"gs {gsStatusType}: {(gsEnabled ? "enabled" : "disabled")}";
        }

        if (payload.Length >= 4 && payload[0] == (byte)TandemV2Table1Command.GeneralSettingRetParam
            && TryParseGsState(payload, out var gsState))
        {
            return gsState.SettingType == TandemGsSettingType.BooleanType
                ? $"gs {gsState.InquiredType}: {gsState.Value}"
                : $"gs {gsState.InquiredType}: list index {gsState.ListIndex}";
        }

        if (payload.Length >= 3 && payload[0] == (byte)TandemV2Table1Command.PowerRetCapability
            && IsAutoPowerOffType(payload[1])
            && TryParseAutoPowerOffCapability(payload, out var autoPowerOffCapability))
        {
            return $"auto power off: {string.Join(", ", autoPowerOffCapability.Elements)}";
        }

        if (payload.Length >= 4
            && (payload[0] == (byte)TandemV2Table1Command.PowerRetParam
                || payload[0] == (byte)TandemV2Table1Command.PowerNtfyParam)
            && IsAutoPowerOffType(payload[1])
            && TryParseAutoPowerOffState(payload, out var autoPowerOffState))
        {
            return $"auto power off: {autoPowerOffState.First}, {autoPowerOffState.Second}";
        }

        if (payload.Length == 4
            && (payload[0] == (byte)TandemV2Table1Command.AudioRetParam
                || payload[0] == (byte)TandemV2Table1Command.AudioNtfyParam)
            && TryParseBgmState(payload, out var bgmState))
        {
            return $"bgm: {bgmState.OnOff} room={bgmState.RoomSize}";
        }

        if (payload.Length == 3
            && (payload[0] == (byte)TandemV2Table1Command.AudioRetParam
                || payload[0] == (byte)TandemV2Table1Command.AudioNtfyParam)
            && TryParseCinemaState(payload, out var cinemaState))
        {
            return $"cinema: {cinemaState.OnOff}";
        }

        if (payload.Length == 3
            && (payload[0] == (byte)TandemV2Table1Command.AudioRetParam
                || payload[0] == (byte)TandemV2Table1Command.AudioNtfyParam)
            && TryParseUpmixSeriesState(payload, out var upmixItem))
        {
            return $"upmix series: {upmixItem}";
        }

        return null;
    }
    private static bool IsAutoPowerOffType(byte type) =>
        type is (byte)TandemPowerInquiredType.AutoPowerOff
            or (byte)TandemPowerInquiredType.AutoPowerOffWearingDetection;

    private static byte[] BuildWithValue(
        TandemV2Table1Command command,
        byte type,
        IReadOnlyList<byte> value)
    {
        var payload = new byte[2 + value.Count];
        payload[0] = (byte)command;
        payload[1] = type;
        for (var index = 0; index < value.Count; index++)
        {
            payload[2 + index] = value[index];
        }

        return payload;
    }
}











using Xm6Control.Protocol;

namespace Xm6Control.Protocol.Tests;

public sealed class TandemV2Table1FeatureTests
{
    [Fact]
    public void DescribesNcAsmCapabilityType21()
    {
        var payload = Convert.FromHexString("6115020001140101011401");

        var detail = TandemV2Table1Messages.DescribeFeature(payload);

        Assert.Equal("ambient modes: Normal level 1..20 step 1; Voice level 1..20 step 1", detail);
    }

    [Fact]
    public void DescribesEqPresetCapability()
    {
        var payload = Convert.FromHexString("51040a0d09000030003100320033002000a000a100a200");

        var detail = TandemV2Table1Messages.DescribeFeature(payload);

        Assert.Equal(
            "presets: Off, Heavy, Clear, Hard, Soft, GamingEq, Custom, UserSetting1, UserSetting2",
            detail);
    }

    [Fact]
    public void DescribesEqParam()
    {
        var payload = Convert.FromHexString("5704a10a070206060a080809070b");

        var detail = TandemV2Table1Messages.DescribeFeature(payload);

        Assert.Equal("preset: UserSetting1; bands: 7, 2, 6, 6, 10, 8, 8, 9, 7, 11", detail);
    }

    [Fact]
    public void DescribesBatteryStatus()
    {
        var payload = Convert.FromHexString("23004c00");

        var detail = TandemV2Table1Messages.DescribeFeature(payload);

        Assert.Equal("battery: 76% charging=0x00", detail);
    }

    [Fact]
    public void DescribesEqErrorStatus()
    {
        var payload = Convert.FromHexString("53040000");

        var detail = TandemV2Table1Messages.DescribeFeature(payload);

        Assert.Equal("preset error codes: none", detail);
    }
    [Fact]
    public void BuildsEqPresetOnlyForDefaultCurve()
    {
        var payload = TandemV2Table1Messages.EqEbbSetPresetOnly(
            TandemEqEbbInquiredType.PresetEqAndErrorCode,
            TandemEqPresetId.UserSetting1);

        Assert.Equal("5804a100", Convert.ToHexString(payload).ToLowerInvariant());
    }

    [Fact]
    public void ParsesEqParam()
    {
        var payload = Convert.FromHexString("5704a10a070206060a080809070b");

        Assert.True(TandemV2Table1Messages.TryParseEqParam(payload, out var preset, out var bands));
        Assert.Equal(TandemEqPresetId.UserSetting1, preset);
        Assert.Equal([7, 2, 6, 6, 10, 8, 8, 9, 7, 11], bands);
    }

    [Fact]
    public void BuildsEqSetPreset()
    {
        var payload = TandemV2Table1Messages.EqEbbSetPreset(
            TandemEqEbbInquiredType.PresetEqAndErrorCode,
            TandemEqPresetId.UserSetting1,
            [7, 2, 6, 6, 10, 8, 8, 9, 7, 11]);

        Assert.Equal("5804a10a070206060a080809070b", Convert.ToHexString(payload).ToLowerInvariant());
    }

    [Fact]
    public void ParsesNcAsmState()
    {
        var payload = Convert.FromHexString("691901010001140100");

        Assert.True(TandemV2Table1Messages.TryParseNcAsmState(payload, out var state));
        Assert.Equal(TandemNcNcssAsmMode.Nc, state.Mode);
        Assert.Equal(TandemAmbientSoundMode.Voice, state.AmbientMode);
        Assert.Equal(20, state.Level);
        Assert.Equal(TandemNcAsmOnOffValue.On, state.OnOff);
        Assert.Equal(TandemNcAsmOnOffValue.On, state.NoiseAdaptiveOnOff);
        Assert.Equal(TandemNoiseAdaptiveSensitivity.Standard, state.Sensitivity);

        Assert.True(TandemV2Table1Messages.TryParseNcAsmState(
            Convert.FromHexString("691901000001140102"),
            out var offState));
        Assert.Equal(TandemNcAsmOnOffValue.Off, offState.OnOff);
        Assert.Equal(TandemNcAsmOnOffValue.On, offState.NoiseAdaptiveOnOff);
        Assert.Equal(TandemNoiseAdaptiveSensitivity.Low, offState.Sensitivity);
    }

    [Fact]
    public void BuildsNcAsmSetMode()
    {
        var payload = TandemV2Table1Messages.NcAsmSetMode(
            TandemNcNcssAsmMode.Asm,
            TandemAmbientSoundMode.Voice,
            20);

        Assert.Equal("681901010101140100", Convert.ToHexString(payload).ToLowerInvariant());

        Assert.Equal(
            "681901000000140002",
            Convert.ToHexString(TandemV2Table1Messages.NcAsmSetMode(
                TandemNcNcssAsmMode.Nc,
                TandemAmbientSoundMode.Normal,
                20,
                TandemNoiseAdaptiveSensitivity.Low,
                enabled: false,
                noiseAdaptive: false)).ToLowerInvariant());
    }

    [Fact]
    public void ParsesGeneralSettingListCapability()
    {
        var payload = Convert.FromHexString(
            "d1d10101124d554c5449504f494e545f53455454494e4700020101410001014200");

        Assert.True(TandemV2Table1Messages.TryParseGsCapability(payload, out var capability));
        Assert.Equal(TandemGsInquiredType.GeneralSetting1, capability.InquiredType);
        Assert.Equal(TandemGsSettingType.ListType, capability.SettingType);
        Assert.Equal(TandemGsStringFormat.EnumName, capability.TitleFormat);
        Assert.Equal("MULTIPOINT_SETTING", capability.Title);
        Assert.Equal(string.Empty, capability.Summary);
        Assert.Equal(["A", "B"], capability.Elements);
    }

    [Fact]
    public void ParsesGeneralSettingBooleanCapability()
    {
        var payload = Convert.FromHexString("d1d200010a4f4e5f4f46465f53455400");

        Assert.True(TandemV2Table1Messages.TryParseGsCapability(payload, out var capability));
        Assert.Equal(TandemGsInquiredType.GeneralSetting2, capability.InquiredType);
        Assert.Equal(TandemGsSettingType.BooleanType, capability.SettingType);
        Assert.Equal("ON_OFF_SET", capability.Title);
        Assert.Empty(capability.Elements);
    }

    [Fact]
    public void ParsesGeneralSettingValueAndStatus()
    {
        Assert.True(TandemV2Table1Messages.TryParseGsState(
            Convert.FromHexString("d7d10000"),
            out var state));
        Assert.Equal(TandemGsInquiredType.GeneralSetting1, state.InquiredType);
        Assert.Equal(TandemGsSettingType.BooleanType, state.SettingType);
        Assert.Equal(TandemGsSettingValue.On, state.Value);

        Assert.True(TandemV2Table1Messages.TryParseGsState(
            Convert.FromHexString("d7d20102"),
            out var listState));
        Assert.Equal(2, listState.ListIndex);

        Assert.True(TandemV2Table1Messages.TryParseGsStatus(
            Convert.FromHexString("d3d100"),
            out var statusType,
            out var enabled));
        Assert.Equal(TandemGsInquiredType.GeneralSetting1, statusType);
        Assert.True(enabled);

        Assert.True(TandemV2Table1Messages.TryParseGsStatus(
            Convert.FromHexString("d3d201"),
            out _,
            out var disabled));
        Assert.False(disabled);
    }

    [Fact]
    public void BuildsGeneralSettingBooleanSet()
    {
        var payload = TandemV2Table1Messages.GeneralSettingSetBool(
            TandemGsInquiredType.GeneralSetting3,
            on: true);

        Assert.Equal("d8d30000", Convert.ToHexString(payload).ToLowerInvariant());
    }

    [Fact]
    public void ParsesAutoPowerOffCapabilityAndState()
    {
        Assert.True(TandemV2Table1Messages.TryParseAutoPowerOffCapability(
            Convert.FromHexString("2104050001020304"),
            out var capability));
        Assert.Equal(
            [
                TandemAutoPowerOffElement.In5Minutes,
                TandemAutoPowerOffElement.In30Minutes,
                TandemAutoPowerOffElement.In60Minutes,
                TandemAutoPowerOffElement.In180Minutes,
                TandemAutoPowerOffElement.In15Minutes,
            ],
            capability.Elements);

        Assert.True(TandemV2Table1Messages.TryParseAutoPowerOffState(
            Convert.FromHexString("27040111"),
            out var state));
        Assert.Equal(TandemAutoPowerOffElement.In30Minutes, state.First);
        Assert.Equal(TandemAutoPowerOffElement.Disable, state.Second);

        Assert.True(TandemV2Table1Messages.TryParseAutoPowerOffState(
            Convert.FromHexString("27051000"),
            out var wearingState));
        Assert.Equal(TandemAutoPowerOffElement.WhenRemovedFromEars, wearingState.First);
        Assert.Equal(TandemAutoPowerOffElement.In5Minutes, wearingState.Second);
    }

    [Fact]
    public void ParsesWearingDetectionAutoPowerOffCapability()
    {
        Assert.True(TandemV2Table1Messages.TryParseAutoPowerOffCapability(
            Convert.FromHexString("2105021011"),
            out var capability));
        Assert.Equal(
            [TandemAutoPowerOffElement.WhenRemovedFromEars, TandemAutoPowerOffElement.Disable],
            capability.Elements);
    }

    [Fact]
    public void BuildsBgmAndCinemaSetParams()
    {
        Assert.Equal(
            "e8090002",
            Convert.ToHexString(TandemV2Table1Messages.AudioSetBgm(
                true,
                TandemRoomSize.Large,
                TandemAudioInquiredType.BgmModeAndErrorCode)).ToLowerInvariant());
        Assert.Equal(
            "e8030100",
            Convert.ToHexString(TandemV2Table1Messages.AudioSetBgm(
                false,
                TandemRoomSize.Small,
                TandemAudioInquiredType.BgmMode)).ToLowerInvariant());
        Assert.Equal(
            "e80400",
            Convert.ToHexString(TandemV2Table1Messages.AudioSetCinema(true)).ToLowerInvariant());
        Assert.Equal(
            "e80401",
            Convert.ToHexString(TandemV2Table1Messages.AudioSetCinema(false)).ToLowerInvariant());
    }

    [Fact]
    public void ParsesBgmAndCinemaStates()
    {
        Assert.True(TandemV2Table1Messages.TryParseBgmState(
            Convert.FromHexString("e7090001"),
            out var bgm));
        Assert.Equal(TandemOnOffSettingValue.On, bgm.OnOff);
        Assert.Equal(TandemRoomSize.Middle, bgm.RoomSize);

        Assert.True(TandemV2Table1Messages.TryParseBgmState(
            Convert.FromHexString("e9030102"),
            out var bgmNotify));
        Assert.Equal(TandemOnOffSettingValue.Off, bgmNotify.OnOff);
        Assert.Equal(TandemRoomSize.Large, bgmNotify.RoomSize);

        Assert.True(TandemV2Table1Messages.TryParseCinemaState(
            Convert.FromHexString("e70400"),
            out var cinema));
        Assert.Equal(TandemOnOffSettingValue.On, cinema.OnOff);

        Assert.Equal(
            "e80a01",
            Convert.ToHexString(TandemV2Table1Messages.AudioSetUpmixSeries(
                TandemUpmixItemId.Cinema)).ToLowerInvariant());
        Assert.True(TandemV2Table1Messages.TryParseUpmixSeriesState(
            Convert.FromHexString("e70a01"),
            out var upmixItem));
        Assert.Equal(TandemUpmixItemId.Cinema, upmixItem);
    }

    [Fact]
    public void ParsesLiveMultipointCapability()
    {
        var payload = Convert.FromHexString(
            "d1d20001124d554c5449504f494e545f53455454494e47294d554c5449504f494e545f53455454494e475f53554d4d4152595f4c4441435f415641494c41424c45");

        Assert.True(TandemV2Table1Messages.TryParseGsCapability(payload, out var capability));
        Assert.Equal(TandemGsInquiredType.GeneralSetting2, capability.InquiredType);
        Assert.Equal("MULTIPOINT_SETTING", capability.Title);
        Assert.Equal("MULTIPOINT_SETTING_SUMMARY_LDAC_AVAILABLE", capability.Summary);
        Assert.Empty(capability.Elements);
    }
}



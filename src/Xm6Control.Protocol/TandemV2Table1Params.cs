namespace Xm6Control.Protocol;

public enum TandemConnectInquiredType : byte
{
    FixedValue = 0,
    OutOfRange = 0xFF,
}

public enum TandemDeviceInfoType : byte
{
    ModelName = 1,
    FirmwareVersion = 2,
    SeriesAndColorInfo = 3,
    InstructionGuide = 4,
    OutOfRange = 0xFF,
}

public enum TandemPowerInquiredType : byte
{
    Battery = 0,
    LeftRightBattery = 1,
    CradleBattery = 2,
    PowerOff = 3,
    AutoPowerOff = 4,
    AutoPowerOffWearingDetection = 5,
    PowerSaveMode = 6,
    LinkControl = 7,
    BatteryWithThreshold = 8,
    LeftRightBatteryWithThreshold = 9,
    CradleBatteryWithThreshold = 10,
    BatterySafeMode = 11,
    CaringCharge = 12,
    Stamina = 14,
    AutomaticTouchPanelBacklightTurnOff = 15,
    OutOfRange = 0xFF,
}

public enum TandemNcAsmInquiredType : byte
{
    NcOnOff = 1,
    NcOnOffAndAsmOnOff = 17,
    NcModeSwitchAndAsmOnOff = 18,
    NcOnOffAndAsmSeamless = 19,
    NcModeSwitchAndAsmSeamless = 20,
    ModeNcAsmAutoNcModeSwitchAndAsmSeamless = 21,
    ModeNcAsmDualSingleNcModeSwitchAndAsmSeamless = 22,
    ModeNcAsmDualNcModeSwitchAndAsmSeamless = 23,
    ModeNcNcssAsmDualNcModeSwitchAndAsmSeamless = 24,
    ModeNcAsmDualNcModeSwitchAndAsmSeamlessNa = 25,
    AsmOnOff = 33,
    AsmSeamless = 34,
    NcAmbToggle = 48,
    NcTestMode = 64,
    OutOfRange = 0xFF,
}

public enum TandemNcValue : byte
{
    Off = 0,
    OnSingle = 1,
    OnDual = 2,
    Auto = 3,
    AutoSingle = 4,
    AutoDual = 5,
    OutOfRange = 0xFF,
}

public enum TandemNcNcssAsmMode : byte
{
    Nc = 0,
    Asm = 1,
    NcSs = 2,
    OutOfRange = 0xFF,
}

public enum TandemNoiseAdaptiveSensitivity : byte
{
    Standard = 0,
    High = 1,
    Low = 2,
    OutOfRange = 0xFF,
}

public sealed record TandemNcAsmState(
    TandemNcNcssAsmMode Mode,
    TandemAmbientSoundMode AmbientMode,
    byte Level,
    TandemNcAsmOnOffValue OnOff,
    TandemNcAsmOnOffValue NoiseAdaptiveOnOff,
    TandemNoiseAdaptiveSensitivity Sensitivity,
    TandemValueChangeStatus ChangeStatus);

public enum TandemNcAsmMode : byte
{
    Nc = 0,
    Asm = 1,
    OutOfRange = 0xFF,
}

public enum TandemAmbientSoundMode : byte
{
    Normal = 0,
    Voice = 1,
    OutOfRange = 0xFF,
}

public enum TandemValueChangeStatus : byte
{
    UnderChanging = 0,
    Changed = 1,
    OutOfRange = 0xFF,
}

public enum TandemNcAsmOnOffValue : byte
{
    Off = 0,
    On = 1,
    OutOfRange = 0xFF,
}

public enum TandemEqEbbInquiredType : byte
{
    PresetEq = 0,
    Ebb = 1,
    PresetEqNonCustomizable = 2,
    PresetEqAndUltMode = 3,
    PresetEqAndErrorCode = 4,
    SoundEffect = 48,
    CustomEq = 49,
    TurnKeyEq = 50,
    CustomizableSoundEffectSelect = 51,
    CustomizableSoundEffectReset = 52,
    CustomizableSoundEffectCustom = 53,
    CustomizableSoundEffectExternalUpdate = 54,
    UltButtonSoundEffectAssign = 64,
    OutOfRange = 0xFF,
}

public enum TandemEqPresetId : byte
{
    Off = 0,
    Rock = 1,
    Pop = 2,
    Jazz = 3,
    Dance = 4,
    Edm = 5,
    RAndBHipHop = 6,
    Acoustic = 7,
    Bright = 16,
    Excited = 17,
    Mellow = 18,
    Relaxed = 19,
    Vocal = 20,
    Treble = 21,
    Bass = 22,
    Speech = 23,
    GamingEq = 32,
    Heavy = 48,
    Clear = 49,
    Hard = 50,
    Soft = 51,
    Custom = 0xA0,
    UserSetting1 = 0xA1,
    UserSetting2 = 0xA2,
    UserSetting3 = 0xA3,
    UserSetting4 = 0xA4,
    UserSetting5 = 0xA5,
    Unspecified = 0xFF,
}

public enum TandemAudioInquiredType : byte
{
    ConnectionMode = 0,
    Upscaling = 1,
    ConnectionModeWithLdacStatus = 2,
    BgmMode = 3,
    UpmixCinema = 4,
    ConnectionModeClassicAudioLeAudio = 5,
    VoiceContents = 6,
    SoundLeakageReduction = 7,
    ListeningOptionAssignCustomizable = 8,
    BgmModeAndErrorCode = 9,
    UpmixSeries = 10,
    UpscalingAutoOffWithStatusDisableReason = 11,
    OutOfRange = 0xFF,
}

public enum TandemRoomSize : byte
{
    Small = 0,
    Middle = 1,
    Large = 2,
    OutOfRange = 0xFF,
}

public enum TandemUpmixItemId : byte
{
    None = 0,
    Cinema = 1,
    Game = 2,
    Music = 3,
    OutOfRange = 0xFF,
}

public enum TandemListeningMode
{
    Standard,
    BackgroundMusic,
    Cinema,
}

public sealed record TandemBgmState(
    TandemOnOffSettingValue OnOff,
    TandemRoomSize RoomSize);

public sealed record TandemCinemaState(
    TandemOnOffSettingValue OnOff);

public sealed record TandemListeningModeState(
    TandemListeningMode Mode,
    TandemOnOffSettingValue BgmOnOff,
    TandemRoomSize RoomSize,
    TandemOnOffSettingValue CinemaOnOff);

public enum TandemGsInquiredType : byte
{
    GeneralSetting1 = 0xD1,
    GeneralSetting2 = 0xD2,
    GeneralSetting3 = 0xD3,
    GeneralSetting4 = 0xD4,
    OutOfRange = 0xFF,
}

public enum TandemGsStringFormat : byte
{
    RawName = 0,
    EnumName = 1,
    OutOfRange = 0xFF,
}

public sealed record TandemGsCapability(
    TandemGsInquiredType InquiredType,
    TandemGsSettingType SettingType,
    TandemGsStringFormat TitleFormat,
    string Title,
    string Summary,
    IReadOnlyList<string> Elements);

public sealed record TandemGsState(
    TandemGsInquiredType InquiredType,
    TandemGsSettingType SettingType,
    TandemGsSettingValue Value,
    int? ListIndex);

public enum TandemGsSettingType : byte
{
    BooleanType = 0,
    ListType = 1,
    OutOfRange = 0xFF,
}

public enum TandemGsSettingValue : byte
{
    On = 0,
    Off = 1,
    OutOfRange = 0xFF,
}

public enum TandemSystemInquiredType : byte
{
    Vibrator = 0,
    SmartTalkingModeType1 = 2,
    AssignableSettings = 3,
    VoiceAssistantSettings = 4,
    WearingStatusDetector = 6,
    EarpieceSelection = 7,
    CallSettings = 8,
    ResetSettings = 9,
    AutoVolume = 10,
    FaceTapTestMode = 11,
    SmartTalkingModeType2 = 12,
    QuickAccess = 13,
    AssignableSettingsWithLimitation = 14,
    HeadGestureOnOff = 15,
    HeadGestureTraining = 16,
    OutOfRange = 0xFF,
}

public enum TandemAutoPowerOffElement : byte
{
    In5Minutes = 0,
    In30Minutes = 1,
    In60Minutes = 2,
    In180Minutes = 3,
    In15Minutes = 4,
    WhenRemovedFromEars = 16,
    Disable = 17,
    OutOfRange = 0xFF,
}

public sealed record TandemAutoPowerOffState(
    TandemAutoPowerOffElement First,
    TandemAutoPowerOffElement Second);

public sealed record TandemAutoPowerOffCapability(
    IReadOnlyList<TandemAutoPowerOffElement> Elements);

public enum TandemOnOffSettingValue : byte
{
    On = 0,
    Off = 1,
    OutOfRange = 0xFF,
}




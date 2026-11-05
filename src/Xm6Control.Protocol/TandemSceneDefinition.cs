namespace Xm6Control.Protocol;

/// <summary>Noise cancelling / ambient sound part of a saved scene.</summary>
public sealed record TandemSceneNcAsm(
    TandemNcAsmMode Mode,
    bool Enabled,
    byte Level,
    bool VoiceFocus,
    bool NoiseAdaptive,
    TandemNoiseAdaptiveSensitivity Sensitivity);

/// <summary>Equalizer part of a saved scene.</summary>
public sealed record TandemSceneEq(TandemEqPresetId Preset, int[]? CustomBands);

/// <summary>Listening mode part of a saved scene.</summary>
public sealed record TandemSceneListening(TandemListeningMode Mode, TandemRoomSize RoomSize);

/// <summary>
/// A named combination of headphone settings that can be applied in one Tandem
/// session. Groups are optional so a scene can change only what it needs.
/// </summary>
public sealed record TandemSceneDefinition(
    string Id,
    string Name,
    TandemSceneNcAsm? NcAsm,
    TandemSceneEq? Eq,
    TandemSceneListening? Listening);

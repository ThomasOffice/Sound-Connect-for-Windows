# WH-1000XM6 protocol notes

Status: LE Tandem handshake succeeds and reads model, firmware, capability and
support functions. No control write has been sent. No official-app HCI capture
is available (phone not rooted; direct pull skipped).

## Identity

- Model: WH-1000XM6
- Manufacturer: Sony
- Firmware: 3.1.5
- Software: 3.1.5
- BLE name: `LE_WH-1000XM6`
- PnP ID: `02 4c 05 8a 0f 15 03` (Sony vendor `0x054C`, product `0x0F8A`)
- Appearance: `0x0943` (headphones)
- BLE address is a resolvable random address; scan before each connection.

## Advertised service UUIDs

- `0000fd82-0000-1000-8000-00805f9b34fb`
- `dc405470-a351-4a59-97d8-2e2e3b207fbb`
- `0000fd2a-0000-1000-8000-00805f9b34fb`

## Windows GATT map

### Generic Access `00001800-0000-1000-8000-00805f9b34fb`

- `00002a00` Device Name: `LE_WH-1000XM6`
- `00002a01` Appearance: `43 09`

### Generic Attribute `00001801-0000-1000-8000-00805f9b34fb`

- `00002a05` Service Changed: Indicate; CCCD unauthorized
- `00002b29` Read/Write; value `01`
- `00002b3a` Read; value `01`
- `00002b2a` Read; 16-byte value

### Device Information `0000180a-0000-1000-8000-00805f9b34fb`

- `00002a24` Model Number: `WH-1000XM6`
- `00002a26` Firmware Revision: `3.1.5`
- `00002a28` Software Revision: `3.1.5`
- `00002a29` Manufacturer Name: `Sony`
- `00002a50` PnP ID: `02 4c 05 8a 0f 15 03`

### Exposed Sony/Airoha services

| Service | Classification |
|---|---|
| `dc405470-a351-4a59-97d8-2e2e3b207fbb` | Airoha FOTA, not control |
| `5b833e06-6bc7-4802-8e9a-723ceca4bd8f` | Bluetooth iAP connection |
| `5b833e26-6bc7-4802-8e9a-723ceca4bd8f` | Bluetooth pairing complete name |
| `5b833e27-6bc7-4802-8e9a-723ceca4bd8f` | LE Audio capability for HPC |
| `f76acb00-7cab-495f-bb1a-e664598fd77f` | Write/notify candidate; purpose unknown |
| `45c93e07-d90d-4b93-a9db-91e5dd734e35` | Write/notify candidate; purpose unknown |
| `11c8b310-80e4-4276-afc0-f81590b2177f` | Write/notify candidate |
| `28bc862f-87d2-457b-b45a-5c838c4a66ff` | Write/notify candidate |
| `76c13020-fe8f-416a-b4c3-ee59d3ef95dc` | Write/read 25-byte blob |
| `15190001-12f4-c226-88ed-2ac5579f2a85` | Read/write/notify candidate |
| `0000fe2c-...` | Google Fast Pair, not Sony control |

The Tandem V2 HPC service `5b833e20-...` was not exposed initially. After the
Alternative A2DP Driver was disabled it became visible, and the LE Tandem
handshake completed successfully.

## Source-derived Tandem map

Facts from JADX decompilation of `classes2.dex`, `classes3.dex`, and
`classes4.dex`.

### Tandem service and characteristics

- Service: `5b833e20-6bc7-4802-8e9a-723ceca4bd8f`
- `5b833c60-6bc7-4802-8e9a-723ceca4bd8f`: `TANDEM_HPC_TO_ACC` (write)
- `5b833c61-6bc7-4802-8e9a-723ceca4bd8f`: `TANDEM_HPC_FROM_ACC` (notify)
- `5b833c91-6bc7-4802-8e9a-723ceca4bd8f`: `WRITABLE_VALUE_LENGTH` (read)
- `5b833c92-6bc7-4802-8e9a-723ceca4bd8f`: `BEACON_INFO`
- `5b833c93-6bc7-4802-8e9a-723ceca4bd8f`: `DETERMINE_MTU` (notify)

The app fails the Tandem session if `5b833e20` is absent.

### Frame format

From `qm0.b`, `cp0.b`, `cp0.f`, and `qm0.e`:

```text
0x3E  [escaped body]  0x3C
body  = type(1) seq(1) length(4, big-endian) payload(N) checksum(1)
```

- Start: `0x3E`, end: `0x3C`, escape: `0x3D`
- `0x3C -> 0x3D 0x2C`
- `0x3D -> 0x3D 0x2D`
- `0x3E -> 0x3D 0x2E`
- Checksum: low byte of `type + seq + length + payload`
- `DATA_MDR = 0x0C`, `ACK = 0x01`
- ACK frame: `3E 01 seq 00 00 00 00 checksum 3C`
- Host ACK sequence is `1 - receivedSequence`.
- Host transmit sequence follows the last received ACK sequence (0/1
  alternating), not a monotonic counter.
- Tandem writes use GATT Write Without Response.

Validated handshake on 2026-10-29:

```text
status: ok
model: WH-1000XM6
firmware: 3.1.5
supportFunctions: 45
protocolVersion: 50343986 (0x03003032, exact semantic unresolved)
```

Read-only feature queries (`features` command) confirmed:

```text
NCASM capability type 21: Normal 1..20 step 1; Voice 1..20 step 1
EQ capability type 0 -> response type 4:
  Off, Heavy, Clear, Hard, Soft, GamingEq, Custom, UserSetting1, UserSetting2
EQ param type 4: preset UserSetting1; 10 bands = 7,2,6,6,10,8,8,9,7,11
Power battery type 0: 76%; second byte 0x00
```

NCASM status/param and EQ status/param for several other inquired types did not
answer. The correct current-state path for ANC remains unknown.

### EQ write

Verified command:

```text
EQEBB_SET_PARAM 0x58, type 0x04 (PresetEqAndErrorCode),
preset, bandCount, band0..bandN
example: 58 04 a1 0a 06 02 06 06 0a 08 08 09 07 0b
```

The set command did not return a direct 0x57 response. Verification is done by
reading `EQEBB_GET_PARAM 0x56` afterward and comparing preset plus bands. A
change and restore both matched on WH-1000XM6.

The APK's preset selection sends `bandCount = 0` and no bands, so the headphone
applies its internal preset curve. Sending the old slider values for a built-in
preset is wrong. Verified default curves read back on this XM6:

```text
Off        6,6,6,6,6,6,6,6,6,6
Heavy      10,10,5,5,6,6,6,6,6,6
Clear      6,6,6,6,6,6,6,8,8,9
Hard       10,10,5,5,6,6,6,8,8,9
Soft       3,4,5,7,7,7,6,6,4,3
GamingEq   0,4,8,9,7,5,7,9,6,4
UserSetting1 7,2,6,6,10,8,8,9,7,11
UserSetting2 5,2,6,6,10,8,8,9,7,11
```

Only `Custom` sends the 10 slider values. The UI pre-fills these curves and
refreshes them from the device after applying a preset.

### NC/ASM state and write

XM6 capability is inquired with type 21, but state uses inquired type 25
(`MODE_NC_ASM_DUAL_NC_MODE_SWITCH_AND_ASM_SEAMLESS_NA`).

```text
NCASM_NTFY_PARAM / NCASM_RET_PARAM (0x69/0x67), type 25:
  type changeStatus onOff mode ambient level noiseAdaptive sensitivity
  example: 69 19 01 01 00 01 14 01 00
           onOff=On, mode=NC, ambient=Voice, level=20,
           noiseAdaptive=On, sensitivity=Standard

NCASM_SET_PARAM (0x68), type 25:
  68 19 01 <onOff> <mode> <ambient> <level> <noiseAdaptive> <sensitivity>
  onOff: 0=Off, 1=On
  mode: NC=0, ASM=1
  ambient: Normal=0, Voice/Focus on voice=1
  noiseAdaptive: 0=Off, 1=On
  sensitivity: Standard=0, High=1, Low=2
```

Verified live: NC off/on, ASM with automatic ambient on/high, voice focus on,
automatic ambient off/low, and all read-backs matched.

When switching ASM -> NC, the device ignores `ambient/level/noiseAdaptive` and
preserves the last ASM values. To restore an exact NC + underlying ASM state, set
ASM first, then switch to NC.

### Listening option / sound effect

The app's “聆听模式” (Listening Option) is built from two audio effects:

```text
AUDIO_GET_CAPABILITY 0xE0 / RET_CAPABILITY 0xE1
AUDIO_GET_PARAM      0xE6 / RET_PARAM      0xE7
AUDIO_SET_PARAM      0xE8 / NTFY_PARAM     0xE9
```

Audio inquired types:

```text
BGM_MODE                 3
UPMIX_CINEMA             4
BGM_MODE_AND_ERRORCODE   9
UPMIX_SERIES            10
```

BGM is sent as:

```text
E8 <type 9 or 3> <onOff> <roomSize>
onOff: 0 = ON, 1 = OFF
roomSize: 0 = Small / My Room, 1 = Middle / Living Room, 2 = Large / Cafe
```

Cinema has two audio representations in the APK. The app prefers the dedicated
`UPMIX_CINEMA` path and falls back to the `UPMIX_SERIES` item list:

```text
UPMIX_CINEMA: E8 04 <onOff>
UPMIX_SERIES: E8 0A <itemId>   (Cinema = 1, None = 0)
```

State read-back:

```text
BGM:          E7 09 <onOff> <roomSize>
UPMIX_CINEMA: E7 04 <onOff>
UPMIX_SERIES: E7 0A <itemId>
```

Standard mode is implemented by turning BGM and Cinema off; it does not write
EQ, so the current EQ preset remains in effect. BGM/Cinema are exclusive sound
effects that temporarily override EQ. The app prefers type 9 when the device
reports it and falls back to type 3.

Source-derived room mapping from `ListeningOptionFragment` and `RoomSize`:
`SMALL -> BGM_Setting_R1`, `MIDDLE -> R2`, `LARGE -> R3`. The UI labels these
My Room / Living Room / Cafe. Live verification is pending because the
headphones were powered off when this feature was added.

### Multipoint (general setting)

The XM6 exposes multipoint as a general-setting Boolean, not a dedicated
command:

```text
GS_GET_CAPABILITY 0xD0 / RET_CAPABILITY 0xD1
GS_GET_STATUS     0xD2 / RET_STATUS     0xD3
GS_GET_PARAM      0xD6 / RET_PARAM      0xD7
GS_SET_PARAM      0xD8 / NTFY_PARAM     0xD9
```

Capability payload layout:

```text
D1 <gsType> <settingType> <format> <titleLen> <title>
   <summaryLen> <summary>
   [<elementCount> <element>...]      (list type only)

element = <format> <titleLen> <title> <summaryLen> <summary>
format: 0 = raw name, 1 = enum name
```

Live map on WH-1000XM6 firmware 3.1.5:

```text
D1 = SIDETONE_SETTING       BooleanType
D2 = MULTIPOINT_SETTING     BooleanType
     summary = MULTIPOINT_SETTING_SUMMARY_LDAC_AVAILABLE
D3 = TOUCH_PANEL_SETTING    BooleanType
D4 = not reported as capability (status answers Disabled)
```

State uses `D7 <gsType> <settingType> <value>` with `ON = 0`, `OFF = 1`.
The XM6 currently returns `D7 D2 00 00` (multipoint On).

Write test:

```text
D8 D2 00 00  (On)  -> read-back On, verified
D8 D2 00 01  (Off) -> D9 D2 00 00 and read-back On
```

Turning multipoint off was refused while LDAC was available / two devices were
connected. The write is still sent and the UI reports the read-back result rather
than claiming success. The Boolean On write is verified.

### Auto power off

```text
POWER_GET_CAPABILITY 0x20, WEARING 0x05 -> 0x21
POWER_GET_PARAM      0x26, WEARING 0x05 -> 0x27
POWER_SET_PARAM      0x28, WEARING 0x05, first, second
```

The APK also contains a time-based `AUTO_POWER_OFF 0x04`, but the XM6 UI exposes
only two user states, so this app uses the wearing-detection path exclusively:

```text
type 5 capability: 21 05 02 10 11
                   WhenRemovedFromEars (0x10), Disable (0x11)
state:              27 05 <first> <second>
```

Example read-backs:

```text
27 05 10 00 = WhenRemovedFromEars
27 05 11 00 = Disable
```

Verified writes:

```text
first = 0x10 (removed) -> verified
first = 0x11 (Disable) -> verified
```

The second byte is a protocol filler (last selected time) and is not exposed in
the UI. The app reads back the first byte and shows only “取下耳机后” or “关闭”.

### Classic RFCOMM Tandem

The APK also contains a Classic SPP transport:

- `ep0.c` creates an RFCOMM socket with `createRfcommSocketToServiceRecord`.
- `dp0.a` carries the Tandem stream.
- Table1 RFCOMM UUID: `96CC203E-5068-46AD-B32D-E316F5E069BA`.
- Table2 RFCOMM UUID: `956C7B26-D49A-4BA8-B03F-B17D393CB6E2`.

Windows result for WH-1000XM6, including a test with the Alternative A2DP
Driver disabled:

```text
table1 96CC203E...: not exposed
table2 956C7B26...: exposed and connected
table2 support-function request: connection closed, no response
```

The driver is not the cause. Table2 lacks NCASM and EQ/EBB, so Classic table2 is
not sufficient for the MVP. Table1 is required for ANC/EQ and is not exposed.
### v2/table1 command table

XM6 is expected to use v2/table1 because NCASM, EQ/EBB, power, general setting,
and system commands exist there. The runtime `CONNECT_RET_PROTOCOL_INFO` must
confirm the table set.

| Command | Byte |
|---|---:|
| `CONNECT_GET_PROTOCOL_INFO` | `0x00` |
| `CONNECT_RET_PROTOCOL_INFO` | `0x01` |
| `CONNECT_GET_CAPABILITY_INFO` | `0x02` |
| `CONNECT_RET_CAPABILITY_INFO` | `0x03` |
| `CONNECT_GET_DEVICE_INFO` | `0x04` |
| `CONNECT_RET_DEVICE_INFO` | `0x05` |
| `CONNECT_GET_SUPPORT_FUNCTION` | `0x06` |
| `CONNECT_RET_SUPPORT_FUNCTION` | `0x07` |
| `POWER_GET_STATUS` | `0x22` |
| `POWER_GET_PARAM` | `0x26` |
| `POWER_SET_PARAM` | `0x28` |
| `LEA_GET_PARAM` | `0x46` |
| `LEA_SET_PARAM` | `0x48` |
| `EQEBB_GET_CAPABILITY` | `0x50` |
| `EQEBB_GET_STATUS` | `0x52` |
| `EQEBB_GET_PARAM` | `0x56` |
| `EQEBB_SET_PARAM` | `0x58` |
| `NCASM_GET_STATUS` | `0x62` |
| `NCASM_SET_STATUS` | `0x64` |
| `NCASM_GET_PARAM` | `0x66` |
| `NCASM_SET_PARAM` | `0x68` |
| `GENERAL_SETTING_GET_PARAM` | `0xD6` |
| `GENERAL_SETTING_SET_PARAM` | `0xD8` |
| `SYSTEM_GET_STATUS` | `0xF2` |
| `SYSTEM_SET_STATUS` | `0xF4` |
| `SYSTEM_GET_PARAM` | `0xF6` |
| `SYSTEM_SET_PARAM` | `0xF8` |

### Handshake payloads

- Protocol info: `[0x00, 0x00]`
- Capability info: `[0x02, 0x00]`
- Support function: `[0x06, 0x00]`
- Device info: `[0x04, DeviceInfoType]`
  - `MODEL_NAME = 1`
  - `FW_VERSION = 2`
  - `SERIES_AND_COLOR_INFO = 3`
  - `INSTRUCTION_GUIDE = 4`
- `CONNECT_RET_PROTOCOL_INFO`: `[0x01, 0x00, version(4), flag1, flag2]`
- `CONNECT_RET_DEVICE_INFO`: `[0x05, type, length, utf8...]`

### MVP feature payloads

#### ANC / ambient

- Get status: `[0x62, NcAsmInquiredType]`
- Get param: `[0x66, NcAsmInquiredType]`
- Set NC on/off: `[0x68, 0x01, ValueChangeStatus, NcAsmOnOffValue, NcAsmOnOffValue]`
  - `NC_ON_OFF = 1`
  - `ValueChangeStatus.CHANGED = 1`
  - `NcAsmOnOffValue.ON = 1`, `OFF = 0`

The second value is present in the HPC payload; read it back from the device
response.

#### Battery and firmware

- Battery status: `[0x22, 0x00]`
- Battery with threshold: `[0x22, 0x08]`
- Firmware: `[0x04, 0x02]`
- Auto power off get: `[0x26, 0x05]`
- Auto power off set: `[0x28, 0x05, element, filler]`
  - `when removed from ears = 0x10`, `disable = 0x11`
- The APK's time-based type `0x04` is not exposed by this XM6 UI.

#### EQ

- Get capability: `[0x50, EqEbbInquiredType]`
- Get status: `[0x52, EqEbbInquiredType]`
- Get param: `[0x56, EqEbbInquiredType]`
- Preset set: `[0x58, type, EqPresetId, bandCount, band...]`
  - `PRESET_EQ = 0`
  - `PRESET_EQ_NONCUSTOMIZABLE = 2`
  - `PRESET_EQ_AND_ERRORCODE = 4`
- Custom set: `[0x58, 0x31, bandCount, band...]`
- Preset IDs: `OFF=0`, `ROCK=1`, `POP=2`, `JAZZ=3`, `DANCE=4`, `EDM=5`,
  `R_AND_B_HIP_HOP=6`, `ACOUSTIC=7`, `BRIGHT=16`, ... `CUSTOM=0xA0`,
  `USER_SETTING1..5=0xA1..0xA5`.

#### Multipoint and general settings

- Get: `[0xD6, GsInquiredType]`
- Set boolean: `[0xD8, GsInquiredType, 0x00, GsSettingValue]`
- Set list: `[0xD8, GsInquiredType, 0x01, value]`
- `GsSettingValue.ON = 0`, `OFF = 1`
- `GsInquiredType`: `GENERAL_SETTING1..4 = 0xD1..0xD4`

The app maps a title such as `MULTIPOINT_SETTING` to the correct
`GENERAL_SETTING1..4` group at runtime by reading general-setting capability and
comparing the returned title. This mapping is not yet live-confirmed for XM6.

#### Touch / assignable settings

- Get status: `[0xF2, SystemInquiredType]`
- Get param: `[0xF6, SystemInquiredType]`
- Set status: `[0xF4, SystemInquiredType, value...]`
- Set param: `[0xF8, SystemInquiredType, value...]`
- `ASSIGNABLE_SETTINGS = 3`
- `ASSIGNABLE_SETTINGS_WITH_LIMITATION = 14`
- Automatic touch panel backlight off is `POWER_SET_PARAM` with
  `PowerInquiredType.AUTOMATIC_TOUCH_PANEL_BACKLIGHT_TURN_OFF = 15`.

#### LE Audio

Out of scope. The protocol commands exist in the APK (`LEA_GET_PARAM`,
`LEA_SET_PARAM`, `LEA_GET_CAPABILITY`), but no LE Audio implementation is
planned or verified. Classic connection only.

## Live status and next evidence

The Tandem attempt currently returns:

```text
status: tandem_service_missing
message: Tandem V2 HPC service 5b833e20 is not exposed by the headphone in this state.
tandemServicePresent: False
```

The documented pairing-name bootstrap was tried:

- Service `5b833e26`, characteristic `5b833c11`
- Write `[0x01]` (`INQUIRY_SCAN`)
- Result: `write=Success`, status notification `01`
- Complete-name notification: timed out
- Tandem service after refresh: still missing

BLE unpair/re-pair was also attempted:

```text
unpair: Unpaired
pair: Failed
```

The BLE bond is now absent; Classic audio still works. Re-pairing needs the
headphone's own pairing-mode trigger.

Earlier Gate 1 probes read Sony / WH-1000XM6 / firmware 3.1.5, so the transport
is viable. Likely causes:

1. Windows BLE bond is invalid / incomplete.
2. The headphone requires a display/phone-side pairing-mode trigger.

LE Audio is out of scope for this project. The next step is Classic-only:

1. Put the headphone in pairing mode.
2. Pair it again from Windows Settings.
3. Connect Windows audio in Classic mode.
4. Run `dotnet run --project src/Xm6Control.Cli -- tandem --bootstrap`.

Do not send Tandem control writes until the handshake succeeds.




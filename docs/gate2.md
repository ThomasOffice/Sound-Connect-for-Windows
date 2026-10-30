# Gate 2 - direct APK protocol reverse engineering

Date: 2026-10-28
Status: **LE Tandem handshake succeeds; Classic-only still blocked**

## What was done

The Android HCI capture was skipped because the phone is not rooted and direct
`btsnoop_hci.log` access was unavailable. Instead, the APK was decompiled with
JADX 1.5.6 from `classes2.dex`, `classes3.dex`, and `classes4.dex`.

## Key findings

### The control service is not `dc405470`

`ServiceUuid.java` identifies:

- `dc405470-a351-4a59-97d8-2e2e3b207fbb` as Airoha FOTA.
- `5b833e06-...` as Bluetooth iAP connection.
- `5b833e26-...` as Bluetooth pairing complete name.
- `5b833e27-...` as LE Audio capability for HPC.
- `5b833e20-...` as **Tandem V2 HPC**, the control service.

Tandem V2 HPC characteristics:

- `5b833c60-...` `TANDEM_HPC_TO_ACC` (write)
- `5b833c61-...` `TANDEM_HPC_FROM_ACC` (notify)
- `5b833c91-...` `WRITABLE_VALUE_LENGTH` (read)
- `5b833c92-...` `BEACON_INFO`
- `5b833c93-...` `DETERMINE_MTU` (notify)

The current Windows GATT table from Gate 1 exposes `5b833e06`, `5b833e26`,
`5b833e27`, `dc405470`, and others, but **not `5b833e20`**. This is why control
cannot start yet.

### Frame format

From `qm0.b`, `cp0.b`, `cp0.f`, and `qm0.e`:

```text
0x3E  [escaped body]  0x3C
body  = seq(1) type(1) length(4, big-endian) payload(N) checksum(1)
```

- Escapes: `0x3C -> 0x3D 0x2C`, `0x3D -> 0x3D 0x2D`, `0x3E -> 0x3D 0x2E`
- Checksum: low byte of the sum of `seq + type + length + payload`
- `DATA_MDR = 0x0C`, `ACK = 0x01`
- ACK: `3E seq 01 00 00 00 00 (seq+1) 3C`

### Command table

The XM6 path is expected to use v2/table1 because NCASM, EQ/EBB, power, general
setting, and system commands exist there. Important command bytes:

- Connect: `0x00..0x07`
- Power: `0x22` get status, `0x26` get param, `0x28` set param
- LEA: `0x46` get param, `0x48` set param
- EQ/EBB: `0x50` get capability, `0x52` get status, `0x56` get param,
  `0x58` set param
- NCASM: `0x62` get status, `0x66` get param, `0x68` set param
- General setting: `0xD6` get param, `0xD8` set param
- System: `0xF2` get status, `0xF4` set status, `0xF6` get param,
  `0xF8` set param

## Code added

- `src/Xm6Control.Protocol/TandemFrameCodec.cs`
  - Frame encode/decode, escaping, checksum, ACK frames.
- `src/Xm6Control.Protocol/TandemV2Table1Command.cs`
  - v2/table1 command values.
- `src/Xm6Control.Protocol/TandemV2Table1Params.cs`
  - Device info, power, NCASM, EQ/EBB, general setting, system, LEA enums.
- `src/Xm6Control.Protocol/TandemV2Table1Messages.cs`
  - Handshake builders/parsers and MVP feature builders.
- `src/Xm6Control.Bluetooth.Windows/TandemGattSession.cs`
  - Finds the Tandem service, creates `GattSession`, subscribes to FROM_ACC,
    sends handshake messages, ACKs incoming frames, and records traffic.
- `Xm6Control.Cli tandem`
  - Live Tandem session attempt and redacted report.

Tests: 30 passing, including frame golden bytes, escaping, checksum rejection,
multi-frame decode, protocol info parsing, device info parsing, NCASM, auto
power off, and EQ preset payloads.

## Live result

The current headphones expose GATT, but not the Tandem service:

```text
status: tandem_service_missing
message: Tandem V2 HPC service 5b833e20 is not exposed by the headphone in this state.
tandemServicePresent: False
```

A known app pairing-name write was attempted through `--bootstrap`:

- Service: `5b833e26` (Bluetooth pairing complete name)
- Write `[0x01]` (`INQUIRY_SCAN`) to `5b833c11`
- Result: write success, status notification `01`
- Tandem service after refresh: still missing

This rules out a simple "inquiry scan" trigger. The likely causes are:

1. Windows BLE bonding is incomplete or invalid.
2. The headphone requires its own pairing-mode trigger before exposing Tandem.

LE Audio and Auracast are out of scope for this project. Only Classic mode is
targeted.

A BLE unpair/re-pair diagnostic was also attempted:

```text
unpair: Unpaired
pair: Failed
```

The BLE bond was removed, and re-pairing failed without the headphone entering
its pairing mode. Classic audio continues to work, but the BLE control bond is
now absent. This should be repaired manually before further Tandem testing.

No Tandem control write was sent. The only non-Tandem write was the documented
pairing-name inquiry-scan trigger above.

## LE Tandem handshake succeeded

After the Alternative A2DP Driver was disabled, the LE Tandem service
`5b833e20-6bc7-4802-8e9a-723ceca4bd8f` became visible to Windows and the
handshake completed:

```text
status: ok
tandemServicePresent: true
model: WH-1000XM6
firmware: 3.1.5
supportFunctions: 45
tableSetFlag: 0
protocolVersion: 50343986 (0x03003032, exact semantic unresolved)
```

The critical frame details were corrected from the APK implementation:

- Body order is `type(1) sequence(1) length(4 big-endian) payload checksum(1)`.
  The earlier implementation used `sequence` before `type`.
- A host ACK uses sequence `1 - receivedSequence`, not the received sequence.
- The host transmit sequence follows the last received ACK sequence, so it
  alternates 0/1 rather than incrementing.
- Tandem writes use GATT Write Without Response.

Observed handshake notifications included `0xF5` (system status) and `0xE9`
(audio notify param), plus valid ACKs. No ANC/EQ control write was sent yet.

## Read-only feature query results

Command: `dotnet run --project src/Xm6Control.Cli -- features`

Confirmed responses:

```text
NCASM capability (type 21): Normal level 1..20 step 1; Voice level 1..20 step 1
EQ capability (type 0 -> response type 4):
  Off, Heavy, Clear, Hard, Soft, GamingEq, Custom, UserSetting1, UserSetting2
EQ param (type 4):
  preset = UserSetting1; bands = 7,2,6,6,10,8,8,9,7,11
EQ status (type 4): no preset error codes
Power battery (type 0): 76%; second byte 0x00 (charging semantics pending)
```

No response was observed for NCASM status/param types 1, 19, 21, 22, or for
EQ status/param types 0, 1, 3. That does not prove the device lacks the state;
the correct inquired type or notification path is still unknown. The answered
read-only queries are enough to start the ANC/EQ state-read UI work, but ANC
current state still needs a confirmed query/notification path.

## EQ write verified

The `eq` command reads the current EQ, writes `EQEBB_SET_PARAM` (0x58) with the
requested preset/bands, then reads back with `EQEBB_GET_PARAM` (0x56).

Confirmed on WH-1000XM6:

```text
before:    UserSetting1 bands = 7,2,6,6,10,8,8,9,7,11
requested: UserSetting1 bands = 6,2,6,6,10,8,8,9,7,11
after:     UserSetting1 bands = 6,2,6,6,10,8,8,9,7,11
verified:  true

second write restored band 0 to 7; read-back matched again.
```

The set command itself did not return a direct response (0x58 timed out), but
the post-write 0x56 read-back proved both the change and the restore.

For built-in presets the APK sends `bandCount = 0`, letting the headphone apply
its stored curve. Verified defaults include Heavy `10,10,5,5,6,6,6,6,6,6`,
Clear `6,6,6,6,6,6,6,8,8,9`, Hard `10,10,5,5,6,6,6,8,8,9`, Soft
`3,4,5,7,7,7,6,6,4,3`, and GamingEq `0,4,8,9,7,5,7,9,6,4`. Only Custom sends the
slider bands.

## NC/ASM write verified

The XM6 reports its NC/ASM state with `NCASM_NTFY_PARAM` (0x69) using inquired
type **25**, not type 21. Type 21 is the capability type. Current state layout:

```text
69 19 changeStatus onOff mode ambient level noiseAdaptive sensitivity
example: 69 19 01 01 00 01 14 01 00
         onOff = On, mode = NC, ambient = Voice, level = 20,
         noiseAdaptive = On, sensitivity = Standard
```

The `anc` command reads type 25, writes `NCASM_SET_PARAM` (0x68) type 25, then
reads back. Verified live:

```text
off mode: onOff=0                       -> verified
nc on:    onOff=1, mode=NC              -> verified
asm:      level 15, noiseAdaptive=On,
          sensitivity=High, voice focus  -> verified
asm:      level 10, noiseAdaptive=Off,
          sensitivity=Low, voice focus off -> verified
```

When switching ASM -> NC, the device preserves the last ASM ambient/level/
noise-adaptive values; set ASM first then NC to restore the exact underlying
state.

## Multipoint and auto power off write results

Multipoint is `GENERAL_SETTING2` (`MULTIPOINT_SETTING`, BooleanType). The live
state was On. Writing On was verified by `GS_GET_PARAM`; writing Off returned a
`0xD9` notification and read-back stayed On while LDAC was available / two
devices were connected. The UI treats this as unverified rather than success.

The XM6 UI exposes auto power off as two states only, so the app uses the
wearing-detection path exclusively:

```text
type 5: WhenRemovedFromEars (0x10), Disable (0x11)
```

Both writes were verified by read-back. The APK's time-based type 4 is not
exposed in the UI and is no longer written.

## Listening mode implementation

The Android UI's “Listening Option” is built from audio effects rather than a
single command:

- Standard: turn BGM and Cinema off; EQ is left untouched.
- Background Music: `AUDIO_SET_PARAM` BGM On plus `RoomSize` (Small / Middle /
  Large -> My Room / Living Room / Cafe).
- Cinema: `UPMIX_CINEMA` On, with an `UPMIX_SERIES` fallback for firmware that
  exposes the newer series item list.

BGM uses inquired type 9 when supported, otherwise type 3. The new UI page and
`listening` CLI command read back BGM/Cinema state after writing. Live write
verification is pending because the headphones were powered off when the
feature was added.

## Classic RFCOMM finding

The APK contains a Classic SPP Tandem transport, not only BLE:

- `ep0.c`: creates `BluetoothSocket` with `createRfcommSocketToServiceRecord`.
- `dp0.a`: reads/writes the Tandem stream over the socket.
- `lm0.i`: table1 RFCOMM UUID `96CC203E-5068-46AD-B32D-E316F5E069BA`.
- `lm0.i`: table2 RFCOMM UUID `956C7B26-D49A-4BA8-B03F-B17D393CB6E2`.

Windows RFCOMM results for WH-1000XM6:

```text
table1 96CC203E...: not exposed by WH-1000XM6
table2 956C7B26...: exposed and connected
table2 support-function request: connected, then connection closed / no response
```

So Classic-only control is **not proven**. Table2 lacks NCASM and EQ/EBB in the
APK, so even if it worked it would not cover ANC/EQ. Table1 is required for the
MVP feature set, and it is not exposed in the current Windows SDP view.

The Alternative A2DP Driver was disabled and the test was repeated:

```text
table1 96CC203E...: still not exposed
table2 956C7B26...: still connects, still no handshake response
```

So the driver is not the cause of the missing table1 service or the table2
handshake failure. Classic-only remains blocked by the headphone service set.

## Next action

1. The LE Tandem handshake is working. Continue on the LE transport.
2. Add read-only NCASM/EQ/EBB capability/status queries and validate the parsed
   feature set before any write.
3. Implement ANC/EQ/auto-power-off writes one feature at a time, with a
   read-back query after each change.
4. Keep the Classic path documented but blocked: table1 is still not exposed,
   and table2 does not carry NCASM/EQ.

## Report files

- Redacted Tandem report: `docs/tandem-xm6.json`
- Redacted GATT probe: `docs/probe-xm6.json`
- Protocol notes: `docs/protocol-xm6.md`








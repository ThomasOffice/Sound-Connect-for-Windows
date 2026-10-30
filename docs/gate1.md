# Gate 1 - Windows transport spike

Date: 2026-10-28
Result: **PASS for GATT transport and device-info reads**

## Environment

- Windows 11 build 26300 x64
- Intel Bluetooth radio with Intel Bluetooth LE Audio offload
- .NET 10 SDK 10.0.204
- Target framework `net10.0-windows10.0.26100.0`
- WH-1000XM6 paired with Windows
- BLE advertisements visible as `LE_WH-1000XM6`

## BLE advertisement

Observed with `Xm6Control.Cli scan`:

- Name: `LE_WH-1000XM6`
- Connectable: yes
- RSSI: about -38 to -43 dBm
- Advertised service UUIDs:
  - `0000fd82-0000-1000-8000-00805f9b34fb`
  - `dc405470-a351-4a59-97d8-2e2e3b207fbb`
  - `0000fd2a-0000-1000-8000-00805f9b34fb`
- Address is a resolvable random BLE address and changes across sessions. Use
  `scan` before probing; do not commit the address.

## GATT result

`Xm6Control.Cli probe` opened the device and enumerated:

- 14 services
- 45 characteristics
- Device name: `LE_WH-1000XM6`
- Paired state observed: true
- Connection status during enumeration: Disconnected

Device Information Service (0x180A) reads:

| Characteristic | Value |
|---|---|
| Manufacturer Name | Sony |
| Model Number | WH-1000XM6 |
| Firmware Revision | 3.1.5 |
| Software Revision | 3.1.5 |
| Serial Number | not exposed / not read |
| PnP ID | `02 4c 05 8a 0f 15 03` |

PnP ID decodes as vendor source USB-IF, vendor `0x054C` (Sony), product
`0x0F8A`. This is consistent with the WH-1000XM6.

Battery Service 0x180F was not present in the enumerated services. Battery is
therefore likely exposed through a Sony service or a protected characteristic
and needs capture-driven mapping.

## Candidate control services

| Service UUID | Characteristics | Notes |
|---|---|---|
| `dc405470-a351-4a59-97d8-2e2e3b207fbb` | Write `bfd869fa-a3f2-4c2f-bcff-...`; Notify `2a6b6575-faf6-418c-923f-...` | Advertised; prime Tandem/control candidate |
| `5b833e06-6bc7-4802-8e9a-...` | Write `5b833c10-...`; Notify `5b833c12-...` | Sony-style service; CCCD succeeded |
| `5b833e26-6bc7-4802-8e9a-...` | Write `5b833c11-...`, `5b833c1c-...`; Notify `5b833c13-...`, `5b833c1b-...` | CCCD succeeded |
| `5b833e27-6bc7-4802-8e9a-...` | Read `5b833c68-...` | Read returned `00` |
| `f76acb00-7cab-495f-bb1a-...` | WriteWithoutResponse `...01/04/07`; Notify `...02/03/05/06/08/09` | CCCD succeeded with `GattSession` |
| `45c93e07-d90d-4b93-a9db-...` | WriteWithoutResponse `45c93c15-...`; Notify `45c93c16/17-...` | CCCD succeeded with `GattSession` |
| `11c8b310-80e4-4276-afc0-...` | `2b01-...` write/notify/indicate | CCCD succeeded |
| `28bc862f-87d2-457b-b45a-...` | `d614da49-...` write/notify/indicate | CCCD succeeded |
| `76c13020-fe8f-416a-b4c3-...` | Write `76c13021-...`; Read `76c13022-...` | Read 25 bytes; purpose unknown |
| `0000fe2c-...` | Fast Pair characteristics | Google Fast Pair, not Sony control |
| `15190001-12f4-c226-88ed-...` | `8e0c0001-...` read/write/notify | Unknown |

Generic Access:

- Device Name `2A00`: `LE_WH-1000XM6`
- Appearance `2A01`: `43 09` (0x0943, headphones)

## Connection stability finding

The first GATT attempt often returned `Unreachable` or zero services. The cause
was not the address: the BLE device was present, but Windows had not established
a GATT session. Creating `GattSession.FromDeviceIdAsync(device.BluetoothDeviceId)`,
setting `MaintainConnection = true`, and waiting for `GattSessionStatus.Active`
made service discovery reliable. The CLI now does this and retries once.

With the session active, all CCCD writes succeeded except `00002a05`
(Service Changed), which still returned `UnauthorizedAccessException`.

## Warnings and limitations

- `00002a05` CCCD is unauthorized; not needed for feature control.
- Device address is random/resolvable; a scan is required before each probe.
- No control write was sent. Gate 1 only read public device information.
- Battery and Sony-specific feature state are not yet mapped.
- Service notifications were subscribed but no payloads arrived during the probe
  window; a capture-driven action is needed to emit feature notifications.

## Evidence

- Redacted report: `docs/probe-xm6.json`
- Redacted BLE scan: `docs/ble-scan.json`
- Full local reports (ignored by git): `artifacts/probe-xm6.full.json`,
  `artifacts/ble-scan.full.json`

## Gate 1 decision

Transport is viable on Windows: the headphones expose GATT, and model/firmware
reads work. Proceed to Gate 2, but first complete the Gate 0 HCI capture to map
the Tandem control service and frame format. Do not send writes to
`dc405470`/`5b833e06` until the captured frames identify them.

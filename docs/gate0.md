# Gate 0 - capture and analysis readiness

Date: 2026-10-28
Status: **tooling ready; live HCI capture skipped (phone not rooted)**

## Completed

### JADX CLI

- Installed JADX 1.5.6 from Maven Central dependencies.
- Maven: `tools/apache-maven-3.9.16`.
- JADX wrapper: `tools/jadx-cli/jadx.cmd`.
- Verified:

```text
java -cp "tools\jadx-cli\lib\*" jadx.cli.JadxCLI --version
1.5.6
```

The JADX runtime and dependency jars are ignored by git.

### Android HCI capture workflow

- `adb` version 37.0.1 is available at `C:\Android\sdk\platform-tools\adb.exe`.
- Capture helper: `tools/capture/Invoke-Xm6Capture.ps1`.
- Procedure documented in `tools/capture/README.md`.
- The helper enables `bluetooth_hci_snoop_log`, prompts for official-app actions,
  collects a bugreport, pulls the btsnoop log when permitted, and disables the
  setting again.

### Device identity and firmware

From the Windows GATT probe:

- Manufacturer: Sony
- Model: WH-1000XM6
- Firmware: 3.1.5
- Software: 3.1.5
- BLE advertising name: `LE_WH-1000XM6`

Evidence: `docs/probe-xm6.json`, `docs/ble-scan.json`.

## Not completed

A capture was attempted, but the Android phone is not rooted. Direct access to
`/data/misc/bluetooth/logs/btsnoop_hci.log` was denied, and the available
bugreport path did not yield a usable HCI log. The capture was therefore skipped
by decision and the investigation continued with direct APK decompilation.

Missing artifact: official-app HCI capture with the action labels from
`tools/capture/README.md`.

## Gate 0 decision

JADX, the capture helper, and firmware identity are complete. Live HCI capture is
blocked by the unrooted phone and is explicitly skipped. Continue with
source-derived protocol work in `docs/gate2.md` and use live GATT only when the
Tandem service is exposed.

Do not start speculative control writes before the handshake is confirmed.



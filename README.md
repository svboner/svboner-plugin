# SVBONER

> **NSFW / Adults Only.** This software controls adult intimate hardware. By using it you confirm you are of legal age in your jurisdiction.

SVBONER is a standalone Windows app that bridges [PHD2](https://openphdguiding.org/) (open-source astrophotography autoguiding software) with the [Buttplug.io](https://buttplug.io/) protocol, allowing you to map your guiding session's real-time telemetry — guide error, star loss events, and more — to vibration intensity on a connected wireless toy.

The name is a crude play on the astronomy brand SVBONY. It is not affiliated with SVBONY in any way.

---

## Status

Early development / MVP. Vibrating toys only for now.

---

## Prerequisites

Before running SVBONER you need three things running:

1. **PHD2** with its event server enabled — in PHD2, go to `Tools > Enable Server`. It listens on TCP port 4400 by default.
2. **Intiface Central** — download from [intiface.com/central](https://intiface.com/central). Start it and make sure your device is connected and the server is running (default WebSocket port 12345).
3. **SVBONER** — run `Svboner.exe`. A browser tab will open automatically at `http://localhost:8787`.

---

## Setup & Usage

1. Open the SVBONER web UI (auto-opens on launch, or go to `http://localhost:8787`).
2. On the **Connections** panel: verify PHD2 and Intiface show as connected. If not, check the addresses under Settings.
3. On the **Devices** panel: select which connected toy to drive.
4. On the **Mapping** panel: configure your continuous signal mapping and/or event triggers.
5. Click **Enable Output** to start. The live intensity gauge will show what is being sent to the device.
6. The red **STOP** button immediately silences all output and disables the engine.

---

## Mapping

**Continuous mapping** tracks a live PHD2 signal and maps it linearly to vibration intensity:
- Signals: `RMS Error (arcsec)`, `Total Error`, `RA Error`, `Dec Error`, `SNR`, `Avg Distance`
- Set input range (e.g. 0–2 arcsec) and output range (e.g. 0%–80%)
- Set `Output Low > Output High` for inverse mapping (better guiding = more vibration)
- Smoothing factor softens rapid changes

**Event triggers** fire a timed vibration burst when a discrete event occurs:
- `Star Lost`, `Alert (error)`, `Alert (warning)`, `Settle Failed`, `Guiding Stopped`, `Lock Position Lost`

**Global settings:**
- Master intensity cap (hard ceiling on all output)
- "Only vibrate while actively guiding" toggle
- Update throttle and ramp rate

---

## Safety & Consent

- **Consent is mandatory.** All parties must be informed and consenting before use.
- The app defaults to output **disabled** on startup. You must explicitly enable it each session.
- The **STOP** button is always visible and immediately kills all output.
- The master intensity cap defaults to 80% — raise it deliberately, not accidentally.
- This software comes with no warranty. Use responsibly.

---

## Building from Source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
git clone https://github.com/svboner/svboner-plugin.git
cd svboner-plugin
dotnet build Svboner.sln
dotnet run --project src/Svboner.App
```

---

## License

[MIT](LICENSE)

# SmartX — PROG7312 Part 2

Tapiwa Sango | ST10276025 | PROG7312 Programming 3B

SmartX is an enterprise-grade IoT Edge Gateway built with **.NET 10**, **WPF**, and an **ASP.NET Core Web API**. Part 2 evolves the Part 1 ingestion gateway into a real-time command stream with priority-queued critical alerts, undo-able manual overrides, live telemetry visualisation, and a predictive action engine that learns from user search and command history.

---

## Table of Contents

- [What Changed From Part 1](#what-changed-from-part-1)
- [Key Features (Part 2)](#key-features-part-2)
- [Data Structures Reference](#data-structures-reference)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Running the Application](#running-the-application)
- [Docker (optional)](#docker-optional)
- [Firebase Authentication (optional)](#firebase-authentication-optional)
- [Testing Guide](#testing-guide)
- [API Endpoints](#api-endpoints)
- [Demo Credentials](#demo-credentials)
- [AI Usage Declaration](#ai-usage-declaration)

---

## What Changed From Part 1

Every item the lecturer flagged in Part 1 feedback is addressed:

| Part 1 Feedback | Fix in Part 2 |
| --- | --- |
| Login accepted matching email **or** password alone | `AuthService.ValidateCredentialsAsync` requires **both** to match |
| Token was never checked by any endpoint | Every endpoint calls `IsAuthed(r)` — invalid or expired tokens return `401 Unauthorized` |
| Hardcoded login was flagged as unacceptable | `AuthService` is **Firebase-ready** — a single config flag switches to Firebase Identity Toolkit; local fallback exists only so markers can run without supplying keys |
| MAC address validation only checked for a colon | Full regex `^[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5}$` enforced client-side and re-validated server-side |
| `PowerMeterValue` was missing equality and comparison operators | Full set: `+ - * /`, `> < >= <= == !=` against both `PowerMeterValue` and `double`, plus `IEquatable<T>` and `IComparable<T>` |
| AES-256 used a fixed all-zero IV | `aes.GenerateIV()` produces a **fresh random IV per file**, prepended to the ciphertext |
| No file type or size check | `EncryptionService.ValidateFile` enforces a 5 MB cap and an allow-list of extensions |
| Upload only showed a success popup | The API parses `.txt`/`.csv` uploads as `MAC,Name,Location,Category` and inserts every valid row into the `DeviceRegistry` dictionary; the grid then refreshes automatically |
| Alert feed was driven by manual typing | `MeshMatrixView` and `CommandStreamView` raise alerts **automatically** when telemetry crosses a threshold |

---

## Key Features (Part 2)

### Tabbed navigation

- **Tab 1** — Sensor Ingestion & Telemetry (Part 1 preserved, plus a Part 1 archive strip linking to the mesh radar, operator overloads, and gateway overview).
- **Tab 2** — Real-Time Command Stream & History.
- **Tab 3** — Network Topology & Mesh Routing, visibly **disabled** with a tooltip stating it is reserved for the final PoE.

Both tab views are cached in memory, so switching tabs never loses filter state, selected devices, or scroll position.

### Real-Time Command Stream

- Live **sparkline chart** redrawing every two seconds from the API's `SensorTimeline` (a `SortedDictionary` keyed on `(timestamp, unique-id)`).
- **Live queue depth pills** — `FIFO: N` and `CRITICAL: N` — reflecting the two API queues in real time.
- **Search box** filters by MAC, name, or location; the term is recorded server-side for the predictive engine.
- **Category** and **Alert State** dropdowns filter by category or by alert state, as required by the brief.
- **Manual override** buttons (PUMP ON, PUMP OFF, RELAY TOGGLE) push commands onto the API's undo stack.
- **Undo last command** pops the stack. If the stack is empty, it returns *"Nothing to undo"* — no crash, ever.
- **Device simulator** — manual send normal, manual send critical, and an auto-stream that pushes real API calls every ~2 seconds.

### Predictive action engine

- Records every search and command as a **bigram** in an API-side event history.
- When the user types a search term, the engine looks up its historical follow-ups and surfaces the highest-confidence **suggested action** with a percentage score.
- Separately, it scans the live device registry and highlights any device that is out of range (moisture < 20%, power > 2000 W, temperature > 35 °C) or silent for over three minutes — **before** the user searches for them.
- Both appear in the clearly labelled **AUTOMATED INSIGHTS** panel.

### Preserved Part 1 work

- Mesh Signal Radar (`MeshMatrixView`) — auto-firing alerts from telemetry thresholds, real API pushes per tick.
- Operator Overloads (`TelemetryStreamView`) — the `PowerMeterValue` struct, jagged arrays, and recursive `DeploymentNode` validation demos.
- Gateway Overview (`GatewayMenuView`) — the original pillar cards, all still functional.

All three are reachable from the Part 1 archive strip inside Tab 1, opened as overlays so the tab bar stays visible and navigation is never lost.

---

## Data Structures Reference

Every data structure required by the brief is a **dedicated, clearly named class** in `SmartX.Api/DataStructures/`.

| Class | Underlying type | Where used | Purpose |
| --- | --- | --- | --- |
| `TelemetryQueue` | `Queue<T>` (via `ConcurrentQueue`) | `POST /api/telemetry/ingest` | FIFO processing of incoming telemetry packets |
| `CriticalAlertQueue` | `PriorityQueue<CriticalAlert, int>` | `POST /api/telemetry/ingest` | Critical alerts **bypass** the FIFO queue; lower number = higher urgency |
| `CommandUndoStack` | `Stack<CommandRecord>` | `POST /api/commands`, `POST /api/commands/undo` | LIFO history for the operator's Undo feature; `TryUndo` checks `Count == 0` so it never crashes |
| `DeviceRegistry` | `Dictionary<string, DeviceRecord>` (via `ConcurrentDictionary`) | All telemetry and device endpoints | O(1) device lookup keyed on normalised MAC address |
| `SensorTimeline` | `SortedDictionary<(DateTime, Guid), TelemetryPacketRecord>` | `POST /api/telemetry/ingest`, `GET /api/telemetry/timeline` | Chronological history; composite key handles duplicate timestamps |
| `DisconnectedNodes` | `HashSet<string>` | `POST /api/telemetry/ingest`, `GET /api/alerts/active` | Guarantees no duplicate alert for the same `(MAC, reason)` pair |
| `PredictiveEngine` | `Dictionary<string, Dictionary<string, int>>` | `POST /api/search`, `GET /api/insights` | Bigram co-occurrence model for action suggestions plus live out-of-range detection |

---

## Project Structure

```
prog7312-Part2/
├── README.md
├── Dockerfile
├── docker-compose.yml
├── .dockerignore
├── sample-devices.txt
└── SmartX/
    ├── SmartX.slnx
    ├── SmartX/                              # WPF desktop client
    │   ├── Controls/
    │   │   └── SparklineChart.xaml(.cs)     # Live telemetry chart
    │   ├── Collections/
    │   │   └── TelemetryBufferCollection.cs # Jagged arrays + generic buffer
    │   ├── Models/
    │   │   ├── CommandRecord.cs
    │   │   ├── DeploymentNode.cs            # Recursive hierarchy validation
    │   │   ├── Device.cs
    │   │   ├── Prediction.cs
    │   │   ├── ReportedIssue.cs
    │   │   ├── SensorRegistration.cs
    │   │   └── TelemetryPacket.cs           # TelemetryPacket<T> + PowerMeterValue
    │   ├── Services/
    │   │   ├── ApiService.cs
    │   │   ├── AuthSession.cs
    │   │   └── NavigationService.cs         # Central navigation
    │   ├── Views/
    │   │   ├── LandingView.xaml(.cs)
    │   │   ├── LoginView.xaml(.cs)
    │   │   ├── MainShellView.xaml(.cs)      # Tabbed shell
    │   │   ├── CommandStreamView.xaml(.cs)  # Part 2 dashboard
    │   │   ├── SensorIngestionView.xaml(.cs)
    │   │   ├── GatewayMenuView.xaml(.cs)    # Part 1 preserved
    │   │   ├── MeshMatrixView.xaml(.cs)     # Part 1 preserved
    │   │   └── TelemetryStreamView.xaml(.cs)# Part 1 preserved
    │   ├── App.xaml(.cs)
    │   └── MainWindow.xaml(.cs)
    └── SmartX.Api/                          # ASP.NET Core Web API
        ├── DataStructures/
        │   ├── CommandUndoStack.cs
        │   ├── CriticalAlertQueue.cs
        │   ├── DeviceRegistry.cs
        │   ├── DisconnectedNodes.cs
        │   ├── PredictiveEngine.cs
        │   ├── SensorTimeline.cs
        │   └── TelemetryQueue.cs
        ├── Models/
        │   └── CommandStreamModels.cs
        ├── Services/
        │   ├── AuthService.cs
        │   └── EncryptionService.cs
        ├── Uploads/                         # Encrypted .enc payloads
        ├── Program.cs
        └── appsettings.json
```

---

## Prerequisites

- **OS:** Windows 10/11 (required for WPF)
- **IDE:** Visual Studio 2022 (v17.8 or newer) with:
  - `.NET desktop development` workload
  - `ASP.NET and web development` workload
- **SDK:** .NET 10 SDK — [download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Optional:** Docker Desktop (for containerised API)

---

## Running the Application

### 1. Clone and open

```bash
git clone https://github.com/EMGPRS/prog7312-2026-prog7312-part-2-sango-tapiwa.git
cd prog7312-Part2/SmartX
```

Open `SmartX.slnx` in Visual Studio.

### 2. Set multiple startup projects

- Right-click the solution in **Solution Explorer** → **Set Startup Projects…**
- Select **Multiple startup projects**
- Set **`SmartX.Api`** to **Start**
- Set **`SmartX`** to **Start**
- Click **Apply** → **OK**

### 3. Press F5

The API launches at `http://localhost:5245` and the WPF client opens on the landing page.

### 4. Log in

Use `guest@smartx.io` / `admin123` (or click **Fill Demo Credentials**).

### Alternative: run from CLI

```bash
# Terminal 1 — API
cd SmartX/SmartX.Api
dotnet run

# Terminal 2 — WPF client
cd SmartX/SmartX
dotnet run
```

---

## Docker (was optional but was done)

The API can be containerised. The WPF client cannot which WPF requires a Windows desktop session. So the client continues to run from Visual Studio and connects to the containerised API on the same port.

### Start the container

```bash
# From the repo root (the folder containing docker-compose.yml)
docker compose up --build
```

On first run this takes 2–4 minutes; subsequent builds are seconds.

Confirm the API is live: `http://localhost:5245/swagger`

### Run the WPF client

In Visual Studio, set **Single startup project** → **`SmartX`** (the WPF project, **not** `SmartX.Api`). Press F5.

> **Do not** also start `SmartX.Api` from Visual Studio while Docker is running — both try to bind port 5245 and one will fail.

### Stop

```bash
docker compose down
```

Encrypted uploads persist to `SmartX/SmartX.Api/Uploads/` on the host via a volume mount.

---

## Firebase Authentication (optional)

The API is Firebase-ready. A single config flag switches `AuthService` from the local demo store to Firebase's Identity Toolkit REST endpoint.

### Enable Firebase

1. Create a project at [console.firebase.google.com](https://console.firebase.google.com).
2. Enable **Authentication → Sign-in method → Email/Password**.
3. Add a test user under **Authentication → Users**.
4. Copy the Web API key from **Project settings → Your apps → Config**.
5. Edit `SmartX.Api/appsettings.json`:

```json
"Firebase": {
  "ApiKey": "YOUR_WEB_API_KEY",
  "UseFirebase": true
}
```

6. Restart the API. Login now validates against Firebase.

When `UseFirebase: true`, the local demo users become inactive and only Firebase-registered accounts can log in. When `UseFirebase: false` (the committed default), the app runs fully offline with the demo credentials below.

---

## Testing Guide

### Login

- Wrong password → error message shown, no crash.
- Correct credentials → shell loads with Tab 2 active.

### Tab navigation

1. Click **Tab 1** → ingestion view plus the Part 1 archive strip.
2. Click **Tab 2** → command stream dashboard.
3. Click **Tab 3** → nothing happens; tab is greyed out with a tooltip.
4. Rapidly switch Tab 1 ↔ Tab 2 — state (search term, selected device, chart) is preserved.

### Queue<T> — FIFO

1. On Tab 2, click **Send Normal** five times.
2. The **`FIFO: N`** pill in the top right increments.
3. Call `GET /api/telemetry/queue` in Swagger — the `standard` array contains all five packets in insertion order.

### PriorityQueue — critical bypass

1. Click **Send CRITICAL**.
2. The **`CRITICAL: N`** pill increments immediately, even while FIFO has pending items.
3. Call `POST /api/telemetry/process-next` — the next processed packet is the **critical** one, not the earliest FIFO item. This proves critical alerts bypass the standard queue.

### Stack<T> — undo, never crashes

1. Select a device in the grid.
2. Click **PUMP ON**, then **RELAY TOGGLE**. Each pushes a record onto the stack; the history panel shows them in reverse order.
3. Click **UNDO LAST COMMAND** — the most recent command is popped. Stack depth drops by one.
4. Click **UNDO** repeatedly until the stack is empty. When it is empty, the API returns *"Nothing to undo"* and the app **does not crash**. Click **UNDO** five more times to prove it.

### Dictionary — O(1) device lookup

1. The device console on Tab 2 is populated via dictionary lookup, not list iteration.
2. In Swagger, call `GET /api/devices/{mac}` with any MAC from the grid. The response returns instantly regardless of how many devices exist.

### SortedDictionary — chronological timeline

1. Send several telemetry packets (normal and critical).
2. Call `GET /api/telemetry/timeline?take=50`.
3. The response is **always** sorted ascending by `timestamp` — that is the `SortedDictionary` guarantee.

### HashSet — unique alerts

1. Send **CRITICAL** for the same device twice.
2. In the **AUTOMATED INSIGHTS** panel the alert appears only **once** — the HashSet gate blocks the duplicate.
3. Call `GET /api/alerts/active` — each `(MAC, reason)` entry appears exactly once.

### Simulating a high-priority alert (end-to-end)

1. Navigate to **Tab 2 — Real-Time Command Stream**.
2. Click **Auto-Stream** in the bottom simulator bar. Every ~2 seconds the simulator pushes readings to the API; ~30% of them are forced out-of-range.
3. Watch the **`CRITICAL`** pill climb while **`FIFO`** only grows for normal readings.
4. The **AUTOMATED INSIGHTS** panel updates in real time with out-of-range devices.

### Predictive action engine

1. Type `Pump` in the search box (3+ characters).
2. Select a device, click **PUMP ON**.
3. Repeat the search-then-command pair three or four times.
4. Clear the search box and type `Pump` again — the **AUTOMATED INSIGHTS** panel now suggests `PUMP ON` with a historical confidence count derived from your recorded actions.

### File upload — auto-import

1. On Tab 1, select any sensor row.
2. Click **Select Device Log / Config File** → choose `sample-devices.txt`.
3. Click **Upload & Encrypt Stream**.
4. A popup shows `devicesImported: 5`. The grid refreshes with the new devices.
5. Check `SmartX.Api/Uploads/` — a new `.enc` file appeared with a GUID prefix. Upload the same file again; the new `.enc` file **differs in bytes** from the first, proving the random IV.

### Part 1 preserved

1. Tab 1 → **Mesh Signal Radar →** → radar view opens as an overlay. Alerts fire automatically when readings cross thresholds.
2. Click **← Back to Dashboard** — returns to the shell.
3. Tab 1 → **Operator Overloads →** — operator overload demo works; click **Simulate Overloaded Telemetry Addition** to see the aggregation.
4. Tab 1 → **Module Overview →** — original pillar cards. **Pillar 01 Launch** opens ingestion; **Pillar 02 Launch** returns to Tab 2.

---

## API Endpoints

All endpoints except `/api/auth/login` require a `Authorization: Bearer <token>` header.

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/api/auth/login` | Authenticate; returns a session token |
| GET | `/api/devices` | All devices (dictionary lookup) |
| GET | `/api/devices/filter?category=&alertState=&search=` | Filtered device list |
| GET | `/api/devices/{mac}` | Single device by MAC (O(1)) |
| POST | `/api/sensors` | Register a device (full MAC regex validated) |
| POST | `/api/sensors/{mac}/upload` | Multipart file upload; AES-256 random IV; `.txt`/`.csv` rows auto-imported |
| POST | `/api/telemetry/ingest` | Ingest one telemetry packet |
| GET | `/api/telemetry/queue` | Snapshot of both queues |
| GET | `/api/telemetry/timeline?mac=&take=` | Sorted timeline |
| POST | `/api/telemetry/process-next` | Pop the priority queue first, then FIFO |
| POST | `/api/commands` | Push a command onto the undo stack |
| POST | `/api/commands/undo` | Pop the undo stack (safe when empty) |
| GET | `/api/commands/history` | Current stack contents |
| GET | `/api/alerts/active` | Active unique alert keys (HashSet) |
| POST | `/api/search` | Record a search term, return suggestions |
| GET | `/api/insights` | Live out-of-range devices |
| GET/POST | `/api/issues` | Manual issue log (Part 1 preserved) |

Swagger UI is available at `http://localhost:5245/swagger`.

---

## Demo Credentials

Default (`UseFirebase: false`):

| Email | Password |
| --- | --- |
| `guest@smartx.io` | `admin123` |
| `operator@smartx.io` | `SmartX@2026` |

When `UseFirebase: true`, log in with the account you created in the Firebase console.

---

## YouTube Demo

https://youtu.be/d_EsKtkrFWA

---

## AI Usage Declaration

Generative AI tools; Gemini, ChatGPT, and DeepSeek were used to assist with UI design concepts, architecture discussions, README formatting, and error diagnosis during development. All AI-generated content was treated as reference material; every design decision, code change, and architectural choice in the final submission is my own work. This declaration applies equally to Part 1 and Part 2 of the assignment.



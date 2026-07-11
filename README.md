# Procedural EASA-Compliant Airspace Design & Acoustic Optimization Tool around Vertiports

An interactive, simulation-driven 3D visualization and planning tool developed within the Institute of Flight Systems and Automatic Control (FSR) at the Technical University of Darmstadt. This software platform automates the procedural generation of high-fidelity urban digital twins to compute EASA-compliant protected airspace volumes and evaluate flight corridors under strict structural constraints.

## 🚀 Key System Features
* **Procedural 3D Environment Generation:** Integrates the Mapbox Unity SDK and OpenStreetMap (OSM) vector data layers to dynamically render metropolitan topology based on real-world coordinates.
* **EASA-Compliant Obstacle-Free Volumes (OFV):** Automatically calculates and casts 3D volumetric meshes (cylinder base, sloping approach/departure surfaces, and horizontal brims) conforming to strict EASA PTS-VPT-DSN guidelines.
* **Infrastructure Footprint Cleanup:** Features an autonomous intersection check using physics colliders to programmatically clear out and disable existing map architecture conflicting with the active vertiport footprint.
* **Clamped Spatial Height Estimation:** Dynamically scans the entire active scene map for the tallest obstacle structure using bounding-box checks (`bounds.size.y`) to determine the high hover ceiling ($h_2$), safely bound between a 30m baseline and a 150m maximum ceiling.
* **Airspace Filtering:** Employs a multi-objective routing architecture. A 360-degree `BoxCast` radial sweep first filters out physically blocked flight vectors, identifying optimal flight directions safely cleared of physical urban obstacles.

---

## 🛠️ Unified Technical Requirements & Core Setup

### 1. Unity Project Initialization
* **Unity Version:** Developed and tested within the Unity3D engine using the Universal Render Pipeline (URP).
* **Text Rendering:** Implemented utilizing TextMeshPro (TMP) via Signed Distance Field (SDF) rendering to preserve the visual anti-aliased clarity of telemetry readouts and coordinate inputs overlaid on the map canvas.

### 2. Mapbox SDK Configuration
1. Import the official Mapbox Unity `.unitypackage` into your scene.
2. Remove deprecated AR-related asset folders to avoid compiler errors in modern Unity versions.
3. Open the Mapbox Setup window and input a valid Mapbox developer API Access Token.
4. Configure the map component to use the **"Mapbox Streets V8 with Building Ids"** tileset to access structural metadata (`height`, `min_height`, and `extrude` properties).
5. Set the map extent option to **"Range Around Center"** with an extent of 2 tiles in each cardinal direction. At zoom level 17, this creates a stabilized $7\times7$ grid covering a total evaluation footprint of approximately $3.06 \times 3.06\text{ km}$ ($9.36\text{ km}^2$).

### 3. Critical Unity Physics Global Settings
To align the built player environment with local editor validation and prevent WebGL timing discrepancies, ensure these global settings are applied under **Project Settings > Physics**:
* **Auto Sync Transforms:** `ON` (Forces the physics engine to instantly update newly spawned Mapbox building colliders to their actual spatial positions).
* **Queries Hit Backfaces:** `ON` (Ensures raycasts and physics sweeps register procedural geometry safely regardless of flipped triangle normals).

---

## 📂 Source Code Implementation
The underlying production architecture and algorithm flows can be reviewed directly within the repository scripts:
* **Core Application Controller:** [`Assets/Scrips/MapController.cs`](https://github.com/YashT-7/Verti/blob/main/Assets/Scrips/MapController.cs) — Handles combined coordinate input token parsing, stabilization delays, and geometric safety envelope calculations.
* **System Automation Orchestrator:** [`Assets/Scrips/VertiportDirector.cs`](https://github.com/YashT-7/Verti/blob/main/Assets/Scrips/VertiportDirector.cs) — Drives the centralized 360-degree discretized radial sweeps, directional optimization, and 3D UI navigation indicators.

---

## 📋 Real-World Operational Example

This example demonstrates how the system executes a real-world analysis, using empirical data gathered from testing inside dense urban canyon configurations.

### 1. Execution Sequence Input
* **Target Geographic Location:** Trianon Park / Avenida Paulista corridor, São Paulo, Brazil.
* **Input Tokens Entered:** ` -23.562548, -46.557598 ` entered into the combined coordinate input field.
* **Vertiport Layout Selection:** "Scenario 1: Rectangular Vertiport" or "Triangular Vertiport" layout chosen from the Dropdown menu.
* **Orientation Value Input:** ` 90 ` degrees entered to orient the physical vertiport layout pad relative to North before the simulation starts.

![User Input Interface.png](https://github.com/YashT-7/Verti/blob/main/User%20Input%20Interface.png)

### 2. Runtime Simulation & Scanning Actions
1. Clicking **"Show OFV"** triggers an explicit programmatic call to the Mapbox Web Services API.
2. The user interface switches to a hidden processing state, displaying an intentional 4-second synchronization sequence while 3D meshes are extruded and settle.
3. The infrastructure cleanup block executes, identifying that an unmapped structural boundary mesh overlaps the target landing space. The script programmatically deactivates this conflicting object.
4. The height estimation scan queries the scene bounds, identifying surrounding skyscraper architecture (such as the Citibank Tower canyon). It registers the maximum structural height.

### 3. Final Output & Visual Telemetry Interpretation
The 4-second loading loop terminates, caching the structural calculations and presenting the visual evaluation output:
* **Max Building Height Extracted:** Formatted and reported as **`95.4m`**.
* **Airspace Safety Classification:** Evaluated against baseline thresholds. Because the maximum height exceeds 15 meters without breaching the aircraft's transition ceiling, the canvas text flags the airspace layout status as **`Cone Height: 95.4m (Building Restricted)`**.
* **Visual Obstacle Surface Shell:** Discretizes and constructs a 3D translucent mesh centered on the FATO layout coordinates using 60 radial segments. Sectors penetrating building bounds are drawn with sharp visual "cut-outs" representing invalid paths.
* **Optimal Airspace Recommendation Summary:** The final optimization completes. A blue 3D directional arrow instantiates, hovering straight over the landing pad and pointing visually to a recommended flight path vector heading of **`222.5°`**—ensuring safe structural obstacle clearance.

![Visual Obstacle Free Volume Simulation Result](https://github.com/YashT-7/Verti/blob/main/Visual%20Obstacle%20Free%20Volume%20Simulation%20Result.png)

***

### 🎓 Academic Citation Details
This project was authored as part of an Advanced Design Project (ADP) at **Technische Universität Darmstadt**. 
* **Authors:** Yashkumar Hareshbhai Thummar, Bhargava Reddy, Mohamed Irshad Anis Syed, Seran Karadas
* **Supervisors:** Prof. Dr.-Ing. Uwe Klingauf, Lukas Biesalski, M.Sc.
* **Institute:** Institut für Flugsysteme und Regelungstechnik (Institute of Flight Systems and Automatic Control)

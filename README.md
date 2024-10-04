# Centralized Buoyancy Manager

## Overview

The `CentralizedBuoyancyManager` is a Unity component designed to manage buoyancy for multiple objects in a shared environment. Idea is to turn it into real physics buoyancy handling tool with HDRP water, but for now, we're only setting object positions at sea level. It can handle multiple pontoons (buoyant points) and is compatible with Unity's HDRP water system. This script utilizes Unity's Job System for efficient calculations.

## Features

- **Centralized Buoyancy Handling**: Manage buoyancy forces for multiple objects using a single component.
- **Job System Optimization**: Utilizes Unity's Job System for parallel buoyancy calculations, ensuring smooth performance.
- **HDRP Water System Compatibility**: Integrates with Unity's High Definition Render Pipeline (HDRP) water system.

## Requirements

- **Unity Version**: Unity 2022 or newer.
- **Rendering Pipeline**: Designed for use with HDRP (High Definition Render Pipeline).
- **Dependencies**: Requires the HDRP Water System and Unity's Job System.

## Installation

1. Clone this repository or download the ZIP.
    ```bash
    git clone https://github.com/yourusername/CentralizedBuoyancyManager.git
    ```
2. Import the scripts into your Unity project.
3. Ensure that HDRP is enabled and the Water Surface component is present in your scene.

## Usage

### Setting Up Centralized Buoyancy Manager

1. **Add the Manager to Your Scene**:
   - Attach the `CentralizedBuoyancyManager` script to an empty GameObject in your scene. This GameObject will manage all buoyant objects.

2. **Configure Buoyancy Settings**:
   - `Depth Before Submerged`: The depth at which pontoons are considered fully submerged.
   - `Displacement Amount`: The force exerted when fully submerged, determining how "buoyant" the objects are.
   - `Water Drag`: The drag applied to objects when submerged.
   - `Water Angular Drag`: The angular drag applied when submerged.

3. **Assign Water Surface**:
   - Link the `WaterSurface` component (from HDRP) to the `CentralizedBuoyancyManager` in the inspector.

### Registering Buoyant Objects

Buoyant objects must be registered with the `CentralizedBuoyancyManager` for them to be affected by buoyancy forces.

1. Attach a `Rigidbody` component to your buoyant object.
2. Create multiple child objects to act as pontoons. These should be positioned at points where the object should be supported by water.
3. Register the object by calling:

   ```csharp
   CentralizedBuoyancyManager.Instance.RegisterBuoyantObject(rigidbody, pontoons);

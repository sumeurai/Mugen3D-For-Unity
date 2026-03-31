# Mugen3D for Unity

This folder contains a Unity sample for the Mugen3D workflow:

1. Upload an image for background matting
2. Poll the matting result and cache a preview locally
3. Submit the processed image to Mugen3D for SQ/HQ generation
4. Download the generated model package
5. Preview the local result in Unity with the bundled `gsplat-unity` renderer

> This sample is intended for integration reference. Configure your API credentials before using the sample.

---

## Features

- `APIManager` wraps login, JSON requests, image upload, polling, and file download
- `APISettingsConfig` centralizes API credentials, endpoint paths, and polling interval settings
- A Project Settings entry is available at `SumeruAI/API Settings`
- The sample scene provides a task list UI for:
  - login
  - image selection
  - image matting submission
  - SQ/HQ generation
  - download progress tracking
  - local model preview
- Downloaded `.ply` model outputs can be previewed with the bundled `GsplatRenderer`
- Task state, preview images, and downloaded files are persisted locally between sessions

## Directory Overview

```text
Assets/SumeruAI
├─ Editor/                  Editor menu and Project Settings integration
├─ Resources/               Runtime-loaded API settings assets
├─ Runtime/
│  ├─ API/                  API manager and request/response models
│  ├─ Core/                 Base HTTP request helpers and config object
│  ├─ Gsplat/               Gsplat-related request data
│  └─ Utils/                General file helpers
├─ Samples/
│  ├─ Scenes/               Sample scene
│  ├─ Scripts/              End-to-end sample workflow UI and storage
│  └─ Prefabs/              Sample UI prefabs
└─ ThirdParty/
   ├─ Gsplat/               Gsplat settings assets
   └─ gsplat-unity/         Bundled third-party Gaussian splat renderer
```

## Setup

1. Open the project in Unity `2021` or later.
2. Ensure your project is using a supported Graphics API for `gsplat-unity`.
   - On Windows, go to `Edit > Project Settings > Player > Other Settings`
   - Disable `Auto Graphics API for Windows`
   - In `Graphics APIs for Windows`, add `Vulkan` or `Direct3D12`
   - Remove unsupported graphics APIs from the list
   - Unity requires a restart after switching the Graphics API
   - Other target platforms may require similar graphics API adjustments
3. Configure API credentials and related settings.
   - Default asset path: `Assets/SumeruAI/Resources/APISettingsConfig.asset`
   - You can also open `SumeruAI/API Settings` from the Unity menu.
   - If you create a new config asset, it must be placed under a `Resources` folder and named so it can be loaded as `APISettingsConfig`.
4. In most cases, you only need to fill in the following fields in `APISettingsConfig`:
   - `Access Key`
   - `Secret Key`
   - Create and obtain your `Access Key` and `Secret Key` from the `Developers` page at `https://www.sumeruai.us/`
   - Other configuration values are already filled with default settings in the sample.
   - Only change the remaining fields if you need a custom configuration.
5. The sample also includes the following configurable fields if needed:
   - `Base Url`
   - `login`
   - `imageMatting`
   - `mugen3dSq`
   - optional polling interval for Mugen3D results
6. Open the sample scene:
   - `Assets/SumeruAI/Samples/Scenes/Samples.unity`
7. Press Play and use the sample UI.

## Sample Workflow

1. Click `Login` to obtain an access token.
2. Click `Select Image` and choose a local image file.
3. The sample uploads the source image for matting and starts polling for the result.
4. When the matting output is ready, a preview image is cached locally.
5. Click `Generate SQ` to start the first 3D generation step.
6. After SQ is ready, you can:
   - download the SQ result
   - submit HQ generation
7. When a model package is available locally, click `View Model` to preview it in the built-in viewer.

## Runtime Notes

- All requests are asynchronous and routed through `BaseHttpRequest`
- `APIManager.Login()` stores the access token in memory and attaches it to subsequent protected requests
- Matting results are queried through `GetImageMattingResult()`
- Mugen3D results are queried through `GetMugen3DResult()`
- File downloads use `DownloadHandlerFile` and automatically create missing directories
- The viewer documentation currently assumes local `.ply` model preview

## Local Data

The sample stores generated task data outside `Assets` under:

`SumeruAI/Mugen3D`

It contains:

- `tasks.json` for persisted task state
- `Previews/` for cached matting preview images
- `Downloads/` for downloaded SQ/HQ model files and extracted viewer data

## Main Scripts

- `Runtime/API/APIManager.cs`: high-level API entry point
- `Runtime/Core/BaseHttpRequest.cs`: POST, GET, upload, and download helpers
- `Runtime/Core/APISettingsConfig.cs`: runtime API settings asset
- `Editor/APISettingsProvider.cs`: Project Settings UI for credentials
- `Samples/Scripts/WorkflowService.cs`: workflow orchestration helpers
- `Samples/Scripts/TaskListUI.cs`: end-to-end sample UI, polling, downloading, and model viewing
- `Samples/Scripts/GsplatLoader.cs`: loads local `.ply` output into a runtime `GsplatAsset`

## Notes

- This project requires Unity `2021` or later
- `gsplat-unity` is already bundled under `Assets/SumeruAI/ThirdParty/gsplat-unity`
- For more detailed renderer setup notes, supported render pipelines, and platform requirements, refer to the bundled `gsplat-unity` documentation or its GitHub repository: `https://github.com/wuyize25/gsplat-unity`
- The current sample uses `UnityEditor` file selection for choosing local images in the Editor
- Runtime file picking for standalone/mobile builds is not implemented yet
- The sample scene is designed as a functional reference, not a production-ready UI
- `RequestUtil.cs` still contains some legacy request models, but the active sample flow in this folder is image matting and Mugen3D generation

# Unity 6 URP Outline Feature (Render Graph API)

A modern, resolution-independent hard outline (cel-shading style) effect for Unity 6 Universal Render Pipeline (URP 17+), built completely from scratch using the new **Render Graph API**.

## 🌟 Features
* **Render Graph Ready:** Uses the modern `RecordRenderGraph` and `Blitter` API. No obsolete warnings (`Execute`, `GetTemporaryRT`, or `fullscreenMesh`).
* **Perfect Corners (Dilation):** Fixes the common issue of broken/disconnected outline corners on sharp geometry by checking 8 neighboring pixels and dilating the mask.
* **Resolution Independent:** The outline thickness remains visually consistent across different screen sizes (from 1080p to 4K mobile screens) by scaling with `_ScreenParams`.

## 📦 Installation
1. Download or clone this repository.
2. Drag and drop the three files (`OutlineFeature.cs`, `HardOutline.shader`, `Silhouette.shader`) into your Unity project.

## 🚀 How to Use
1. **Create Materials:** * Create a material named `Mat_Silhouette` and assign the `Hidden/OutlineSilhouette` shader.
   * Create a material named `Mat_HardOutline` and assign the `Custom/HardOutline` shader. Customize the color and thickness here.
2. **Setup Renderer:**
   * Find your active `UniversalRendererData` asset.
   * Click **Add Renderer Feature** and select **Outline Feature**.
3. **Configure:**
   * Assign a specific **Layer Mask** (e.g., "OutlineObjects") in the feature settings.
   * Drag your `Mat_Silhouette` and `Mat_HardOutline` into the corresponding slots.
   * Apply the chosen Layer to the objects you want to outline in your scene.
4. **Camera Setup:** Ensure **Post Processing** is checked on your Main Camera.

## 📝 Requirements
* Unity 6 (2023.3+)
* Universal Render Pipeline (URP) 17+

---
*Created as a modern replacement for legacy URP Outline tutorials.*

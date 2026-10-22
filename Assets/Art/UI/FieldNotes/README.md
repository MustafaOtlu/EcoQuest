# EcoQuest — Field Notes UI

Original, editable vector backgrounds made for EcoQuest. No external asset dependency.

Palette: warm paper #F4EFDF, forest #2F5842, sage #E8E8D7. Typography and icons stay separate from the backgrounds.

- Sources/: editable 128×128 SVG files. Open in Inkscape or another vector editor.
- Assets/Resources/EcoUI/FieldNotes/: matching antialiased PNG exports used by both uGUI and IMGUI.
- Export SVG edits as 128×128 transparent PNG to the matching resource path. Keep the outer 16 px intact for nine-slicing.
- Texture import: uncompressed, sRGB, no mipmaps, clamp, bilinear. Runtime sprites use 16 px borders.

Paper is for main cards/windows, card is for insets, button/button-hover for controls, selected for active tools/buttons.
The border export is optional; shadows are drawn by Unity so they stay consistent at every panel size.

using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using System.IO;

/// <summary>
/// Generates a 1024×1024 app icon for THE MAZE and assigns it to Player Settings.
/// Menu: THE MAZE → Generate App Icon
/// </summary>
public static class IconGenerator
{
    private const string IconPath = "Assets/AppIcon.png";
    private const int    Size     = 1024;

    [MenuItem("THE MAZE/Generate App Icon")]
    public static void GenerateIcon()
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);

        // Fill background — near-black with very slight warm tint
        FillRect(tex, 0, 0, Size, Size, new Color32(8, 5, 4, 255));

        // Draw stylised maze grid — a 5×5 grid of cells, some walls removed
        // to hint at a maze without looking like a QR code
        DrawMazePattern(tex);

        // Red/orange glow in the center — the "exit"
        DrawGlow(tex, Size / 2, Size / 2, 160, new Color(1f, 0.35f, 0.04f));

        // Outer vignette — darken edges
        DrawVignette(tex);

        // Border line — thin dark red frame
        DrawBorder(tex, 18, new Color32(140, 30, 8, 220));

        // Second inner border
        DrawBorder(tex, 36, new Color32(80, 18, 4, 140));

        // "THE MAZE" title text at bottom
        DrawTitle(tex);

        tex.Apply();

        // Save as PNG
        File.WriteAllBytes(Path.GetFullPath(IconPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.Refresh();

        // Re-import as sprite/texture with correct settings
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType         = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize      = 1024;
            importer.isReadable          = false;
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh();

        // Assign to Player Settings for all icon slots
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null) { Debug.LogError("[IconGenerator] Failed to load icon after save."); return; }

        // Android adaptive icon
        PlayerSettings.SetIcons(NamedBuildTarget.Android, new Texture2D[] { icon }, IconKind.Application);

        // Default / unknown target (covers other slots)
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new Texture2D[] { icon }, IconKind.Any);

        Debug.Log($"[IconGenerator] Icon generated and assigned: {IconPath}");
        EditorUtility.DisplayDialog("THE MAZE — Icon Generated",
            $"App icon saved to {IconPath} and assigned in Player Settings.\n\n" +
            "Verify: Edit → Project Settings → Player → Icon section.",
            "OK");
    }

    // -------------------------------------------------------------------------
    // Drawing helpers
    // -------------------------------------------------------------------------

    private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color32 col)
    {
        for (int py = y; py < y + h && py < Size; py++)
        for (int px = x; px < x + w && px < Size; px++)
            tex.SetPixel(px, py, col);
    }

    /// Draws a stylised maze — walls as dark lines on slightly lighter cells
    private static void DrawMazePattern(Texture2D tex)
    {
        // 7×7 cells, each cell 128px, 8px walls
        const int cells    = 7;
        const int cellSize = Size / cells;    // ~146px
        const int wall     = 8;

        // Cell fill — warm very dark brown
        var cellCol = new Color32(18, 11, 7, 255);
        for (int cy = 0; cy < cells; cy++)
        for (int cx = 0; cx < cells; cx++)
        {
            int px = cx * cellSize + wall;
            int py = cy * cellSize + wall;
            int pw = cellSize - wall * 2;
            FillRect(tex, px, py, pw, pw, cellCol);
        }

        // Horizontal walls (between rows) — remove some to carve passages
        // 1 = wall present, 0 = passage
        // Index [row][col] — row 0 is bottom wall of row 0
        int[,] hWalls = {
            { 1, 0, 1, 1, 0, 1, 1 },   // between row 0 and 1
            { 0, 1, 1, 0, 1, 0, 1 },   // between row 1 and 2
            { 1, 0, 0, 1, 1, 0, 1 },   // between row 2 and 3
            { 0, 1, 0, 0, 1, 1, 0 },   // between row 3 and 4
            { 1, 0, 1, 1, 0, 0, 1 },   // between row 4 and 5
            { 0, 1, 0, 1, 1, 0, 1 },   // between row 5 and 6
        };

        // Vertical walls (between cols) — 6 separators × 7 rows
        int[,] vWalls = {
            { 1, 0, 1, 1, 0, 1, 1 },   // between col 0 and 1
            { 0, 1, 0, 1, 1, 0, 0 },   // between col 1 and 2
            { 1, 1, 0, 0, 1, 1, 1 },   // between col 2 and 3
            { 0, 1, 1, 1, 0, 1, 0 },   // between col 3 and 4
            { 1, 0, 1, 0, 1, 0, 1 },   // between col 4 and 5
            { 0, 1, 0, 1, 1, 1, 0 },   // between col 5 and 6
        };

        var wallCol = new Color32(5, 3, 2, 255);

        // Draw horizontal walls
        for (int row = 0; row < cells - 1; row++)
        for (int col = 0; col < cells;     col++)
        {
            if (hWalls[row, col] == 1)
            {
                int px = col * cellSize;
                int py = (row + 1) * cellSize - wall / 2;
                FillRect(tex, px, py, cellSize, wall, wallCol);
            }
        }

        // Draw vertical walls
        for (int col = 0; col < cells - 1; col++)
        for (int row = 0; row < cells;     row++)
        {
            if (vWalls[col, row] == 1)
            {
                int px = (col + 1) * cellSize - wall / 2;
                int py = row * cellSize;
                FillRect(tex, px, py, wall, cellSize, wallCol);
            }
        }
    }

    /// Soft radial glow at center using Gaussian-like falloff
    private static void DrawGlow(Texture2D tex, int cx, int cy, int radius, Color col)
    {
        int r2 = radius * radius;
        for (int py = cy - radius; py <= cy + radius; py++)
        for (int px = cx - radius; px <= cx + radius; px++)
        {
            if (px < 0 || px >= Size || py < 0 || py >= Size) continue;
            float dist = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            if (dist > radius) continue;
            float t = 1f - dist / radius;
            t = t * t;  // quadratic falloff — concentrated core
            var existing = tex.GetPixel(px, py);
            tex.SetPixel(px, py, Color.Lerp(existing, col, col.a * t * 0.85f));
        }
    }

    /// Darkens pixels toward the corners/edges
    private static void DrawVignette(Texture2D tex)
    {
        float half = Size * 0.5f;
        for (int py = 0; py < Size; py++)
        for (int px = 0; px < Size; px++)
        {
            float dx = (px - half) / half;
            float dy = (py - half) / half;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);
            float t  = Mathf.Clamp01((d - 0.55f) / 0.55f);
            t = t * t;
            var c = tex.GetPixel(px, py);
            tex.SetPixel(px, py, Color.Lerp(c, Color.black, t * 0.75f));
        }
    }

    private static void DrawBorder(Texture2D tex, int thickness, Color32 col)
    {
        // Top
        FillRect(tex, 0, Size - thickness, Size, thickness, col);
        // Bottom
        FillRect(tex, 0, 0, Size, thickness, col);
        // Left
        FillRect(tex, 0, 0, thickness, Size, col);
        // Right
        FillRect(tex, Size - thickness, 0, thickness, Size, col);
    }

    /// Renders "THE MAZE" as a pixel-art label near the bottom of the icon
    private static void DrawTitle(Texture2D tex)
    {
        // 5×7 pixel-art font, each character encoded as 7 row-bytes (bits = columns, MSB=left)
        var glyphs = new System.Collections.Generic.Dictionary<char, byte[]>
        {
            ['T'] = new byte[] { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 },
            ['H'] = new byte[] { 0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            ['E'] = new byte[] { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 },
            ['M'] = new byte[] { 0b10001, 0b11011, 0b10101, 0b10001, 0b10001, 0b10001, 0b10001 },
            ['A'] = new byte[] { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 },
            ['Z'] = new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111 },
            [' '] = new byte[] { 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000 },
        };

        const string text  = "THE MAZE";
        const int    scale = 9;    // each "pixel" = 9×9 real pixels
        const int    gap   = 4;    // spacing between characters
        int charW  = 5 * scale + gap;
        int totalW = text.Length * charW - gap;
        int startX = (Size - totalW) / 2;
        int startY = 68;  // distance from bottom edge (Unity texture Y=0 is bottom)

        var gold = new Color32(255, 200, 40, 255);

        int cx = startX;
        foreach (char ch in text)
        {
            if (!glyphs.TryGetValue(ch, out var rows)) { cx += charW; continue; }
            for (int row = 0; row < 7; row++)
            for (int col = 0; col < 5; col++)
            {
                if ((rows[row] & (1 << (4 - col))) == 0) continue;
                int px0 = cx + col * scale;
                int py0 = startY + (6 - row) * scale; // flip Y
                for (int py = py0; py < py0 + scale; py++)
                for (int px = px0; px < px0 + scale; px++)
                    if (px >= 0 && px < Size && py >= 0 && py < Size)
                        tex.SetPixel(px, py, gold);
            }
            cx += charW;
        }
    }
}

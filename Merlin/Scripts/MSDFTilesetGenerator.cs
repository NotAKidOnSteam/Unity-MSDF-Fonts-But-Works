/**
 *                                                                      
 * MIT License
 * 
 * Copyright(c) 2019 Merlin
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

public class MSDFTilesetGenerator : EditorWindow
{
    public Font FontToConvert = null;
    public int TileSize = 32;
    public int DistanceRange = 4;
    public int Padding = 0;

    public Texture2D AtlasToSave = null;
    public bool OffByOne = false;
    public bool IncludeAllGlyphs = false;
    public bool UseTextureCompression = false;

    private const string MSDFGenPath = "Assets/Merlin/MSDF/bin/msdfgen.exe";
    private const string MSDFTempPath = "Assets/Merlin/MSDF/gen/glyph{0}.png";

    [MenuItem("Window/Merlin/MSDF Font Tileset Generator")]
    public static void ShowWindow()
    {
        EditorWindow window = EditorWindow.GetWindow(typeof(MSDFTilesetGenerator));
        window.maxSize = new Vector2(400, 200);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("MSDF Tileset Generator", EditorStyles.boldLabel);

        FontToConvert = (Font)EditorGUILayout.ObjectField("Font Asset:", FontToConvert, typeof(Font), false);

        TileSize = EditorGUILayout.IntField("Tile size", TileSize);
        Padding = EditorGUILayout.IntField("Padding", Padding);
        DistanceRange = EditorGUILayout.IntField("Distance Range in Pixels", DistanceRange);
        IncludeAllGlyphs = EditorGUILayout.Toggle("Include all Glyphs", IncludeAllGlyphs);
        OffByOne = EditorGUILayout.Toggle("Off by one", OffByOne);
        UseTextureCompression = EditorGUILayout.Toggle("Compress Font Tileset", UseTextureCompression);

        if (UseTextureCompression)
        {
            EditorGUILayout.HelpBox("Enabling compression can cause visible artifacts on text depending on the font. On most fonts the artifacts may make the text look wobbly along edges. Check to make sure artifacts do not appear when you enable this.", MessageType.Warning);
        }

        EditorGUI.BeginDisabledGroup(FontToConvert == null);
        if (GUILayout.Button("Generate Tileset"))
        {
            GenerateAtlas();
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Save Tileset to PNG"))
        {
            SaveToPNG();
        }
    }

    private string getResultPath()
    {
        string fontPath = AssetDatabase.GetAssetPath(FontToConvert);
        return Path.Combine(Path.GetDirectoryName(fontPath), Path.GetFileNameWithoutExtension(fontPath) + "_msdfTileset");
    }

    private void SaveToPNG()
    {
        string path = getResultPath();
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path + ".asset");

        if (tex != null)
        {
            // TODO copy over texture parameters (colorspace, compression)
            File.WriteAllBytes(path + ".png", ImageConversion.EncodeToPNG(tex));
            AssetDatabase.Refresh();
        }
        else
        {
            Debug.LogWarning("Tileset not yet generated: " + path);
        }
    }

    private void GenerateAtlas()
    {
        TrueTypeFontImporter fontImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(FontToConvert)) as TrueTypeFontImporter;

        if (fontImporter == null)
            Debug.LogError("Could not import mesh asset! Builtin Unity fonts like Arial don't work unless you put them in the project directory!");

        fontImporter.characterSpacing = 4;
        fontImporter.characterPadding = 2;

        fontImporter.SaveAndReimport();

        int tilemapsize = TileSize * 16;

        Texture2D newAtlas = new Texture2D(tilemapsize, tilemapsize, TextureFormat.ARGB32, false, true);
        for (int x = 0; x < newAtlas.width; ++x)
        {
            for (int y = 0; y < newAtlas.height; ++y)
            {
                newAtlas.SetPixel(x, y, Color.black);
            }
        }

		for(int i=1; i<256; i++)
        {
            char c = (char)i;
            if (!IncludeAllGlyphs && !FontToConvert.HasCharacter(c))
                continue;

            EditorUtility.DisplayProgressBar("Generating MSDF Tileset...", string.Format("Glyph {0}/256", i+1), i / 255.0f);

            Texture2D currentGlyphTex = GenerateGlyphTexture(i, TileSize-Padding, TileSize-Padding);
            if (currentGlyphTex == null)
                continue;

            int tileindex = i - (OffByOne ? 1 : 0);
            int rowoffset = TileSize * (tileindex / 16) + Padding / 2;
            int columnoffset = TileSize * (tileindex % 16) + Padding / 2;
            for (int y = 0; y < currentGlyphTex.height; ++y)
            {
                for (int x = 0; x < currentGlyphTex.width; ++x)
                {
                    Color glyphCol = currentGlyphTex.GetPixel(x, currentGlyphTex.height-1-y);
                    newAtlas.SetPixel(columnoffset+x, tilemapsize-1-(rowoffset + y), glyphCol);
                }
            }
        }

        newAtlas.Apply(false);
        
        if (UseTextureCompression)
        {
            EditorUtility.DisplayProgressBar("Generating MSDF Tileset...", "Compressing Tileset...", 1f);
            EditorUtility.CompressTexture(newAtlas, TextureFormat.BC7, UnityEditor.TextureCompressionQuality.Best);
        }

        EditorUtility.ClearProgressBar();

        string savePath = getResultPath() + ".asset";
        AssetDatabase.CreateAsset(newAtlas, savePath);
        /*Texture2D outtex = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        if (outtex != null)
        {
            outtex = newAtlas;
            AssetDatabase.ForceReserializeAssets();
        }
        else
        {
            AssetDatabase.CreateAsset(newAtlas, savePath);
        }*/

        EditorGUIUtility.PingObject(newAtlas);
    }

    private Texture2D GenerateGlyphTexture(int UTFChar, int glyphWidth, int glyphHeight)
    {
        System.Diagnostics.Process msdfProcess = new System.Diagnostics.Process();

        msdfProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
        msdfProcess.StartInfo.CreateNoWindow = true;
        msdfProcess.StartInfo.UseShellExecute = false;
        msdfProcess.StartInfo.FileName = Path.GetFullPath(MSDFGenPath);
        msdfProcess.EnableRaisingEvents = true;

        string fontPath = Path.GetFullPath(AssetDatabase.GetAssetPath(FontToConvert));
        //string glyphLocalPath = string.Format(MSDFTempPath, UTFChar);
        string glyphLocalPath = string.Format(MSDFTempPath, 0);
        string glyphPath = Path.GetFullPath(glyphLocalPath);

        Directory.CreateDirectory(Path.GetDirectoryName(string.Format(MSDFTempPath, 0)));
        string argStr = string.Format("msdf -o \"{0}\" -font \"{1}\" {4} -size {2} {3} -pxrange {5} -autoframe", glyphPath, fontPath, glyphWidth, glyphHeight, UTFChar, DistanceRange);

        msdfProcess.StartInfo.Arguments = argStr;

        msdfProcess.Start();
        msdfProcess.WaitForExit();

        if (!File.Exists(glyphLocalPath))
        {
            Debug.LogWarning("Could not load glyph " + UTFChar);
            return null;
        }

        Texture2D loadedGlyph = new Texture2D(glyphWidth, glyphHeight);
        ImageConversion.LoadImage(loadedGlyph, File.ReadAllBytes(glyphLocalPath), false);

        return loadedGlyph;
    }
}

#endif


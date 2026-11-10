#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Follow-up to DumpKenneyModels: for each Kenney car body and
/// wheel mesh, log the top 8 atlas cells by UV-vertex count, so the paint
/// step recolours the right cells (the atlas has paint gradients, glass
/// greys and tyre darks — the dominant cell alone may be glass, not
/// paint). Run: -executeMethod DumpUvHistogram.Dump -quit
/// -logFile /home/hatch/workspace/kenney-uv.log</summary>
public static class DumpUvHistogram
{
    public static void Dump()
    {
        string[] cars = { "sedan", "sedan-sports", "hatchback-sports", "race", "race-future", "suv", "suv-luxury" };
        foreach (string c in cars)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Models/Kenney/CarKit/" + c + ".fbx");
            if (prefab == null) { Debug.LogError("[UV] MISSING " + c); continue; }
            GameObject go = Object.Instantiate(prefab);
            foreach (string part in new string[] { "body", "wheel-front-left" })
            {
                Transform t = KenneyModels.FindDeep(go.transform, part);
                if (t == null) continue;
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Vector2[] uvs = mf.sharedMesh.uv;
                int[,] grid = new int[16, 16];
                foreach (Vector2 uv in uvs)
                    grid[Mathf.Clamp((int)(uv.x * 16f), 0, 15), Mathf.Clamp((int)(uv.y * 16f), 0, 15)]++;
                // Top 8 cells.
                string top = "";
                for (int n = 0; n < 8; n++)
                {
                    int best = 0, bx = -1, by = -1;
                    for (int x = 0; x < 16; x++) for (int y = 0; y < 16; y++)
                        if (grid[x, y] > best) { best = grid[x, y]; bx = x; by = y; }
                    if (best == 0 || bx < 0) break;
                    top += $" ({bx},{by})={best}";
                    grid[bx, by] = 0;
                }
                Debug.Log($"[UV] {c} {part}: total={uvs.Length} top8:{top}");
            }
            Object.DestroyImmediate(go);
        }
        // Atlas cell colours for the cells that matter (read from the
        // imported texture — it is readable now).
        Texture2D atlas = Resources.Load<Texture2D>("Models/Kenney/CarKit/Textures/colormap");
        if (atlas != null)
        {
            foreach (Vector2Int cell in new Vector2Int[]
                { new Vector2Int(7, 5), new Vector2Int(15, 10), new Vector2Int(5, 4),
                  new Vector2Int(6, 5), new Vector2Int(12, 5), new Vector2Int(14, 10),
                  new Vector2Int(13, 10), new Vector2Int(12, 10), new Vector2Int(8, 9),
                  new Vector2Int(7, 4), new Vector2Int(8, 5), new Vector2Int(2, 3) })
            {
                Color c = atlas.GetPixel(cell.x * 32 + 16, cell.y * 32 + 16);
                Debug.Log($"[UV] atlas cell ({cell.x},{cell.y}) = #{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}");
            }
        }
        Debug.Log("[UV] DONE");
    }
}
#endif

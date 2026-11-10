using UnityEngine;

/// <summary>Procedural mesh + part builders for the visual rebuild.
/// No licensed models exist in the project: car bodies are lofted from a
/// real sports-car silhouette (low nose, long hood, raked cabin, fastback
/// tail) and wheels are torus + spoked rims. Target look: Forza Horizon 1
/// (2012) at modern resolution — detailed PBR cars, not primitive boxes.</summary>
public static class MeshFactory
{
    /// <summary>Car body from a BodyStyleDef (9-style roster in
    /// CarBodyStyles.cs): each garage car and traffic variant gets its
    /// own loft stations, so silhouettes are genuinely distinct.</summary>
    public static Mesh BuildCarBody(BodyStyleDef style)
    {
        return BuildLoft(style.z, style.w, style.top, -0.30f, 12, 0.55f);
    }

    /// <summary>Cabin glass from a BodyStyleDef.</summary>
    public static Mesh BuildCabinGlass(BodyStyleDef style)
    {
        return BuildLoft(style.cz, style.cw, style.ctop, -0.24f, 16, 0.6f);
    }

    /// <summary>Lofted car body: a superellipse cross-section swept along
    /// stations so the silhouette reads as a real sports car. Local space:
    /// +Z forward, Y up, X width. Bottom is flat-ish, top follows topY[].</summary>
    public static Mesh BuildCarBody()
    {
        return BuildCarBody(1);
    }

    /// <summary>Car body by class: 0 = city hatch (shorter, taller, upright
    /// cabin), 1 = GT (the default sports silhouette), 2 = supercar (lower,
    /// wider, longer nose, flatter roof). All three keep the same length
    /// and wheelbase so WheelColliders / physics are untouched.</summary>
    public static Mesh BuildCarBody(int carClass)
    {
        if (carClass == 0)
        {
            float[] z = { -1.85f, -1.70f, -1.35f, -0.90f, -0.45f, 0.00f, 0.45f, 0.90f, 1.25f, 1.55f, 1.75f, 1.85f };
            float[] w = { 0.78f, 0.86f, 0.90f, 0.92f, 0.93f, 0.93f, 0.92f, 0.90f, 0.86f, 0.80f, 0.72f, 0.56f };
            float[] top = { 0.38f, 0.44f, 0.46f, 0.46f, 0.45f, 0.44f, 0.42f, 0.38f, 0.32f, 0.24f, 0.15f, 0.06f };
            return BuildLoft(z, w, top, -0.30f, 12, 0.55f);
        }
        if (carClass == 2)
        {
            float[] z = { -2.15f, -2.00f, -1.65f, -1.15f, -0.55f, 0.00f, 0.60f, 1.10f, 1.50f, 1.85f, 2.05f, 2.15f };
            float[] w = { 0.82f, 0.92f, 0.97f, 0.99f, 1.00f, 1.00f, 0.99f, 0.96f, 0.90f, 0.83f, 0.74f, 0.56f };
            float[] top = { 0.16f, 0.20f, 0.21f, 0.21f, 0.20f, 0.19f, 0.17f, 0.14f, 0.10f, 0.05f, 0.00f, -0.04f };
            return BuildLoft(z, w, top, -0.32f, 12, 0.55f);
        }
        if (carClass >= 3)
        {
            // Classes 3-8 (SUV, muscle, sedan, roadster, hypercar, coupe):
            // profiles live in CarBodyStyles, same 4.2 m wheelbase as GT.
            BodyStyleDef def = CarBodyStyles.ForClass(carClass);
            return BuildLoft(def.z, def.w, def.top, -0.30f, 12, 0.55f);
        }
        float[] zg = { -2.10f, -1.95f, -1.60f, -1.10f, -0.55f, 0.00f, 0.55f, 1.05f, 1.45f, 1.80f, 2.02f, 2.12f };
        float[] wg = { 0.80f, 0.88f, 0.92f, 0.94f, 0.95f, 0.95f, 0.94f, 0.92f, 0.88f, 0.82f, 0.74f, 0.58f };
        float[] topg = { 0.26f, 0.30f, 0.31f, 0.31f, 0.30f, 0.29f, 0.27f, 0.24f, 0.20f, 0.14f, 0.08f, 0.02f };
        return BuildLoft(zg, wg, topg, -0.30f, 12, 0.55f);
    }

    /// <summary>Lofted glass cabin: raked windshield + fastback rear glass.
/// Local space centred on the cabin; caller positions it on the body.</summary>
    public static Mesh BuildCabinGlass()
    {
        return BuildCabinGlass(1);
    }

    /// <summary>Cabin glass by class, matching BuildCarBody: city = tall
    /// upright greenhouse, GT = default, supercar = low raked canopy.</summary>
    public static Mesh BuildCabinGlass(int carClass)
    {
        if (carClass == 0)
        {
            float[] z = { -0.95f, -0.65f, -0.20f, 0.25f, 0.65f, 0.90f, 1.00f };
            float[] w = { 0.60f, 0.72f, 0.78f, 0.78f, 0.74f, 0.64f, 0.48f };
            float[] top = { 0.22f, 0.38f, 0.44f, 0.44f, 0.38f, 0.26f, 0.10f };
            return BuildLoft(z, w, top, -0.24f, 12, 0.6f);
        }
        if (carClass == 2)
        {
            float[] z = { -1.10f, -0.80f, -0.30f, 0.15f, 0.60f, 0.95f, 1.08f };
            float[] w = { 0.58f, 0.70f, 0.78f, 0.78f, 0.74f, 0.62f, 0.46f };
            float[] top = { 0.02f, 0.12f, 0.16f, 0.16f, 0.12f, 0.04f, -0.06f };
            return BuildLoft(z, w, top, -0.24f, 12, 0.6f);
        }
        if (carClass >= 3)
        {
            BodyStyleDef def = CarBodyStyles.ForClass(carClass);
            return BuildLoft(def.cz, def.cw, def.ctop, -0.24f, 12, 0.6f);
        }
        float[] zg = { -1.05f, -0.75f, -0.30f, 0.15f, 0.60f, 0.95f, 1.08f };
        float[] wg = { 0.62f, 0.74f, 0.80f, 0.80f, 0.76f, 0.66f, 0.50f };
        float[] topg = { 0.10f, 0.24f, 0.28f, 0.28f, 0.24f, 0.14f, 0.02f };
        return BuildLoft(zg, wg, topg, -0.24f, 12, 0.6f);
    }

    /// <summary>Generic loft: superellipse rings swept along Z, capped ends.</summary>
    public static Mesh BuildLoft(float[] z, float[] halfWidth, float[] topY, float bottomY, int radial, float squash)
    {
        int stations = z.Length;
        Vector3[] verts = new Vector3[stations * radial + 2];
        Vector2[] uvs = new Vector2[verts.Length];
        for (int s = 0; s < stations; s++)
        {
            float yc = (topY[s] + bottomY) * 0.5f;
            float h = (topY[s] - bottomY) * 0.5f;
            for (int i = 0; i < radial; i++)
            {
                float a = 2f * Mathf.PI * i / radial;
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                float x = halfWidth[s] * Shape(c, squash);
                float y = yc + h * Shape(sn, squash);
                verts[s * radial + i] = new Vector3(x, y, z[s]);
                uvs[s * radial + i] = new Vector2((float)i / radial, (z[s] - z[0]) / (z[stations - 1] - z[0]));
            }
        }
        int rearCentre = stations * radial, frontCentre = stations * radial + 1;
        verts[rearCentre] = new Vector3(0f, (topY[0] + bottomY) * 0.5f, z[0]);
        verts[frontCentre] = new Vector3(0f, (topY[stations - 1] + bottomY) * 0.5f, z[stations - 1]);
        uvs[rearCentre] = new Vector2(0.5f, 0f);
        uvs[frontCentre] = new Vector2(0.5f, 1f);

        var tris = new System.Collections.Generic.List<int>();
        for (int s = 0; s < stations - 1; s++)
        {
            for (int i = 0; i < radial; i++)
            {
                int j = (i + 1) % radial;
                int a = s * radial + i, b = s * radial + j;
                int c = (s + 1) * radial + i, d = (s + 1) * radial + j;
                // Winding gives outward-facing normals (verified: the
                // right/top region normal points +X/+Y, not inward).
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        }
        for (int i = 0; i < radial; i++)
        {
            int j = (i + 1) % radial;
            tris.Add(rearCentre); tris.Add(j); tris.Add(i);
            int f0 = (stations - 1) * radial + i, f1 = (stations - 1) * radial + j;
            tris.Add(frontCentre); tris.Add(f0); tris.Add(f1);
        }
        Mesh m = new Mesh { name = "LoftBody" };
        m.vertices = verts;
        m.uv = uvs;
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static float Shape(float v, float squash)
    {
        return Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), squash);
    }

    /// <summary>Torus with axle along local Y (same convention as Unity's
    /// built-in cylinder, so WheelCollider poses orient it correctly).</summary>
    public static Mesh BuildTorus(float ringRadius, float tubeRadius, int ringSeg, int tubeSeg)
    {
        Vector3[] verts = new Vector3[(ringSeg + 1) * (tubeSeg + 1)];
        Vector2[] uvs = new Vector2[verts.Length];
        for (int i = 0; i <= ringSeg; i++)
        {
            float a = 2f * Mathf.PI * i / ringSeg;
            Vector3 centre = new Vector3(Mathf.Cos(a) * ringRadius, 0f, Mathf.Sin(a) * ringRadius);
            Vector3 radialDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int j = 0; j <= tubeSeg; j++)
            {
                float b = 2f * Mathf.PI * j / tubeSeg;
                Vector3 off = radialDir * Mathf.Cos(b) * tubeRadius + Vector3.up * Mathf.Sin(b) * tubeRadius;
                verts[i * (tubeSeg + 1) + j] = centre + off;
                uvs[i * (tubeSeg + 1) + j] = new Vector2((float)i / ringSeg, (float)j / tubeSeg);
            }
        }
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < ringSeg; i++)
            for (int j = 0; j < tubeSeg; j++)
            {
                int a = i * (tubeSeg + 1) + j, b = a + tubeSeg + 1;
                // Winding gives outward-facing normals.
                tris.Add(a); tris.Add(a + 1); tris.Add(b);
                tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
            }
        Mesh m = new Mesh { name = "Torus" };
        m.vertices = verts;
        m.uv = uvs;
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Partial loft: only the ring-angle arc from startDeg to
    /// endDeg (0° = +X side, 90° = top) is generated, with no end caps
    /// — a curved skin that hugs another loft's surface. The surface
    /// underneath (glass cabin / body) closes the open edges visually.
    /// Used for the painted roof panel over the glass cabin and for
    /// racing stripes over the body top. Same superellipse section as
    /// BuildLoft, so the skin follows the loft it sits on.</summary>
    public static Mesh BuildLoftArc(float[] z, float[] halfWidth, float[] topY,
        float bottomY, int steps, float squash, float startDeg, float endDeg)
    {
        int stations = z.Length;
        int ring = steps + 1;
        Vector3[] verts = new Vector3[stations * ring];
        Vector2[] uvs = new Vector2[verts.Length];
        for (int s = 0; s < stations; s++)
        {
            float yc = (topY[s] + bottomY) * 0.5f;
            float h = (topY[s] - bottomY) * 0.5f;
            for (int i = 0; i < ring; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(startDeg, endDeg, (float)i / steps);
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                float x = halfWidth[s] * Shape(c, squash);
                float y = yc + h * Shape(sn, squash);
                verts[s * ring + i] = new Vector3(x, y, z[s]);
                uvs[s * ring + i] = new Vector2((float)i / steps,
                    (z[s] - z[0]) / (z[stations - 1] - z[0]));
            }
        }
        var tris = new System.Collections.Generic.List<int>();
        for (int s = 0; s < stations - 1; s++)
            for (int i = 0; i < steps; i++)
            {
                int a = s * ring + i, b = a + 1;
                int c = (s + 1) * ring + i, d = c + 1;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        Mesh m = new Mesh { name = "LoftArc" };
        m.vertices = verts; m.uv = uvs; m.triangles = tris.ToArray();
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    /// <summary>Tyre: torus + merged tread blocks (ONE mesh, one
    /// renderer per wheel — no GameObject per block). Blocks sit 4 mm
    /// inside the torus surface and 12 mm proud of it, 62% of the
    /// pitch wide, so the tyre reads as treaded rubber from the side
    /// instead of a smooth black donut.</summary>
    public static Mesh BuildTyreWithTread(float ringRadius, float tubeRadius,
        int ringSeg, int tubeSeg, int treadBlocks)
    {
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i <= ringSeg; i++)
        {
            float a = 2f * Mathf.PI * i / ringSeg;
            Vector3 centre = new Vector3(Mathf.Cos(a) * ringRadius, 0f, Mathf.Sin(a) * ringRadius);
            Vector3 radialDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int j = 0; j <= tubeSeg; j++)
            {
                float b = 2f * Mathf.PI * j / tubeSeg;
                Vector3 off = radialDir * Mathf.Cos(b) * tubeRadius + Vector3.up * Mathf.Sin(b) * tubeRadius;
                verts.Add(centre + off);
                uvs.Add(new Vector2((float)i / ringSeg, (float)j / tubeSeg));
            }
        }
        for (int i = 0; i < ringSeg; i++)
            for (int j = 0; j < tubeSeg; j++)
            {
                int a = i * (tubeSeg + 1) + j, b = a + tubeSeg + 1;
                tris.Add(a); tris.Add(a + 1); tris.Add(b);
                tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
            }
        float outerR = ringRadius + tubeRadius;
        float pitch = 2f * Mathf.PI * outerR / treadBlocks;
        Vector3 blockSize = new Vector3(pitch * 0.62f, tubeRadius * 2f * 0.85f, 0.016f);
        for (int k = 0; k < treadBlocks; k++)
        {
            float th = 2f * Mathf.PI * k / treadBlocks;
            Vector3 centre = new Vector3(Mathf.Cos(th) * (outerR + 0.004f), 0f,
                Mathf.Sin(th) * (outerR + 0.004f));
            AppendBox(verts, uvs, tris, centre, blockSize, 90f - th * Mathf.Rad2Deg);
        }
        Mesh m = new Mesh { name = "TyreTread" };
        m.vertices = verts.ToArray(); m.uv = uvs.ToArray(); m.triangles = tris.ToArray();
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    /// <summary>Append a box (rotated about Y by rotYDeg) to the given
    /// vertex/triangle lists. Corner bit 0 = +X, bit 1 = +Y, bit 2 = +Z;
    /// face winding is outward, verified by cross product per face.</summary>
    static void AppendBox(System.Collections.Generic.List<Vector3> verts,
        System.Collections.Generic.List<Vector2> uvs,
        System.Collections.Generic.List<int> tris,
        Vector3 centre, Vector3 size, float rotYDeg)
    {
        float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
        Quaternion q = Quaternion.Euler(0f, rotYDeg, 0f);
        int baseIdx = verts.Count;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = new Vector3(
                (i & 1) == 0 ? -hx : hx,
                (i & 2) == 0 ? -hy : hy,
                (i & 4) == 0 ? -hz : hz);
            verts.Add(centre + q * c);
            uvs.Add(Vector2.zero);
        }
        int[][] faces = {
            new[] { 0, 3, 1, 0, 2, 3 }, // -Z
            new[] { 4, 5, 7, 4, 7, 6 }, // +Z
            new[] { 0, 4, 6, 0, 6, 2 }, // -X
            new[] { 1, 3, 7, 1, 7, 5 }, // +X
            new[] { 0, 1, 5, 0, 5, 4 }, // -Y
            new[] { 2, 6, 7, 2, 7, 3 }, // +Y
        };
        foreach (int[] f in faces)
            foreach (int idx in f) tris.Add(baseIdx + idx);
    }

    /// <summary>Visual part from a built-in primitive mesh — no collider,
    /// so it can never disturb the car's physics.</summary>
    public static GameObject Part(string name, string builtinMesh, Transform parent,
        Vector3 localPos, Vector3 localScale, Material mat, Vector3 euler = default(Vector3))
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.transform.localRotation = Quaternion.Euler(euler);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = Resources.GetBuiltinResource<Mesh>(builtinMesh);
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;
        return go;
    }

    /// <summary>URP Lit material helper. URP Lit is the PBR shader used
    /// for every rebuilt surface (paint, glass, asphalt, facades).</summary>
    public static Material Lit(string name, Color color, float metallic, float smoothness)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(s) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    public static Material Emissive(string name, Color color, float intensity)
    {
        Material m = Lit(name, Color.black, 0f, 0.4f);
        m.SetColor("_EmissionColor", color * intensity);
        m.EnableKeyword("_EMISSION");
        return m;
    }

    // ---------- Sculpted shapes (no plain primitives) ----------
    // The user rule: nothing visible may read as a plain box / cylinder /
    // cone. These builders replace the remaining Cube/Sphere/Cylinder
    // parts: bumpers, grilles, mirrors, spoilers, interiors, roof slabs,
    // tree trunks/canopies, spires and lamp housings are all lofted or
    // rounded. Real cylinders stay ONLY where the real object is a
    // cylinder (exhaust pipes, streetlight poles, brake discs).

    static readonly System.Collections.Generic.Dictionary<string, Mesh> meshCache =
        new System.Collections.Generic.Dictionary<string, Mesh>();

    /// <summary>Box with rounded vertical corners AND chamfered front/back
    /// edges: a rounded-rectangle cross-section extruded along Z, the end
    /// rings inset so the faces are bevelled. Reads as a moulded part
    /// (bumper, grille, mirror, spoiler blade), not a cube.</summary>
    public static Mesh BuildRoundedBox(float w, float h, float d, float corner)
    {
        string key = "RB:" + w.ToString("0.00") + "," + h.ToString("0.00") + "," + d.ToString("0.00") + "," + corner.ToString("0.00");
        Mesh cached;
        if (meshCache.TryGetValue(key, out cached)) return cached;
        float hw = w * 0.5f, hh = h * 0.5f, hd = d * 0.5f;
        float r = Mathf.Min(corner, hw * 0.45f, hh * 0.45f, hd * 0.45f);
        float ch = r * 0.6f;
        int perCorner = 6, ring = perCorner * 4;
        float[] zs = { -hd, -hd + ch, hd - ch, hd };
        float[] inset = { ch, 0f, 0f, ch };
        Vector3[] verts = new Vector3[4 * ring + 2];
        Vector2[] uvs = new Vector2[verts.Length];
        for (int s = 0; s < 4; s++)
        {
            float ex = hw - inset[s], ey = hh - inset[s];
            float cx = Mathf.Max(0.001f, ex - r), cy = Mathf.Max(0.001f, ey - r);
            for (int i = 0; i < ring; i++)
            {
                int cIdx = i / perCorner, k = i % perCorner;
                float a = (cIdx * 90f + 90f * k / perCorner) * Mathf.Deg2Rad;
                float sx = cIdx == 0 || cIdx == 3 ? cx : -cx;
                float sy = cIdx < 2 ? cy : -cy;
                float x = sx + r * Mathf.Cos(a), y = sy + r * Mathf.Sin(a);
                verts[s * ring + i] = new Vector3(x, y, zs[s]);
                uvs[s * ring + i] = new Vector2(x / w + 0.5f, y / h + 0.5f);
            }
        }
        int rearCentre = 4 * ring, frontCentre = 4 * ring + 1;
        verts[rearCentre] = new Vector3(0f, 0f, -hd);
        verts[frontCentre] = new Vector3(0f, 0f, hd);
        uvs[rearCentre] = new Vector2(0.5f, 0.5f);
        uvs[frontCentre] = new Vector2(0.5f, 0.5f);
        var tris = new System.Collections.Generic.List<int>();
        for (int s = 0; s < 3; s++)
            for (int i = 0; i < ring; i++)
            {
                int j = (i + 1) % ring;
                int a = s * ring + i, b = s * ring + j;
                int c = (s + 1) * ring + i, dd = (s + 1) * ring + j;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(dd); tris.Add(c);
            }
        for (int i = 0; i < ring; i++)
        {
            int j = (i + 1) % ring;
            tris.Add(rearCentre); tris.Add(j); tris.Add(i);
            int f0 = 3 * ring + i, f1 = 3 * ring + j;
            tris.Add(frontCentre); tris.Add(f0); tris.Add(f1);
        }
        Mesh m = new Mesh { name = "RoundedBox" };
        m.vertices = verts; m.uv = uvs; m.triangles = tris.ToArray();
        m.RecalculateNormals(); m.RecalculateBounds();
        meshCache[key] = m;
        return m;
    }

    /// <summary>Surface of revolution around local Y (radii/ys must have
    /// the same length; use 0.02 instead of 0 for a pointed tip). Used for
    /// tapered tree trunks, teardrop canopies, tower spires and beacon
    /// domes — real curved silhouettes, not cones/spheres.</summary>
    public static Mesh BuildLathe(float[] radii, float[] ys, int segments)
    {
        string key = "LT:" + string.Join(",", System.Array.ConvertAll(radii, v => v.ToString("0.00")))
            + "|" + string.Join(",", System.Array.ConvertAll(ys, v => v.ToString("0.00"))) + "|" + segments;
        Mesh cached;
        if (meshCache.TryGetValue(key, out cached)) return cached;
        int rings = radii.Length;
        Vector3[] verts = new Vector3[rings * segments + 2];
        Vector2[] uvs = new Vector2[verts.Length];
        for (int s = 0; s < rings; s++)
            for (int i = 0; i < segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments;
                verts[s * segments + i] = new Vector3(radii[s] * Mathf.Cos(a), ys[s], radii[s] * Mathf.Sin(a));
                uvs[s * segments + i] = new Vector2((float)i / segments, (float)s / (rings - 1));
            }
        int bottomCentre = rings * segments, topCentre = rings * segments + 1;
        verts[bottomCentre] = new Vector3(0f, ys[0], 0f);
        verts[topCentre] = new Vector3(0f, ys[rings - 1], 0f);
        uvs[bottomCentre] = new Vector2(0.5f, 0f);
        uvs[topCentre] = new Vector2(0.5f, 1f);
        var tris = new System.Collections.Generic.List<int>();
        for (int s = 0; s < rings - 1; s++)
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                int a = s * segments + i, b = s * segments + j;
                int c = (s + 1) * segments + i, d = (s + 1) * segments + j;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        for (int i = 0; i < segments; i++)
        {
            int j = (i + 1) % segments;
            tris.Add(bottomCentre); tris.Add(i); tris.Add(j);
            int t0 = (rings - 1) * segments + i, t1 = (rings - 1) * segments + j;
            tris.Add(topCentre); tris.Add(t1); tris.Add(t0);
        }
        Mesh m = new Mesh { name = "Lathe" };
        m.vertices = verts; m.uv = uvs; m.triangles = tris.ToArray();
        m.RecalculateNormals(); m.RecalculateBounds();
        meshCache[key] = m;
        return m;
    }

    /// <summary>Teardrop tree canopy: wide belly, pointed top — the
    /// silhouette of a real tree, centred on its origin so it can replace
    /// a sphere 1:1. Radius ~0.9 m, height ~2.2 m before scaling.</summary>
    public static Mesh BuildCanopy()
    {
        return BuildLathe(
            new float[] { 0.02f, 0.55f, 0.85f, 0.90f, 0.72f, 0.42f, 0.02f },
            new float[] { -1.10f, -0.85f, -0.35f, 0.20f, 0.65f, 0.95f, 1.10f }, 12);
    }

    /// <summary>Tapered tree trunk, centred on its origin (height ~3 m).</summary>
    public static Mesh BuildTrunk()
    {
        return BuildLathe(
            new float[] { 0.17f, 0.14f, 0.11f, 0.08f },
            new float[] { -1.50f, -0.50f, 0.60f, 1.50f }, 10);
    }

    /// <summary>Airfoil wing blade swept along X (span), chord along Z,
    /// thin cambered superellipse section — a real wing profile, not a
    /// flat box. Tips taper to 70% chord. Ends capped.</summary>
    public static Mesh BuildAirfoil(float span, float chord, float thick)
    {
        string key = "AF:" + span.ToString("0.00") + "," + chord.ToString("0.00") + "," + thick.ToString("0.00");
        Mesh cached;
        if (meshCache.TryGetValue(key, out cached)) return cached;
        int stations = 9, ring = 14;
        float halfSpan = span * 0.5f;
        Vector3[] verts = new Vector3[stations * ring + 2];
        Vector2[] uvs = new Vector2[verts.Length];
        for (int s = 0; s < stations; s++)
        {
            float t = (float)s / (stations - 1);          // 0..1 across span
            float x = -halfSpan + t * span;
            float tip = 1f - 0.30f * Mathf.Abs(t - 0.5f) * 2f; // 0.70 at tips
            float hc = chord * 0.5f * tip, ht = thick * 0.5f;
            for (int i = 0; i < ring; i++)
            {
                float a = 2f * Mathf.PI * i / ring;
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                // Camber: top surface bulges up, bottom stays flatter.
                float z = hc * Shape(c, 0.6f);
                float y = ht * Shape(sn, 0.6f) + (sn > 0f ? ht * 0.35f * (1f - c * c) : 0f);
                verts[s * ring + i] = new Vector3(x, y, z);
                uvs[s * ring + i] = new Vector2(t, (float)i / ring);
            }
        }
        int leftCentre = stations * ring, rightCentre = stations * ring + 1;
        verts[leftCentre] = new Vector3(-halfSpan, 0f, 0f);
        verts[rightCentre] = new Vector3(halfSpan, 0f, 0f);
        uvs[leftCentre] = new Vector2(0f, 0.5f);
        uvs[rightCentre] = new Vector2(1f, 0.5f);
        var tris = new System.Collections.Generic.List<int>();
        for (int s = 0; s < stations - 1; s++)
            for (int i = 0; i < ring; i++)
            {
                int j = (i + 1) % ring;
                int a = s * ring + i, b = s * ring + j;
                int c = (s + 1) * ring + i, d = (s + 1) * ring + j;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
        for (int i = 0; i < ring; i++)
        {
            int j = (i + 1) % ring;
            tris.Add(leftCentre); tris.Add(j); tris.Add(i);
            int f0 = (stations - 1) * ring + i, f1 = (stations - 1) * ring + j;
            tris.Add(rightCentre); tris.Add(f0); tris.Add(f1);
        }
        Mesh m = new Mesh { name = "Airfoil" };
        m.vertices = verts; m.uv = uvs; m.triangles = tris.ToArray();
        m.RecalculateNormals(); m.RecalculateBounds();
        meshCache[key] = m;
        return m;
    }

    /// <summary>Domed projector lens / round lamp: closed lathe disc,
    /// axis Y (rotate 90° X to face forward). Used for headlight
    /// projectors, round taillights (R34), fog lamps and lamp domes.</summary>
    public static Mesh BuildLens(float radius, float depth)
    {
        return BuildLathe(
            new float[] { 0.02f, radius * 0.98f, radius, radius * 0.94f, 0.02f },
            new float[] { 0f, 0.01f, depth * 0.45f, depth * 0.85f, depth }, 16);
    }

    /// <summary>Part from an already-built mesh (no collider).</summary>
    public static GameObject PartMesh(string name, Mesh mesh, Transform parent,
        Vector3 localPos, Vector3 localScale, Material mat, Vector3 euler = default(Vector3))
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.transform.localRotation = Quaternion.Euler(euler);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;
        return go;
    }

    /// <summary>Rounded-box part: size is baked into the mesh, localScale
    /// stays 1. Replaces Cube.fbx for every visible moulded part.</summary>
    public static GameObject PartRounded(string name, Transform parent,
        Vector3 localPos, Vector3 size, float corner, Material mat, Vector3 euler = default(Vector3))
    {
        return PartMesh(name, BuildRoundedBox(size.x, size.y, size.z, corner),
            parent, localPos, Vector3.one, mat, euler);
    }

    /// <summary>Lathe part: radii/ys profile revolved around Y.</summary>
    public static GameObject PartLathe(string name, Transform parent,
        Vector3 localPos, Vector3 localScale, float[] radii, float[] ys, int segments,
        Material mat, Vector3 euler = default(Vector3))
    {
        return PartMesh(name, BuildLathe(radii, ys, segments),
            parent, localPos, localScale, mat, euler);
    }
}

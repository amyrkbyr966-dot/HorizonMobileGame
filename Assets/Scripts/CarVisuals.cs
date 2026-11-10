using UnityEngine;
using TMPro;

/// <summary>Visual rebuild of cars (player + traffic). AAA pass
/// (2026-11-07): NO plain boxes anywhere visible — every moulded part
/// (bumpers, grille, mirrors, handles, plates, spoiler, interior) is a
/// rounded-box / lathe / lofted mesh from MeshFactory. Headlights are
/// shaped assemblies (rounded housing + projector lenses + chrome
/// rings), taillights are shaped (bar + round lenses where the real car
/// has them), wheels are torus tyres + spoked rims + brake discs +
/// sidewall rings, licence plates carry readable 3D text, panel shut
/// lines and chrome window trim outline the body, and hero cars
/// (LaFerrarri 27, Chiron 33, R34 38, Tourbillon 49, LFA 43, Supra 39,
/// Mustang 20, 911 14, Countach 29) get their own silhouette tweaks
/// (CarBodyStyles.StyleForCar) plus signature details. Round-2 fixes
/// (verified on screen): the URP Lit _EMISSION variant was stripped
/// from player builds, so every emissive part rendered black — an
/// EmissiveAnchor material asset in Resources/Materials keeps the
/// variant in the build; glass is now opaque/reflective and writes
/// depth (the old invisible glass exposed the whole interior and let
/// the rear plate text show through the cabin); TextMesh glyphs face
/// -Z, so the front plate text is rotated 180°; headrests no longer
/// poke through the roof. Paint stays PBR metallic (metallic 0.9,
/// smoothness 0.95) so the reflection probe + sun give a Forza Horizon
/// 1 style clearcoat look. Gameplay (stats, colliders, WheelColliders,
/// save data) is untouched; the "Body" child keeps its name so
/// CarController.SetBodyColor still works.
/// NOTE: brake calipers are NOT added to the wheels — WheelMesh
/// spins with the wheel, so a caliper child would visibly orbit;
/// the brake disc is symmetric so it reads correctly while spinning.</summary>
public static class CarVisuals
{
    static readonly Color[] TrafficPalette = new Color[]
    {
        new Color(0.10f, 0.25f, 0.80f), new Color(0.92f, 0.92f, 0.92f),
        new Color(0.85f, 0.45f, 0.10f), new Color(0.12f, 0.12f, 0.14f),
        new Color(0.10f, 0.55f, 0.25f), new Color(0.85f, 0.10f, 0.10f),
        new Color(0.75f, 0.75f, 0.80f), new Color(0.35f, 0.15f, 0.65f),
    };

    // The per-car model table lives in CarModelMap.cs — DATA-DRIVEN: a
    // later pass swaps a car by changing that file's data only, never
    // CarVisuals.cs. Detailed Sketchfab models land at
    // Resources/Models/Cars/<Name> and are picked up automatically via
    // CarModelMap.Resolve.
    // Traffic variety: class-based pick, with taxi / van / truck /
    // delivery mixed in by hash so the streets are not all sedans.
    static string ModelForTraffic(Transform root, BodyStyleDef style)
    {
        int h = Mathf.Abs(root.GetInstanceID()) % 10;
        switch (style.name)
        {
            case "SUV": return h < 2 ? "suv-luxury" : "suv";
            case "Hatchback": return "hatchback-sports";
            case "Supercar": return "race";
            case "Hypercar": return "race-future";
            case "Muscle": return "sedan-sports";
            case "GT": return h < 2 ? "sedan" : "sedan-sports";
            case "Coupe": return h < 2 ? "taxi" : "sedan-sports";
            default: // Sedan and anything else
                if (h == 0) return "taxi";
                if (h == 1) return "van";
                if (h == 2) return "truck";
                if (h == 3) return "delivery";
                return "sedan";
        }
    }

    // Rounded-box part shorthand: size is baked into the mesh.
    static GameObject R(string name, Transform parent, Vector3 pos, Vector3 size,
        float corner, Material mat, Vector3 euler = default(Vector3))
    {
        return MeshFactory.PartRounded(name, parent, pos, size, corner, mat, euler);
    }

    public static void Rebuild(Transform root, bool isPlayer)
    {
        if (root == null || root.Find("VisualsDone") != null) return;
        new GameObject("VisualsDone").transform.SetParent(root, false);

        int carIndex = isPlayer ? CarSelection.SelectedIndex : -1;
        Color paint = PaintFor(root, isPlayer);
        // Hero cars get their own silhouette (StyleForCar clones +
        // tweaks the class def); traffic is varied by class.
        BodyStyleDef style = isPlayer
            ? CarBodyStyles.StyleForCar(CarSelection.SelectedIndex)
            : CarBodyStyles.ForClass(CarBodyStyles.ClassForTraffic(root.GetInstanceID()));
        Material paintMat = MeshFactory.Lit("CarPaint", paint, 0.9f, 0.95f);
        Material trimMat = MeshFactory.Lit("CarTrim", new Color(0.05f, 0.05f, 0.06f), 0.6f, 0.5f);
        Material chromeMat = MeshFactory.Lit("Chrome", new Color(0.85f, 0.87f, 0.9f), 1f, 0.95f);
        Material glassMat = GlassMaterial();

        // --- Real imported models (Kenney CC0) replace the procedural
        // body. If the model loads, it builds the whole car (body, wheels,
        // plates, headlight beams) and the procedural path below is
        // skipped. If it fails to load, the procedural body is the
        // fallback, so a missing model never leaves an invisible car. ---
        if (TryBuildKenneyCar(root, isPlayer, carIndex, paint))
            return;

        // --- Body: lofted sculpted shell. ---
        Transform body = root.Find("Body");
        if (body != null)
        {
            Renderer br = body.GetComponent<Renderer>();
            if (br != null && br.sharedMaterial != null)
            {
                Color c = br.material.color;
                if (c.r + c.g + c.b > 0.05f) { paint = c; paintMat.SetColor("_BaseColor", c); paintMat.SetColor("_Color", c); }
            }
            body.localScale = Vector3.one;
            body.localPosition = new Vector3(0f, 0.55f + style.bodyY, 0f);
            MeshFilter mf = body.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = MeshFactory.BuildCarBody(style);
            if (br != null) br.sharedMaterial = paintMat;
        }

        // --- Cabin: tinted glass canopy. ---
        Transform cabin = root.Find("Cabin");
        if (cabin != null)
        {
            cabin.localScale = Vector3.one;
            cabin.localPosition = new Vector3(0f, style.cabinY, style.cabinZ);
            MeshFilter mf = cabin.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = MeshFactory.BuildCabinGlass(style);
            Renderer cr = cabin.GetComponent<Renderer>();
            if (cr != null) cr.sharedMaterial = glassMat;
        }

        // --- Fender bulges over both axles + painted roof panel over
        // the glass cabin (detail pass 2026-11-08: haunches and a real
        // roof instead of a smooth tube + dark glass slab). ---
        AddFenderBulges(root, style, paintMat);
        AddRoofPanel(root, style, paintMat);

        // --- Interior (seats, dash, steering wheel), visible through the
        // glass now that it is lighter (alpha 0.55). ---
        BuildInterior(root, style, trimMat);

        // --- Panel shut lines: thin dark seams outlining the doors and
        // the hood, a few mm proud of the body surface so they read in
        // the light. Widths come from the style loft so they fit every
        // class (hatchback 0.95 m half-width, hypercar 1.03 m). ---
        // Panel shut lines sit ON the door's flat midsection (y 0.52..0.68,
        // where the body is at full width). The old verticals (y 0.63..0.93
        // at x = w[5]) floated up to 0.4 m off the surface on low cars —
        // the body narrows to 0.54 m half-width at y 0.78 and ends at
        // y 0.71-0.79, so the top half of every line hung in mid-air.
        float doorHalfW = style.w[5] + 0.008f;
        float hoodY = 0.55f + style.bodyY + style.top[7] + 0.006f;
        foreach (float sx in new float[] { -1f, 1f })
        {
            float x = sx * doorHalfW;
            // Door outline: front edge, rear edge, bottom edge.
            R("ShutLineDoorFront", root, new Vector3(x, 0.60f, 0.15f),
                new Vector3(0.012f, 0.16f, 0.012f), 0.005f, trimMat);
            R("ShutLineDoorRear", root, new Vector3(x, 0.60f, -0.72f),
                new Vector3(0.012f, 0.16f, 0.012f), 0.005f, trimMat);
            R("ShutLineDoorBottom", root, new Vector3(x, 0.62f, -0.28f),
                new Vector3(0.012f, 0.012f, 0.88f), 0.005f, trimMat);
            // Chrome window trim along the cabin base. Open-top cars get
            // a short trim at the windshield base instead of a full-length
            // strip (there is no rear glass to trim).
            float maxCw = 0f;
            foreach (float cw in style.cw) if (cw > maxCw) maxCw = cw;
            float trimLen = style.openTop ? 0.95f : 1.75f;
            float trimZ = style.openTop ? style.cabinZ + 0.58f : style.cabinZ;
            R("WindowTrim", root, new Vector3(sx * (maxCw + 0.012f), style.cabinY - 0.04f, trimZ),
                new Vector3(0.015f, 0.025f, trimLen), 0.006f, chromeMat);
        }
        // Hood shut line across the front, shortened to the flat part of
        // the hood (the old 1.45 m bar floated 4 cm off the curved nose
        // at its ends) and dropped 1 cm onto the surface.
        R("ShutLineHood", root, new Vector3(0f, hoodY - 0.01f, 1.06f),
            new Vector3(1.00f, 0.012f, 0.012f), 0.005f, trimMat);

        // --- Bumpers / splitter / skirts / diffuser (rounded mouldings).
        // BumperMat is a lighter dark grey than the near-black grille
        // trim, so the fascia reads as separate parts, not one black slab.
        // Widths come from the loft at the bumper z: the old fixed
        // 1.72 m bumpers overhung the tapered nose (only ~1.26 m wide at
        // z 2.02) by 0.23 m per side, like a shelf.
        Material bumperMat = MeshFactory.Lit("CarBumper", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.5f);
        float bumperW = 2f * HalfWidthAt(style, 1.98f) + 0.04f;
        R("FrontBumper", root, new Vector3(0f, 0.38f, 2.02f),
            new Vector3(bumperW, 0.22f, 0.28f), 0.08f, bumperMat);
        R("FrontSplitter", root, new Vector3(0f, 0.27f, 2.08f),
            new Vector3(bumperW + 0.06f, 0.07f, 0.30f), 0.03f, bumperMat);
        R("RearBumper", root, new Vector3(0f, 0.38f, -2.02f),
            new Vector3(bumperW, 0.22f, 0.28f), 0.08f, bumperMat);
        R("RearDiffuser", root, new Vector3(0f, 0.28f, -2.08f),
            new Vector3(bumperW - 0.06f, 0.10f, 0.22f), 0.04f, bumperMat);
        // Diffuser fins: three vertical vanes under the rear bumper.
        foreach (float fx in new float[] { -0.45f, 0f, 0.45f })
            R("DiffuserFin", root, new Vector3(fx, 0.26f, -2.14f),
                new Vector3(0.03f, 0.12f, 0.20f), 0.01f, trimMat);
        foreach (float sx in new float[] { -1f, 1f })
            R("SideSkirt", root, new Vector3(sx * (HalfWidthAt(style, 0f) + 0.005f), 0.30f, 0f),
                new Vector3(0.08f, 0.12f, 2.20f), 0.04f, bumperMat);

        // --- Grille + lower intake (width/height per body style) + slats.
        // The grille sits on the nose cap, so its width is clamped to the
        // nose width at its height (a 1.5 m grille overhung the 1.16 m
        // nose tip) and its front face stands 0.02 m proud of the bumper
        // face (the old flush faces z-fought).
        float grilleY = 0.55f + style.bodyY + TopAt(style, 2.08f) - 0.09f;
        float grilleW = Mathf.Min(style.grilleW, 2f * BodyHalfWidthAt(style, 2.08f, grilleY) - 0.06f);
        R("Grille", root, new Vector3(0f, grilleY, 2.15f),
            new Vector3(grilleW, style.grilleH, 0.06f), 0.03f, trimMat);
        R("GrilleTrim", root, new Vector3(0f, grilleY + style.grilleH / 2f + 0.015f, 2.185f),
            new Vector3(grilleW + 0.05f, 0.03f, 0.03f), 0.01f, chromeMat);
        if (style.name == "GT" || style.name == "Supercar" || style.name == "Hypercar")
            for (int i = -1; i <= 1; i++)
                R("GrilleSlat", root, new Vector3(i * grilleW * 0.28f, grilleY, 2.185f),
                    new Vector3(0.025f, style.grilleH * 0.85f, 0.02f), 0.008f, chromeMat);

        // --- Hood scoop (muscle / GT styles only): seated on the hood
        // curve and tilted to its slope (the old flat scoop at y 0.86
        // floated ~5 cm above the muscle hood).
        if (style.scoop)
            R("HoodScoop", root, new Vector3(0f, 0.55f + style.bodyY + TopAt(style, 1.25f) + 0.03f, 1.25f),
                new Vector3(0.55f, 0.10f, 0.60f), 0.04f, trimMat, new Vector3(8f, 0f, 0f));

        // --- Mirrors: mounted at the beltline — the body top at the
        // mirror z minus 6 cm — where the door is widest. The old
        // y 0.85 mount sat above the body shoulder (top 0.69-0.81 on
        // low cars), so the housings and their stalks floated in
        // mid-air 0.15-0.30 m off the glass.
        float mirrorY = 0.55f + style.bodyY + TopAt(style, 0.55f) - 0.06f;
        float mirrorX = BodyHalfWidthAt(style, 0.55f, mirrorY);
        foreach (float sx in new float[] { -1.0f, 1.0f })
        {
            R("MirrorStalk", root, new Vector3(sx * (mirrorX + 0.02f), mirrorY, 0.55f),
                new Vector3(0.14f, 0.05f, 0.08f), 0.015f, paintMat);
            R("Mirror", root, new Vector3(sx * (mirrorX + 0.10f), mirrorY + 0.01f, 0.55f),
                new Vector3(0.15f, 0.09f, 0.20f), 0.035f, paintMat);
            R("MirrorGlass", root, new Vector3(sx * (mirrorX + 0.10f), mirrorY + 0.01f, 0.46f),
                new Vector3(0.02f, 0.07f, 0.15f), 0.008f, chromeMat);
        }

        // --- Door handles, side indicators, licence plates WITH text.
        // Handles and indicators sit on the door's flat midsection
        // (y 0.62-0.64), at the body's exact half-width there — the old
        // y 0.80 handles and y 0.70 indicators at x ±0.96 floated off
        // the narrowing shoulder on low cars.
        Material plateMat = MeshFactory.Lit("Plate", new Color(0.92f, 0.92f, 0.88f), 0.2f, 0.5f);
        Material indicatorMat = MeshFactory.Emissive("Indicator", new Color(1f, 0.55f, 0.05f), 1.6f);
        foreach (float sx in new float[] { -1f, 1f })
        {
            foreach (float hz in new float[] { 0.35f, -0.45f })
                R("DoorHandle", root, new Vector3(sx * (BodyHalfWidthAt(style, hz, 0.64f) + 0.005f), 0.64f, hz),
                    new Vector3(0.03f, 0.05f, 0.25f), 0.01f, chromeMat);
            R("SideIndicator", root, new Vector3(sx * (BodyHalfWidthAt(style, 1.55f, 0.62f) + 0.005f), 0.62f, 1.55f),
                new Vector3(0.03f, 0.06f, 0.18f), 0.01f, indicatorMat);
        }
        string plateText = isPlayer
            ? "HZ-" + carIndex.ToString("00")
            : "TK-" + (100 + Mathf.Abs(root.GetInstanceID()) % 900).ToString();
        // Plate glyphs face -Z (verified on screen in round 6: rotY 0
        // on the front plate read mirrored). So the FRONT text is
        // rotated 180° (readable side toward +Z) and the REAR text
        // stays at 0° (readable side toward -Z).
        AddPlate(root, new Vector3(0f, 0.32f, 2.17f), plateText, plateMat, 180f);
        AddPlate(root, new Vector3(0f, 0.45f, -2.17f), plateText, plateMat, 0f);

        // --- Side air intakes behind the doors (supercar / hypercar).
        // Seated on the body surface at y 0.68 (the old y 0.72 at
        // x ±0.97 floated off the shoulder).
        if (style.name == "Supercar" || style.name == "Hypercar" || style.name == "GT")
            foreach (float sx in new float[] { -1f, 1f })
                R("SideIntake", root, new Vector3(sx * (BodyHalfWidthAt(style, -0.85f, 0.68f) + 0.005f), 0.68f, -0.85f),
                    new Vector3(0.06f, 0.16f, 0.55f), 0.025f, trimMat);

        // --- Spoiler: airfoil blade (not a flat box) + struts + endplates.
        // The deck height at the wing z comes from the loft (TopAt):
        // the old fixed struts (y 0.83..1.13) floated up to 16 cm above
        // the rear deck on low cars (BigWing struts ended at 1.13 while
        // the blade sat at 1.24, and the deck itself is only ~0.66) —
        // the classic floating-spoiler defect. Struts now run from the
        // deck surface to the blade, and a lip spoiler sits ON the deck.
        Transform spoiler = root.Find("Spoiler");
        bool hasWing = style.wing != WingType.None;
        float deckY = 0.55f + style.bodyY + TopAt(style, -1.92f);
        float wingY = style.wing == WingType.Lip ? deckY + 0.035f
            : style.wing == WingType.BigWing ? 1.24f : 1.12f;
        float wingW = style.wing == WingType.Lip ? 1.30f
            : style.wing == WingType.BigWing ? 2.05f : 1.80f;
        float chord = style.wing == WingType.Lip ? 0.35f : 0.50f;
        if (spoiler != null)
        {
            if (!hasWing) spoiler.gameObject.SetActive(false);
            else
            {
                spoiler.localPosition = new Vector3(0f, wingY, -1.92f);
                spoiler.localScale = Vector3.one;
                MeshFilter smf = spoiler.GetComponent<MeshFilter>();
                if (smf != null) smf.sharedMesh = MeshFactory.BuildAirfoil(wingW, chord, 0.055f);
                Renderer sr = spoiler.GetComponent<Renderer>();
                if (sr != null) sr.sharedMaterial = paintMat;
            }
        }
        if (hasWing && style.wing != WingType.Lip)
        {
            // Struts: deck surface -> blade underside, embedded 0.03 m
            // at both ends so nothing floats and nothing z-fights.
            // Endplates sit at the blade tips.
            float strutH = (wingY - deckY) + 0.06f;
            foreach (float sx in new float[] { -1f, 1f })
            {
                R("SpoilerStrut", root, new Vector3(sx * 0.62f, (deckY + wingY) * 0.5f, -1.92f),
                    new Vector3(0.08f, strutH, 0.10f), 0.03f, trimMat);
                R("SpoilerEndplate", root, new Vector3(sx * (wingW * 0.5f - 0.02f), wingY, -1.92f),
                    new Vector3(0.04f, 0.22f, 0.55f), 0.015f, trimMat);
            }
        }

        // --- Headlights: shaped assemblies — a SMOKED housing (dark,
        // metallic, glossy — like a real headlight cover) with two
        // glowing projector lenses + chrome rings inside it. The old
        // all-emissive housing rendered as one solid white light-bar
        // across the whole front; the lamps must read as distinct
        // projector clusters, not a bar.
        // The housings sit ON the nose: their z comes from the loft
        // surface at (±0.6, 0.60) — the old prefab z 2.12 floated the
        // housings ~0.15 m ahead of the tapered nose (the surface at
        // that x/y is back at z ~1.90). The housing is embedded 0.05 m
        // into the nose so its face reads flush.
        Material smokedMat = MeshFactory.Lit("HeadlightSmoked", new Color(0.08f, 0.09f, 0.11f), 0.9f, 0.95f);
        Material lensMat = MeshFactory.Emissive("ProjectorLens", new Color(0.75f, 0.85f, 1f), 3.2f);
        float noseZ = SurfaceZAt(style, 0.6f, 0.60f);
        foreach (Transform t in root)
        {
            if (t.name != "Headlight") continue;
            MeshFilter hmf = t.GetComponent<MeshFilter>();
            if (hmf != null) hmf.sharedMesh = MeshFactory.BuildRoundedBox(0.38f, 0.13f, 0.12f, 0.045f);
            t.localScale = Vector3.one;
            Renderer hr = t.GetComponent<Renderer>();
            if (hr != null) hr.sharedMaterial = smokedMat;
            float hx = t.localPosition.x;
            t.localPosition = new Vector3(hx, 0.60f, noseZ + 0.01f);
            foreach (float off in new float[] { -0.10f, 0.10f })
            {
                // Projector lens: domed lathe disc facing forward.
                GameObject lens = MeshFactory.PartMesh("ProjectorLens",
                    MeshFactory.BuildLens(0.052f, 0.05f), root,
                    new Vector3(hx + off, 0.60f, noseZ + 0.075f), Vector3.one, lensMat,
                    new Vector3(90f, 0f, 0f));
                lens.transform.localPosition = new Vector3(hx + off, 0.60f, noseZ + 0.075f);
                // Chrome ring around the projector.
                GameObject ring = new GameObject("ProjectorRing");
                ring.transform.SetParent(root, false);
                ring.transform.localPosition = new Vector3(hx + off, 0.60f, noseZ + 0.07f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                MeshFilter rmf = ring.AddComponent<MeshFilter>();
                rmf.sharedMesh = MeshFactory.BuildTorus(0.058f, 0.008f, 18, 8);
                MeshRenderer rmr = ring.AddComponent<MeshRenderer>();
                rmr.sharedMaterial = chromeMat;
            }
        }
        // Fog lamps (GT / supercar / hypercar / coupe), seated on the
        // nose face, 0.01 m proud of the bumper face.
        if (style.name == "GT" || style.name == "Supercar" || style.name == "Hypercar" || style.name == "Coupe")
            foreach (float sx in new float[] { -0.55f, 0.55f })
                MeshFactory.PartMesh("FogLamp", MeshFactory.BuildLens(0.06f, 0.05f), root,
                    new Vector3(sx, 0.42f, 2.17f), Vector3.one, lensMat, new Vector3(90f, 0f, 0f));

        // --- Taillights: full-width LED bar + style-specific lenses.
        // The bar sits on the tail at y 0.66, its width from the body
        // half-width there — the old 1.55 m bar at y 0.72 overhung the
        // narrow tail top by ~0.4 m per side.
        Material tailMat = MeshFactory.Emissive("Taillight", new Color(1f, 0.08f, 0.08f), 2.2f);
        float tailW = 2f * BodyHalfWidthAt(style, -2.06f, 0.66f) + 0.02f;
        R("TaillightBar", root, new Vector3(0f, 0.66f, -2.11f),
            new Vector3(tailW, 0.10f, 0.06f), 0.03f, tailMat);
        // Muscle cars get round taillights as well as the bar, seated
        // on the tail surface.
        if (style.name == "Muscle")
            foreach (float sx in new float[] { -0.55f, 0.55f })
                MeshFactory.PartMesh("TaillightRound", MeshFactory.BuildLens(0.10f, 0.05f), root,
                    new Vector3(sx, 0.66f, SurfaceZRearAt(style, sx, 0.66f) + 0.005f), Vector3.one, tailMat, new Vector3(-90f, 0f, 0f));

        // --- Exhausts: dual chrome pipes by default; heroes override. ---
        bool heroExhaust = AddHeroDetails(root, carIndex, style, paintMat, trimMat, chromeMat, tailMat, lensMat);
        if (!heroExhaust)
            foreach (float sx in new float[] { -0.28f, 0.28f })
                MeshFactory.Part("Exhaust", "Cylinder.fbx", root,
                    new Vector3(sx, 0.34f, -2.14f), new Vector3(0.16f, 0.16f, 0.16f), chromeMat,
                    new Vector3(90f, 0f, 0f));

        // --- Headlight beams (player only, 2 spotlights), from the
        // repositioned headlights on the nose.
        if (isPlayer) AddHeadlightBeams(root, style);

        // --- LED daytime-running-light strip (styles that have one),
        // seated on the nose cap at y 0.50 (the old y 0.62 strip sat
        // above the nose cap's top edge, floating ahead of the car).
        if (style.drl)
            R("DRLStrip", root, new Vector3(0f, 0.50f, SurfaceZAt(style, 0f, 0.50f) + 0.02f),
                new Vector3(2f * BodyHalfWidthAt(style, 2.05f, 0.50f) - 0.08f, 0.05f, 0.04f), 0.015f,
                MeshFactory.Emissive("DRL", new Color(0.85f, 0.95f, 1f), 2.0f));

        // --- Wheels: torus tyre + rim + spokes + brake disc + hub. ---
        Material rimOverride = null;
        if (carIndex == 38) // R34 GT-R: gold rims, like the real Bayside car.
            rimOverride = MeshFactory.Lit("RimGold", new Color(0.85f, 0.65f, 0.25f), 0.9f, 0.8f);
        foreach (string w in new string[] { "WheelMeshFL", "WheelMeshFR", "WheelMeshRL", "WheelMeshRR" })
        {
            Transform wm = root.Find(w);
            if (wm != null) RebuildWheel(wm, trimMat, chromeMat, style, rimOverride);
        }
    }

    /// <summary>Licence plate: rounded white base + a readable 3D text
    /// ("HZ-27" for the player car, "TK-123" for traffic). Size is
    /// analytic (line height ≈ fontSize * characterSize * 0.1): 100 *
    /// 0.012 * 0.1 = 0.12 m line, ~0.084 m cap height — fills the 0.12 m
    /// plate without needing a bounds pass (the old bounds pass shrank
    /// the text and measured world-space bounds of a rotated object).</summary>
    static void AddPlate(Transform root, Vector3 pos, string text, Material plateMat, float rotY)
    {
        R("Plate", root, pos, new Vector3(0.55f, 0.12f, 0.02f), 0.01f, plateMat);
        // Text via TextMeshPro (the project's real text system — its
        // font asset and shader are already in the build through the
        // HUD). Legacy TextMesh + a hand-built alpha-test material
        // rendered solid black cells in round 5: the built-in font's
        // material.mainTexture is not the glyph atlas at build time.
        // Round 6 proved TMP glyphs face -Z like legacy TextMesh (the
        // front text read mirrored with rotY 0), so the legacy rotY
        // convention applies. TMP's Distance Field shader defaults to
        // Cull Off, so the rear plate's text showed through the glass
        // as a ghost "X" on the front fascia (rounds 2-6) — a cloned
        // material with Cull Back removes it.
        GameObject go = new GameObject("PlateText");
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos + new Vector3(0f, 0f, pos.z > 0f ? 0.015f : -0.015f);
        go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) tmp.font = font;
        else if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = 36f;
        tmp.characterSpacing = 0f;
        tmp.color = new Color(0.05f, 0.10f, 0.35f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.ForceMeshUpdate();
        if (tmp.fontSharedMaterial != null)
        {
            Material m = new Material(tmp.fontSharedMaterial);
            m.SetInt("_CullMode", 2); // Back
            tmp.fontMaterial = m;
        }
        float h = tmp.textBounds.size.y;
        if (h > 0.001f)
        {
            float s = 0.085f / h;
            if (tmp.textBounds.size.x * s > 0.51f) s = 0.51f / tmp.textBounds.size.x;
            go.transform.localScale = Vector3.one * s;
        }
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    /// <summary>Signature details for the hero cars. Returns true when
    /// the hero defines its own exhaust (so the default dual pipes are
    /// skipped). Every other car keeps the class-default details.</summary>
    static bool AddHeroDetails(Transform root, int carIndex, BodyStyleDef style,
        Material paintMat, Material trimMat, Material chromeMat, Material tailMat, Material lensMat)
    {
        switch (carIndex)
        {
            case 0: // City Sprint — city hatch: fog lamps + side moulding.
                foreach (float sx in new float[] { -0.55f, 0.55f })
                    MeshFactory.PartMesh("FogLampCity", MeshFactory.BuildLens(0.05f, 0.04f), root,
                        new Vector3(sx, 0.42f, 2.17f), Vector3.one, lensMat, new Vector3(90f, 0f, 0f));
                foreach (float sx in new float[] { -1f, 1f })
                    R("SideMoulding", root, new Vector3(sx * (BodyHalfWidthAt(style, -0.2f, 0.62f) + 0.005f), 0.62f, -0.2f),
                        new Vector3(0.03f, 0.06f, 1.80f), 0.01f, trimMat);
                return false;
            case 1: // Street GT — long-hood tourer: hood vents + centre dual exhaust.
                foreach (float sx in new float[] { -0.35f, 0.35f })
                    R("HoodVent", root, new Vector3(sx, 0.55f + style.bodyY + TopAt(style, 1.35f) + 0.01f, 1.35f),
                        new Vector3(0.25f, 0.03f, 0.32f), 0.01f, trimMat, new Vector3(10f, 0f, 0f));
                foreach (float sx in new float[] { -0.14f, 0.14f })
                    MeshFactory.Part("Exhaust", "Cylinder.fbx", root,
                        new Vector3(sx, 0.34f, -2.14f), new Vector3(0.14f, 0.14f, 0.16f), chromeMat,
                        new Vector3(90f, 0f, 0f));
                return true;
            case 2: // Desert Fox — SUV: side steps + front skid plate.
                foreach (float sx in new float[] { -1f, 1f })
                    R("SideStep", root, new Vector3(sx * (HalfWidthAt(style, 0f) + 0.02f), 0.32f, 0f),
                        new Vector3(0.10f, 0.06f, 2.20f), 0.02f, trimMat);
                R("SkidPlate", root, new Vector3(0f, 0.26f, 2.06f),
                    new Vector3(1.10f, 0.05f, 0.25f), 0.02f, chromeMat);
                return false;
            case 3: // Night Hawk — muscle: side exhaust pipes + twin racing stripes.
                foreach (float sx in new float[] { -1f, 1f })
                    MeshFactory.Part("SidePipe", "Cylinder.fbx", root,
                        new Vector3(sx * (BodyHalfWidthAt(style, 0.2f, 0.30f) + 0.01f), 0.30f, 0.2f),
                        new Vector3(0.09f, 2.10f, 0.09f), chromeMat, new Vector3(90f, 0f, 0f));
                AddRacingStripes(root, style, trimMat);
                return false;
            case 4: // Horizon R — supercar: hood vents + centre quad exhaust.
                foreach (float sx in new float[] { -0.35f, 0.35f })
                    R("HoodVent", root, new Vector3(sx, 0.55f + style.bodyY + TopAt(style, 1.35f) + 0.01f, 1.35f),
                        new Vector3(0.25f, 0.03f, 0.32f), 0.01f, trimMat, new Vector3(10f, 0f, 0f));
                AddCentreExhaust(root, chromeMat, 4, 0.09f);
                return true;
            case 6: // Apex GT — open-top roadster: roll hoops behind the
                    // seats + centre quad exhaust. Hoop bottoms sit 3 cm
                    // inside the rear deck; tops clear the headrests.
                foreach (float sx in new float[] { -0.38f, 0.38f })
                {
                    GameObject hoop = new GameObject("RollHoop");
                    hoop.transform.SetParent(root, false);
                    hoop.transform.localPosition = new Vector3(sx, 0.92f, -0.80f);
                    hoop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    MeshFilter hmf = hoop.AddComponent<MeshFilter>();
                    hmf.sharedMesh = MeshFactory.BuildTorus(0.15f, 0.028f, 20, 8);
                    MeshRenderer hmr = hoop.AddComponent<MeshRenderer>();
                    hmr.sharedMaterial = paintMat;
                }
                AddCentreExhaust(root, chromeMat, 4, 0.09f);
                return true;
            case 7: // Horizon X — hypercar flagship: black lower band,
                    // hood vents, centre quad exhaust.
                foreach (float sx in new float[] { -1f, 1f })
                    R("LowerBand", root, new Vector3(sx * (BodyHalfWidthAt(style, 0f, 0.45f) + 0.005f), 0.45f, 0f),
                        new Vector3(0.02f, 0.10f, 1.80f), 0.01f, trimMat);
                foreach (float sx in new float[] { -0.35f, 0.35f })
                    R("HoodVent", root, new Vector3(sx, 0.55f + style.bodyY + TopAt(style, 1.35f) + 0.01f, 1.35f),
                        new Vector3(0.25f, 0.03f, 0.32f), 0.01f, trimMat, new Vector3(10f, 0f, 0f));
                AddCentreExhaust(root, chromeMat, 4, 0.09f);
                return true;
            case 30: // Maseratii MC20 — mid-engine supercar: low intake
                     // below the main grille + hood vents + quad exhaust.
                R("MC20Intake", root, new Vector3(0f, 0.34f, 2.17f),
                    new Vector3(1.05f, 0.10f, 0.05f), 0.04f, trimMat);
                foreach (float sx in new float[] { -0.35f, 0.35f })
                    R("HoodVent", root, new Vector3(sx, 0.55f + style.bodyY + TopAt(style, 1.35f) + 0.01f, 1.35f),
                        new Vector3(0.25f, 0.03f, 0.32f), 0.01f, trimMat, new Vector3(10f, 0f, 0f));
                AddCentreExhaust(root, chromeMat, 4, 0.09f);
                return true;
            case 27: // Ferrarri LaFerrarri — side intakes, centre quad exhaust.
                AddSideScoops(root, style, trimMat, 0.20f, 0.60f);
                AddCentreExhaust(root, chromeMat, 4, 0.09f);
                return true;
            case 33: // Bpcati Chiron — C-line chrome side strips, horseshoe
                     // grille (wider), centre quad exhaust, black lower band.
                     // C-line and band sit on the body surface (the old
                     // y 0.84 C-line floated above the hypercar deck).
                R("ChironGrille", root, new Vector3(0f, 0.46f, 2.17f),
                    new Vector3(2f * BodyHalfWidthAt(style, 2.08f, 0.46f) - 0.06f, 0.18f, 0.05f), 0.04f, trimMat);
                foreach (float sx in new float[] { -1f, 1f })
                {
                    R("ChironCLine", root, new Vector3(sx * (BodyHalfWidthAt(style, 0.75f, 0.66f) + 0.005f), 0.66f, 0.75f),
                        new Vector3(0.02f, 0.03f, 0.95f), 0.008f, chromeMat,
                        new Vector3(0f, sx * 12f, 0f));
                    R("ChironLowerBand", root, new Vector3(sx * (BodyHalfWidthAt(style, 0f, 0.45f) + 0.005f), 0.45f, 0f),
                        new Vector3(0.02f, 0.10f, 1.80f), 0.01f, trimMat);
                }
                AddCentreExhaust(root, chromeMat, 4, 0.09f);
                return true;
            case 38: // MSAIN R34 GT — four round taillights (the real R34
                     // signature), hood vents, big dual exhaust, gold rims.
                     // Round lenses sit on the tail surface; vents sit on
                     // the hood curve, tilted to its slope (the old flat
                     // vents at y 0.86 floated ~9 cm above the hood).
                foreach (float sx in new float[] { -0.68f, -0.45f, 0.45f, 0.68f })
                    MeshFactory.PartMesh("TaillightRoundR34", MeshFactory.BuildLens(0.09f, 0.05f), root,
                        new Vector3(sx, 0.66f, SurfaceZRearAt(style, sx, 0.66f) + 0.005f), Vector3.one, tailMat, new Vector3(-90f, 0f, 0f));
                foreach (float sx in new float[] { -0.35f, 0.35f })
                    R("HoodVent", root, new Vector3(sx, 0.55f + style.bodyY + TopAt(style, 1.35f) + 0.01f, 1.35f),
                        new Vector3(0.28f, 0.03f, 0.32f), 0.01f, trimMat, new Vector3(10f, 0f, 0f));
                foreach (float sx in new float[] { -0.32f, 0.32f })
                    MeshFactory.Part("Exhaust", "Cylinder.fbx", root,
                        new Vector3(sx, 0.34f, -2.14f), new Vector3(0.20f, 0.20f, 0.18f), chromeMat,
                        new Vector3(90f, 0f, 0f));
                return true;
            case 49: // Bpcati Tourbillon — horseshoe grille, centre quad exhaust.
                R("TourbillonGrille", root, new Vector3(0f, 0.50f, 2.16f),
                    new Vector3(1.60f, 0.15f, 0.05f), 0.04f, trimMat);
                AddCentreExhaust(root, chromeMat, 4, 0.09f);
                return true;
            case 43: // Lexuss LFA — triple centre exhaust (stacked triangle),
                     // side intakes.
                AddSideScoops(root, style, trimMat, 0.16f, 0.50f);
                foreach (Vector3 p in new Vector3[]
                    { new Vector3(-0.09f, 0.30f, -2.14f), new Vector3(0.09f, 0.30f, -2.14f),
                      new Vector3(0f, 0.42f, -2.14f) })
                    MeshFactory.Part("Exhaust", "Cylinder.fbx", root,
                        p, new Vector3(0.11f, 0.11f, 0.16f), chromeMat, new Vector3(90f, 0f, 0f));
                return true;
            case 39: // Toyata Supra — hood vents + ducktail lip + dual exhaust.
                foreach (float sx in new float[] { -0.35f, 0.35f })
                    R("HoodVent", root, new Vector3(sx, 0.55f + style.bodyY + TopAt(style, 1.30f) + 0.01f, 1.30f),
                        new Vector3(0.25f, 0.03f, 0.32f), 0.01f, trimMat, new Vector3(10f, 0f, 0f));
                return false;
            case 20: // Forde Mustang GT — tri-bar taillights + round
                     // headlight rings + bigger scoop. Tri-bars sit on
                     // the tail surface (the old z -2.15 bars floated
                     // 5 cm behind the tail).
                foreach (float sx in new float[] { -1f, 1f })
                    for (int i = -1; i <= 1; i++)
                    {
                        float bx = sx * 0.55f + i * 0.11f;
                        R("TriBarTaillight", root,
                            new Vector3(bx, 0.66f, SurfaceZRearAt(style, bx, 0.66f) - 0.01f),
                            new Vector3(0.08f, 0.16f, 0.04f), 0.01f, tailMat);
                    }
                foreach (float sx in new float[] { -0.6f, 0.6f })
                {
                    GameObject ring = new GameObject("HeadlightRing");
                    ring.transform.SetParent(root, false);
                    ring.transform.localPosition = new Vector3(sx, 0.60f, 2.17f);
                    ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    MeshFilter rmf = ring.AddComponent<MeshFilter>();
                    rmf.sharedMesh = MeshFactory.BuildTorus(0.075f, 0.010f, 18, 8);
                    MeshRenderer rmr = ring.AddComponent<MeshRenderer>();
                    rmr.sharedMaterial = chromeMat;
                }
                return false;
            case 14: // Porscha 911 Carrera — round headlamps on the fenders,
                     // seated on the nose surface (the old z 2.08 lamps
                     // floated ahead of the tapered nose).
                foreach (float sx in new float[] { -0.55f, 0.55f })
                    MeshFactory.PartMesh("RoundHeadlamp", MeshFactory.BuildLens(0.11f, 0.06f), root,
                        new Vector3(sx, 0.68f, SurfaceZAt(style, sx, 0.68f) + 0.01f), Vector3.one, lensMat, new Vector3(90f, 0f, 0f));
                return false;
            case 29: // Lamborghinni Countach — NACA side ducts + pop-up
                     // headlight humps on the hood, seated on the hood
                     // curve (the old y 0.82 humps floated ~0.24 m above
                     // the low Countach hood).
                AddSideScoops(root, style, trimMat, 0.18f, 0.55f);
                foreach (float sx in new float[] { -0.55f, 0.55f })
                    R("PopupHump", root, new Vector3(sx, 0.55f + style.bodyY + TopAt(style, 1.55f) + 0.02f, 1.55f),
                        new Vector3(0.34f, 0.05f, 0.40f), 0.025f, paintMat, new Vector3(8f, 0f, 0f));
                return false;
            default:
                return false;
        }
    }

    static void AddSideScoops(Transform root, BodyStyleDef style, Material trimMat, float h, float len)
    {
        // Seated on the body surface at y 0.68 (the old y 0.76 at
        // x ±0.97 floated off the shoulder on low cars).
        foreach (float sx in new float[] { -1f, 1f })
            R("SideScoop", root, new Vector3(sx * (BodyHalfWidthAt(style, -0.55f, 0.68f) + 0.005f), 0.68f, -0.55f),
                new Vector3(0.07f, h, len), 0.025f, trimMat);
    }

    static void AddCentreExhaust(Transform root, Material chromeMat, int count, float dia)
    {
        float start = -(count - 1) * 0.5f * (dia + 0.03f);
        for (int i = 0; i < count; i++)
            MeshFactory.Part("Exhaust", "Cylinder.fbx", root,
                new Vector3(start + i * (dia + 0.03f), 0.34f, -2.14f),
                new Vector3(dia, dia, 0.16f), chromeMat, new Vector3(90f, 0f, 0f));
    }

    // --- Exact body-surface helpers (2026-11-08 detail pass). The old
    // code placed side details at hard-coded x = ±0.96/0.97 and
    // y = 0.70-0.86, but the lofted body narrows sharply above its
    // waist: at y 0.78 a GT body is only 0.54 m half-wide (verified
    // numerically), so mirrors, handles, indicators, shut lines and
    // spoilers floated centimetres off the surface. These helpers read
    // the real loft, so every side part sits ON the surface by
    // construction. ---

    /// <summary>Body half-width at station z (linear interpolation
    /// over the style's loft stations).</summary>
    static float HalfWidthAt(BodyStyleDef style, float z)
    {
        return Interp(style.z, style.w, z);
    }

    /// <summary>Body top height (relative to the body origin) at z.</summary>
    static float TopAt(BodyStyleDef style, float z)
    {
        return Interp(style.z, style.top, z);
    }

    static float Interp(float[] xs, float[] ys, float x)
    {
        if (x <= xs[0]) return ys[0];
        if (x >= xs[xs.Length - 1]) return ys[ys.Length - 1];
        for (int i = 0; i < xs.Length - 1; i++)
        {
            if (x >= xs[i] && x <= xs[i + 1])
            {
                float t = (x - xs[i]) / (xs[i + 1] - xs[i]);
                return ys[i] + t * (ys[i + 1] - ys[i]);
            }
        }
        return ys[ys.Length - 1];
    }

    /// <summary>Exact body half-width at world height yWorld and
    /// station z: inverts the superellipse section (squash 0.55, the
    /// same constant BuildLoft uses) so a part placed at this x is
    /// exactly on the body surface at that height.</summary>
    static float BodyHalfWidthAt(BodyStyleDef style, float z, float yWorld)
    {
        float w = HalfWidthAt(style, z);
        float top = TopAt(style, z);
        float bottom = -0.30f;
        float yLocal = yWorld - (0.55f + style.bodyY);
        float yc = (top + bottom) * 0.5f, h = (top - bottom) * 0.5f;
        float s = Mathf.Clamp((yLocal - yc) / h, -1f, 1f);
        float sn = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 1f / 0.55f);
        float cs = Mathf.Sqrt(Mathf.Max(0f, 1f - sn * sn));
        return w * Mathf.Pow(cs, 0.55f);
    }

    /// <summary>Body-surface z at lateral position x and world height
    /// yWorld, scanning from the nose backward. Used to seat the
    /// headlights, fog lamps and DRL on the tapered nose instead of
    /// floating ahead of it.</summary>
    static float SurfaceZAt(BodyStyleDef style, float x, float yWorld)
    {
        float ax = Mathf.Abs(x);
        for (int i = style.z.Length - 1; i > 0; i--)
        {
            float w1 = BodyHalfWidthAt(style, style.z[i], yWorld);
            float w0 = BodyHalfWidthAt(style, style.z[i - 1], yWorld);
            if ((w1 <= ax && ax <= w0) || (w0 <= ax && ax <= w1))
            {
                float t = (ax - w1) / (w0 - w1 + 0.0001f);
                return Mathf.Lerp(style.z[i], style.z[i - 1], t);
            }
        }
        return style.z[style.z.Length - 1];
    }

    /// <summary>Body-surface z at the rear (scans from the tail
    /// forward). Used for the taillights and rear lenses.</summary>
    static float SurfaceZRearAt(BodyStyleDef style, float x, float yWorld)
    {
        float ax = Mathf.Abs(x);
        for (int i = 0; i < style.z.Length - 1; i++)
        {
            float w0 = BodyHalfWidthAt(style, style.z[i], yWorld);
            float w1 = BodyHalfWidthAt(style, style.z[i + 1], yWorld);
            if ((w0 <= ax && ax <= w1) || (w1 <= ax && ax <= w0))
            {
                float t = (ax - w0) / (w1 - w0 + 0.0001f);
                return Mathf.Lerp(style.z[i], style.z[i + 1], t);
            }
        }
        return style.z[0];
    }

    /// <summary>Fender bulges: a short loft shell around each axle,
    /// 5-35 mm wider than the body at the wheel stations and 10 mm
    /// lower on top (so only the flanks stand proud — the top stays
    /// hidden inside the body). Gives the car real haunches over the
    /// wheels instead of a smooth tube with tyres poking out of it.
    /// Wheel centres are the WheelCollider positions (±0.85, 0.35,
    /// ±1.35).</summary>
    static void AddFenderBulges(Transform root, BodyStyleDef style, Material paintMat)
    {
        foreach (float wheelZ in new float[] { 1.35f, -1.35f })
        {
            float[] zs = { wheelZ - 0.50f, wheelZ - 0.30f, wheelZ, wheelZ + 0.30f, wheelZ + 0.50f };
            float[] bulge = { 0.005f, 0.028f, 0.035f, 0.028f, 0.005f };
            float[] w = new float[5], top = new float[5];
            for (int i = 0; i < 5; i++)
            {
                w[i] = HalfWidthAt(style, zs[i]) + bulge[i];
                top[i] = TopAt(style, zs[i]) - 0.01f;
            }
            Mesh mesh = MeshFactory.BuildLoft(zs, w, top, -0.28f, 12, 0.55f);
            MeshFactory.PartMesh(wheelZ > 0f ? "FenderBulgeF" : "FenderBulgeR", mesh, root,
                new Vector3(0f, 0.55f + style.bodyY, 0f), Vector3.one, paintMat);
        }
    }

    /// <summary>Painted roof panel: a partial loft (top arc only) over
    /// the glass cabin, 6-8 mm proud of the glass. The old car read as
    /// one dark glass slab; with a painted roof the cabin reads as
    /// roof + windshield + rear glass, like a real car. Open-top cars
    /// skip it (windshield only, no roof).</summary>
    static void AddRoofPanel(Transform root, BodyStyleDef style, Material paintMat)
    {
        if (style.openTop) return;
        int n = style.cz.Length;
        int i0 = 1, i1 = n - 3;
        if (i1 <= i0) return;
        int count = i1 - i0 + 1;
        float[] z = new float[count], w = new float[count], top = new float[count];
        for (int i = 0; i < count; i++)
        {
            z[i] = style.cz[i0 + i];
            w[i] = style.cw[i0 + i] + 0.006f;
            top[i] = style.ctop[i0 + i] + 0.008f;
        }
        Mesh mesh = MeshFactory.BuildLoftArc(z, w, top, -0.24f, 12, 0.6f, 25f, 155f);
        MeshFactory.PartMesh("RoofPanel", mesh, root,
            new Vector3(0f, style.cabinY, style.cabinZ), Vector3.one, paintMat);
    }

    /// <summary>Twin racing stripes over the hood and deck: two narrow
    /// top-arc loft skins (82-86° and 94-98°) that follow the body's
    /// top curve exactly, 6 mm proud. A flat stripe box would float
    /// centimetres off the curved deck at its ends.</summary>
    static void AddRacingStripes(Transform root, BodyStyleDef style, Material mat)
    {
        float[] top = new float[style.top.Length];
        for (int i = 0; i < top.Length; i++) top[i] = style.top[i] + 0.006f;
        Mesh left = MeshFactory.BuildLoftArc(style.z, style.w, top, -0.30f, 4, 0.55f, 82f, 86f);
        Mesh right = MeshFactory.BuildLoftArc(style.z, style.w, top, -0.30f, 4, 0.55f, 94f, 98f);
        MeshFactory.PartMesh("RacingStripeL", left, root,
            new Vector3(0f, 0.55f + style.bodyY, 0f), Vector3.one, mat);
        MeshFactory.PartMesh("RacingStripeR", right, root,
            new Vector3(0f, 0.55f + style.bodyY, 0f), Vector3.one, mat);
    }

    static Color PaintFor(Transform root, bool isPlayer)
    {
        if (isPlayer)
        {
            Transform body = root.Find("Body");
            if (body != null)
            {
                Renderer r = body.GetComponent<Renderer>();
                if (r != null) return r.material.color;
            }
            return new Color(0.85f, 0.10f, 0.10f);
        }
        int h = Mathf.Abs(root.GetInstanceID()) % TrafficPalette.Length;
        return TrafficPalette[h];
    }

    static Material GlassMaterial()
    {
        // The old glass (alpha 0.55, _ZWrite 0) was effectively invisible:
        // the whole interior showed as if the car had no roof, and the
        // rear plate text showed THROUGH the cabin from the front.
        // This glass is darker, more opaque and reflective (metallic
        // 0.85, smoothness 1.0 so the sky/probe reflects in it) and it
        // WRITES DEPTH, so transparent things behind it (rear plate
        // text, clouds) are correctly occluded. The interior is still
        // visible through it, just behind a real canopy now.
        Material m = MeshFactory.Lit("CarGlass", new Color(0.10f, 0.14f, 0.20f, 0.80f), 0.85f, 1f);
        m.SetFloat("_Surface", 1f);          // Transparent
        m.SetFloat("_Blend", 0f);            // Alpha blend
        m.SetFloat("_SrcBlend", 1f);
        m.SetFloat("_DstBlend", 10f);
        m.SetFloat("_ZWrite", 1f);
        m.SetFloat("_AlphaClip", 0f);
        m.renderQueue = 3000;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHABLEND_ON");
        return m;
    }

    /// <summary>Interior sized to fit under the cabin roof of any style:
    /// floor pan, two seats with headrests, dashboard with a centre
    /// screen, centre console with gear knob, and a steering wheel
    /// (left-hand drive). All moulded parts are rounded boxes now —
    /// no sharp cubes inside the cabin. Seen through the lighter glass
    /// from outside and from the chase camera it reads as a real cabin,
    /// not an empty shell.</summary>
    static void BuildInterior(Transform root, BodyStyleDef style, Material trimMat)
    {
        Material seatMat = MeshFactory.Lit("Seat", new Color(0.08f, 0.08f, 0.10f), 0.1f, 0.6f);
        Material screenMat = MeshFactory.Emissive("DashScreen", new Color(0.05f, 0.12f, 0.20f), 0.7f);
        float roof = style.cabinY;
        foreach (float t in style.ctop) if (style.cabinY + t > roof) roof = style.cabinY + t;
        float floorY = 0.82f;
        float seatBaseY = floorY + 0.08f;
        // Seat backs stop 0.16 m below the roof and the headrests sit
        // 0.13 m below it — the old headrests poked 6 cm THROUGH the
        // glass roof and read as grey boxes on top of the car.
        float backH = Mathf.Max(0.22f, (roof - 0.16f) - seatBaseY);
        R("InteriorFloor", root, new Vector3(0f, floorY, -0.10f),
            new Vector3(1.30f, 0.06f, 1.90f), 0.02f, trimMat);
        foreach (float sx in new float[] { -0.38f, 0.38f })
        {
            R("SeatBase", root, new Vector3(sx, seatBaseY, -0.35f),
                new Vector3(0.50f, 0.12f, 0.50f), 0.05f, seatMat);
            GameObject back = R("SeatBack", root,
                new Vector3(sx, seatBaseY + backH / 2f, -0.62f),
                new Vector3(0.50f, backH, 0.12f), 0.05f, seatMat);
            back.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            R("Headrest", root, new Vector3(sx, roof - 0.13f, -0.68f),
                new Vector3(0.28f, 0.14f, 0.12f), 0.05f, seatMat);
        }
        R("Dashboard", root, new Vector3(0f, 0.97f, 0.62f),
            new Vector3(1.30f, 0.16f, 0.35f), 0.05f, trimMat);
        R("DashScreen", root, new Vector3(0f, 1.02f, 0.52f),
            new Vector3(0.30f, 0.12f, 0.03f), 0.01f, screenMat);
        R("Console", root, new Vector3(0f, 0.90f, 0.15f),
            new Vector3(0.25f, 0.10f, 0.70f), 0.04f, trimMat);
        // Gear knob: small lathe knob on a stalk.
        MeshFactory.PartLathe("GearKnob", root, new Vector3(0f, 1.00f, 0.10f),
            Vector3.one, new float[] { 0.02f, 0.05f, 0.05f, 0.02f },
            new float[] { 0f, 0.02f, 0.08f, 0.10f }, 10, trimMat);
        // Steering wheel: small torus, tilted like a real wheel.
        GameObject wheel = new GameObject("SteeringWheel");
        wheel.transform.SetParent(root, false);
        wheel.transform.localPosition = new Vector3(-0.38f, 0.98f, 0.45f);
        wheel.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
        MeshFilter wmf = wheel.AddComponent<MeshFilter>();
        wmf.sharedMesh = MeshFactory.BuildTorus(0.13f, 0.022f, 16, 8);
        MeshRenderer wmr = wheel.AddComponent<MeshRenderer>();
        wmr.sharedMaterial = trimMat;
        MeshFactory.Part("SteeringColumn", "Cylinder.fbx", root,
            new Vector3(-0.38f, 0.94f, 0.55f), new Vector3(0.06f, 0.22f, 0.06f), trimMat,
            new Vector3(65f, 0f, 0f));
        // Rear-view mirror on the windshield, under the roof.
        R("RearViewMirror", root, new Vector3(0f, style.cabinY + 0.08f, 0.55f),
            new Vector3(0.30f, 0.08f, 0.03f), 0.01f, trimMat);
    }

    static void RebuildWheel(Transform wheelMesh, Material trimMat, Material chromeMat,
        BodyStyleDef style, Material rimOverride)
    {
        // Remove the old flat cylinder disc; keep the transform itself —
        // CarController drives its position/rotation from the WheelCollider.
        MeshFilter mf = wheelMesh.GetComponent<MeshFilter>();
        if (mf != null) mf.sharedMesh = null;
        MeshRenderer mr = wheelMesh.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;
        wheelMesh.localScale = Vector3.one;

        Material tyreMat = MeshFactory.Lit("Tyre", new Color(0.07f, 0.07f, 0.08f), 0f, 0.25f);
        Material sidewallMat = MeshFactory.Lit("Sidewall", new Color(0.13f, 0.13f, 0.14f), 0f, 0.3f);
        // Rim finish comes from the body style: silver / black / chrome
        // (or a hero override, e.g. gold for the R34).
        Material rimMat = rimOverride != null ? rimOverride
            : style.rim == RimType.Black
            ? MeshFactory.Lit("RimBlack", new Color(0.08f, 0.08f, 0.09f), 0.9f, 0.6f)
            : style.rim == RimType.Chrome
                ? MeshFactory.Lit("RimChrome", new Color(0.9f, 0.92f, 0.95f), 1f, 1f)
                : MeshFactory.Lit("Rim", new Color(0.75f, 0.77f, 0.80f), 1f, 0.85f);

        GameObject tyre = new GameObject("Tyre");
        tyre.transform.SetParent(wheelMesh, false);
        MeshFilter tmf = tyre.AddComponent<MeshFilter>();
        // Treaded tyre (detail pass 2026-11-08): 16 tread blocks merged
        // into the torus mesh — the old smooth torus read as a black
        // donut. One mesh, one renderer per wheel.
        tmf.sharedMesh = MeshFactory.BuildTyreWithTread(0.24f, 0.11f, 22, 10, 16);
        MeshRenderer tmr = tyre.AddComponent<MeshRenderer>();
        tmr.sharedMaterial = tyreMat;
        // Sidewall ring on the outer face (lighter band, like a real
        // sidewall) — a thin torus, symmetric so spinning is invisible.
        GameObject sw = new GameObject("SidewallRing");
        sw.transform.SetParent(wheelMesh, false);
        sw.transform.localPosition = new Vector3(0f, 0.105f, 0f);
        MeshFilter swmf = sw.AddComponent<MeshFilter>();
        swmf.sharedMesh = MeshFactory.BuildTorus(0.30f, 0.014f, 22, 8);
        MeshRenderer swmr = sw.AddComponent<MeshRenderer>();
        swmr.sharedMaterial = sidewallMat;

        // Rim barrel + face disc (built-in cylinder, axle = local Y).
        MeshFactory.Part("RimBarrel", "Cylinder.fbx", wheelMesh,
            Vector3.zero, new Vector3(0.44f, 0.10f, 0.44f), rimMat);
        MeshFactory.Part("RimFace", "Cylinder.fbx", wheelMesh,
            new Vector3(0f, 0.10f, 0f), new Vector3(0.40f, 0.02f, 0.40f), rimMat);
        MeshFactory.Part("RimFaceInner", "Cylinder.fbx", wheelMesh,
            new Vector3(0f, -0.10f, 0f), new Vector3(0.40f, 0.02f, 0.40f), rimMat);
        // Rim lip: polished ring around the outer face.
        GameObject lip = new GameObject("RimLip");
        lip.transform.SetParent(wheelMesh, false);
        lip.transform.localPosition = new Vector3(0f, 0.115f, 0f);
        MeshFilter lmf = lip.AddComponent<MeshFilter>();
        lmf.sharedMesh = MeshFactory.BuildTorus(0.205f, 0.012f, 22, 8);
        MeshRenderer lmr = lip.AddComponent<MeshRenderer>();
        lmr.sharedMaterial = rimMat;
        // Brake disc behind the rim.
        MeshFactory.Part("BrakeDisc", "Cylinder.fbx", wheelMesh,
            Vector3.zero, new Vector3(0.32f, 0.05f, 0.32f), chromeMat);
        // Spokes on the outer face (count per body style: 5-10), rounded
        // and widened to 0.085 m so they read as real spokes, not wires.
        int spokes = Mathf.Max(3, style.spokes);
        for (int i = 0; i < spokes; i++)
        {
            float a = i * (360f / spokes) * Mathf.Deg2Rad;
            GameObject spoke = R("Spoke", wheelMesh,
                new Vector3(Mathf.Cos(a) * 0.11f, 0.115f, Mathf.Sin(a) * 0.11f),
                new Vector3(0.085f, 0.02f, 0.22f), 0.008f, trimMat);
            // Long axis of the spoke box is Z; rotate it to point radially.
            spoke.transform.localRotation = Quaternion.Euler(0f, 90f - i * (360f / spokes), 0f);
        }
        MeshFactory.Part("Hub", "Cylinder.fbx", wheelMesh,
            new Vector3(0f, 0.13f, 0f), new Vector3(0.12f, 0.03f, 0.12f), chromeMat);
    }

    /// <summary>Build the car from its real Kenney model. Returns false
    /// when the model is missing or malformed (caller falls back to the
    /// procedural body). The model's wheelbase is scaled to the prefab's
    /// 2.7 m (wheel holders at z = +/-1.35 m) so the wheels sit under the
    /// model's arches; the wheels are reparented under the WheelMesh
    /// transforms so CarController still drives them from the
    /// WheelColliders. Paint comes from the car's CarDef colour, applied
    /// by recolouring the atlas's paint-gradient column.</summary>
    static bool TryBuildKenneyCar(Transform root, bool isPlayer, int carIndex, Color paint)
    {
        // Procedural-only cars (CarModelMap.ProceduralOnlyCars): no
        // detailed model exists and Kenney low-poly was rejected as
        // the final look, so they use the improved procedural body
        // below instead of a Kenney stand-in. Traffic is unaffected.
        if (isPlayer && CarModelMap.IsProceduralOnly(carIndex)) return false;
        // Data-driven resolution (CarModelMap): a detailed Sketchfab
        // model at Models/Cars/<Name> wins when one has landed, else the
        // Kenney stand-in for the car's body style. Traffic uses a
        // class-based Kenney pick.
        bool detailed = false;
        GameObject prefab;
        string model;
        if (isPlayer)
        {
            prefab = CarModelMap.Resolve(carIndex, out detailed);
            model = detailed ? ("detailed:" + carIndex) : CarModelMap.KenneyStyleForCar[Mathf.Clamp(carIndex, 0, 54)];
        }
        else
        {
            model = ModelForTraffic(root, CarBodyStyles.ForClass(
                CarBodyStyles.ClassForTraffic(root.GetInstanceID())));
            prefab = KenneyModels.LoadPrefab("Models/Kenney/CarKit/" + model);
        }
        if (prefab == null) return false;
        if (detailed) return TryBuildDetailedCar(root, isPlayer, carIndex, prefab);
        GameObject inst = Object.Instantiate(prefab, root, false);
        inst.name = "KenneyCar";

        Transform wfl = KenneyModels.FindDeep(inst.transform, "wheel-front-left");
        Transform wfr = KenneyModels.FindDeep(inst.transform, "wheel-front-right");
        Transform wrl = KenneyModels.FindDeep(inst.transform, "wheel-back-left");
        Transform wrr = KenneyModels.FindDeep(inst.transform, "wheel-back-right");
        Transform body = KenneyModels.FindDeep(inst.transform, "body");
        if (wfl == null || wfr == null || wrl == null || wrr == null || body == null)
        {
            Debug.LogWarning("[CarVisuals] Kenney model " + model + " is missing body/wheel nodes");
            Object.Destroy(inst);
            return false;
        }
        // Wheelbase in model units (Kenney cars are ~2 m long in their own
        // units); scale so it matches the prefab's 2.7 m wheelbase.
        float wbModel = Mathf.Abs(wfl.localPosition.z - wrl.localPosition.z);
        if (wbModel < 0.1f)
        {
            Debug.LogWarning("[CarVisuals] Kenney model " + model + " has a degenerate wheelbase");
            Object.Destroy(inst);
            return false;
        }
        float s = 2.7f / wbModel;
        inst.transform.localScale = Vector3.one * s;
        // Align the model's wheelbase centre to the car centre and its
        // wheel-centre height to the holders' 0.35 m.
        float zc = (wfl.localPosition.z + wrl.localPosition.z) * 0.5f;
        float yc = wfl.localPosition.y;
        inst.transform.localPosition = new Vector3(0f, 0.35f - yc * s, -zc * s);

        // Paint: recolour the body's paint-gradient column (rows 8-11
        // of the atlas) in the car's roster colour — NOT the dominant
        // cell, which is glass grey. See KenneyModels.PaintColumn.
        MeshFilter bmf = body.GetComponent<MeshFilter>();
        Material mat = KenneyModels.AtlasMaterial("CarKit", 0.3f, 0.6f);
        if (bmf != null && bmf.sharedMesh != null)
        {
            int paintColumn = KenneyModels.PaintColumn(bmf.sharedMesh);
            mat = KenneyModels.PaintedAtlasMaterial("CarKit", paint, paintColumn);
        }
        KenneyModels.ApplyMaterial(inst, mat);
        if (carIndex == 38) // R34 GT-R: gold rims like the real Bayside car.
            ApplyGoldRims(inst);

        // Wheels: reparent under the WheelMesh transforms (player wheels
        // are driven by CarController from the WheelColliders; traffic
        // wheels stay static at the holder positions).
        ReparentWheel(wfl, root.Find("WheelMeshFL"), isPlayer);
        ReparentWheel(wfr, root.Find("WheelMeshFR"), isPlayer);
        ReparentWheel(wrl, root.Find("WheelMeshRL"), isPlayer);
        ReparentWheel(wrr, root.Find("WheelMeshRR"), isPlayer);

        // Hide the procedural children (kept in the hierarchy so
        // SetBodyColor / Find("Body") keep working).
        foreach (string n in new string[] { "Body", "Cabin", "Spoiler" })
        {
            Transform t = root.Find(n);
            if (t != null) t.gameObject.SetActive(false);
        }
        foreach (Transform t in root)
            if (t.name == "Headlight") t.gameObject.SetActive(false);

        // Licence plates at the model's nose and tail.
        Material plateMat = MeshFactory.Lit("Plate", new Color(0.92f, 0.92f, 0.88f), 0.2f, 0.5f);
        string plateText = isPlayer
            ? "HZ-" + carIndex.ToString("00")
            : "TK-" + (100 + Mathf.Abs(root.GetInstanceID()) % 900).ToString();
        if (bmf != null && bmf.sharedMesh != null)
        {
            Bounds lb = bmf.sharedMesh.bounds;
            float frontZ = s * (lb.center.z + lb.size.z * 0.5f - zc);
            float rearZ = s * (lb.center.z - lb.size.z * 0.5f - zc);
            float plateY = inst.transform.localPosition.y + s * (lb.center.y - lb.size.y * 0.5f) + 0.14f;
            AddPlate(root, new Vector3(0f, plateY, frontZ + 0.02f), plateText, plateMat, 180f);
            AddPlate(root, new Vector3(0f, plateY + 0.10f, rearZ - 0.02f), plateText, plateMat, 0f);
        }

        if (isPlayer) AddHeadlightBeams(root);
        Debug.Log("[CarVisuals] Kenney model " + model + " for car " + carIndex
            + " scale=" + s.ToString("0.00"));
        return true;
    }

    /// <summary>Detailed-model path (Sketchfab, via CarModelMap): the
    /// model carries its own PBR materials and wheels, so there is no
    /// atlas paint and no wheel reparenting. UNTESTED until the first
    /// detailed model lands (none on disk yet) — the Kenney path above is
    /// the one verified by the showcase build.</summary>
    static bool TryBuildDetailedCar(Transform root, bool isPlayer, int carIndex, GameObject prefab)
    {
        GameObject inst = Object.Instantiate(prefab, root, false);
        inst.name = "DetailedCar";
        Bounds b = KenneyModels.Measure(inst);
        if (b.size.z < 0.5f || b.size.y < 0.2f)
        {
            Debug.LogWarning("[CarVisuals] detailed model for car " + carIndex + " has degenerate bounds");
            Object.Destroy(inst);
            return false;
        }
        // Scale so the car is ~4.3 m long (models face +Z like the Kenney
        // cars; a -Z model is flipped by the pipeline agent's data, not here).
        float s = 4.3f / b.size.z;
        inst.transform.localScale = Vector3.one * s;
        // Centre on the car root and ground the tyres (bounds min y = 0
        // in root space, matching the WheelCollider wheel bottoms).
        Bounds b2 = KenneyModels.Measure(inst);
        Vector3 centreLocal = root.InverseTransformPoint(b2.center);
        Vector3 minLocal = root.InverseTransformPoint(new Vector3(b2.center.x, b2.min.y, b2.center.z));
        inst.transform.localPosition += new Vector3(-centreLocal.x, -minLocal.y, -centreLocal.z);
        foreach (string n in new string[] { "Body", "Cabin", "Spoiler" })
        {
            Transform t = root.Find(n);
            if (t != null) t.gameObject.SetActive(false);
        }
        foreach (Transform t in root)
            if (t.name == "Headlight") t.gameObject.SetActive(false);
        Material plateMat = MeshFactory.Lit("Plate", new Color(0.92f, 0.92f, 0.88f), 0.2f, 0.5f);
        string plateText = isPlayer ? "HZ-" + carIndex.ToString("00")
            : "TK-" + (100 + Mathf.Abs(root.GetInstanceID()) % 900).ToString();
        Bounds b3 = KenneyModels.Measure(inst);
        Vector3 frontLocal = root.InverseTransformPoint(new Vector3(b3.center.x, b3.center.y, b3.max.z));
        Vector3 rearLocal = root.InverseTransformPoint(new Vector3(b3.center.x, b3.center.y, b3.min.z));
        AddPlate(root, new Vector3(0f, 0.45f, frontLocal.z + 0.02f), plateText, plateMat, 180f);
        AddPlate(root, new Vector3(0f, 0.55f, rearLocal.z - 0.02f), plateText, plateMat, 0f);
        if (isPlayer) AddHeadlightBeams(root);
        Debug.Log("[CarVisuals] detailed model for car " + carIndex + " scale=" + s.ToString("0.00"));
        return true;
    }

    // R34 gold rims: the Kenney wheels paint their rims from one atlas
    // column (rows 4-7); a gold-filled clone of the atlas on the wheel
    // meshes gives gold rims while the tyres stay dark.
    static void ApplyGoldRims(GameObject inst)
    {
        Transform sample = KenneyModels.FindDeep(inst.transform, "wheel-front-left");
        if (sample == null) return;
        MeshFilter smf = sample.GetComponent<MeshFilter>();
        if (smf == null || smf.sharedMesh == null) return;
        int rimColumn = KenneyModels.RimColumn(smf.sharedMesh);
        if (rimColumn < 0) return;
        Material goldMat = KenneyModels.GoldAtlasMaterial("CarKit", rimColumn);
        foreach (string w in new string[] { "wheel-front-left", "wheel-front-right",
            "wheel-back-left", "wheel-back-right" })
        {
            Transform t = KenneyModels.FindDeep(inst.transform, w);
            Renderer r = t != null ? t.GetComponent<Renderer>() : null;
            if (r != null) r.sharedMaterial = goldMat;
        }
    }

    /// <summary>Move a Kenney wheel node under its WheelMesh transform,
    /// centred on the wheel's mesh centre and scaled so the wheel radius
    /// matches the 0.35 m WheelCollider. The pivot rotation aligns the
    /// wheel's axle (smallest mesh-bounds axis) with the car's X axis
    /// after the WheelMesh rotation CarController applies.</summary>
    static void ReparentWheel(Transform wheelNode, Transform wheelMesh, bool isPlayer)
    {
        if (wheelNode == null || wheelMesh == null) return;
        MeshFilter mf = wheelNode.GetComponent<MeshFilter>();
        Vector3 meshCenter = Vector3.zero;
        float radius = 0.35f;
        int axleAxis = 0; // 0 = X, 1 = Y, 2 = Z
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            meshCenter = b.center;
            float[] sz = { b.size.x, b.size.y, b.size.z };
            if (sz[1] < sz[axleAxis]) axleAxis = 1;
            if (sz[2] < sz[axleAxis]) axleAxis = 2;
            radius = Mathf.Max(sz[(axleAxis + 1) % 3], sz[(axleAxis + 2) % 3]) * 0.5f;
        }
        GameObject pivot = new GameObject("KenneyWheelPivot");
        pivot.transform.SetParent(wheelMesh, false);
        pivot.transform.localPosition = Vector3.zero;
        pivot.transform.localRotation = WheelPivotRotation(axleAxis, isPlayer);
        float ws = radius > 0.001f ? 0.35f / radius : 1f;
        pivot.transform.localScale = Vector3.one * ws;
        wheelNode.SetParent(pivot.transform, false);
        wheelNode.localPosition = -meshCenter;
        wheelNode.localRotation = Quaternion.identity;
        wheelNode.localScale = Vector3.one;
    }

    static Quaternion WheelPivotRotation(int axleAxis, bool isPlayer)
    {
        // Player: WheelMesh rotation = colliderPose * Rz(90). The pivot
        // must cancel that 90 deg AND put the axle on X.
        if (isPlayer)
        {
            switch (axleAxis)
            {
                case 0: return Quaternion.Euler(0f, 0f, -90f);  // axle X
                case 1: return Quaternion.Euler(180f, 0f, 0f);  // axle Y
                default: return Quaternion.Euler(90f, 0f, 0f);  // axle Z
            }
        }
        // Traffic: WheelMesh rotation is identity.
        switch (axleAxis)
        {
            case 0: return Quaternion.identity;              // axle X
            case 1: return Quaternion.Euler(0f, 0f, -90f);   // axle Y
            default: return Quaternion.Euler(0f, 90f, 0f);   // axle Z
        }
    }

    static void AddHeadlightBeams(Transform root)
    {
        foreach (float sx in new float[] { -0.6f, 0.6f })
        {
            GameObject lgo = new GameObject("HeadlightBeam");
            lgo.transform.SetParent(root, false);
            lgo.transform.localPosition = new Vector3(sx, 0.62f, 2.1f);
            lgo.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Light l = lgo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.96f, 0.85f);
            l.intensity = 3f;
            l.range = 32f;
            l.spotAngle = 52f;
            l.shadows = LightShadows.None;
        }
    }

    /// <summary>Beams from the procedural headlights: same spots, but
    /// starting at the repositioned housings on the nose.</summary>
    static void AddHeadlightBeams(Transform root, BodyStyleDef style)
    {
        float noseZ = SurfaceZAt(style, 0.6f, 0.60f);
        foreach (float sx in new float[] { -0.6f, 0.6f })
        {
            GameObject lgo = new GameObject("HeadlightBeam");
            lgo.transform.SetParent(root, false);
            lgo.transform.localPosition = new Vector3(sx, 0.62f, noseZ + 0.05f);
            lgo.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Light l = lgo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.96f, 0.85f);
            l.intensity = 3f;
            l.range = 32f;
            l.spotAngle = 52f;
            l.shadows = LightShadows.None;
        }
    }
}

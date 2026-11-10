using System;
using UnityEngine;

/// <summary>PHOTO MODE — freeze the game and photograph your car
/// against Tokyo neon, sakura or sunset. Public API:
///   EnterPhotoMode() / ExitPhotoMode() / TogglePhotoMode()
///   CapturePhoto() — saves a PNG to Application.persistentDataPath/Photos
///   IsActive, OrbitYaw / OrbitPitch / OrbitDistance (read/write)
/// Controls (keyboard + mouse, touch on mobile):
///   drag = orbit around the car, wheel / pinch = zoom,
///   WASD / arrows = pan, Q/E = up/down, P or Esc = exit, C = capture.
/// The game is frozen with Time.timeScale = 0 while the camera moves
/// on unscaled time, so the shot is perfectly still. HUD hiding goes
/// through the HUD component's own GameObject (the whole HUD canvas)
/// — HUD.cs itself is never edited. CameraFollow is disabled while
/// photo mode owns the camera and restored on exit. Fired when a
/// photo is saved: PhotoCaptured(path) — ViewpointManager listens
/// for viewpoint shots.</summary>
public class PhotoModeController : MonoBehaviour
{
    public static PhotoModeController Instance { get; private set; }

    /// <summary>Fired with the saved PNG path after CapturePhoto.</summary>
    public event Action<string> PhotoCaptured;

    public bool IsActive { get; private set; }
    public float OrbitYaw { get; set; } = 35f;
    public float OrbitPitch { get; set; } = 12f;
    public float OrbitDistance { get; set; } = 9f;

    public float minDistance = 3f;
    public float maxDistance = 40f;
    public float orbitSpeed = 120f;
    public float panSpeed = 12f;
    public float zoomSpeed = 8f;

    Transform target;
    Vector3 panOffset;
    Vector3 savedCamPos;
    Quaternion savedCamRot;
    CameraFollow followCam;
    GameObject hudRoot;
    GameObject touchControlsRoot;
    bool savedFollowEnabled;
    float lastPinchDist;
    bool pinchActive;
    HUD hud;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        // P opens photo mode anywhere in the Main scene (not in menus).
        if (!IsActive)
        {
            if (Input.GetKeyDown(KeyCode.P) && GameManager.Instance != null
                && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Main")
                EnterPhotoMode();
            return;
        }

        float dt = Time.unscaledDeltaTime;
        if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape))
        {
            ExitPhotoMode();
            return;
        }
        if (Input.GetKeyDown(KeyCode.C)) CapturePhoto();

        // Mouse orbit + wheel zoom.
        if (Input.GetMouseButton(0))
        {
            OrbitYaw += Input.GetAxis("Mouse X") * orbitSpeed * dt;
            OrbitPitch = Mathf.Clamp(OrbitPitch - Input.GetAxis("Mouse Y") * orbitSpeed * dt, -10f, 80f);
        }
        OrbitDistance = Mathf.Clamp(OrbitDistance - Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, minDistance, maxDistance);

        // Touch: one finger orbits, two fingers pinch-zoom.
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved)
        {
            var t = Input.GetTouch(0);
            OrbitYaw += t.deltaPosition.x * 0.25f;
            OrbitPitch = Mathf.Clamp(OrbitPitch - t.deltaPosition.y * 0.25f, -10f, 80f);
        }
        else if (Input.touchCount == 2)
        {
            var t0 = Input.GetTouch(0);
            var t1 = Input.GetTouch(1);
            float d = Vector2.Distance(t0.position, t1.position);
            if (pinchActive && lastPinchDist > 0f)
                OrbitDistance = Mathf.Clamp(OrbitDistance - (d - lastPinchDist) * 0.02f, minDistance, maxDistance);
            lastPinchDist = d;
            pinchActive = true;
        }
        else { pinchActive = false; lastPinchDist = 0f; }

        // Pan (WASD / arrows) + height (Q/E), relative to camera yaw.
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move += Vector3.forward;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move -= Vector3.forward;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move -= Vector3.right;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move += Vector3.right;
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;
        if (move != Vector3.zero)
        {
            Quaternion yawRot = Quaternion.Euler(0f, OrbitYaw, 0f);
            panOffset += yawRot * move.normalized * panSpeed * dt;
            panOffset.y = Mathf.Clamp(panOffset.y, -2f, 30f);
        }

        ApplyCamera();
    }

    /// <summary>Freeze the game, hide the HUD, hand the camera to
    /// the player. Safe to call twice (second call is a no-op).</summary>
    public void EnterPhotoMode()
    {
        if (IsActive) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogWarning("[PhotoMode] no player car to photograph"); return; }
        target = player.transform;

        Camera cam = Camera.main;
        if (cam != null) { savedCamPos = cam.transform.position; savedCamRot = cam.transform.rotation; }

        followCam = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (followCam != null) { savedFollowEnabled = followCam.enabled; followCam.enabled = false; }

        // Hide the whole HUD canvas + touch controls through their own
        // GameObjects (HUD.cs is owned by another agent — never edited).
        // The hint is shown first, while the canvas is still visible.
        if (hud == null) hud = FindFirstObjectByType<HUD>();
        if (hud != null)
        {
            hud.ShowMessage("Photo Mode - drag to orbit, wheel to zoom, C to capture, P to exit", 5f);
            hudRoot = hud.gameObject;
            hudRoot.SetActive(false);
        }
        var mobile = FindFirstObjectByType<MobileControls>();
        if (mobile != null && mobile.gameObject != hudRoot)
        {
            touchControlsRoot = mobile.gameObject;
            touchControlsRoot.SetActive(false);
        }

        // Start from the current chase-camera framing.
        if (cam != null)
        {
            Vector3 toCam = cam.transform.position - target.position;
            OrbitDistance = Mathf.Clamp(toCam.magnitude, minDistance, maxDistance);
            OrbitYaw = cam.transform.eulerAngles.y;
            OrbitPitch = 12f;
        }
        panOffset = Vector3.zero;
        IsActive = true;
        Time.timeScale = 0f;
        ApplyCamera();
        Debug.Log("[PhotoMode] entered (game frozen, HUD hidden)");
    }

    /// <summary>Unfreeze, restore the camera and the HUD.</summary>
    public void ExitPhotoMode()
    {
        if (!IsActive) return;
        IsActive = false;
        Time.timeScale = 1f;
        Camera cam = Camera.main;
        if (cam != null) { cam.transform.position = savedCamPos; cam.transform.rotation = savedCamRot; }
        if (followCam != null) followCam.enabled = savedFollowEnabled;
        if (hudRoot != null) hudRoot.SetActive(true);
        if (touchControlsRoot != null) touchControlsRoot.SetActive(true);
        hudRoot = null;
        touchControlsRoot = null;
        Debug.Log("[PhotoMode] exited");
    }

    public void TogglePhotoMode()
    {
        if (IsActive) ExitPhotoMode();
        else EnterPhotoMode();
    }

    /// <summary>Save the current frame as a PNG. Works while frozen
    /// (CaptureScreenshot runs at end of frame). Returns the path,
    /// or "" when photo mode is not active.</summary>
    public string CapturePhoto()
    {
        if (!IsActive) return "";
        string dir = System.IO.Path.Combine(Application.persistentDataPath, "Photos");
        System.IO.Directory.CreateDirectory(dir);
        string file = "photo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        string path = System.IO.Path.Combine(dir, file);
        // FIX (2026-11-07, model-integration agent): ScreenCapture and
        // Texture2D.EncodeToPNG do NOT compile in this project — Unity
        // 6000.3's Assembly-CSharp references do not include
        // UnityEngine.ScreenCaptureModule or ImageConversionModule
        // (55 of 81 engine modules are referenced), so the original
        // ScreenCapture.CaptureScreenshot call was error CS0103 and
        // blocked ALL script compilation. This captures via ReadPixels
        // (CoreModule) and writes the PNG with a self-contained encoder
        // (stored DEFLATE blocks, no compression) below. Same API and
        // same output file; replace with ScreenCapture if the modules
        // are ever referenced.
        SaveScreenshotPng(path);
        Debug.Log("[PhotoMode] captured " + path);
        if (PhotoCaptured != null) PhotoCaptured.Invoke(path);
        return path;
    }

    // Capture the current (frozen) frame and save it as a PNG without
    // ScreenCapture / ImageConversion (see the note in CapturePhoto).
    // ReadPixels reads the last presented frame, which is the frozen
    // photo-mode frame; rows are flipped to PNG top-down order.
    void SaveScreenshotPng(string path)
    {
        Texture2D tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        tex.Apply();
        WritePng(tex, path);
        UnityEngine.Object.Destroy(tex);
    }

    static void WritePng(Texture2D tex, string path)
    {
        int w = tex.width, h = tex.height;
        Color32[] px = tex.GetPixels32();
        int stride = w * 4 + 1;
        byte[] raw = new byte[stride * h];
        for (int y = 0; y < h; y++)
        {
            int srcRow = h - 1 - y;
            raw[y * stride] = 0; // filter: none
            for (int x = 0; x < w; x++)
            {
                Color32 c = px[srcRow * w + x];
                int o = y * stride + 1 + x * 4;
                raw[o] = c.r; raw[o + 1] = c.g; raw[o + 2] = c.b; raw[o + 3] = c.a;
            }
        }
        // zlib stream: header 0x78 0x01, stored blocks, Adler-32.
        byte[] idat;
        using (var ms = new System.IO.MemoryStream())
        {
            ms.WriteByte(0x78); ms.WriteByte(0x01);
            int pos = 0;
            while (pos < raw.Length)
            {
                int len = System.Math.Min(65535, raw.Length - pos);
                bool last = pos + len >= raw.Length;
                ms.WriteByte((byte)(last ? 1 : 0));
                ms.WriteByte((byte)(len & 0xff)); ms.WriteByte((byte)((len >> 8) & 0xff));
                int nlen = ~len & 0xffff;
                ms.WriteByte((byte)(nlen & 0xff)); ms.WriteByte((byte)((nlen >> 8) & 0xff));
                ms.Write(raw, pos, len);
                pos += len;
            }
            uint adler = Adler32(raw);
            ms.WriteByte((byte)((adler >> 24) & 0xff)); ms.WriteByte((byte)((adler >> 16) & 0xff));
            ms.WriteByte((byte)((adler >> 8) & 0xff)); ms.WriteByte((byte)(adler & 0xff));
            idat = ms.ToArray();
        }
        using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Create))
        {
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            WriteChunk(fs, "IHDR", IhdrBytes(w, h));
            WriteChunk(fs, "IDAT", idat);
            WriteChunk(fs, "IEND", new byte[0]);
        }
    }

    static byte[] IhdrBytes(int w, int h)
    {
        return new byte[]
        {
            (byte)((w >> 24) & 0xff), (byte)((w >> 16) & 0xff), (byte)((w >> 8) & 0xff), (byte)(w & 0xff),
            (byte)((h >> 24) & 0xff), (byte)((h >> 16) & 0xff), (byte)((h >> 8) & 0xff), (byte)(h & 0xff),
            8, 6, 0, 0, 0 // bit depth 8, colour type 6 (RGBA), no compression/filter/interlace
        };
    }

    static void WriteChunk(System.IO.Stream s, string type, byte[] data)
    {
        byte[] tb = System.Text.Encoding.ASCII.GetBytes(type);
        s.WriteByte((byte)((data.Length >> 24) & 0xff)); s.WriteByte((byte)((data.Length >> 16) & 0xff));
        s.WriteByte((byte)((data.Length >> 8) & 0xff)); s.WriteByte((byte)(data.Length & 0xff));
        s.Write(tb, 0, 4);
        s.Write(data, 0, data.Length);
        uint crc = Crc32(tb, data);
        s.WriteByte((byte)((crc >> 24) & 0xff)); s.WriteByte((byte)((crc >> 16) & 0xff));
        s.WriteByte((byte)((crc >> 8) & 0xff)); s.WriteByte((byte)(crc & 0xff));
    }

    static uint Crc32(byte[] type, byte[] data)
    {
        uint[] table = CrcTable();
        uint c = 0xffffffff;
        foreach (byte b in type) c = table[(c ^ b) & 0xff] ^ (c >> 8);
        foreach (byte b in data) c = table[(c ^ b) & 0xff] ^ (c >> 8);
        return c ^ 0xffffffff;
    }

    static uint[] crcTable;
    static uint[] CrcTable()
    {
        if (crcTable != null) return crcTable;
        crcTable = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
            crcTable[n] = c;
        }
        return crcTable;
    }

    static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (byte x in data)
        {
            a = (a + x) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    void ApplyCamera()
    {
        if (target == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Quaternion rot = Quaternion.Euler(OrbitPitch, OrbitYaw, 0f);
        Vector3 centre = target.position + panOffset + Vector3.up * 1.2f;
        cam.transform.position = centre + rot * new Vector3(0f, 0f, -OrbitDistance);
        cam.transform.LookAt(centre);
    }

    void OnDisable()
    {
        // Never leave the game frozen if this object is destroyed.
        if (IsActive) { IsActive = false; Time.timeScale = 1f; }
    }
}

﻿using UnityEngine;
using UnityEngine.Rendering;
using Plugin = Klak.Syphon.Interop.PluginServer;

namespace Klak.Syphon {

public enum CaptureMethod { GameView, Camera, Texture }

[ExecuteInEditMode]
public sealed class SyphonServer : MonoBehaviour
{
    #region Public properties

    public string ServerName
      { get => _serverName;
        set { Teardown(); _serverName = value; } }

    public CaptureMethod CaptureMethod
      { get => _captureMethod;
        set { Teardown(); _captureMethod = value; } }

    public Camera SourceCamera
      { get => _sourceCamera;
        set { Teardown(); _sourceCamera = value; } }

    public Texture SourceTexture
      { get => _sourceTexture;
        set { Teardown(); _sourceTexture = value; } }

    public bool KeepAlpha
      { get => _keepAlpha;
        set => _keepAlpha = value; }

    #endregion

    #region Backing fields

    [SerializeField] string _serverName = "Syphon Server";
    [SerializeField] CaptureMethod _captureMethod;
    [SerializeField] Camera _sourceCamera;
    [SerializeField] Texture _sourceTexture;
    [SerializeField] bool _keepAlpha;

    #endregion

    #region Private members

    (Plugin instance, Texture2D texture) _plugin;
    Material _blitMaterial;

    #if KLAK_SYPHON_HAS_SRP
    Camera _attachedCamera;
    #endif

    void Setup()
    {
        if (_plugin.instance != null) return;

        // Server name validity check
        if (string.IsNullOrEmpty(_serverName)) return;

        // Texture capture mode
        if (_captureMethod == CaptureMethod.Texture)
        {
            if (_sourceTexture == null) return;
            _plugin = Plugin.CreateWithBackedTexture
              (_serverName, _sourceTexture.width, _sourceTexture.height);
        }

        // Camera capture mode
        if (_captureMethod == CaptureMethod.Camera)
        {
            if (_sourceCamera == null) return;
            _plugin = Plugin.CreateWithBackedTexture
              (_serverName, _sourceCamera.pixelWidth, _sourceCamera.pixelHeight);

            #if KLAK_SYPHON_HAS_SRP
            // Callback registration
            CameraCaptureBridge.AddCaptureAction(_sourceCamera, OnCameraCapture);
            _attachedCamera = _sourceCamera;
            #endif
        }

        // Game View capture mode
        if (_captureMethod == CaptureMethod.GameView)
            _plugin = Plugin.CreateWithBackedTexture
              (_serverName, Screen.width, Screen.height);
    }

    void Teardown()
    {
        // Plugin instance/texture disposal
        _plugin.instance?.Dispose();
        Utility.Destroy(_plugin.texture);
        _plugin = (null, null);

        #if KLAK_SYPHON_HAS_SRP
        // Camera capture callback reset
        if (_attachedCamera != null)
        {
            CameraCaptureBridge.RemoveCaptureAction(_attachedCamera, OnCameraCapture);
            _attachedCamera = null;
        }
        #endif
    }

    #endregion

    #region MonoBehaviour implementation

    void Start()
      => InternalCommon.ApplyCurrentColorSpace();

    void OnValidate()
      => Teardown();

    void OnDisable()
      => Teardown();

    void OnDestroy()
    {
        Utility.Destroy(_blitMaterial);
        _blitMaterial = null;
    }

    void Update()
    {
        Setup();

        // Blitter lazy initialization
        if (_blitMaterial == null)
        {
            _blitMaterial = new Material(Shader.Find("Hidden/Klak/Syphon/Blit"));
            _blitMaterial.hideFlags = HideFlags.DontSave;
        }

        // Texture capture mode update
        if (_captureMethod == CaptureMethod.Texture)
            Graphics.CopyTexture(_sourceTexture, _plugin.texture);

        // Game View capture mode update
        if (_captureMethod == CaptureMethod.GameView)
        {
            var rt1 = RenderTexture.GetTemporary(Screen.width, Screen.height, 0);
            var rt2 = RenderTexture.GetTemporary(Screen.width, Screen.height, 0);
            ScreenCapture.CaptureScreenshotIntoRenderTexture(rt1);
            Graphics.Blit(rt1, rt2, _blitMaterial, _keepAlpha ? 1 : 0);
            Graphics.CopyTexture(rt2, _plugin.texture);
            RenderTexture.ReleaseTemporary(rt1);
            RenderTexture.ReleaseTemporary(rt2);
        }

        // Frame update notification
        _plugin.instance.PublishTexture();
    }

    #if KLAK_SYPHON_HAS_SRP
    void OnCameraCapture(RenderTargetIdentifier source, CommandBuffer cb)
    {
        if (_attachedCamera == null) return;
        // TO BE IMPLEMENTED
    }
    #endif

    #endregion
}

} // namespace Klak.Syphon

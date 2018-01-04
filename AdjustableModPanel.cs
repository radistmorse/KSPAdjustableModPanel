﻿/*
Copyright (c) 2017, radistmorse
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using UnityEngine;
using KSP.UI.Screens;
using System.Collections.Generic;

namespace AdjustableModPanel {
  [KSPAddon (KSPAddon.Startup.MainMenu, true)]
  public class AdjustableModPanel : MonoBehaviour {
    private class ModDescriptor {
      public string module;
      public string method;
      public uint textureHash;
      public bool textureHashNeeded = false;
      public bool unmaintainable = false;

      public ApplicationLauncher.AppScenes defaultScenes;
      public ApplicationLauncher.AppScenes requiredScenes;
      public Texture2D modIcon;
    }

    private List<ModDescriptor> descriptors = new List<ModDescriptor> ();
    private List<ModDescriptor> tempDescriptors;
    private Dictionary <ModDescriptor, ApplicationLauncher.AppScenes> currentScenes = new Dictionary<ModDescriptor, ApplicationLauncher.AppScenes> ();
    private Dictionary <ModDescriptor, ApplicationLauncher.AppScenes> pendingChanges = new Dictionary<ModDescriptor, ApplicationLauncher.AppScenes> ();

    private ApplicationLauncherButton appButton = null;
    private Texture2D appButtonTexture;
    private EventData<GameScenes>.OnEvent updateCallback1 = null;
    private Callback updateCallback2 = null;

    private GameObject mainWindow = null;

    private List< KeyValuePair <ApplicationLauncher.AppScenes, string> > appScenesNames = new List< KeyValuePair <ApplicationLauncher.AppScenes, string> >  () {
      new KeyValuePair<ApplicationLauncher.AppScenes, string> (ApplicationLauncher.AppScenes.SPACECENTER, "KSC" ),
      new KeyValuePair<ApplicationLauncher.AppScenes, string> ((ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB), "SPH/VAB" ),
      new KeyValuePair<ApplicationLauncher.AppScenes, string> (ApplicationLauncher.AppScenes.FLIGHT,  "FLT" ),
      new KeyValuePair<ApplicationLauncher.AppScenes, string> (ApplicationLauncher.AppScenes.MAPVIEW, "MAP" ),
      new KeyValuePair<ApplicationLauncher.AppScenes, string> (ApplicationLauncher.AppScenes.TRACKSTATION, "TRS" ),
      new KeyValuePair<ApplicationLauncher.AppScenes, string> ( (ApplicationLauncher.AppScenes)63, "ALL" )
    };

    static internal AdjustableModPanel Instance { get; private set; } = null;

    #region Main window

    private class ToggleParameters : MonoBehaviour {
      public ModDescriptor currentModKey;
      public ApplicationLauncher.AppScenes toggleModScenes;
    }

    private void OpenMainWindow () {
      if (mainWindow != null) {
        Destroy (mainWindow);
        mainWindow = null;
      }
      if (appButton?.toggleButton?.CurrentState == KSP.UI.UIRadioButton.State.False)
        return;
      UISkinDef skin = UISkinManager.defaultSkin;

      // main window
      mainWindow = new GameObject ("AdjustableModPanelWindow");
      mainWindow.AddComponent<CanvasRenderer> ();
      var transform = mainWindow.AddComponent<RectTransform> ();
      var image = mainWindow.AddComponent<UnityEngine.UI.Image> ();
      image.sprite = skin.window.normal.background;
      image.type = UnityEngine.UI.Image.Type.Sliced;
      // put in the center of the screen
      transform.anchorMin = new Vector2 (0.5f, 0.5f);
      transform.anchorMax = new Vector2 (0.5f, 0.5f);
      transform.pivot = new Vector2 (0.5f, 0.5f);
      var vert = mainWindow.AddComponent<UnityEngine.UI.VerticalLayoutGroup> ();
      vert.spacing = 3;
      vert.padding = new RectOffset (5, 5, 5, 5);
      var horzfitter = mainWindow.AddComponent<UnityEngine.UI.ContentSizeFitter> ();
      horzfitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
      horzfitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

      // header
      var line = new GameObject();
      line.AddComponent<RectTransform> ();
      var horlayout = line.AddComponent<UnityEngine.UI.HorizontalLayoutGroup> ();
      horlayout.spacing = 5;
      // header spacing
      var elem = new GameObject();
      elem.AddComponent<RectTransform> ();
      var layout = elem.AddComponent<UnityEngine.UI.LayoutElement> ();
      layout.preferredHeight = 30;
      layout.preferredWidth = 255;
      elem.transform.SetParent (line.transform);
      // header elements
      foreach (var scene in appScenesNames) {
        elem = new GameObject ();
        elem.AddComponent<CanvasRenderer> ();
        elem.AddComponent<RectTransform> ();
        layout = elem.AddComponent<UnityEngine.UI.LayoutElement> ();
        var text = elem.AddComponent<TMPro.TextMeshProUGUI> ();
        text.text = scene.Value;
        text.font = UISkinManager.TMPFont;
        text.fontSize = 14;
        text.color = Color.white;
        text.fontStyle = TMPro.FontStyles.Bold;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        layout.preferredHeight = 30;
        layout.preferredWidth = 70;
        elem.transform.SetParent (line.transform);
      }
      line.transform.SetParent (mainWindow.transform);

      int num = 0;
      foreach (var mod in descriptors) {
        if (!mod.unmaintainable && mod.modIcon != null)
          num++;
      }

      Transform contentTransform = null;
      if (num < 11) {
        contentTransform = mainWindow.transform;
      } else {
        // scroller
        var scrollobj = new GameObject ();
        transform = scrollobj.AddComponent<RectTransform> ();
        layout = scrollobj.AddComponent<UnityEngine.UI.LayoutElement> ();
        layout.preferredHeight = 500;
        layout.preferredWidth = 630;
        scrollobj.transform.SetParent (mainWindow.transform);
        var scroll = scrollobj.AddComponent<UnityEngine.UI.ScrollRect> ();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 10f;
        var viewport = new GameObject ();
        viewport.AddComponent<CanvasRenderer> ();
        transform = viewport.AddComponent<RectTransform> ();
        viewport.AddComponent<UnityEngine.UI.Image> ();
        viewport.AddComponent<UnityEngine.UI.Mask> ().showMaskGraphic = false;
        viewport.transform.SetParent (scrollobj.transform);
        transform.anchorMin = Vector2.zero;
        transform.anchorMax = Vector2.one;
        transform.pivot = new Vector2 (0.5f, 0.5f);
        transform.sizeDelta = Vector2.zero;
        scroll.viewport = transform;
        var content = new GameObject();
        transform = content.AddComponent<RectTransform> ();
        content.AddComponent<UnityEngine.UI.VerticalLayoutGroup> ();
        var fitter = content.AddComponent<UnityEngine.UI.ContentSizeFitter> ();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        content.transform.SetParent (viewport.transform);
        scroll.content = transform;
        transform.anchorMin = new Vector2 (0f, 1f);
        transform.anchorMax = new Vector2 (0f, 1f);
        transform.pivot = new Vector2 (0f, 1f);
        transform.sizeDelta = Vector2.zero;
        // scrollbar
        var scrollbar = new GameObject();
        scrollbar.AddComponent<CanvasRenderer> ();
        transform = scrollbar.AddComponent<RectTransform> ();
        image = scrollbar.AddComponent<UnityEngine.UI.Image> ();
        image.sprite = skin.verticalScrollbar.normal.background;
        image.type = UnityEngine.UI.Image.Type.Sliced;
        var scrollbarcomp = scrollbar.AddComponent<UnityEngine.UI.Scrollbar> ();
        scrollbarcomp.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        scrollbar.transform.SetParent (scrollobj.transform);
        transform.anchorMin = new Vector2 (1f, 0f);
        transform.anchorMax = Vector2.one;
        transform.pivot = Vector2.one;
        transform.sizeDelta = new Vector2 (20, 0);
        // scrollbar handle
        var handle = new GameObject();
        handle.AddComponent<CanvasRenderer> ();
        transform = handle.AddComponent<RectTransform> ();
        image = handle.AddComponent<UnityEngine.UI.Image> ();
        image.sprite = skin.verticalScrollbarThumb.normal.background;
        image.type = UnityEngine.UI.Image.Type.Sliced;
        scrollbarcomp.targetGraphic = image;
        scrollbarcomp.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
        var spritestate = scrollbarcomp.spriteState;
        spritestate.highlightedSprite = skin.verticalScrollbarThumb.highlight.background;
        spritestate.pressedSprite = skin.verticalScrollbarThumb.active.background;
        spritestate.disabledSprite = skin.verticalScrollbarThumb.disabled.background;
        scrollbarcomp.spriteState = spritestate;
        handle.transform.SetParent (scrollbar.transform);
        scrollbarcomp.handleRect = transform;
        scroll.verticalScrollbar = scrollbarcomp;
        contentTransform = content.transform;
        // reset config to fix some weirdness
        scrollbar.SetActive (false);
        scrollbar.SetActive (true);
        scrollobj.SetActive (false);
        scrollobj.SetActive (true);
        transform.offsetMin = Vector2.zero;
        transform.offsetMax = Vector2.zero;
      }

      foreach (var mod in descriptors) {
        if (mod.unmaintainable || mod.modIcon == null)
          continue;
        // mod lines
        line = new GameObject ();
        line.AddComponent<RectTransform> ();
        horlayout = line.AddComponent<UnityEngine.UI.HorizontalLayoutGroup> ();
        horlayout.spacing = 5;
        // mod icon
        elem = new GameObject ();
        elem.AddComponent<CanvasRenderer> ();
        elem.AddComponent<RectTransform> ();
        var rimage = elem.AddComponent<UnityEngine.UI.RawImage> ();
        rimage.texture = mod.modIcon;
        layout = elem.AddComponent<UnityEngine.UI.LayoutElement> ();
        layout.preferredHeight = 50;
        layout.preferredWidth = 50;
        elem.transform.SetParent (line.transform);
        // mod name
        elem = new GameObject ();
        elem.AddComponent<CanvasRenderer> ();
        elem.AddComponent<RectTransform> ();
        layout = elem.AddComponent<UnityEngine.UI.LayoutElement> ();
        var text = elem.AddComponent<TMPro.TextMeshProUGUI> ();
        text.text = mod.module;
        text.font = UISkinManager.TMPFont;
        text.fontSize = 14;
        text.color = Color.white;
        text.fontStyle = TMPro.FontStyles.Bold;
        text.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        layout.preferredHeight = 50;
        layout.preferredWidth = 200;
        elem.transform.SetParent (line.transform);
        foreach (var scene in appScenesNames) {
          // mod line elements
          elem = new GameObject ();
          elem.AddComponent<RectTransform> ();
          layout = elem.AddComponent<UnityEngine.UI.LayoutElement> ();
          layout.preferredHeight = 50;
          layout.preferredWidth = 70;
          // toggle
          var toggle = new GameObject ();
          toggle.AddComponent<CanvasRenderer> ();
          transform = toggle.AddComponent<RectTransform> ();
          transform.anchorMin = new Vector2 (0.5f, 0.5f);
          transform.anchorMax = new Vector2 (0.5f, 0.5f);
          transform.pivot = new Vector2 (0.5f, 0.5f);
          transform.sizeDelta = new Vector2 (50, 50);
          image = toggle.AddComponent<UnityEngine.UI.Image> ();
          image.sprite = skin.toggle.normal.background;
          image.type = UnityEngine.UI.Image.Type.Sliced;
          var togglecomp = toggle.AddComponent<UnityEngine.UI.Toggle> ();
          togglecomp.targetGraphic = image;
          togglecomp.image.sprite = skin.toggle.normal.background;
          togglecomp.image.type = UnityEngine.UI.Image.Type.Sliced;
          togglecomp.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
          var spritestate = togglecomp.spriteState;
          spritestate.highlightedSprite = skin.toggle.highlight.background;
          spritestate.pressedSprite = skin.toggle.active.background;
          spritestate.disabledSprite = skin.toggle.disabled.background;
          togglecomp.spriteState = spritestate;
          // checkbox for toggle
          var check = new GameObject ();
          check.AddComponent<CanvasRenderer> ();
          transform = check.AddComponent<RectTransform> ();
          check.transform.SetParent (toggle.transform);
          transform.anchorMin = new Vector2 (0f, 0f);
          transform.anchorMax = new Vector2 (1f, 1f);
          transform.pivot = new Vector2 (0.5f, 0.5f);
          transform.sizeDelta = Vector2.zero;
          image = check.AddComponent<UnityEngine.UI.Image> ();
          togglecomp.graphic = image;
          image.sprite = skin.toggle.active.background;
          image.type = UnityEngine.UI.Image.Type.Sliced;
          // parameters
          var parameters = toggle.AddComponent<ToggleParameters> ();
          parameters.currentModKey = mod;
          parameters.toggleModScenes = scene.Key;
          // checkbox state
          bool allowed = (mod.defaultScenes & scene.Key) != ApplicationLauncher.AppScenes.NEVER;
          ApplicationLauncher.AppScenes curvalue = currentScenes[mod];
          if (pendingChanges.ContainsKey(mod)) {
            curvalue = pendingChanges[mod];
          }
          bool enabled = (curvalue & (scene.Key & (~mod.requiredScenes))) != ApplicationLauncher.AppScenes.NEVER;
          togglecomp.interactable = allowed;
          togglecomp.isOn = allowed & enabled;
          if (!allowed) {
            togglecomp.GetComponent<UnityEngine.UI.Image> ().color = new Color (0f, 0f, 0f, 0.25f);
          }
          // special case for "always on"
          if ((parameters.toggleModScenes & (~mod.requiredScenes)) == ApplicationLauncher.AppScenes.NEVER) {
            image.color = Color.gray;
            togglecomp.isOn = true;
            // just keep it always on
            togglecomp.onValueChanged.AddListener ((value) => {
              if (!value) {
                togglecomp.isOn = true;
              }
            });
          } else {
            // checkbox callback
            togglecomp.onValueChanged.AddListener ((value) => {
              ApplicationLauncher.AppScenes currentvalue = currentScenes[mod];
              if (pendingChanges.ContainsKey (mod)) {
                currentvalue = pendingChanges[mod];
              }
              if (value != ((currentvalue & (parameters.toggleModScenes & (~mod.requiredScenes))) != ApplicationLauncher.AppScenes.NEVER)) {
                if (value) {
                  pendingChanges[mod] = currentvalue | (parameters.toggleModScenes & mod.defaultScenes);
                } else {
                  pendingChanges[mod] = (currentvalue & (~parameters.toggleModScenes)) | mod.requiredScenes;
                }
                Debug.Log ("[Adjustable Mod Panel] New value for " + parameters.currentModKey.module + ": " + pendingChanges[mod]);
                forceUpdateCount += 1;
                UpdateToggles ();
              }
            });
          }
          var navigation = togglecomp.navigation;
          navigation.mode = UnityEngine.UI.Navigation.Mode.None;
          togglecomp.navigation = navigation;
          toggle.transform.SetParent (elem.transform);
          elem.transform.SetParent (line.transform);
        }
        line.transform.SetParent (contentTransform);
      }
      // input locks
      mainWindow.transform.SetParent (MainCanvasUtil.MainCanvas.transform);
      var trigger = mainWindow.AddComponent<UnityEngine.EventSystems.EventTrigger> ();
      var onMouseEnter = new UnityEngine.EventSystems.EventTrigger.Entry() {
        eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
      };
      onMouseEnter.callback.AddListener ((data) => InputLockManager.SetControlLock (ControlTypes.ALLBUTCAMERAS, "adjustablemodpanellock"));
      trigger.triggers.Add (onMouseEnter);
      var onMouseExit = new UnityEngine.EventSystems.EventTrigger.Entry () {
        eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
      };
      onMouseExit.callback.AddListener ((data) => InputLockManager.RemoveControlLock ("adjustablemodpanellock"));
      trigger.triggers.Add (onMouseExit);
      mainWindow.SetActive (true);
    }

    private void CloseMainWindow () {
      if (mainWindow != null) {
        Destroy (mainWindow);
        mainWindow = null;
      }
      InputLockManager.RemoveControlLock ("adjustablemodpanellock");
    }

    private void UpdateToggles () {
      foreach (var toggle in mainWindow.GetComponentsInChildren<UnityEngine.UI.Toggle> (true)) {
        var pars = toggle.GetComponent<ToggleParameters> ();
        if ((pars.toggleModScenes & (~pars.currentModKey.requiredScenes)) == ApplicationLauncher.AppScenes.NEVER) {
          toggle.isOn = true;
          continue;
        }
        ApplicationLauncher.AppScenes currentvalue = currentScenes[pars.currentModKey];
        if (pendingChanges.ContainsKey (pars.currentModKey)) {
          currentvalue = pendingChanges[pars.currentModKey];
        }
        if (toggle.isOn != ((currentvalue & (pars.toggleModScenes & (~pars.currentModKey.requiredScenes))) != ApplicationLauncher.AppScenes.NEVER)) {
          toggle.isOn = ((currentvalue & (pars.toggleModScenes & (~pars.currentModKey.requiredScenes))) != ApplicationLauncher.AppScenes.NEVER);
        }
      }
    }

    #endregion

    public void Awake () {
      DontDestroyOnLoad (this);
      updateCallback1 = (scene) => this.forceUpdateCount += 2;
      updateCallback2 = () => this.forceUpdateCount += 3;

      GameEvents.onGUIApplicationLauncherReady.Add (OnGUIApplicationLauncherReady);
      GameEvents.onGUIApplicationLauncherUnreadifying.Add (OnGUIApplicationLauncherUnreadifying);
      GameEvents.onLevelWasLoadedGUIReady.Add (updateCallback1);
      GameEvents.onHideUI.Add (CloseMainWindow);
      GameEvents.onShowUI.Add (OpenMainWindow);

      MapView.OnEnterMapView = (Callback)Delegate.Combine (MapView.OnEnterMapView, updateCallback2);
      MapView.OnExitMapView = (Callback)Delegate.Combine (MapView.OnExitMapView, updateCallback2);

      LoadConfig ();
      Instance = this;

      appButtonTexture = new Texture2D (64, 64);
      appButtonTexture.LoadImage (new byte[] {
        0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0x00, 0x00, 0x0d, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0x40,
        0x08, 0x04, 0x00, 0x00, 0x00, 0x00, 0x60, 0xb9, 0x55, 0x00, 0x00, 0x06, 0xfc, 0x49, 0x44, 0x41, 0x54, 0x68, 0xde, 0xed, 0x99, 0x5b, 0x4c, 0x1c,
        0xd7, 0x19, 0xc7, 0xff, 0x67, 0x16, 0x58, 0x96, 0x01, 0x83, 0xc1, 0x80, 0x2f, 0x78, 0xcd, 0xda, 0x09, 0x76, 0x94, 0x34, 0x30, 0xb6, 0xdb, 0x88,
        0x07, 0x27, 0x55, 0xfb, 0x50, 0x25, 0x7d, 0x49, 0xa4, 0x3e, 0x54, 0x8a, 0x54, 0xc9, 0xe4, 0xc5, 0x51, 0x94, 0xca, 0x12, 0xc4, 0x4a, 0x4c, 0xa4,
        0x4a, 0x4e, 0x6d, 0xc5, 0x16, 0x2b, 0xa7, 0x4d, 0x54, 0xbf, 0xb4, 0x4b, 0xab, 0x56, 0x95, 0xaa, 0xa8, 0x8e, 0x54, 0x45, 0x72, 0xf3, 0xe0, 0x87,
        0x54, 0x36, 0x52, 0x2e, 0xf5, 0xb4, 0xd8, 0xb2, 0x5c, 0x88, 0x61, 0xb3, 0x0b, 0xec, 0x2e, 0xb0, 0xcb, 0xb2, 0xb3, 0x67, 0xf6, 0x3e, 0x5f, 0x1f,
        0x86, 0xbd, 0x32, 0xb3, 0x3b, 0x83, 0xf3, 0xd4, 0x66, 0x90, 0x40, 0x3e, 0x33, 0x73, 0xfe, 0xbf, 0xef, 0xff, 0x9d, 0xcb, 0x37, 0xc7, 0xc0, 0xb7,
        0xd7, 0xff, 0xcf, 0xc5, 0x0d, 0x5b, 0x99, 0xd9, 0xc3, 0x22, 0x00, 0xde, 0x45, 0x4f, 0x15, 0x0e, 0x64, 0x1c, 0x4d, 0x9b, 0x4d, 0x0f, 0x76, 0x3d,
        0x2c, 0xb6, 0xda, 0x95, 0x15, 0x41, 0x7b, 0xfe, 0xf1, 0xfd, 0xe1, 0x6e, 0x47, 0x08, 0x37, 0x3b, 0x54, 0xcb, 0x7d, 0xf0, 0x31, 0x3e, 0xc7, 0x49,
        0x25, 0x4e, 0x49, 0xe2, 0xc4, 0x49, 0x29, 0xa4, 0xae, 0x2f, 0x48, 0xc0, 0x57, 0x36, 0xe5, 0x79, 0x27, 0xbf, 0xc9, 0x29, 0x43, 0x0a, 0x25, 0x28,
        0x41, 0xea, 0x1f, 0xc9, 0xa1, 0x58, 0x75, 0x40, 0x59, 0x8c, 0x1e, 0x52, 0x59, 0x1e, 0x04, 0x80, 0xa1, 0x09, 0x2e, 0x74, 0xa1, 0x70, 0xfb, 0x93,
        0x17, 0x5f, 0x5e, 0xb7, 0x01, 0xe0, 0xc6, 0x1c, 0x9c, 0x31, 0x28, 0xc8, 0x81, 0x20, 0x40, 0x44, 0x77, 0x34, 0x35, 0xd0, 0x99, 0x16, 0x1b, 0x01,
        0xe8, 0x08, 0x77, 0x1e, 0x32, 0x4f, 0xf5, 0x13, 0x4e, 0xf4, 0x50, 0xf0, 0x27, 0x27, 0xaf, 0x5b, 0x94, 0x3f, 0x88, 0x79, 0x72, 0x06, 0x28, 0xbd,
        0xce, 0x6e, 0xe3, 0xc5, 0x2d, 0x41, 0x3a, 0x78, 0xab, 0xe7, 0xd9, 0xf2, 0x33, 0x82, 0xd9, 0xcb, 0x22, 0x64, 0x1c, 0x3f, 0x82, 0x45, 0x50, 0x65,
        0x6b, 0x06, 0xcb, 0x6c, 0xcf, 0x5f, 0x1f, 0xbc, 0x6c, 0x31, 0xfa, 0x79, 0x72, 0x06, 0x28, 0x13, 0x63, 0x83, 0xd2, 0x4b, 0xf8, 0x8f, 0xde, 0x4a,
        0x2c, 0x72, 0x2a, 0xfd, 0x64, 0xb2, 0x31, 0x00, 0x20, 0x41, 0x86, 0xb4, 0x0d, 0x81, 0x21, 0x86, 0xe6, 0x3f, 0x6d, 0x3e, 0xd5, 0x30, 0xf7, 0x07,
        0x31, 0xa7, 0xcb, 0xc3, 0x2d, 0xa9, 0xfc, 0x0d, 0xf7, 0x51, 0x56, 0x0a, 0x62, 0xe9, 0xe7, 0xed, 0x56, 0x00, 0xcc, 0x11, 0x36, 0x11, 0xbd, 0x05,
        0x28, 0xf5, 0x26, 0xdc, 0x41, 0xcc, 0x6b, 0xce, 0x60, 0x49, 0x1e, 0x57, 0xb2, 0x15, 0x9d, 0x64, 0x7b, 0x61, 0x0d, 0xc0, 0x1c, 0x41, 0xe9, 0xbc,
        0x7b, 0xbe, 0xa3, 0x81, 0x7c, 0x80, 0xd2, 0x25, 0xf9, 0x24, 0x22, 0xc4, 0xe2, 0xae, 0xc7, 0x0b, 0x97, 0x18, 0x80, 0x5c, 0xc1, 0x00, 0x60, 0x0e,
        0x40, 0xc4, 0x32, 0x82, 0x86, 0xb6, 0xc9, 0x7a, 0xf2, 0x05, 0xe7, 0xd7, 0x94, 0xad, 0x90, 0x5f, 0x21, 0x4a, 0x30, 0xcf, 0xb1, 0xaf, 0x8e, 0x24,
        0x5a, 0x00, 0x68, 0x9f, 0x1b, 0x00, 0x0c, 0x61, 0x6d, 0xb6, 0xdf, 0x70, 0xb5, 0xd2, 0x11, 0x58, 0x0d, 0x9d, 0xda, 0x16, 0x3c, 0x65, 0x2e, 0xbf,
        0x48, 0x39, 0x5d, 0xfe, 0x1c, 0xae, 0x28, 0x08, 0x11, 0x12, 0xc2, 0xe0, 0x70, 0x9c, 0x9f, 0x71, 0xbe, 0xeb, 0x02, 0x00, 0x9f, 0xbc, 0x1d, 0x20,
        0x79, 0xb4, 0xed, 0x3b, 0x9b, 0x7e, 0xd1, 0x04, 0x01, 0x60, 0xbf, 0x77, 0x54, 0xb5, 0xe5, 0xe0, 0x38, 0x6d, 0x2c, 0x9f, 0x77, 0x7e, 0x4d, 0x5a,
        0x31, 0xfa, 0xcb, 0x0a, 0xc2, 0x44, 0x09, 0xc1, 0x33, 0x1c, 0xe7, 0x67, 0x70, 0x2d, 0x89, 0x24, 0xd8, 0x2f, 0xa4, 0xa8, 0x64, 0x30, 0x06, 0x46,
        0xd2, 0x58, 0x74, 0x47, 0x4d, 0x10, 0x00, 0x4f, 0xde, 0x55, 0xf5, 0x6f, 0x42, 0xfb, 0x90, 0xb1, 0xfc, 0x12, 0xe5, 0x4b, 0xe6, 0x2b, 0x08, 0x11,
        0x25, 0x84, 0xc1, 0xe1, 0x0d, 0x7e, 0x06, 0xd7, 0x38, 0xc2, 0xc8, 0xfb, 0x46, 0x2e, 0xc8, 0x30, 0x48, 0x81, 0x76, 0x3c, 0x0d, 0x8d, 0x05, 0xdd,
        0x6b, 0x06, 0x08, 0x1c, 0x40, 0xf3, 0x8f, 0x9b, 0x6a, 0x96, 0xd0, 0x44, 0xaf, 0xb1, 0x7c, 0x65, 0xee, 0x43, 0x84, 0x52, 0xf4, 0x1c, 0x21, 0x68,
        0x3e, 0xe9, 0x15, 0x19, 0x92, 0x21, 0x80, 0x33, 0x0b, 0x06, 0x62, 0xcb, 0xee, 0xb0, 0x5f, 0x44, 0xa2, 0x66, 0x4d, 0x54, 0x7d, 0x05, 0x29, 0x53,
        0x03, 0x90, 0x6e, 0xa9, 0x2f, 0xaf, 0x14, 0xe5, 0x37, 0xca, 0xf2, 0x23, 0x35, 0xf2, 0x15, 0x00, 0xb9, 0x55, 0x2a, 0xae, 0x54, 0xee, 0x25, 0xff,
        0xae, 0x12, 0x02, 0x87, 0x08, 0xf5, 0x77, 0xda, 0xe9, 0x38, 0x52, 0x35, 0x29, 0x68, 0xe5, 0xa6, 0xf2, 0xe7, 0x70, 0xa5, 0x94, 0xfb, 0x0d, 0xf3,
        0xe8, 0x2b, 0x00, 0x38, 0x62, 0xb3, 0x45, 0x8b, 0x35, 0x16, 0x75, 0x2f, 0xf9, 0x77, 0x21, 0xb4, 0x25, 0x9f, 0xf2, 0x69, 0x63, 0x71, 0xac, 0x6d,
        0x1b, 0x15, 0x99, 0xa8, 0x9e, 0x1c, 0x0e, 0xb8, 0x31, 0x9f, 0x77, 0x06, 0xcb, 0xd1, 0x5f, 0xae, 0x95, 0x0f, 0x83, 0xa6, 0x8d, 0xe4, 0x4b, 0x00,
        0x22, 0xda, 0x3f, 0x2f, 0x8f, 0x47, 0x8d, 0x45, 0xdd, 0x01, 0x3f, 0xdb, 0x07, 0x90, 0x8b, 0xff, 0x59, 0x3b, 0xbd, 0x69, 0x20, 0xcf, 0xa0, 0x3e,
        0xbb, 0x7e, 0x51, 0x84, 0x02, 0x11, 0xf8, 0x48, 0x6b, 0x09, 0x14, 0x27, 0xde, 0x1b, 0xb8, 0x92, 0x40, 0x08, 0xa4, 0xb0, 0xc1, 0x72, 0xf4, 0x85,
        0xe9, 0x91, 0x31, 0x23, 0xf9, 0xaa, 0xdd, 0x30, 0xbc, 0xb6, 0xb2, 0xa7, 0x72, 0xeb, 0x3b, 0x80, 0x8e, 0x28, 0xf5, 0x68, 0x30, 0x8a, 0xbe, 0xb8,
        0x37, 0x3a, 0xbd, 0x47, 0x26, 0x38, 0x44, 0xdc, 0x59, 0x10, 0x76, 0x51, 0x79, 0xd9, 0x01, 0x14, 0xc1, 0x33, 0x1c, 0xd5, 0xe5, 0x23, 0xc8, 0x4f,
        0x4b, 0x26, 0xf2, 0x55, 0x4b, 0xf1, 0xe6, 0x05, 0x67, 0xd5, 0xad, 0x65, 0x04, 0x7a, 0x22, 0x58, 0x32, 0x95, 0x07, 0x32, 0xc8, 0x8e, 0x2b, 0x53,
        0x22, 0x7e, 0x8b, 0x81, 0xa3, 0xda, 0xd1, 0xd2, 0xd0, 0x03, 0x12, 0x65, 0xf9, 0x30, 0x0a, 0x3e, 0x73, 0xf9, 0x9a, 0x7a, 0xe0, 0x6e, 0x3c, 0xd7,
        0xc9, 0x6c, 0x96, 0x5c, 0x4e, 0xec, 0xf7, 0x76, 0x4d, 0x70, 0x88, 0xe0, 0xaf, 0xb3, 0x5f, 0x2b, 0x08, 0x81, 0x2a, 0xa2, 0x0f, 0x81, 0x4c, 0xcd,
        0x37, 0xd8, 0x8c, 0xf2, 0xa7, 0x9b, 0x6d, 0xd7, 0x7c, 0x19, 0x44, 0xc6, 0x53, 0x53, 0x22, 0x38, 0xc4, 0xf7, 0x97, 0xfc, 0x21, 0x2a, 0x28, 0xae,
        0x92, 0xfc, 0x2a, 0xf2, 0xbe, 0xfa, 0xf2, 0x35, 0x00, 0xd2, 0x47, 0xea, 0x3b, 0xf6, 0xab, 0x5d, 0x15, 0xc1, 0xf1, 0xe4, 0x94, 0x88, 0x3f, 0x60,
        0xc8, 0x43, 0xb7, 0x17, 0x0e, 0x1f, 0x2b, 0xe5, 0x7e, 0xdd, 0x77, 0xe2, 0x95, 0xfa, 0xf2, 0x35, 0x29, 0x90, 0x21, 0x41, 0xbe, 0x8a, 0xb3, 0xf6,
        0x21, 0x5c, 0x38, 0xe4, 0x75, 0x4d, 0xcc, 0xe2, 0x69, 0x00, 0xfc, 0x55, 0xfc, 0x86, 0x23, 0x82, 0xf8, 0xf4, 0xa9, 0xb1, 0xcf, 0xf0, 0x4c, 0x83,
        0x37, 0x6b, 0x52, 0x2e, 0x43, 0x82, 0xfc, 0x1a, 0x3e, 0x00, 0xc1, 0xd6, 0x60, 0x20, 0x74, 0xa3, 0xdf, 0xeb, 0x9a, 0xe0, 0x10, 0x91, 0x58, 0xca,
        0xee, 0x5f, 0x66, 0xb9, 0xe9, 0x93, 0x63, 0x5f, 0xe2, 0x64, 0xc3, 0x37, 0x0d, 0x65, 0xe4, 0x7e, 0xf8, 0xf0, 0x82, 0x3d, 0x0f, 0x08, 0x22, 0x04,
        0xef, 0xe3, 0xfa, 0xa4, 0xf4, 0xb3, 0x9b, 0x52, 0x43, 0xf3, 0xeb, 0x01, 0x40, 0x82, 0xdc, 0x87, 0x9f, 0xe1, 0x79, 0x78, 0xe0, 0x01, 0x90, 0x46,
        0xab, 0x15, 0x88, 0x56, 0xec, 0xf1, 0xf6, 0x4e, 0x2c, 0xa2, 0xd9, 0x71, 0xaf, 0xd0, 0x6f, 0x49, 0xbe, 0x4e, 0x59, 0x5e, 0xe6, 0x97, 0x99, 0x44,
        0xf2, 0x79, 0x5c, 0xb4, 0x86, 0xd0, 0xe6, 0x3d, 0x34, 0x91, 0x44, 0xbb, 0x65, 0xe7, 0x2c, 0x65, 0x5a, 0x86, 0x04, 0x79, 0x12, 0xbf, 0xb4, 0x86,
        0xd0, 0xe3, 0xed, 0x9b, 0xb0, 0xfe, 0x11, 0x27, 0x58, 0x79, 0x48, 0x82, 0x0c, 0xe9, 0x22, 0xde, 0xb6, 0xf2, 0x6c, 0x1a, 0xd1, 0xf1, 0xe8, 0x94,
        0x58, 0xa7, 0x66, 0xde, 0x81, 0x03, 0xf6, 0x5d, 0x70, 0x7a, 0x0f, 0x5b, 0x74, 0x41, 0xb0, 0x0a, 0x60, 0xcf, 0x85, 0xcc, 0xb8, 0x7f, 0xca, 0xac,
        0xb8, 0xdb, 0xa1, 0x03, 0xf6, 0x5d, 0xc8, 0x7a, 0x87, 0x2d, 0xb8, 0x60, 0x73, 0xef, 0x09, 0xc0, 0x6d, 0x19, 0xc1, 0x05, 0xd5, 0x7b, 0xbc, 0x21,
        0x82, 0x60, 0x47, 0x3e, 0xf2, 0xcc, 0xf9, 0x96, 0xa4, 0xe5, 0x44, 0xa4, 0xd0, 0x36, 0xfe, 0xaf, 0x86, 0x89, 0xb0, 0xe1, 0xc0, 0x83, 0xd1, 0x43,
        0x33, 0xd9, 0x60, 0xa7, 0x3b, 0x89, 0x76, 0x1b, 0x89, 0x70, 0x78, 0x87, 0xea, 0xba, 0x20, 0x58, 0x97, 0x1f, 0x98, 0x51, 0xb1, 0x32, 0xc0, 0x03,
        0xed, 0x88, 0xd9, 0x18, 0x8e, 0x85, 0xf1, 0xf9, 0xba, 0x2e, 0x58, 0x04, 0x98, 0x1f, 0x1d, 0x98, 0xc9, 0x62, 0x05, 0x69, 0x16, 0x3c, 0x10, 0xbe,
        0xd1, 0xad, 0xcf, 0x88, 0x49, 0x6b, 0x08, 0xf9, 0xba, 0x08, 0x96, 0x00, 0xe4, 0xd1, 0xbd, 0x33, 0x59, 0x2c, 0x23, 0x0f, 0x50, 0x2a, 0xb0, 0xf7,
        0xf9, 0x87, 0xfa, 0xa4, 0xbc, 0x64, 0xd5, 0x85, 0x7a, 0x08, 0x16, 0x00, 0x3e, 0x1d, 0x7d, 0x6c, 0x26, 0x8b, 0x15, 0xe4, 0x01, 0x82, 0x5f, 0xf2,
        0xc8, 0x38, 0x62, 0x7b, 0x5d, 0x30, 0x47, 0x60, 0x8d, 0xe5, 0xa5, 0x99, 0x1c, 0x42, 0xc8, 0xe9, 0xf2, 0x87, 0x2b, 0x37, 0x59, 0x7b, 0xeb, 0x42,
        0xd3, 0xd6, 0x66, 0x6d, 0x0b, 0xe0, 0xee, 0xe8, 0x60, 0x65, 0xf4, 0x87, 0x6b, 0xf7, 0x78, 0x7b, 0x08, 0x2d, 0x5b, 0x45, 0xbc, 0x45, 0x00, 0xb5,
        0x49, 0xfd, 0x51, 0xeb, 0xc7, 0xc5, 0xdc, 0x1b, 0xc9, 0xdb, 0x47, 0xd8, 0xed, 0xdd, 0x5b, 0x83, 0xc0, 0x0c, 0x0f, 0x19, 0x0e, 0x60, 0x8a, 0x1d,
        0xc7, 0x10, 0x83, 0x6a, 0x62, 0xfe, 0xce, 0x11, 0xba, 0xdf, 0xed, 0x7f, 0xab, 0xa1, 0x03, 0xea, 0xfd, 0xfc, 0x13, 0x29, 0xa4, 0x91, 0x46, 0x1a,
        0x5a, 0x03, 0x79, 0x8b, 0x08, 0xab, 0xe8, 0x2b, 0xd5, 0x0b, 0x3f, 0xed, 0xfb, 0x4b, 0xdd, 0x59, 0xc0, 0xcf, 0x69, 0x4f, 0x84, 0xb1, 0x8c, 0x18,
        0x54, 0x68, 0x00, 0xb0, 0x50, 0x5f, 0xbe, 0xc1, 0x8c, 0x48, 0xe3, 0x55, 0x34, 0x4b, 0xfd, 0xe8, 0xc5, 0xfb, 0xfa, 0x02, 0xad, 0x5e, 0xab, 0xe3,
        0x80, 0x82, 0xd5, 0x7d, 0xfb, 0x57, 0x36, 0xaa, 0x8f, 0xab, 0x5e, 0xc0, 0x8d, 0xc6, 0xf5, 0x9d, 0xa9, 0x0b, 0xc3, 0xd2, 0x6c, 0xe9, 0xfe, 0x25,
        0xbc, 0x05, 0x34, 0xa3, 0xe5, 0x44, 0xdf, 0x9d, 0x2e, 0x63, 0x07, 0x3a, 0xd0, 0xff, 0xf7, 0x4c, 0xed, 0xd7, 0xe0, 0x3d, 0x2b, 0xe5, 0xa5, 0x89,
        0x0b, 0x7f, 0xd3, 0xe5, 0xf5, 0xfb, 0x4f, 0x4e, 0xb2, 0x1c, 0x90, 0x47, 0xfa, 0x87, 0x5d, 0x66, 0x29, 0x50, 0xce, 0x0a, 0x4f, 0xaf, 0xe9, 0xc6,
        0x97, 0xbe, 0xbd, 0xa4, 0xe0, 0x23, 0x94, 0x2c, 0x9f, 0x56, 0xde, 0x6f, 0xa1, 0xfe, 0x7b, 0x04, 0x42, 0xc1, 0x63, 0x38, 0x06, 0x38, 0x62, 0xbd,
        0xc2, 0xd5, 0x38, 0x92, 0xd5, 0xfd, 0xbe, 0x67, 0x7d, 0xbf, 0xdc, 0x8e, 0x40, 0xee, 0x9a, 0x13, 0xe8, 0x0e, 0x87, 0x7e, 0xc4, 0x66, 0x04, 0x20,
        0xa2, 0xf5, 0x46, 0x0e, 0x55, 0x67, 0xf1, 0x71, 0x7c, 0x4f, 0x7a, 0x53, 0xc6, 0xce, 0x11, 0x9a, 0x9f, 0x2b, 0xff, 0x4f, 0x09, 0xc7, 0xc6, 0x80,
        0xf0, 0x98, 0x00, 0x00, 0x1f, 0x1a, 0x02, 0x24, 0x5f, 0xa3, 0x13, 0xeb, 0xa8, 0x38, 0x45, 0xbd, 0x8a, 0xdd, 0xd2, 0x17, 0xb0, 0xf8, 0x81, 0x61,
        0x8c, 0x40, 0x23, 0xfe, 0x0b, 0xe2, 0xd6, 0x39, 0x9b, 0x88, 0xa6, 0xdb, 0x19, 0x14, 0x80, 0x94, 0x74, 0x6b, 0xdb, 0x2c, 0xe0, 0xc8, 0xef, 0x6e,
        0x89, 0xc5, 0x11, 0x2e, 0xb6, 0xc7, 0xf0, 0x03, 0xe9, 0xdf, 0xb2, 0x4d, 0x71, 0xa3, 0x19, 0xd1, 0x8c, 0xdd, 0xff, 0xcc, 0x7b, 0x1d, 0x11, 0xf1,
        0xbb, 0x2d, 0xe7, 0xa9, 0x63, 0x85, 0x71, 0xe0, 0xac, 0xf4, 0x2b, 0x83, 0x97, 0x92, 0x1f, 0x6f, 0x6a, 0x77, 0x49, 0xd6, 0x7f, 0x2e, 0xe3, 0x11,
        0x2f, 0x19, 0x80, 0x3c, 0xa9, 0xf7, 0x36, 0x4b, 0x6b, 0xc4, 0x29, 0x49, 0x51, 0x9a, 0x23, 0x99, 0xe4, 0x0f, 0xf5, 0xbb, 0xdb, 0x1c, 0x68, 0x73,
        0xdc, 0x0f, 0x64, 0xf6, 0x31, 0x86, 0x75, 0x3c, 0x27, 0xdd, 0xdf, 0x69, 0xec, 0xdb, 0xbe, 0xb3, 0xdf, 0x83, 0x03, 0x0c, 0x10, 0xc0, 0xf4, 0xe4,
        0x7e, 0x20, 0xbd, 0x6e, 0xd2, 0xb7, 0x17, 0x60, 0xf2, 0xaa, 0xfc, 0x4e, 0x35, 0xdf, 0x23, 0x3b, 0xd1, 0x2d, 0xfb, 0xe4, 0xfb, 0x32, 0xc9, 0x24,
        0x2f, 0xc8, 0xd7, 0xe5, 0x63, 0x75, 0x7b, 0x97, 0x2b, 0x7e, 0x7f, 0x63, 0x00, 0xc5, 0xbf, 0xc2, 0x37, 0xdf, 0xf7, 0xb7, 0xd7, 0xff, 0xca, 0xf5,
        0x5f, 0xcd, 0xfc, 0xd5, 0x20, 0x13, 0x76, 0xce, 0x31, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4e, 0x44, 0xae, 0x42, 0x60, 0x82
      });
    }

    public void OnDestroy () {
      GameEvents.onGUIApplicationLauncherReady.Remove (OnGUIApplicationLauncherReady);
      GameEvents.onGUIApplicationLauncherUnreadifying.Remove (OnGUIApplicationLauncherUnreadifying);
      GameEvents.onLevelWasLoadedGUIReady.Remove (updateCallback1);
      GameEvents.onHideUI.Remove (CloseMainWindow);
      GameEvents.onShowUI.Remove (OpenMainWindow);

      MapView.OnEnterMapView = (Callback)Delegate.Remove (MapView.OnEnterMapView, updateCallback2);
      MapView.OnExitMapView = (Callback)Delegate.Remove (MapView.OnExitMapView, updateCallback2);
      SaveConfig ();
    }

    public void OnGUIApplicationLauncherReady () {
      ApplicationLauncher.Instance.prefab_verticalTopDown.GetGameObject ().AddOrGetComponent<ModPanelComponent> ();
      ApplicationLauncher.Instance.prefab_horizontalRightLeft.GetGameObject ().AddOrGetComponent<ModPanelComponent> ();
      if (ApplicationLauncher.Ready && appButton == null) {
        appButton = ApplicationLauncher.Instance.AddModApplication (OpenMainWindow, CloseMainWindow, null, null,
          AppButtonEnable, AppButtonDisable, ApplicationLauncher.AppScenes.ALWAYS & (~ApplicationLauncher.AppScenes.MAINMENU), appButtonTexture);
        // this mod should be always on in KSC
        appButton.container.Data = ApplicationLauncher.AppScenes.SPACECENTER;
      }
      forceUpdateCount += 3;
    }

    private void OnGUIApplicationLauncherUnreadifying (GameScenes scene) {
      appButton?.SetFalse ();
    }

    public void AppButtonDisable () {
      appButton?.SetFalse ();
    }

    public void AppButtonEnable () {
      // not needed, really
    }

    internal ApplicationLauncher.AppScenes GetModScenes (string module, string method, ApplicationLauncher.AppScenes scenes, uint hash) {
      foreach (var mod in descriptors) {
        if (mod.module == module && mod.method == method) {
          if (!mod.textureHashNeeded || (mod.textureHash == hash)) {
            if (mod.unmaintainable) {
              return scenes;
            }
            // lets check that the scenes didn't change while we were frolicking about
            if (scenes != currentScenes[mod] && scenes != mod.defaultScenes) {
              Debug.Log ("[Adjustable Mod Panel] WARNING: mod " + module + "+" + method + " changed visibility. This is unexpected, ignoring it.");
              return scenes;
            }
            if (pendingChanges.ContainsKey (mod)) {
              currentScenes[mod] = pendingChanges[mod];
              pendingChanges.Remove (mod);
            }
            return currentScenes[mod];
          }
        }
      }
      Debug.Log ("[Adjustable Mod Panel] WARNING: mod " + module + "+" + method + " was not found.");
      return scenes;
    }

    internal bool IsAppButton (ApplicationLauncherButton appButton) {
      if (appButton == this.appButton)
        return true;
      return false;
    }

    private int forceUpdateCount = 0;
    internal bool ForceUpdate {
      get {
        if (forceUpdateCount > 0) {
          forceUpdateCount--;
          return true;
        }
        return false;
      }
    }

    internal void StartRecordingMods () {
      tempDescriptors = new List<ModDescriptor> ();
    }

    internal void RecordMod (string module, string method, ApplicationLauncher.AppScenes scenes, uint hash, ApplicationLauncher.AppScenes alwaysOn,Texture2D texture) {
      if (tempDescriptors == null) {
        Debug.Log ("[Adjustable Mod Panel] WARNING: mod is recorded in unexpected place.");
        return;
      }
      tempDescriptors.Add (new ModDescriptor () {
        module = module,
        method = method,
        textureHash = hash,
        textureHashNeeded = false,
        unmaintainable = false,
        defaultScenes = scenes,
        requiredScenes = alwaysOn,
        modIcon = texture
      });
      /*if (!modList.ContainsKey (descriptor)) {
        Debug.Log ("[Adjustable Mod Panel] Recorded a new mod: " + descriptor.Split ('+')[0]);
        modList[descriptor] = new KeyValuePair<ApplicationLauncher.AppScenes, Texture2D> (visibleInScenes, texture);
      } else {
        // update the value in case the visibility was expanded by the mod
        modList[descriptor] = new KeyValuePair<ApplicationLauncher.AppScenes, Texture2D> ((visibleInScenes | modList[descriptor].Key), texture);
      }
      if (!modMatrix.ContainsKey (descriptor))
        modMatrix[descriptor] = visibleInScenes;
      if (!modMatrixAlwaysOn.ContainsKey (descriptor)) {
        if (alwaysOn != ApplicationLauncher.AppScenes.NEVER)
          Debug.Log ("[Adjustable Mod Panel]  Mod " + descriptor.Split('+')[0] + " requests to be unswitchable in scenes: " + alwaysOn);
        modMatrixAlwaysOn[descriptor] = alwaysOn;
      }*/
    }

    internal void EndRecordingMods () {
      if (tempDescriptors == null) {
        Debug.Log ("[Adjustable Mod Panel] WARNING: mod is recorded in unexpected place.");
        return;
      }
      if (tempDescriptors.Count == 0) {
        tempDescriptors = null;
        return;
      }

      var easy = new List<ModDescriptor> ();
      var hard = new List<ModDescriptor> ();
      var unmaintainable = new List<ModDescriptor> ();
      for (int i = 0; i < tempDescriptors.Count; i++) {
        var a = tempDescriptors[i];
        for (int j = i + 1; j < tempDescriptors.Count; j++) {
          var b = tempDescriptors[j];
          if (a.module == b.module && a.method == b.method) {
            a.textureHashNeeded = b.textureHashNeeded = true;
            if (a.textureHash == b.textureHash) {
              Debug.Log ("[Adjustable Mod Panel] WARNING: " + a.module + " defines several app buttons that are exactly the same. We will ignore them from now on.");
              a.unmaintainable = true;
            }
          }
        }
        if (a.textureHashNeeded == false) {
          easy.Add (a);
        } else if (a.unmaintainable == false) {
          hard.Add (a);
        } else {
          unmaintainable.Add (a);
        }
      }
      foreach (var mod in easy) {
        bool add = true;
        foreach (var modref in descriptors) {
          if (mod.module == modref.module && mod.method == modref.method) {
            if (modref.textureHashNeeded == false) {
              // we assume this is the mod, and end with it
              add = false;
              FillModIfNeeded (modref, mod);
              break;
            }
            if (modref.textureHashNeeded == true) {
              // there used to be several mods with this signature
              // throw it into the "hard" pile
              mod.textureHashNeeded = true;
              hard.Add (mod);
              add = false;
              break;
            }
          }
        }
        if (add) {
          //new mod
          Debug.Log ("[Adjustable Mod Panel] Record a new mod: module " + mod.module + ", method " + mod.method);
          descriptors.Add (mod);
          currentScenes[mod] = mod.defaultScenes;
        }
      }
      foreach (var mod in hard) {
        bool add = true;
        foreach (var modref in descriptors) {
          if (mod.module == modref.module && mod.method == modref.method) {
            // whatever was before, now we consider the texture as a part of a descriptor
            modref.textureHashNeeded = true;
            if (mod.textureHash == modref.textureHash) {
              // mod found
              FillModIfNeeded (modref, mod);
              add = false;
              break;
            }
          }
        }
        if (add) {
          //new mod
          Debug.Log ("[Adjustable Mod Panel] Record a new mod: module " + mod.module + ", method " + mod.method + ", hash " + mod.textureHash);
          Debug.Log ("[Adjustable Mod Panel] WARNING: Since there are several mods with such signature, the icon would be used as a descriptor");
          descriptors.Add (mod);
          currentScenes[mod] = mod.defaultScenes;
        }
      }
      foreach (var mod in unmaintainable) {
        bool add = true;
        foreach (var modref in descriptors) {
          if (mod.module == modref.module && mod.method == modref.method && mod.textureHash == modref.textureHash) {
            modref.unmaintainable = true;
            add = false;
          }
        }
        if (add) {
          descriptors.Add (mod);
        }
      }
      tempDescriptors = null;
    }

    void FillModIfNeeded (ModDescriptor to, ModDescriptor from) {
      if (to.modIcon != null)
        return;
      Debug.Log ("[Adjustable Mod Panel] Encountered a known mod: module " + to.module + ", method " + to.method + 
        (to.textureHashNeeded? (", hash " + to.textureHash.ToString()) : ""));
      to.modIcon = from.modIcon;
      to.requiredScenes = from.requiredScenes;
      to.defaultScenes = from.defaultScenes;
    }

    #region Config

    internal void SaveConfig () {
      Debug.Log ("[Adjustable Mod Panel] Saving settings.");
      KSP.IO.PluginConfiguration config = KSP.IO.PluginConfiguration.CreateForType<AdjustableModPanel> (null);
      int i = 1;
      foreach (var mod in descriptors) {
        if (mod.unmaintainable)
          continue;
        // artifact from an old config
        if (mod.textureHashNeeded && mod.textureHash == 0u)
          continue;
        config["module" + i.ToString ()] = mod.module;
        config["method" + i.ToString ()] = mod.method;
        config["hashNeeded" + i.ToString ()] = mod.textureHashNeeded;
        if (mod.textureHashNeeded)
          config["hash" + i.ToString ()] = (long)mod.textureHash;
        config["scenes" + i.ToString ()] = (int)currentScenes[mod];
        i++;
      }
      config["count"] = i-1;
      config.save ();
    }

    private void LoadConfig () {
      Debug.Log ("[Adjustable Mod Panel] Loading settings.");
      KSP.IO.PluginConfiguration config = KSP.IO.PluginConfiguration.CreateForType<AdjustableModPanel> (null);
      config.load ();
      try {
        int count = config.GetValue<int> ("count");
        for (int i = 1; i <= count; i++) {
          string key = config.GetValue<string> ("mod" + i.ToString (), "");
          ModDescriptor desc = null;
          if (key != "") {
            var vals = key.Split('+');
            desc = new ModDescriptor () {
              module = vals[0],
              method = vals[1],
              textureHash = 0,
              textureHashNeeded = false,
              unmaintainable = false,
              modIcon = null,
              defaultScenes = ApplicationLauncher.AppScenes.NEVER,
              requiredScenes = ApplicationLauncher.AppScenes.NEVER
            };
          } else {
            desc = new ModDescriptor () {
              module = config.GetValue<string> ("module" + i.ToString ()),
              method = config.GetValue<string> ("method" + i.ToString ()),
              textureHashNeeded = config.GetValue<bool> ("hashNeeded" + i.ToString ()),
              textureHash = (uint)config.GetValue<long> ("hash" + i.ToString (), 0u),
              unmaintainable = false,
              modIcon = null,
              defaultScenes = ApplicationLauncher.AppScenes.NEVER,
              requiredScenes = ApplicationLauncher.AppScenes.NEVER
            };
          }
          ApplicationLauncher.AppScenes value = (ApplicationLauncher.AppScenes)config.GetValue<int> ("scenes" + i.ToString ());
          descriptors.Add (desc);
          currentScenes[desc] = value;
        }
      } catch (System.Exception e) {
        Debug.Log ("[Adjustable Mod Panel] There was an error reading config: " + e);
      }
    }

    #endregion

  }
}

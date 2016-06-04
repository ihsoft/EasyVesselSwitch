// KSP Easy Vessel Switch
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using HighlightingSystem;
using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EasyVesselSwitch {

/// <summary>Main mod's class that monitors use interactions.</summary>
[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
[PersistentFieldsFile("EasyVesselSwitch/Plugins/PluginData/settings.cfg", "")]
sealed class Controller : MonoBehaviour {
  // ===== BEGIN of persistent fields section.
  [PersistentField("UI/switchModifier")]
  static KeyModifiers switchModifier = KeyModifiers.AnyAlt;
  [PersistentField("UI/switchMouseButton")]
  static Mouse.Buttons switchMouseButton = Mouse.Buttons.Left;
  [PersistentField("UI/targetVesselHighlightColor")]
  static Color targetVesselHighlightColor = Color.yellow;
  [PersistentField("UI/newVesselHighlightTimeout")]
  static float newVesselHighlightTimeout = 0.5f;

  [PersistentField("CameraStabilization/animationDuration")]
  static float cameraStabilizationAnimationDuration = 1f;
  [PersistentField("CameraStabilization/mode")]
  static CameraStabilization cameraStabilizationMode = CameraStabilization.KeepDistanceAndRotation;
  [PersistentField("CameraStabilization/maxVesselDistance")]
  static int maxVesselDistance = 100;  // Meters.
  [PersistentField("CameraStabilization/switchModeKey")]
  static KeyCode switchStabilizationModeKey = KeyCode.F7;

  [PersistentField("VesselInfo/fontSize")]
  static int vesselInfoFontSize = 10;
  [PersistentField("VesselInfo/backgroundColor")]
  static Color vesselInfoBackgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.5f);
  [PersistentField("VesselInfo/textColor")]
  static Color vesselInfoTextColor = Color.white;
  [PersistentField("VesselInfo/hintPadding")]
  static int vesselInfoHintPadding = 3;
  // ===== END of persistent fields section.

  // ===== BEGIN of localizable strings
  Message SwitchToMsg = "Switch to:";
  Message CurrentVesselMsg = "Currently selected:";
  Message<VesselType, string> VesselTitleMsg = "{0}: {1}";
  Message<string, string, int> KerbalTitleMsg = "{0} ({1}-{2})";
  Message<float> VesselMassMsg = "Total mass {0:0.###}t";
  Message<string, double> VesselResourceMsg = "{0}: {1:P0}";
  Message<string> SinglePartTitleMsg = "Part: {0}";
  Message<string> AssemblyTitleMsg = "Assembly: {0}";
  Message<double> KerbalEvaFuelMsg = "EVA propellant: {0:F3}";
  Message VesselIsControllableMsg = "IS controllable";
  Message VesselIsNotControllableMsg = "Is NOT controllable";
  Message<CameraStabilization> CameraStabilizationModeChanged = "EVS stabilization: {0}";
  Message<float> DistantVesselTargeted = "Vessel is too distant: {0:N0}m";
  Message vesselIsAttachedToTheGroundMsg = "IS attached to the ground";
  Message vesselIsNotAttachedToTheGroundMsg = "Is NOT attached to the ground";
  // ===== END of localizable strings

  /// <summary>A mode of camera stabilization.</summary>
  enum CameraStabilization {
    /// <summary>No stabilization. Allow dfeault KSP behavior.</summary>
    None = 0,
    /// <summary>Keep camera at the same position and move focus to the new vessel.</summary>
    KeepPosition = 1,
    /// <summary>Keep the same camera distance and rotation as was on the old vessel.</summary>
    KeepDistanceAndRotation = 2,
  }

  /// <summary>Vessel which is currently hovered.</summary>
  Vessel hoveredVessel;
  /// <summary>Tells if camera needs to be adjused.</summary>
  bool needCameraFix;
  /// <summary>Overaly window to show info about vessel under the mouse cursor.</summary>
  HintOverlay vesselInfoOverlay;
  /// <summary>Old vessel context.</summary>
  VesselInfo oldInfo;
  /// <summary>New vessel context.</summary>
  VesselInfo newInfo;
  /// <summary>Defines if currently selected vessel was a result of EVS mouse click event.</summary>
  bool evsSwitchAction;
  /// <summary>Specifies if hovered vessel is attached to the ground.</summary>
  /// <remarks><c>null</c> means no static attachable parts found.</remarks>
  bool? isKisStaticAttached;
  /// <summary>If hovered vessel is a kerbal then this will be the component.</summary>
  /// <remarks><c>null</c> means hovered vessel is not a kerbal.</remarks>
  KerbalEVA kerbalEva;

  /// <summary>Overridden from MonoBehavior.</summary>
  /// <remarks>Registers listeners, reads configuration and creates global UI objects.</remarks>
  void Awake() {
    GameEvents.onVesselSwitching.Add(OnVesselSwitch);
    GameEvents.onVesselChange.Add(OnVesselChange);
    ConfigAccessor.ReadFieldsInType(typeof(Controller), null);
    vesselInfoOverlay = new HintOverlay(
        vesselInfoFontSize, vesselInfoHintPadding, vesselInfoTextColor, vesselInfoBackgroundColor);
  }

  /// <summary>Overridden from MonoBehavior.</summary>
  void OnDestroy() {
    GameEvents.onVesselSwitching.Remove(OnVesselSwitch);
    GameEvents.onVesselChange.Remove(OnVesselChange);
  }

  /// <summary>Overridden from MonoBehavior.</summary>
  /// <remarks>Persents hovered vessel info.</remarks>
  void OnGUI() {
    if (hoveredVessel != null) {
      ShowHoveredVesselInfo();
    }
  }

  /// <summary>Overridden from MonoBehavior.</summary>
  /// <remarks>Tracks keys and mouse moveement.</remarks>
  void Update() {
    // Cancel any selection if game is paused or time warped. Block UI as well. 
    if (Mathf.Approximately(Time.timeScale, 0f) || Time.timeScale > 1f) {
      if (hoveredVessel != null) {
        SetHoveredVessel(null);
      }
      return;
    }

    // Handle stabilization mode switch. 
    if (Input.GetKeyDown(switchStabilizationModeKey)) {
      if (cameraStabilizationMode == CameraStabilization.None) {
        cameraStabilizationMode = CameraStabilization.KeepPosition;
      } else if (cameraStabilizationMode == CameraStabilization.KeepPosition) {
        cameraStabilizationMode = CameraStabilization.KeepDistanceAndRotation;
      } else {
        cameraStabilizationMode = CameraStabilization.None;
      }
      ScreenMessaging.ShowPriorityScreenMessage(
          CameraStabilizationModeChanged.Format(cameraStabilizationMode));
    }

    // Handle the switch.
    if (Mouse.HoveredPart && EventChecker.IsModifierCombinationPressed(switchModifier)) {
      SetHoveredVessel(Mouse.HoveredPart.vessel);
      if (Mouse.GetAllMouseButtonsDown() == switchMouseButton
          && hoveredVessel != null && hoveredVessel != FlightGlobals.ActiveVessel) {
        if (hoveredVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned) {
          // Cannot switch to unowned vessel. Invoke standard "soft" switch to have error message
          // triggered.
          FlightGlobals.SetActiveVessel(hoveredVessel);
        } else {
          // Use forced version since "soft" switch blocks on many normal situations (e.g. "on
          // ladder" or "in atmosphere").
          var vesselToSelect = hoveredVessel;  // Save hovered vessel as it'll be reset on blur. 
          SetHoveredVessel(null);
          FlightGlobals.ForceSetActiveVessel(vesselToSelect);
          evsSwitchAction = true;
        }
      }
    } else if (hoveredVessel != null) {
      SetHoveredVessel(null);  // Cancel highlight.
    }
  }

  /// <summary>GameEvents callback.</summary>
  /// <remarks>Detects vessel switch and remembers old camera settings if switching from the
  /// currently active vessel.</remarks>
  /// <param name="fromVessel">A vessel prior the switch.</param>
  /// <param name="toVessel">A new active vessel.</param>  
  void OnVesselSwitch(Vessel fromVessel, Vessel toVessel) {
    if (fromVessel != null && fromVessel == FlightGlobals.ActiveVessel
        && cameraStabilizationMode != CameraStabilization.None) {
      oldInfo = new VesselInfo(fromVessel, FlightCamera.fetch);
      newInfo = new VesselInfo(toVessel);
      needCameraFix = true;
    }
  }

  /// <summary>GameEvents callback.</summary>
  /// <remarks>Highlights newly selected vessel and handles camear stabilization.</remarks>
  /// <param name="vessel">A new active vessel.</param>
  void OnVesselChange(Vessel vessel) {
    // Temporarily highlight the new vessel. 
    StartCoroutine(TimedHighlightCoroutine(
        vessel, newVesselHighlightTimeout, targetVesselHighlightColor));

    // Handle camera stabilization if needed.
    var camera = FlightCamera.fetch;
    newInfo.UpdateCameraFrom(camera);
    if (needCameraFix) {
      needCameraFix = false;
      // Camera position cannot be transitioned between any modes. Some modes (e.g. LOCKED) don't
      // allow the camera to be placed at any place. Don't do camera stablization or
      // aligning for such modes. In the modes that allow free camera position the transformations
      // can be very different so, just copy source mode into the target vessel.
      // TODO(IgorZ): Find a way to do the translation between different modes. 
      if (Vector3.Distance(oldInfo.anchorPos, newInfo.anchorPos) > maxVesselDistance) {
        // On the distant vessels respect camera modes of the both vessels. If either of them is not
        // "free" then just fallback to the default behavior (restore latest known position).
        if (IsFreeCameraPositionMode(oldInfo.cameraMode)
            && IsFreeCameraPositionMode(newInfo.cameraMode)) {
          SetCurrentCameraMode(oldInfo.cameraMode);  // Sync modes to match transformations.
          AlignCamera();
        }
      } else {
        // On close vessels if source mode is "free" then substitute target mode with it. Only do so
        // when mouse select is used (i.e. it's an explicit EVS action). Fallback to the default
        // behavior if it was an implicit switch (via a KSP hotkey) and the traget mode is not
        // "free".
        if (IsFreeCameraPositionMode(oldInfo.cameraMode)
            && (evsSwitchAction || IsFreeCameraPositionMode(newInfo.cameraMode))) {
          SetCurrentCameraMode(oldInfo.cameraMode);  // Sync modes to match transformations.
          StabilizeCamera();
        }
      }
    }
    evsSwitchAction = false;
  }

  /// <summary>Aligns new camera FOV when switching to a distant vessel.</summary>
  /// <remarks>If usual stabilization modes are not feasible then position new camera so what that
  /// old and new vessels are on the same line of view.</remarks>
  void AlignCamera() {
    var oldCameraDistance = Vector3.Distance(oldInfo.cameraPos, oldInfo.cameraPivotPos);
    var fromOldToNewDir = oldInfo.cameraPivotPos - newInfo.cameraPivotPos;

    Vector3 newCameraPos;
    if (FlightGlobals.ActiveVessel.Landed) {
      // When vessel is landed the new camera position may end up under the surface. To work it
      // around keep the same angle between line of sight and vessel's up axis as it was with the
      // previous vessel.
      var oldPivotUp = FlightGlobals.getUpAxis(oldInfo.cameraPivotPos);
      var oldCameraDir = oldInfo.cameraPivotPos - oldInfo.cameraPos;
      var angle = Vector3.Angle(oldPivotUp, oldCameraDir);
      var newPivotUp = FlightGlobals.getUpAxis(newInfo.cameraPivotPos);
      var rot = Quaternion.AngleAxis(angle, Vector3.Cross(newPivotUp, fromOldToNewDir));
      newCameraPos = newInfo.cameraPivotPos - rot * (newPivotUp * oldCameraDistance);
    } else {
      // In space just put camera on the opposite side and direct it to the old vessel. This way
      // both old and new vessela will be in camera's field of view.
      newCameraPos = newInfo.cameraPivotPos - fromOldToNewDir.normalized * oldCameraDistance;
    }
    
    var camera = FlightCamera.fetch;
    camera.SetCamCoordsFromPosition(newCameraPos);
    camera.GetCameraTransform().position = newCameraPos;
  }

  /// <summary>Prevents random jumping of the new vessel's camera.</summary>
  /// <remarks>Depending on the mode either preserves old camera position and changes focus of view
  /// to the new vessel or keeps vessel-to-camera rotation.</remarks>
  void StabilizeCamera() {
    var camera = FlightCamera.fetch;

    if (cameraStabilizationMode == CameraStabilization.KeepDistanceAndRotation) {
      // Restore old pivot and camera position to have original rotations applied to the camera.
      // Then, either animate the pivot or set it instantly. KSP code will move the camera
      // following the pivot without changing its rotation or distance.
      Logger.logInfo("Fix camera position while keeping distance and orientation");
      camera.GetPivot().position = oldInfo.cameraPivotPos;
      camera.SetCamCoordsFromPosition(oldInfo.cameraPos);
      if (cameraStabilizationAnimationDuration < float.Epsilon) {
        camera.GetPivot().position = newInfo.cameraPivotPos;
      } else {
        StartCoroutine(AnimateCameraPositionCoroutine(camera.Target, oldInfo, newInfo));
      }
    }

    if (cameraStabilizationMode == CameraStabilization.KeepPosition) {
      // Restore old camera position and recalculate camera orientation. KSP code always orient
      // camera on the pivot. When animation is disabled then just setting the position is enough
      // since the pivot is set to the new vessel. When animation is enabled we animate the pivot
      // and reset the camera position to have only direction recalculated.
      Logger.logInfo("Fix camera focus while keeping its position");
      if (cameraStabilizationAnimationDuration < float.Epsilon) {
        camera.SetCamCoordsFromPosition(oldInfo.cameraPos);
        camera.GetCameraTransform().position = oldInfo.cameraPos;
      } else {
        StartCoroutine(AnimateCameraPivotCoroutine(camera.Target, oldInfo, newInfo));
      }
    }
  }

  /// <summary>Sets vessel which is currently has mouse focus.</summary>
  /// <remarks>Current vessel is highlighted with the configured color.</remarks>
  /// <param name="vessel">A vessel to set as current.</param>
  void SetHoveredVessel(Vessel vessel) {
    if (vessel != hoveredVessel) {
      if (hoveredVessel != null) {
        SetVesselHighlight(hoveredVessel, null);
        isKisStaticAttached = null;
        kerbalEva = null;
      }
      if (vessel != null) {
        SetVesselHighlight(vessel, targetVesselHighlightColor);
        isKisStaticAttached = IsAttachedToGround(vessel.rootPart);
        kerbalEva = vessel.GetComponent<KerbalEVA>();
      }
      hoveredVessel = vessel;
    }
  }

  /// <summary>Displays brief information about the vessel under mouse cursor.</summary>
  /// <remarks>It's called every frame so, don't put heavy code here. If focus change needs heavy
  /// processing do it in <see cref="SetHoveredVessel"/>.</remarks>
  /// <remarks><see cref="hoveredVessel"/> must not be <c>null</c>.</remarks>
  void ShowHoveredVesselInfo() {
    var sb = new List<string>();
    sb.Add(hoveredVessel == FlightGlobals.ActiveVessel ? CurrentVesselMsg : SwitchToMsg);
    sb.Add("");

    // Give a hint when distance is too long.
    float distanceBetweenVessels = Vector3.Distance(
        FlightGlobals.ActiveVessel.transform.position, hoveredVessel.transform.position);
    if (distanceBetweenVessels > maxVesselDistance) {
      sb.Add(DistantVesselTargeted.Format(distanceBetweenVessels));
    }

    if (kerbalEva != null) {
      var protoCrewMember = hoveredVessel.GetVesselCrew()[0];
      sb.Add(KerbalTitleMsg.Format(hoveredVessel.GetName(),
                                   protoCrewMember.experienceTrait.Title,
                                   protoCrewMember.experienceLevel));
      sb.Add(KerbalEvaFuelMsg.Format(kerbalEva.Fuel));
    } else {
      if (hoveredVessel.vesselType == VesselType.Unknown) {
        // Unknown vessels are likely dropped parts or assemblies.
        if (hoveredVessel.parts.Count == 1) {
          sb.Add(SinglePartTitleMsg.Format(hoveredVessel.vesselName));
        } else {
          sb.Add(AssemblyTitleMsg.Format(hoveredVessel.vesselName));
        }
      } else {
        sb.Add(VesselTitleMsg.Format(hoveredVessel.vesselType, hoveredVessel.vesselName));
      }
      sb.Add(VesselMassMsg.Format(hoveredVessel.GetTotalMass()));
      sb.Add(hoveredVessel.IsControllable ? VesselIsControllableMsg : VesselIsNotControllableMsg);
      if (isKisStaticAttached.HasValue) {
        sb.Add(isKisStaticAttached.Value
            ? vesselIsAttachedToTheGroundMsg
            : vesselIsNotAttachedToTheGroundMsg);
      }
      foreach (var res in hoveredVessel.GetActiveResources()) {
        sb.Add(VesselResourceMsg.Format(res.info.name, res.amount / res.maxAmount));
      }
    }

    vesselInfoOverlay.text = string.Join("\n", sb.ToArray());
    vesselInfoOverlay.ShowAtCursor();
  }

  /// <summary>Verifies if KIS part is attached to the ground.</summary>
  /// <param name="part">Part to check.</param>
  /// <returns><c>null</c> if it's not a KIS part. Otherwise, either <c>true</c> or <c>false</c>.
  /// </returns>
  static bool? IsAttachedToGround(Part part) {
    var kisItemModule = part.GetComponent("ModuleKISItem");
    if (kisItemModule != null) {
      var staticAttachedField = kisItemModule.GetType().GetField("staticAttached");
      if (staticAttachedField != null) {
        return staticAttachedField.GetValue(kisItemModule).Equals(true);
      }
    }
    return null;
  }
  
  /// <summary>Highlights entire vessel with the specified color.</summary>
  /// <param name="vessel">A vessel to highlight.</param>
  /// <param name="color">A color to use for highlighting. If set to <c>null</c> then vessel
  /// highlighting will be cancelled.</param>
  static void SetVesselHighlight(Vessel vessel, Color? color) {
    if (vessel.isEVA) {
      // Kerbal "parts" are not usual parts. In spite of there is a highlighter on the model it
      // doesn't work for some reason. A highlighter added on the part's game object does the trick.
      var highlighter = vessel.rootPart.gameObject.GetComponent<Highlighter>()
          ?? vessel.rootPart.gameObject.AddComponent<Highlighter>();
      if (color.HasValue) {
        highlighter.ConstantOn(targetVesselHighlightColor);
      } else {
        highlighter.ConstantOff();
      }
    } else {
      // Each regular part has a hgighlighter, just use it.
      foreach (var part in vessel.parts) {
        if (color.HasValue) {
          part.highlighter.ConstantOn(targetVesselHighlightColor);
        } else {
          part.highlighter.ConstantOff();
        }
      }
    }
  }

  /// <summary>Sets camera mode if it differs from the requested.</summary>
  /// <param name="newMode">New camera mode.</param>
  static void SetCurrentCameraMode(FlightCamera.Modes newMode) {
    if (FlightCamera.fetch.mode != newMode) {
      FlightCamera.fetch.setMode(newMode);
    }
  }

  /// <summary>Checks if camera position can be preserved in the mode.</summary>
  /// <param name="mode">Mode to check.</param>
  /// <returns><c>true</c> if mode allows perserving asme camera position.</returns>
  static bool IsFreeCameraPositionMode(FlightCamera.Modes mode) {
    return mode == FlightCamera.Modes.AUTO  // It never chooses LOCKED.
           || mode == FlightCamera.Modes.FREE
           || mode == FlightCamera.Modes.ORBITAL
           || mode == FlightCamera.Modes.CHASE;
  }

  /// <summary>A coroutine to temporarily highlight a vessel.</summary>
  /// <param name="vessel">A vessel to highlight.</param>
  /// <param name="timeout">A duration to keep vessel highlighet.</param>
  /// <param name="color">A color to assign to the highlighter.</param>
  /// <returns><c>WaitForSeconds</c>.</returns>
  static IEnumerator TimedHighlightCoroutine(Vessel vessel, float timeout, Color color) {
    SetVesselHighlight(vessel, color);
    yield return new WaitForSeconds(timeout);
    SetVesselHighlight(vessel, null);
  }

  /// <summary>Preserves fixed position of camera, and moves its focus to the new vessel.</summary>
  /// <remarks>The vessel may move while the animation is done (e.g. on the orbit). To compensate
  /// this movement all camera calculations are done relative to the original vessel position at the
  /// time of the switch. And then a difference is added given the current vessel position.
  /// </remarks>
  /// <param name="target">A camera target. If it's changed the animation will abort.</param>
  /// <param name="oldInfo">Previous vessel info.</param>
  /// <param name="newInfo">New vessel info.</param>
  /// <returns><c>null</c> until animation is done or aborted.</returns>
  static IEnumerator AnimateCameraPivotCoroutine(
      Transform target, VesselInfo oldInfo, VesselInfo newInfo) {
    float startTime = Time.unscaledTime;
    float progress;
    do {
      // Calculate vessel movement compensation offset.
      var movementOffset = FlightGlobals.ActiveVessel.transform.position - newInfo.anchorPos;
      progress = (Time.unscaledTime - startTime) / cameraStabilizationAnimationDuration;
      var camera = FlightCamera.fetch;
      camera.GetPivot().position =
          movementOffset + Vector3.Lerp(oldInfo.cameraPivotPos, newInfo.cameraPivotPos, progress);
      var newCameraPos = movementOffset + oldInfo.cameraPos;
      camera.SetCamCoordsFromPosition(newCameraPos);
      camera.GetCameraTransform().position = newCameraPos;
      yield return null;
    } while (progress < 1.0f && FlightCamera.fetch.Target == target);
  }

  /// <summary>
  /// Preserves camera-to-target rotation and distance, and moves camera's focus to the new vessel. 
  /// </summary>
  /// <remarks>The vessel may move while the animation is done (e.g. on the orbit). To compensate
  /// this movement all camera calculations are done relative to the original vessel position at the
  /// time of the switch. And then a difference is added given the current vessel position.
  /// </remarks>
  /// <param name="target">A camera target. If it's changed the animation will abort.</param>
  /// <param name="oldInfo">Previous vessel info.</param>
  /// <param name="newInfo">New vessel info.</param>
  /// <returns></returns>
  static IEnumerator AnimateCameraPositionCoroutine(
      Transform target, VesselInfo oldInfo, VesselInfo newInfo) {
    float startTime = Time.unscaledTime;
    float progress;
    do {
      // Calculate vessel movement compensation offset.
      var movementOffset = FlightGlobals.ActiveVessel.transform.position - newInfo.anchorPos;
      progress = (Time.unscaledTime - startTime) / cameraStabilizationAnimationDuration;
      // Only animate the pivot position. The camera's position will be adjusted by the FlighCamera
      // code.
      FlightCamera.fetch.GetPivot().transform.position =
          movementOffset + Vector3.Lerp(oldInfo.cameraPivotPos, newInfo.cameraPivotPos, progress);
      yield return null;
    } while (progress < 1.0f && FlightCamera.fetch.Target == target);
  }
}

}

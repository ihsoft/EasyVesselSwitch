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
public sealed class Controller : MonoBehaviour {
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
  Message VesselIsControllable = "Is controllable: YES";
  Message VesselIsNotControllable = "Is controllable: NO";
  Message<CameraStabilization> CameraStabilizationModeChanged =
      "Camera stabilization mode changed to: {0}";
  Message<float> DistantVesselTargeted = "Vessel is too distant: {0:N0}m";
  // ===== END of localizable strings
  
  /// <summary>A mode of camera stabilization.</summary>
  enum CameraStabilization {
    /// <summary>No stabilization. Allow dfeault KSP behavior.</summary>
    None = 0,
    /// <summary>Keep camera at the same position and move focus to the new vessel.</summary>
    KeepPosition = 1,
    /// <summary>Keep the same camera distance and rotation as was on the old vessel.</summary>
    KeepDistanceAndRotation = 2
  }

  /// <summary>Vessel which is currently hovered.</summary>
  Vessel hoveredVessel;
  /// <summary>Camera position before vessel change.</summary>
  Vector3 previousCameraPosition;
  /// <summary>Camera focus position before vessel change.</summary>
  Vector3 previousPivotPosition;
  /// <summary>Tells if new camera needs to be adjused for close vessel switch.</summary>
  bool needFixForCloseVesselsSwitch;
  /// <summary>Tells if new camera needs to be adjused for distant vessel switch.</summary>
  bool needFixForDistantVesselsSwitch;
  /// <summary>Overaly window to show info about vessel under mouse cursor.</summary>
  HintOverlay vesselInfoOverlay;

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
      cameraStabilizationMode = cameraStabilizationMode == CameraStabilization.KeepPosition
          ? CameraStabilization.KeepDistanceAndRotation
          : CameraStabilization.KeepPosition;
      ScreenMessaging.ShowInfoScreenMessage(
          CameraStabilizationModeChanged.Format(cameraStabilizationMode));
    }

    // handle the switch.
    if (Mouse.HoveredPart && EventChecker.IsModifierCombinationPressed(switchModifier)) {
      SetHoveredVessel(Mouse.HoveredPart.vessel);
      if (Mouse.GetAllMouseButtonsDown() == switchMouseButton
          && hoveredVessel != null && hoveredVessel != FlightGlobals.ActiveVessel) {
        var vesselToSelect = hoveredVessel;
        SetHoveredVessel(null);
        if (!FlightGlobals.SetActiveVessel(vesselToSelect)) {
          // Something is blocking. KSP should have reported it on the screen.
          Logger.logWarning("Vessel switch aborted by FlightGlobals");
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
    if (fromVessel && fromVessel == FlightGlobals.ActiveVessel
        && cameraStabilizationMode != CameraStabilization.None) {
      previousCameraPosition = FlightCamera.fetch.GetCameraTransform().position;
      previousPivotPosition = FlightCamera.fetch.GetPivot().transform.position;
      float unusedVesselDistance;
      if (!IsDistantVessel(toVessel, out unusedVesselDistance)) {
        needFixForCloseVesselsSwitch = true;
      } else {
        needFixForDistantVesselsSwitch = true;
      }
    }
  }

  /// <summary>GameEvents callback.</summary>
  /// <remarks>Highlights newly selected vessel and handles camear stabilization.</remarks>
  /// <param name="newVessel">A new active vessel.</param>  
  void OnVesselChange(Vessel newVessel) {
    StartCoroutine(TimedHighlightCoroutine(
        newVessel, newVesselHighlightTimeout, targetVesselHighlightColor));
    if (needFixForCloseVesselsSwitch) {
      needFixForCloseVesselsSwitch = false;
      StabilizeCamera();
    } else if (needFixForDistantVesselsSwitch) {
      needFixForDistantVesselsSwitch = false;
      SetCameraOrientation();
    }
  }

  /// <summary>Aligns new camera FOV when switching to a distant vessel.</summary>
  /// <remarks>When usual stabilization modes are not feasible position new camera so what that the
  /// old vessel is in the field of view as well as the new vessel.</remarks>
  void SetCameraOrientation() {
    var camera = FlightCamera.fetch;
    var vessel = FlightGlobals.ActiveVessel;
    var oldCameraDistance = Vector3.Distance(previousCameraPosition, previousPivotPosition);
    var fromOldToNewDir = previousPivotPosition - camera.GetPivot().position;
    Vector3 newCameraPos;

    if (vessel.Landed) {
      // When vessel is landed the new camera position may end up under the surface. To work it
      // around keep the same angle between line of sight and vessel's up axis as it was with the
      // previous vessel.
      var oldPivotUp = FlightGlobals.getUpAxis(previousPivotPosition);
      var oldCameraDir = previousPivotPosition - previousCameraPosition;
      var angle = Vector3.Angle(oldPivotUp, oldCameraDir);
      var newPivotPos = camera.GetPivot().transform.position;
      var newPivotUp = FlightGlobals.getUpAxis(newPivotPos);
      var rot = Quaternion.AngleAxis(angle, Vector3.Cross(newPivotUp, fromOldToNewDir));
      newCameraPos = newPivotPos - rot * (newPivotUp * oldCameraDistance);
    } else {
      // In space just put camear on the opposite size and direct it to the old vessel. This way
      // both old and new vessela will be in camera's field of view.
      newCameraPos =
          camera.GetPivot().transform.position - fromOldToNewDir.normalized * oldCameraDistance;
    }
    
    camera.SetCamCoordsFromPosition(newCameraPos);
    camera.GetCameraTransform().position = newCameraPos;
  }

  /// <summary>Prevents random jumping of the new vessel's camera.</summary>
  /// <remarks>Depending on the mode either preserves old camera position and changes focus of view
  /// to the new vessel or keeps vessel-to-camera rotation.</remarks>
  void StabilizeCamera() {
    var camera = FlightCamera.fetch;
    var cameraTransfrom = FlightCamera.fetch.GetCameraTransform();

    if (cameraStabilizationMode == CameraStabilization.KeepDistanceAndRotation) {
      // Restore old pivot and camera position to have original rotations applied to the camera.
      // Then, either animate the pivot or set it instantly. KSP code will move the camera
      // following the pivot without changing its rotation or distance.
      Logger.logInfo("Fix camera position while keeping distance and orientation");
      var tgtPivotPosition = camera.GetPivot().transform.position;
      camera.GetPivot().transform.position = previousPivotPosition;
      camera.SetCamCoordsFromPosition(previousCameraPosition);
      if (cameraStabilizationAnimationDuration < float.Epsilon) {
        camera.GetPivot().transform.position = tgtPivotPosition;
      } else {
        StartCoroutine(AnimateCameraPivotPositionCoroutine(
            camera.Target, camera.GetPivot().position, tgtPivotPosition));
      }
    }

    if (cameraStabilizationMode == CameraStabilization.KeepPosition) {
      // Restore old camera position and recalculate camera orientation. KSP code always orient
      // camera on the pivot. When animation is disabled then just setting the p[osition is enough
      // since the pivot is set to the new vessel. When anuimation is enabled we animate the pivot
      // and reset the camera position to have only direction recalculated.
      Logger.logInfo("Fix camera focus while keeping its position");
      if (cameraStabilizationAnimationDuration < float.Epsilon) {
        camera.SetCamCoordsFromPosition(previousCameraPosition);
        camera.GetCameraTransform().position = previousCameraPosition;
      } else {
        StartCoroutine(AnimateCameraFocusCoroutine(
            camera.Target, previousCameraPosition,
            previousPivotPosition, camera.GetPivot().transform.position));
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
      }
      if (vessel != null) {
        SetVesselHighlight(vessel, targetVesselHighlightColor);
      }
      hoveredVessel = vessel;
    }
  }

  /// <summary>Displays brief information about the vessel under mouse cursor.</summary>
  /// <remarks><see cref="hoveredVessel"/> must not be <c>null</c>.</remarks>
  void ShowHoveredVesselInfo() {
    var vessel = hoveredVessel;
    var sb = new List<string>();
    sb.Add(vessel == FlightGlobals.ActiveVessel ? CurrentVesselMsg : SwitchToMsg);
    sb.Add("");

    // Give a hint when distance is too long.
    float distanceBetweenVessels;
    if (IsDistantVessel(vessel, out distanceBetweenVessels)) {
      sb.Add(DistantVesselTargeted.Format(distanceBetweenVessels));
    }

    if (vessel.isEVA) {
      var protoCrewMember = vessel.GetVesselCrew()[0];
      sb.Add(KerbalTitleMsg.Format(vessel.GetName(),
                                   protoCrewMember.experienceTrait.Title,
                                   protoCrewMember.experienceLevel));
      var kerbalEva = vessel.GetComponent<KerbalEVA>();
      sb.Add(KerbalEvaFuelMsg.Format(kerbalEva.Fuel));
    } else {
      if (vessel.vesselType == VesselType.Unknown) {
        // Unknown vessels are likely dropped parts or assemblies.
        if (vessel.parts.Count == 1) {
          sb.Add(SinglePartTitleMsg.Format(vessel.vesselName));
        } else {
          sb.Add(AssemblyTitleMsg.Format(vessel.vesselName));
        }
      } else {
        sb.Add(VesselTitleMsg.Format(vessel.vesselType, vessel.vesselName));
      }
      sb.Add(VesselMassMsg.Format(vessel.GetTotalMass()));
      sb.Add(vessel.IsControllable ? VesselIsControllable : VesselIsNotControllable);
      foreach (var res in vessel.GetActiveResources()) {
        sb.Add(VesselResourceMsg.Format(res.info.name, res.amount / res.maxAmount));
      }
    }

    vesselInfoOverlay.text = string.Join("\n", sb.ToArray());
    vesselInfoOverlay.ShowAtCursor();
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
  /// <param name="target">A camera target. If it's changed the animation will abort.</param>
  /// <param name="cameraPosition">A position of the camera to preserve.</param>
  /// <param name="srcPivotPosition">A starting focus position.</param>
  /// <param name="trgPivotPosition">An ending focus position.</param>
  /// <returns><c>null</c> until animation is done or aborted.</returns>
  static IEnumerator AnimateCameraFocusCoroutine(
      Transform target, Vector3 cameraPosition,
      Vector3 srcPivotPosition, Vector3 trgPivotPosition) {
    float startTime = Time.unscaledTime;
    float progress;
    do {
      progress = (Time.unscaledTime - startTime) / cameraStabilizationAnimationDuration;
      var camera = FlightCamera.fetch;
      camera.GetPivot().transform.position =
          Vector3.Lerp(srcPivotPosition, trgPivotPosition, progress);
      camera.SetCamCoordsFromPosition(cameraPosition);
      camera.GetCameraTransform().position = cameraPosition;
      yield return null;
    } while (progress < 1.0f && FlightCamera.fetch.Target == target);
  }

  /// <summary>
  /// Preserves camera-to-target rotation and distance, and moves camera's focus to the new vessel. 
  /// </summary>
  /// <param name="target">A camera target. If it's changed the animation will abort.</param>
  /// <param name="srcPivotPosition">A starting focus position.</param>
  /// <param name="trgPivotPosition">An ending focus position.</param>
  /// <returns></returns>
  static IEnumerator AnimateCameraPivotPositionCoroutine(
      Transform target, Vector3 srcPivotPosition, Vector3 trgPivotPosition) {
    float startTime = Time.unscaledTime;
    float progress;
    do {
      progress = (Time.unscaledTime - startTime) / cameraStabilizationAnimationDuration;
      FlightCamera.fetch.GetPivot().transform.position =
          Vector3.Lerp(srcPivotPosition, trgPivotPosition, progress);
      yield return null;
    } while (progress < 1.0f && FlightCamera.fetch.Target == target);
  }

  /// <summary>
  /// Tells if vessel is located too far from the curernt one for the camera stabilization.
  /// </summary>
  /// <remarks>Camera stabilization when switching ot a very distant vessel doesn't make sense.
  /// Instead, player would like to get close to the vessel. Though, some UI improvement still can
  /// be made if new camera orientation set so what the old vessel is in the FOV.</remarks>
  /// <param name="vessel">A vessel to check distance to.</param>
  /// <param name="distance">[out] An actual distance.</param>
  /// <returns><c>true</c> if distance is too long. Maximum value is set via
  /// <see cref="maxVesselDistance"/> and can be overwritten via settings file.</returns>
  static bool IsDistantVessel(Vessel vessel, out float distance) {
    distance =
        (FlightGlobals.ActiveVessel.transform.position - vessel.transform.position).magnitude;
    return distance > maxVesselDistance;
  }
}

}

// KSP Easy Vessel Switch
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.InputUtils;
using KSPDev.ProcessingUtils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EasyVesselSwitch {

/// <summary>Main mod's class that monitors use interactions.</summary>
// Next localization ID: #evsLOC_00025.
[KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
[PersistentFieldsFile("EasyVesselSwitch/Plugins/PluginData/settings.cfg", "")]
sealed class Controller : MonoBehaviour {
  #region Persistent fields
  /// <summary>Key to activate vessel select mode.</summary>
  [PersistentField("UI/vesselSwitchKey")]
  KeyboardInputSwitch vesselSwitchKey = new KeyboardInputSwitch(KeyCode.LeftAlt);
  
  /// <summary>Key to activate focused part select mode.</summary>
  [PersistentField("UI/partFocusSwitchKey")]
  KeyboardInputSwitch partFocusSwitchKey = new KeyboardInputSwitch(KeyCode.O);

  /// <summary>Mouse button to trigger part/vessel select.</summary>
  [PersistentField("UI/switchMouseButton")]
  Mouse.Buttons switchMouseButton = Mouse.Buttons.Left;

  /// <summary>Highlight color for the vessel in vessel select mode.</summary>
  [PersistentField("UI/targetVesselHighlightColor")]
  Color targetVesselHighlightColor = Color.yellow;

  /// <summary>Fade out tmeout for the current vessel selection.</summary>
  [PersistentField("UI/newVesselHighlightTimeout")]
  float newVesselHighlightTimeout = 0.5f;

  /// <summary>Animation duration for transitioning camera to the new position/rotation.</summary>
  [PersistentField("CameraStabilization/animationDuration")]
  float cameraStabilizationAnimationDuration = 1f;

  /// <summary>Default mode of camera stabilization. It's not persisted when changed.</summary>
  [PersistentField("CameraStabilization/mode")]
  CameraStabilization cameraStabilizationMode = CameraStabilization.KeepDistanceAndRotation;

  /// <summary>
  /// Maximum distance to use normal camera stabilization. For greater distances camera is simply
  /// positioned on the vector that connects new target and the old target so what both targets are
  /// in the focus.
  /// </summary>
  [PersistentField("CameraStabilization/maxVesselDistance")]
  int maxVesselDistance = 100;  // Meters.

  /// <summary>Key which swicthes camera stabilization modes.</summary>
  [PersistentField("CameraStabilization/switchModeKey")]
  KeyboardInputSwitch switchStabilizationModeKey = new KeyboardInputSwitch(KeyCode.F7);

  /// <summary>
  /// Size of the font in the overlay that displays info on the hovered vessel/part.
  /// </summary>
  [PersistentField("InfoOverlay/fontSize")]
  int infoOverlayFontSize = 10;

  /// <summary>
  /// Background color of the overlay that displays info on the hovered vessel/part.
  /// </summary>
  [PersistentField("InfoOverlay/backgroundColor")]
  Color infoOverlayBackgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.5f);

  /// <summary>
  /// Foreground color of the text in overlay that displays info on the hovered vessel/part.
  /// </summary>
  [PersistentField("InfoOverlay/textColor")]
  Color infoOverlayTextColor = Color.white;

  /// <summary>
  /// Padding between text and the border in overlay that displays info on the hovered vessel/part.
  /// </summary>
  [PersistentField("InfoOverlay/hintPadding")]
  int infoOverlayHintPadding = 6;
  #endregion

  #region Localizable strings
  static readonly Message<string> SwitchToMsg = new Message<string>(
      "#evsLOC_00000",
      defaultTemplate: "Click [<<1>>] to switch to:",
      description: "The message to display in the title of the hover menu when the switch mode is"
      + " activated and the vessel, being hovered, is NOT the currently selected vessel."
      + " \nArgument <<1>> is the localized name of the mouse button that's bound to the action.");

  static readonly Message CurrentVesselMsg = new Message(
      "#evsLOC_00001",
      defaultTemplate: "Currently selected:",
      description: "The message to display in the title of the hover menu when the switch mode is"
      + " activated and the vessel, being hovered, IS the currently selected vessel.");

  static readonly Message<string, string> VesselTitleMsg = new Message<string, string>(
      "#evsLOC_00002",
      defaultTemplate: "<<1>>: <<2>>",
      description: "The pattern to construct a human friendly vessel name."
      + "\nArgument <<1>> is a localized vessel type (e.g. \"station\")."
      + "\nArgument <<2>> is the vessel's name.");

  static readonly Message<string, string, int> KerbalTitleMsg = new Message<string, string, int>(
      "#evsLOC_00003",
      defaultTemplate: "<<1>> (<<2>>-<<3>>)",
      description: "The pattern to construct a human friendly kerbal's name."
      + "\nArgument <<1>> is a full name of the kerbal."
      + "\nArgument <<2>> is a localized name of the kerbal's main skill (e.g. \"pilot\")."
      + "\nArgument <<3>> is the current level of the kerbal.");

  static readonly Message<MassType> VesselMassMsg = new Message<MassType>(
      "#evsLOC_00004",
      defaultTemplate: "Total mass: <<1>>",
      description: "The message to display in the hover menu to present the vessel's total mass."
      + "\nArgument <<1>> is the total mass. Format: MassType.");

  static readonly Message<string> SinglePartTitleMsg = new Message<string>(
      "#evsLOC_00005",
      defaultTemplate: "Part: <<1>>",
      description: "The pattern to construct a human friendly vessel name when the type is Unknown,"
      + " and the vessel has only one part."
      + "\nArgument <<1>> is the vessel's name.");

  static readonly Message<string> AssemblyTitleMsg = new Message<string>(
      "#evsLOC_00006",
      defaultTemplate: "Assembly: <<1>>",
      description: "The pattern to construct a human friendly vessel name when the type is Unknown,"
      + " and the vessel has multiple parts."
      + "\nArgument <<1>> is the vessel's name.");

  static readonly Message<CompactNumberType> KerbalEvaFuelMsg = new Message<CompactNumberType>(
      "#evsLOC_00007",
      defaultTemplate: "EVA propellant: <<1>>",
      description: "The message to display in the hover menu to present the kerbal's EVA fuel"
      + " reserve."
      + "\nArgument <<1>> is the total amount of the fuel. Format: CompactNumberType.");

  static readonly Message VesselIsControllableMsg = new Message(
      "#evsLOC_00008",
      defaultTemplate: "Vessel IS controllable",
      description: "The message to display in the hover menu to tell that the vessel CAN be"
      + " operated by the player.");

  static readonly Message VesselIsNotControllableMsg = new Message(
      "#evsLOC_00009",
      defaultTemplate: "Vessel is NOT controllable",
      description: "The message to display in the hover menu to tell that the vessel CANNOT be"
      + " operated by the player.");

  #region CameraStabilization enum values
  static readonly Message CameraStabilizationMsg_None = new Message(
      "#evsLOC_00010",
      defaultTemplate: "NONE",
      description: "The string that identifies a no camera stabilization mode. It's displayed when"
      + " the mode is changed.");

  static readonly Message CameraStabilizationMsg_KeepPosition = new Message(
      "#evsLOC_00011",
      defaultTemplate: "Keep camera POSITION",
      description: "The string that identifies a stabilization mode when the camera position is"
      + " constant, and it only changes the focus to the newly selected vessel/part. It's displayed"
      + " when the mode is changed.");

  static readonly Message CameraStabilizationMsg_KeepDistanceAndRotation = new Message(
      "#evsLOC_00012",
      defaultTemplate: "Keep camera DISTANCE",
      description: "The string that identifies a stabilization mode when the distance from the"
      + " camera to the newly selected vessel/part is kept the same as it was on the former"
      + " vessel/part. It's displayed when the mode is changed.");
  #endregion
  
  static readonly MessageLookup<CameraStabilization> CameraStabilizationModeLookup =
      new MessageLookup<CameraStabilization>(new Dictionary<CameraStabilization, Message>() {
          {CameraStabilization.None, CameraStabilizationMsg_None},
          {CameraStabilization.KeepPosition, CameraStabilizationMsg_KeepPosition},
          {CameraStabilization.KeepDistanceAndRotation,
           CameraStabilizationMsg_KeepDistanceAndRotation},
      });

  static readonly Message<string> CameraStabilizationModeChangedMsg = new Message<string>(
      "#evsLOC_00013",
      defaultTemplate: "Stabilization mode: <<1>>",
      description: "The mesage to present when the EVS mode has changed or updated."
      + "\nArgumnent <<1>> is a localized description of the mode.");

  static readonly Message<DistanceType> DistantVesselTargetedMsg = new Message<DistanceType>(
      "#evsLOC_00014",
      defaultTemplate: "Vessel is too distant: <<1>>",
      description: "The message to display in the hover menu when the vessel, being hoverd, is too"
      + " far to switch to it."
      + "\nArgument <<1>> is the vessel's name.");

  static readonly Message VesselIsAttachedToTheGroundMsg = new Message(
      "#evsLOC_00015",
      defaultTemplate: "Vessel IS attached to the ground",
      description: "The message to display in the hover menu to tell that the vessel IS attached"
      + " to the ground via the KIS mod part(s).");

  static readonly Message VesselIsNotAttachedToTheGroundMsg = new Message(
      "#evsLOC_00016",
      defaultTemplate: "Vessel is NOT attached to the ground",
      description: "The message to display in the hover menu to tell that the vessel is NOT"
      + "attached to the ground via the KIS mod part(s).");

  static readonly Message CurrenPartInFocusStatusMsg = new Message(
      "#evsLOC_00017",
      defaultTemplate: "This part is currently in focus.",
      description: "The status message in the hover menu when the player enables the part's focus"
      + " change mode, but the currently hovered part is already has the focus.");

  static readonly Message NotCurrentVesselPartStatusMsg = new Message(
      "#evsLOC_00018",
      defaultTemplate: "This part does NOT belong to the current vessel!",
      description: "The message to display when the player attempts to set the focus to a part"
      + " which doesn't belong to the current vessel.");

  #region Mouse.Buttons enum localiztion (not all values)
  static readonly Message MouseButtonMsg_Left = new Message(
      "#autoLOC_223620",  // Re-use the stock localization.
      defaultTemplate: "LMB",
      description: "The string that identifies the LEFT mouse button.");

  static readonly Message MouseButtonMsg_Right = new Message(
      "#autoLOC_223621",  // Re-use the stock localization.
      defaultTemplate: "RMB",
      description: "The string that identifies the RIGHT mouse button.");

  static readonly Message MouseButtonMsg_Middle = new Message(
      "#evsLOC_00019",
      defaultTemplate: "Middle mouse button",
      description: "The string that identifies the MIDDLE mouse button.");
  #endregion

  static readonly MessageLookup<Mouse.Buttons> MouseButtonLookup =
      new MessageLookup<Mouse.Buttons>(new Dictionary<Mouse.Buttons, Message>() {
          {Mouse.Buttons.Left, MouseButtonMsg_Left},
          {Mouse.Buttons.Right, MouseButtonMsg_Right},
          {Mouse.Buttons.Middle, MouseButtonMsg_Middle},
      });

  static readonly Message<string> SetFocusToCurrentPartHintMsg = new Message<string>(
      "#evsLOC_00020",
      defaultTemplate: "Click [<<1>>] to set the focus on this part.",
      description: "The hint message to display in the hover menu to tell what to do to set the"
      + " focus on the hoverd part."
      + "\nArgument <<1>> is the localized name of the mosue button event (e.g. \"LMB\").");

  static readonly Message<string> ResetFocusToCurrentVesselHintMsg = new Message<string>(
      "#evsLOC_00021",
      defaultTemplate: "Click [<<1>>] to reset the focus to the current vessel.",
      description: "The hint message to display in the hover menu to tell what to do to reset the"
      + " part's focus back to the vessel focus mode."
      + "\nArgument <<1>> is the localized name of the mosue button event (e.g. \"LMB\").");

  static readonly Message AnotherPartFocusHintMsg = new Message(
      "#evsLOC_00022",
      defaultTemplate: "Hover over another part to change the focus.",
      description: "The hint message to display in the hover menu when the part's focus mode is"
      + " enabled, but the cirrently hovered part is already in focus.");

  static readonly Message SomePartFocusHintMsg = new Message(
      "#evsLOC_00023",
      defaultTemplate: "Hover over a part to set the focus on it.",
      description: "The hint message to display in the hover menu to tell what to do to set the"
      + " focus on the currently hovered part.");

  static readonly Message ResetFocusHintMsg = new Message(
      "#evsLOC_00024",
      defaultTemplate: "Point to the outer space to reset focus.",
      description: "The hint message to display in the hover menu to tell what to do to reset the"
      + " focus to the default vessel focus mode.");
  #endregion

  /// <summary>A mode of camera stabilization.</summary>
  enum CameraStabilization {
    /// <summary>No stabilization. Allow dfeault KSP behavior.</summary>
    None = 0,
    /// <summary>Keep camera at the same position and move focus to the new vessel.</summary>
    KeepPosition = 1,
    /// <summary>Keep the same camera distance and rotation as was on the old vessel.</summary>
    KeepDistanceAndRotation = 2,
  }

  /// <summary>Type of switch event pending.</summary>
  enum SwitchEvent {
    /// <summary>No event pending.</summary>
    Idle,
    /// <summary>Current vessel has changed due to EVS action or keyboard shortkey.</summary>
    VesselSwitched,
    /// <summary>Either active vessel has docked to a station or another vessel has docked to the
    /// active station.</summary>
    VesselDocked,
  }

  /// <summary>Vessel which is currently hovered.</summary>
  Vessel hoveredVessel;
  /// <summary>Overaly window to show info about vessel under the mouse cursor.</summary>
  HintOverlay mouseInfoOverlay;
  /// <summary>Old vessel context.</summary>
  VesselInfo oldInfo;
  /// <summary>New vessel context.</summary>
  VesselInfo newInfo;
  /// <summary>Defines if currently selected vessel was a result of EVS mouse click event.</summary>
  bool evsSwitchAction;
  /// <summary>Event to handle in the controller.</summary>
  /// <remarks>
  /// Controller code reacts to anything different from <see cref="SwitchEvent.Idle"/>. Once the
  /// event is handled it's reset to the default.
  /// </remarks>
  SwitchEvent state = SwitchEvent.Idle;
  /// <summary>Specifies if hovered vessel is attached to the ground.</summary>
  /// <remarks><c>null</c> means no static attachable parts found.</remarks>
  bool? isKisStaticAttached;
  /// <summary>If hovered vessel is a kerbal then this will be the component.</summary>
  /// <remarks><c>null</c> means hovered vessel is not a kerbal.</remarks>
  KerbalEVA kerbalEva;
  /// <summary>Part that is currently hovered when EVS mode is enabled.</summary>
  /// <remarks>Used to determine part's focus change.</remarks>
  Part lastHoveredPart;

  /// <summary>Overridden from MonoBehaviour.</summary>
  /// <remarks>Registers listeners, reads configuration and creates global UI objects.</remarks>
  void Awake() {
    GameEvents.onVesselSwitching.Add(OnVesselSwitch);
    GameEvents.onVesselChange.Add(OnVesselChange);
    GameEvents.onPartCouple.Add(OnPartCouple);
    ConfigAccessor.ReadFieldsInType(typeof(Controller), this);
    mouseInfoOverlay = new HintOverlay(
        infoOverlayFontSize, infoOverlayHintPadding, infoOverlayTextColor, infoOverlayBackgroundColor);

    // Drop vessel selection when main modifier is released.
    vesselSwitchKey.OnRelease += delegate{ SetHoveredVessel(null); };
    // Iterate thru stabilization modes.
    switchStabilizationModeKey.OnClick += SelectNextStabilizationMode;
  }

  /// <summary>Overridden from MonoBehaviour.</summary>
  void OnDestroy() {
    GameEvents.onVesselSwitching.Remove(OnVesselSwitch);
    GameEvents.onVesselChange.Remove(OnVesselChange);
    GameEvents.onPartCouple.Remove(OnPartCouple);
  }

  /// <summary>Overridden from MonoBehaviour.</summary>
  /// <remarks>Persents hovered vessel info.</remarks>
  void OnGUI() {
    if (hoveredVessel != null) {
      ShowHoveredVesselInfo();
    }
    if (partFocusSwitchKey.isHold) {
      ShowHoveredPartInfo();
    }
  }

  /// <summary>Overridden from MonoBehaviour.</summary>
  /// <remarks>Tracks keys and mouse moveement.</remarks>
  void Update() {
    switchStabilizationModeKey.Update();
    if (vesselSwitchKey.Update()) {
      HandleVesselSelection();
    }
    if (partFocusSwitchKey.Update()) {
      HanleCameraFocusSelection();
    }

    // Core KSP logic highlights hovered parts. Once focus is lost so does the highlight state on
    // the part. Here we detect changing focus in scope of the current vessel, and restore EVS
    // highlighting when mouse focus moves out.
    if (hoveredVessel == null) {
      lastHoveredPart = null;
    } else if (lastHoveredPart != Mouse.HoveredPart) {
      if (lastHoveredPart != null && lastHoveredPart.vessel == hoveredVessel) {
        // Let game core to disable highlighter and then restore it.
        var restoreHighlightPart = lastHoveredPart; // Make a cope for the delayed call.
        AsyncCall.CallOnEndOfFrame(
            this, () => restoreHighlightPart.highlighter.ConstantOn(targetVesselHighlightColor));
      }
      lastHoveredPart = Mouse.HoveredPart;
    }
  }

  /// <summary>Overridden from MonoBehaviour.</summary>
  /// <remarks>
  /// Updates camera and vessels highlighting when two vessels docked.
  /// <para>
  /// EVS stabilzation mode is not supported. Camera position is updated via normal KSP behavior:
  /// keep distance and rotation while slowly moving focus to the new center of mass.
  /// </para>
  /// </remarks>
  void LateUpdate() {
    if (state == SwitchEvent.VesselDocked) {
      state = SwitchEvent.Idle;
      Debug.Log("Setting camera pivot to the new CoM.");
      var camera = FlightCamera.fetch;
      camera.GetPivot().position = oldInfo.cameraPivotPos;
      camera.SetCamCoordsFromPosition(oldInfo.cameraPos);
      camera.GetCameraTransform().position = oldInfo.cameraPos;

      StartCoroutine(TimedHighlightCoroutine(
          FlightGlobals.ActiveVessel, newVesselHighlightTimeout, targetVesselHighlightColor,
          isBeingDocked: true));
    }
  }

  /// <summary>GameEvents callback.</summary>
  /// <remarks>
  /// Detects vessel docking events.
  /// <para>
  /// Parts coupling is not a strightforward event. Depending on what has docked to what there may
  /// or may not be a vessel switch event sent. To be on a safe side just disable switch event
  /// handling in such case and fix camera in <c>LastUpdate</c>.
  /// </para>
  /// </remarks>
  void OnPartCouple(GameEvents.FromToAction<Part, Part> action) {
    // Only do camera fix if either the source or the destination is an active vessel. 
    if (action.from.vessel.isActiveVessel) {
      state = SwitchEvent.VesselDocked;
      Debug.Log("Active vessel docked to a station. Waiting for LateUpdate...");
      oldInfo = new VesselInfo(action.from.vessel, FlightCamera.fetch);
    } else if (action.to.vessel.isActiveVessel) {
      state = SwitchEvent.VesselDocked;
      Debug.Log("Something has docked to the active vessel. Waiting for LateUpdate...");
      oldInfo = new VesselInfo(action.to.vessel, FlightCamera.fetch);
    }
  }

  /// <summary>GameEvents callback.</summary>
  /// <remarks>
  /// Detects vessel switch and remembers old camera settings if switching from the currently active
  /// vessel.
  /// </remarks>
  /// <param name="fromVessel">A vessel prior the switch.</param>
  /// <param name="toVessel">A new active vessel.</param>  
  void OnVesselSwitch(Vessel fromVessel, Vessel toVessel) {
    if (state == SwitchEvent.Idle && cameraStabilizationMode != CameraStabilization.None
        && fromVessel != null && fromVessel.isActiveVessel) {
      state = SwitchEvent.VesselSwitched;
      Debug.LogFormat(
          "Detected switch from {0} to {1}. Request camera stabilization.", fromVessel, toVessel);
      newInfo = new VesselInfo(toVessel);
      oldInfo = new VesselInfo(fromVessel, FlightCamera.fetch);
    }
  }

  /// <summary>GameEvents callback.</summary>
  /// <remarks>Highlights newly selected vessel and handles camera stabilization.</remarks>
  /// <param name="vessel">A new active vessel.</param>
  void OnVesselChange(Vessel vessel) {
    // Temporarily highlight the new vessel.
    if (state == SwitchEvent.VesselSwitched || state == SwitchEvent.Idle) {
      StartCoroutine(TimedHighlightCoroutine(
          vessel, newVesselHighlightTimeout, targetVesselHighlightColor));
    }

    if (state != SwitchEvent.VesselSwitched) {
      return;
    }
    state = SwitchEvent.Idle;

    var camera = FlightCamera.fetch;
    newInfo.UpdateCameraFrom(camera);
    // Camera position cannot be transitioned between any modes. Some modes (e.g. LOCKED) don't
    // allow the camera to be placed at any place. Don't do camera stablization or
    // aligning for such modes. In the modes that allow free camera position the transformations
    // can be very different so, just copy source mode into the target vessel.
    // TODO(ihsoft): Find a way to do the translation between different modes.
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
    evsSwitchAction = false;
  }

  /// <summary>Handles vessel selection logic.</summary>
  void HandleVesselSelection() {
    // Highlight focused vessel.
    if (Mouse.HoveredPart) {
      SetHoveredVessel(Mouse.HoveredPart.vessel);
    } else {
      SetHoveredVessel(null);  // Cancel highlight.
    }

    // Select vessel if clicked.
    if (Mouse.GetAllMouseButtonsDown() == switchMouseButton
        && hoveredVessel != null && !hoveredVessel.isActiveVessel) {
      if (hoveredVessel.DiscoveryInfo.Level != DiscoveryLevels.Owned) {
        // Cannot switch to unowned vessel. Invoke standard "soft" switch to have error message
        // triggered.
        FlightGlobals.SetActiveVessel(hoveredVessel);
      } else {
        // Use forced version since "soft" switch blocks on many normal situations (e.g. "on
        // ladder" or "in atmosphere").
        var vesselToSelect = hoveredVessel;  // Save hovered vessel as it'll be reset on blur. 
        SetHoveredVessel(null);
        evsSwitchAction = true;
        FlightGlobals.ForceSetActiveVessel(vesselToSelect);
      }
    }
  }

  /// <summary>Handles camera focus selection logic.</summary>
  void HanleCameraFocusSelection() {
    var camera = FlightCamera.fetch;
    var targetPart = Mouse.HoveredPart;
    var targetTransform = targetPart != null ? targetPart.transform : null;
    if (Mouse.GetAllMouseButtonsDown() == switchMouseButton && targetTransform != camera.Target) {
      if (targetPart != null) {
        oldInfo = VesselInfo.CaptureCurrentState();
        if (targetPart.vessel == FlightGlobals.ActiveVessel
            && targetPart.vessel.parts.Count == 1) {
          // When moving focus back to a single part vessel reset to the vessel mode.
          camera.TargetActiveVessel();
        } else {
          camera.SetTargetPart(Mouse.HoveredPart);
        }
        newInfo = VesselInfo.CaptureCurrentState();
        StabilizeCamera();
      } else {
        oldInfo = VesselInfo.CaptureCurrentState();
        camera.TargetActiveVessel();
        newInfo = VesselInfo.CaptureCurrentState();
        StabilizeCamera();
      }
    }
  }
  
  /// <summary>Aligns new camera FOV when switching to a distant vessel.</summary>
  /// <remarks>
  /// If usual stabilization modes are not feasible then position new camera so what that old and
  /// new vessels are on the same line of view.
  /// </remarks>
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
  /// <remarks>
  /// Depending on the mode either preserves old camera position and changes focus of view to the
  /// new vessel or keeps vessel-to-camera rotation.
  /// </remarks>
  void StabilizeCamera() {
    var camera = FlightCamera.fetch;

    if (cameraStabilizationMode == CameraStabilization.KeepDistanceAndRotation) {
      // Restore old pivot and camera position to have original rotations applied to the camera.
      // Then, either animate the pivot or set it instantly. KSP code will move the camera
      // following the pivot without changing its rotation or distance.
      Debug.Log("Fix camera position while keeping distance and orientation");
      camera.GetPivot().position = oldInfo.cameraPivotPos;
      camera.SetCamCoordsFromPosition(oldInfo.cameraPos);
      if (cameraStabilizationAnimationDuration < float.Epsilon) {
        camera.GetPivot().position = newInfo.cameraPivotPos;
      } else {
        StartCoroutine(AnimateCameraPositionCoroutine(
            camera.Target, oldInfo, newInfo, cameraStabilizationAnimationDuration));
      }
    }

    if (cameraStabilizationMode == CameraStabilization.KeepPosition) {
      // Restore old camera position and recalculate camera orientation. KSP code always orient
      // camera on the pivot. When animation is disabled then just setting the position is enough
      // since the pivot is set to the new vessel. When animation is enabled we animate the pivot
      // and reset the camera position to have only direction recalculated.
      Debug.Log("Fix camera focus while keeping its position");
      if (cameraStabilizationAnimationDuration < float.Epsilon) {
        camera.SetCamCoordsFromPosition(oldInfo.cameraPos);
        camera.GetCameraTransform().position = oldInfo.cameraPos;
      } else {
        StartCoroutine(AnimateCameraPivotCoroutine(
            camera.Target, oldInfo, newInfo, cameraStabilizationAnimationDuration));
      }
    }
  }

  /// <summary>Sets vessel which is currently has mouse focus.</summary>
  /// <remarks>
  /// Current vessel is highlighted with the configured color. It's OK to call this method multiple
  /// time, nothing will be done if vessel is already highlighted.
  /// </remarks>
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

  /// <summary>Displays info and hint when in camera focus mode.</summary>
  /// <remarks>It's called every frame so, don't put heavy code here.</remarks>
  void ShowHoveredPartInfo() {
    var sb = new List<string>();
    var camera = FlightCamera.fetch;
    var trgPart = Mouse.HoveredPart;
    if (trgPart != null) {
      if (camera.Target == trgPart.transform) {
        sb.Add(CurrenPartInFocusStatusMsg);
        sb.Add(AnotherPartFocusHintMsg);
      } else {
        sb.Add(SetFocusToCurrentPartHintMsg.Format(
            MouseButtonLookup.Lookup(switchMouseButton)));
      }
      if (camera.targetMode == FlightCamera.TargetMode.Part) {
        sb.Add(ResetFocusHintMsg);
      }
      if (trgPart.vessel != FlightGlobals.ActiveVessel) {
        sb.Add("");
        sb.Add(NotCurrentVesselPartStatusMsg);
      }
    } else {
      if (camera.targetMode == FlightCamera.TargetMode.Part) {
        sb.Add(ResetFocusToCurrentVesselHintMsg.Format(
            MouseButtonLookup.Lookup(switchMouseButton)));
      } else {
        sb.Add(SomePartFocusHintMsg);
      }
    }
    mouseInfoOverlay.text = string.Join("\n", sb.ToArray());
    mouseInfoOverlay.ShowAtCursor();
  }

  /// <summary>Displays brief information about the vessel under mouse cursor.</summary>
  /// <remarks>
  /// It's called every frame so, don't put heavy code here. If focus change needs heavy processing
  /// do it in <see cref="SetHoveredVessel"/>.
  /// <para><see cref="hoveredVessel"/> must not be <c>null</c>.</para>
  /// </remarks>
  void ShowHoveredVesselInfo() {
    var sb = new List<string>();
    sb.Add(hoveredVessel.isActiveVessel
        ? CurrentVesselMsg.Format()
        : SwitchToMsg.Format(MouseButtonLookup.Lookup(switchMouseButton)));
    sb.Add("");

    // Give a hint when distance is too long.
    float distanceBetweenVessels = Vector3.Distance(
        FlightGlobals.ActiveVessel.transform.position, hoveredVessel.transform.position);
    if (distanceBetweenVessels > maxVesselDistance) {
      sb.Add(DistantVesselTargetedMsg.Format(distanceBetweenVessels));
    }

    if (kerbalEva != null) {
      var protoCrewMember = hoveredVessel.GetVesselCrew()[0];
      sb.Add(KerbalTitleMsg.Format(hoveredVessel.GetName(),
                                   protoCrewMember.experienceTrait.Title,
                                   protoCrewMember.experienceLevel));
      sb.Add(KerbalEvaFuelMsg.Format(kerbalEva.Fuel));
    } else {
      sb.Add(GetShortVesselTitle(hoveredVessel));
      sb.Add(VesselMassMsg.Format(hoveredVessel.GetTotalMass()));
      sb.Add(hoveredVessel.IsControllable ? VesselIsControllableMsg : VesselIsNotControllableMsg);
      if (isKisStaticAttached.HasValue) {
        sb.Add(isKisStaticAttached.Value
            ? VesselIsAttachedToTheGroundMsg
            : VesselIsNotAttachedToTheGroundMsg);
      }
    }

    mouseInfoOverlay.text = string.Join("\n", sb.ToArray());
    mouseInfoOverlay.ShowAtCursor();
  }

  /// <summary>
  /// Iterates thru <see cref="CameraStabilization"/> values and pick next mode on each call.
  /// </summary>
  void SelectNextStabilizationMode() {
    if (cameraStabilizationMode == CameraStabilization.None) {
      cameraStabilizationMode = CameraStabilization.KeepPosition;
    } else if (cameraStabilizationMode == CameraStabilization.KeepPosition) {
      cameraStabilizationMode = CameraStabilization.KeepDistanceAndRotation;
    } else {
      cameraStabilizationMode = CameraStabilization.None;
    }
    ScreenMessaging.ShowPriorityScreenMessage(
        CameraStabilizationModeChangedMsg.Format(
            CameraStabilizationModeLookup.Lookup(cameraStabilizationMode)));
  }

  /// <summary>Shortcut to get a short vessel title.</summary>
  /// <remarks>It considers and reports vessel's type.</remarks>
  /// <param name="vessel">Vessel to get title for.</param>
  /// <returns>A short string that fits one line.</returns>
  string GetShortVesselTitle(Vessel vessel) {
    if (vessel.vesselType == VesselType.Unknown) {
      // Unknown vessels are likely dropped parts or assemblies.
      return vessel.parts.Count == 1
          ? SinglePartTitleMsg.Format(vessel.vesselName)
          : AssemblyTitleMsg.Format(vessel.vesselName);
    }
    return VesselTitleMsg.Format(vessel.vesselType.displayDescription(), vessel.vesselName);
  }

  /// <summary>Verifies if KIS part is attached to the ground.</summary>
  /// <param name="part">Part to check.</param>
  /// <returns>
  /// <c>null</c> if it's not a KIS part. Otherwise, either <c>true</c> or <c>false</c>.
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
  /// <param name="color">
  /// A color to use for highlighting. If set to <c>null</c> then vessel highlighting will be
  /// cancelled.
  /// </param>
  static void SetVesselHighlight(Vessel vessel, Color? color) {
    foreach (var part in vessel.parts) {
      if (part == Mouse.HoveredPart) {
        continue;  // KSP core highlights hovered part with own color. 
      }
      if (color.HasValue) {
        part.highlighter.ConstantOn(color.Value);
      } else {
        part.highlighter.ConstantOff();
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
  /// <returns><c>true</c> if mode allows perserving the same camera position.</returns>
  static bool IsFreeCameraPositionMode(FlightCamera.Modes mode) {
    return mode == FlightCamera.Modes.AUTO  // It never chooses LOCKED.
        || mode == FlightCamera.Modes.FREE
        || mode == FlightCamera.Modes.ORBITAL
        || mode == FlightCamera.Modes.CHASE;
  }

  /// <summary>A coroutine to temporarily highlight a vessel.</summary>
  /// <param name="vessel">Vessel to highlight.</param>
  /// <param name="timeout">Duration to keep vessel highlighet.</param>
  /// <param name="color">Color to assign to the highlighter.</param>
  /// <param name = "isBeingDocked">
  /// If <c>true</c> then highlighting logic will expect number of parts in the vessel to increase.
  /// It may put some extra loading on CPU, though.
  /// </param>
  /// <returns><c>WaitForSeconds</c>.</returns>
  static IEnumerator TimedHighlightCoroutine(Vessel vessel, float timeout, Color color,
                                             bool isBeingDocked = false) {
    SetVesselHighlight(vessel, color);
    if (isBeingDocked) {
      // On dock event completion the set of vessel parts will increase. Though, there is no
      // reliable way to detect when new parts are redy to accept highlighter changes. So, just set
      // the highlight on every frame. It will cost some performance but it's not critical here. 
      var startTime = Time.unscaledTime;
      while (Time.unscaledTime - startTime < timeout) {
        yield return null;
        SetVesselHighlight(vessel, color);
      }
    } else {
      yield return new WaitForSeconds(timeout);
    }
    SetVesselHighlight(vessel, null);
  }

  /// <summary>Preserves fixed position of camera, and moves its focus to the new vessel.</summary>
  /// <remarks>
  /// The vessel may move while the animation is done (e.g. on the orbit). To compensate this
  /// movement all camera calculations are done relative to the original vessel position at the
  /// time of the switch. And then a difference is added given the current vessel position.
  /// </remarks>
  /// <param name="target">A camera target. If it's changed the animation will abort.</param>
  /// <param name="oldInfo">Previous vessel info.</param>
  /// <param name="newInfo">New vessel info.</param>
  /// <param name="trasitionDuration">The duration to play the transition animation.</param>
  /// <returns><c>null</c> until the animation is done or aborted.</returns>
  static IEnumerator AnimateCameraPivotCoroutine(
      Transform target, VesselInfo oldInfo, VesselInfo newInfo, float trasitionDuration) {
    float startTime = Time.unscaledTime;
    float progress;
    do {
      // Calculate vessel movement compensation offset.
      var movementOffset = FlightGlobals.ActiveVessel.transform.position - newInfo.anchorPos;
      progress = (Time.unscaledTime - startTime) / trasitionDuration;
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
  /// Preserves the camera-to-target rotation and the distance. It also moves the camera's focus to
  /// the new vessel.
  /// </summary>
  /// <remarks>
  /// The vessel may move while the animation is playing (e.g. on the orbit). To compensate this
  /// movement all the camera calculations are done relative to the original vessel position at the
  /// time of the switch.
  /// </remarks>
  /// <param name="target">The camera target. If it's changed then the animation will abort.</param>
  /// <param name="oldInfo">The previous vessel info.</param>
  /// <param name="newInfo">The new vessel info.</param>
  /// <param name="trasitionDuration">The duration to play the transition animation.</param>
  /// <returns><c>null</c> until the animation is done or aborted.</returns>
  static IEnumerator AnimateCameraPositionCoroutine(
      Transform target, VesselInfo oldInfo, VesselInfo newInfo, float trasitionDuration) {
    float startTime = Time.unscaledTime;
    float progress;
    do {
      // Calculate vessel movement compensation offset.
      var movementOffset = FlightGlobals.ActiveVessel.transform.position - newInfo.anchorPos;
      progress = (Time.unscaledTime - startTime) / trasitionDuration;
      // Only animate the pivot position. The camera's position will be adjusted by the FlighCamera
      // code.
      FlightCamera.fetch.GetPivot().transform.position =
          movementOffset + Vector3.Lerp(oldInfo.cameraPivotPos, newInfo.cameraPivotPos, progress);
      yield return null;
    } while (progress < 1.0f && FlightCamera.fetch.Target == target);
  }
}

}

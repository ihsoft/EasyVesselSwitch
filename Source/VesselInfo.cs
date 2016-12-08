// KSP Easy Vessel Switch
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using UnityEngine;

namespace EasyVesselSwitch {

/// <summary>Holds metadata about vessel &amp; camera context used when switching.</summary>
/// <remarks>It must be struct since copy-by-value behavior will be needed.</remarks>
struct VesselInfo {
  /// <summary>Position of the new vessel at the moment of switch.</summary>
  public Vector3 anchorPos;
  /// <summary>Camera position before vessel change.</summary>
  public Vector3 cameraPos;
  /// <summary>Camera focus position before vessel change.</summary>
  public Vector3 cameraPivotPos;
  /// <summary>Camera mode. It will be <c>AUTO</c> by default.</summary>
  public FlightCamera.Modes cameraMode;

  /// <summary>Captures vessel info.</summary>  
  public VesselInfo(Vessel vessel) {
    anchorPos = vessel.transform.position;
    cameraPos = Vector3.zero;
    cameraPivotPos = Vector3.zero;
    cameraMode = FlightCamera.Modes.AUTO;
  }
  
  /// <summary>Captures vessel and camera info.</summary>  
  public VesselInfo(Vessel vessel, FlightCamera camera) {
    anchorPos = vessel.transform.position;
    cameraPos = camera.GetCameraTransform().position;
    cameraPivotPos = camera.GetPivot().position;
    cameraMode = camera.mode;
  }

  /// <summary>Updates camera info from the provided instance.</summary>  
  public void UpdateCameraFrom(FlightCamera camera) {
    cameraPos = camera.GetCameraTransform().position;
    cameraPivotPos = camera.GetPivot().position;
    cameraMode = camera.mode;
  }

  /// <summary>Gets vessel info for the current camera and vessel.</summary>
  /// <returns>Vessel info.</returns>
  public static VesselInfo CaptureCurrentState() {
    return new VesselInfo(FlightGlobals.ActiveVessel, FlightCamera.fetch);
  }
}

}  // namespace

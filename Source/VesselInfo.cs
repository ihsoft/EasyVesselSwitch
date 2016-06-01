// KSP Easy Vessel Switch
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using UnityEngine;

namespace EasyVesselSwitch {

/// <summary>Vessel &amp; camera context used when switching.</summary>
struct VesselInfo {
  /// <summary>Position of the new vessel at the moment of switch.</summary>
  public Vector3 anchorPos;
  /// <summary>Camera position before vessel change.</summary>
  public Vector3 cameraPos;
  /// <summary>Camera focus position before vessel change.</summary>
  public Vector3 cameraPivotPos;

  /// <summary>Captures vessel info.</summary>  
  public VesselInfo(Vessel vessel) {
    anchorPos = vessel.transform.position;
    cameraPos = Vector3.zero;
    cameraPivotPos = Vector3.zero;
  }
  
  /// <summary>Captures vessel and camera info.</summary>  
  public VesselInfo(Vessel vessel, FlightCamera camera) {
    anchorPos = vessel.transform.position;
    cameraPos = camera.GetCameraTransform().position;
    cameraPivotPos = camera.GetPivot().position;
  }

  /// <summary>Updates camera info from the rpovided instance.</summary>  
  public void UpdateCameraFrom(FlightCamera camera) {
    cameraPos = camera.GetCameraTransform().position;
    cameraPivotPos = camera.GetPivot().position;
  }
}

}  // namespace

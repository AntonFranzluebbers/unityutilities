﻿#define OCULUS_UTILITIES_AVAILABLE

using UnityEngine;

namespace unityutilities {
	public class VRObject : MonoBehaviour {
		#if OCULUS_UTILITIES_AVAILABLE
		void Update() {
			Vector3 pos = OVRPlugin.GetNodePose(OVRPlugin.Node.DeviceObjectZero, OVRPlugin.Step.Render).ToOVRPose()
				.position;
			Quaternion rot = OVRPlugin.GetNodePose(OVRPlugin.Node.DeviceObjectZero, OVRPlugin.Step.Render).ToOVRPose()
				.orientation;

			bool garbageUpdate = pos == Vector3.zero;

			if (garbageUpdate) return;
			var t = transform;
			t.localPosition = pos;
			t.localRotation = rot;
		}
		#endif
	}
}
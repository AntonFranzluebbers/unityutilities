﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace unityutilities.VRInteraction
{
	/// <summary>
	/// Spins 🔄
	/// In VRDial2, the goal position is entirely virtual,
	/// and both remote and local dials try to achieve the goal position with physics in some way.
	/// Limits are defined by hinge joint/physics
	/// </summary>
	[AddComponentMenu("unityutilities/Interaction/VRDial2")]
	[DisallowMultipleComponent]
	public class VRDial2 : VRGrabbable
	{
		public Vector3 dialAxis = Vector3.forward;
		private Rigidbody rb;
		private Quaternion lastGrabbedRotation;
		private Vector3 lastGrabbedPosition;
		private float lastAngle;
		
		
		/// <summary>
		/// float currentAngleDeg, float deltaAngleDeg, bool localInput
		/// </summary>
		public Action<float, float, bool> DialTurned;
		public float multiplier = 1;

		[Tooltip("How much to use position of hand rather than rotation")]
		[Range(0, 1)]
		public float positionMix = 1;
		[Tooltip("Whether to vary the position mix with distance to the center of the object")]
		public bool dynamicPositionMix = false;
		public float dynamicPositionMixDistanceMultiplier = 10f;

		public MixingMethod mixingMethod;

		public enum MixingMethod
		{
			WeightedAvg,
			Max,
			Min,
			Sum
		}

		public float CurrentAngle => Vector3.SignedAngle();
		[Tooltip("Set this in inspector to change the starting angle\n" +
			"The object should still be at 0deg")]
		public float goalAngle;

		public float goalDeadzoneDeg = .01f;

		private float vibrationDelta = 10;
		private float vibrationDeltaSum = 0;


		private Queue<float> lastRotationVels = new Queue<float>();
		private int lastRotationVelsLength = 10;


		// Use this for initialization
		private void Start()
		{
			rb = GetComponent<Rigidbody>();
			SetData(goalAngle, false);
		}

		private void Update()
		{
			float angleDifference = goalAngle - CurrentAngle;
			if (Mathf.Abs(angleDifference) > goalDeadzoneDeg)
			{
				transform.Rotate(dialAxis, angleDifference / 10f, Space.Self);
			}


			if (GrabbedBy != null)
			{
				GrabInput();

				// update the last velocities 
				lastRotationVels.Enqueue(CurrentAngle - lastAngle);
				if (lastRotationVels.Count > lastRotationVelsLength)
					lastRotationVels.Dequeue();
			}

			lastAngle = CurrentAngle;
		}

		protected void GrabInput()
		{

			#region Position of the hand

			// the direction vectors to the two hand positions
			Vector3 posDiff = GrabbedBy.transform.position - transform.position;
			Vector3 lastPosDiff = lastGrabbedPosition - transform.position;

			Vector3 localDialAxis = transform.TransformDirection(dialAxis);

			// remove the rotation-axis component of the vectors
			posDiff = Vector3.ProjectOnPlane(posDiff, localDialAxis);
			lastPosDiff = Vector3.ProjectOnPlane(lastPosDiff, localDialAxis);

			// convert them into a rotation
			float angleDiff = Vector3.SignedAngle(lastPosDiff, posDiff, localDialAxis);

			float rotationBasedOnHandPosition = goalAngle + angleDiff;

			lastGrabbedPosition = GrabbedBy.transform.position;

			#endregion


			#region Rotation of the hand

			// get the rotation of the hand in the last frame
			Quaternion diff = GrabbedBy.transform.rotation * Quaternion.Inverse(lastGrabbedRotation);

			// convert to angle axis
			diff.ToAngleAxis(out float angle, out Vector3 axis);

			// adjust speed
			angle *= multiplier;

			float rotationBaseOnHandRotation = goalAngle;

			// angle should be > 0 if there is a change
			if (angle > 0)
			{
				if (angle >= 180)
				{
					angle -= 360;
				}

				Vector3 moment = angle * axis;

				//project the moment vector onto the dial axis
				float newAngle = Vector3.Dot(moment, transform.localToWorldMatrix.MultiplyVector(dialAxis)) / transform.lossyScale.x;

				lastGrabbedRotation = GrabbedBy.transform.rotation;

				rotationBaseOnHandRotation = goalAngle + newAngle;
			}

			#endregion

			// set mix
			if (dynamicPositionMix)
			{
				positionMix = Mathf.Clamp01(Vector3.Distance(transform.position, GrabbedBy.transform.position) * dynamicPositionMixDistanceMultiplier);
			}


			float finalAngle = 0;

			switch (mixingMethod)
			{
				case MixingMethod.WeightedAvg:
					finalAngle = positionMix * rotationBasedOnHandPosition + (1 - positionMix) * rotationBaseOnHandRotation;
					break;
				case MixingMethod.Max:
					finalAngle = Mathf.Abs(rotationBasedOnHandPosition) > Mathf.Abs(rotationBaseOnHandRotation) ?
						rotationBasedOnHandPosition :
						rotationBaseOnHandRotation;
					break;
				case MixingMethod.Min:
					finalAngle = Mathf.Abs(rotationBasedOnHandPosition) < Mathf.Abs(rotationBaseOnHandRotation) ?
						rotationBasedOnHandPosition :
						rotationBaseOnHandRotation;
					break;
				case MixingMethod.Sum:
					finalAngle = rotationBasedOnHandPosition + rotationBaseOnHandRotation;
					break;
			}


			SetData(finalAngle, true);
		}

		public virtual void SetData(float updatedAngle, bool localInput)
		{

			locallyOwned = localInput;

			if (localInput)
			{
				float angleDifference = updatedAngle - goalAngle;
				goalAngle = updatedAngle;
				transform.Rotate(dialAxis, angleDifference, Space.Self);
				DialTurned?.Invoke(goalAngle, angleDifference, localInput);

				// vibrate
				// vibrate only when rotated by a certain amount
				if (vibrationDeltaSum > vibrationDelta)
				{
					if (GrabbedBy)
					{
						InputMan.Vibrate(GrabbedBy.side, 1f, .01f);
					}
					vibrationDeltaSum = 0;
				}

				vibrationDeltaSum += Mathf.Abs(angleDifference);

			}
			else
			{
				DialTurned?.Invoke(updatedAngle, updatedAngle - goalAngle, false);
				goalAngle = updatedAngle;
			}

		}



		public override void HandleGrab(VRGrabbableHand h)
		{
			base.HandleGrab(h);

			lastGrabbedRotation = GrabbedBy.transform.rotation;
			lastGrabbedPosition = GrabbedBy.transform.position;
		}

		public override void HandleRelease(VRGrabbableHand h = null)
		{
			base.HandleRelease(h);

			// add velocity
			if (rb && lastRotationVels.Count > 0)
			{
				// y not convert to rad?
				rb.angularVelocity = dialAxis * lastRotationVels.Average();
			}
		}

		public override byte[] PackData()
		{
			return BitConverter.GetBytes(goalAngle);
		}

		public override void UnpackData(byte[] data)
		{
			using (MemoryStream inputStream = new MemoryStream(data))
			{
				BinaryReader reader = new BinaryReader(inputStream);

				SetData(reader.ReadSingle(), false);

			}
		}
	}
}
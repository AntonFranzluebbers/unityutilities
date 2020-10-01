﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace unityutilities.VRInteraction
{
	/// <summary>
	/// ✋ Handles grab and release of VRGrabbable objects. Needs to be able to receive OnTriggerEnter events.
	/// </summary>
	public class VRGrabbableHand : MonoBehaviour
	{
		public Rig rig;
		public Side side;

		[Tooltip("Turning this to false prevents this hand from grabbing objects itself.")]
		public bool canGrab = true;

		[Tooltip("Can be set to none to use external input sources by using actions.")]
		public VRInput grabInput = VRInput.Trigger;

		public bool vibrateOnGrab = true;

		public float remoteGrabbingDistance = 5f;

		[Tooltip("Which layers to interact with")]
		public LayerMask layerMask = ~0;

		[Header("Debug")]
		[ReadOnly]
		public VRGrabbable grabbedVRGrabbable;
		[ReadOnly]
		public VRGrabbable selectedVRGrabbable;
		[ReadOnly]
		public List<VRGrabbable> touchedObjs = new List<VRGrabbable>();

		[ReadOnly]
		public List<VRGrabbable> raycastedObjs = new List<VRGrabbable>();


		public Queue<Vector3> lastVels = new Queue<Vector3>();
		private int lastVelsLength = 5;

		public Action<VRGrabbable> GrabEvent;
		public Action<VRGrabbable> ReleaseEvent;

		private void Update()
		{
			// Grab ✊
			if (InputMan.GetDown(grabInput, side))
			{
				Grab();
			}
			// Release 🤚
			else if (InputMan.GetUp(grabInput, side))
			{
				Release();
			}

			// Highlight 🖊
			if (grabbedVRGrabbable)
			{
				if (selectedVRGrabbable)
				{
					selectedVRGrabbable.HandleDeselection();
					selectedVRGrabbable = null;
				}
			}
			else
			{
				var best = GetBestGrabbable();
				if (selectedVRGrabbable != best)
				{
					if (selectedVRGrabbable)
						selectedVRGrabbable.HandleDeselection();

					if (best != null)
					{
						selectedVRGrabbable = best;
						selectedVRGrabbable.HandleSelection();
					}
				}
			}


			// Add remote objects to the touched list
			RaycastHit[] hitList = null;
			if (false && Physics.Raycast(transform.position, transform.forward, out RaycastHit directHit, remoteGrabbingDistance, layerMask))
			{
				// Find VRGrabbable
				VRGrabbable vrGrabbable;
				if (directHit.collider.attachedRigidbody)
					vrGrabbable = directHit.collider.attachedRigidbody.GetComponent<VRGrabbable>();
				else
					vrGrabbable = directHit.collider.GetComponent<VRGrabbable>();

				if (vrGrabbable && vrGrabbable.remoteGrabbable)
				{
					hitList = new RaycastHit[] { directHit };
				}
			}

			// if there still isn't anything in the hitlist (no direct hits)
			if (hitList == null)
			{
				hitList = Util.ConeCastAll(transform.position, transform.forward, 1.5f, remoteGrabbingDistance, layerMask);
			}

			var newRaycastedObjs = new List<VRGrabbable>();
			foreach (var hit in hitList)
			{
				Collider other = hit.collider;

				// Find VRGrabbable
				VRGrabbable vrGrabbable;
				if (other.attachedRigidbody)
					vrGrabbable = other.attachedRigidbody.GetComponent<VRGrabbable>();
				else
					vrGrabbable = other.GetComponent<VRGrabbable>();


				// Select the object
				if (vrGrabbable && vrGrabbable.remoteGrabbable)
				{
					newRaycastedObjs.Add(vrGrabbable);

					if (!touchedObjs.Contains(vrGrabbable))
					{
						touchedObjs.Add(vrGrabbable);
					}
				}
			}

			// stop selecting the previous objs that aren't currently raycasted at
			foreach (var obj in raycastedObjs)
			{
				if (!newRaycastedObjs.Contains(obj))
				{
					touchedObjs.Remove(obj);
					if (obj == selectedVRGrabbable)
					{
						selectedVRGrabbable.HandleDeselection();
						selectedVRGrabbable = null;
					}
				}
			}

			raycastedObjs = newRaycastedObjs;

			// update the last velocities ➡➡
			if (rig)
			{
				lastVels.Enqueue(rig.transform.TransformVector(InputMan.ControllerVelocity(side)));
				if (lastVels.Count > lastVelsLength)
					lastVels.Dequeue();
			}
		}

		/// <summary>
		/// Call this from somewhere to try to grab anything that is being hovered.
		/// Allows for remote sources if input, such as for tracked hands.
		/// </summary>
		public void Grab()
		{
			if (canGrab && !grabbedVRGrabbable)
			{
				VRGrabbable best = GetBestGrabbable();
				if (best != null)
				{
					Grab(best);
				}
			}
		}

		/// <summary>
		/// Call this from somewhere to try to release the currently grabbed object.
		/// Allows for remote sources if input, such as for tracked hands.
		/// </summary>
		public void Release()
		{
			if (grabbedVRGrabbable)
			{
				grabbedVRGrabbable.HandleRelease(this);
				if (touchedObjs.Contains(grabbedVRGrabbable))
				{
					selectedVRGrabbable = grabbedVRGrabbable;
					selectedVRGrabbable.HandleSelection();
				}
			}

			ReleaseEvent?.Invoke(grabbedVRGrabbable);

			grabbedVRGrabbable = null;
		}


		/// <summary>
		/// Call this from somewhere to grab the passed-in object.
		/// Allows for manually grabbing an object, such as for a spawner.
		/// </summary>
		/// <param name="grabbable">The object to be grabbed</param>
		public void Grab(VRGrabbable grabbable)
		{
			grabbable.HandleDeselection();
			grabbable.HandleGrab(this);
			grabbedVRGrabbable = grabbable;
			if (vibrateOnGrab)
			{
				InputMan.Vibrate(side, .5f, .1f);
			}
			GrabEvent?.Invoke(grabbable);
		}

		/// <summary>
		/// Finds the VRGrabbable obj being collided with that has the highest priority (or some other algorithm)
		/// </summary>
		/// <returns>The VRGrabbable</returns>
		private VRGrabbable GetBestGrabbable()
		{
			// remove any null objects 😊👌
			touchedObjs.RemoveAll(item => item == null);

			// Cancel if not allowed to grab
			if (!canGrab) return null;

			// Cancel if not touching anything
			if (touchedObjs.Count <= 0) return null;


			// Sort the list of grabbables by priority, then distance
			touchedObjs.Sort((a, b) =>
			{
				if (a.priority != b.priority)
					return b.priority.CompareTo(a.priority);
				else
				{
					// combine both distance and angular distance from the center for remote grabbing
					float dist = Vector3.Distance(a.transform.position, transform.position)
						.CompareTo(Vector3.Distance(b.transform.position, transform.position));
					float angle = Vector3.Angle(transform.forward, a.transform.position - transform.position)
						.CompareTo(Vector3.Angle(transform.forward, b.transform.position - transform.position));
					return (int)(0 * dist + angle);
				}

			});

			// return the first element on the list
			return touchedObjs[0];
		}

		private void OnTriggerStay(Collider other)
		{
			// ignore if not included in the layermask
			if (layerMask != (layerMask | 1 << other.gameObject.layer)) return;

			// Find VRGrabbable
			VRGrabbable vrGrabbable;
			if (other.attachedRigidbody)
				vrGrabbable = other.attachedRigidbody.GetComponent<VRGrabbable>();
			else
				vrGrabbable = other.GetComponent<VRGrabbable>();

			// Select the object
			if (vrGrabbable)
			{
				if (!touchedObjs.Contains(vrGrabbable))
				{
					touchedObjs.Add(vrGrabbable);
				}
			}
		}

		private void OnTriggerExit(Collider other)
		{
			// ignore if not included in the layermask
			if (layerMask != (layerMask | 1 << other.gameObject.layer)) return;

			// Find VRGrabbable
			VRGrabbable vrGrabbable;
			if (other.attachedRigidbody)
				vrGrabbable = other.attachedRigidbody.GetComponent<VRGrabbable>();
			else
				vrGrabbable = other.GetComponent<VRGrabbable>();

			// Deselect the object
			if (vrGrabbable)
			{
				touchedObjs.Remove(vrGrabbable);
				if (vrGrabbable == selectedVRGrabbable)
				{
					selectedVRGrabbable.HandleDeselection();
					selectedVRGrabbable = null;
				}
			}
		}
	}
}
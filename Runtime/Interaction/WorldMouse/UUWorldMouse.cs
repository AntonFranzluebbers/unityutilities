﻿using System;
using UnityEditor;
using UnityEngine;

namespace unityutilities
{

#if UNITY_EDITOR
	[CustomEditor(typeof(UUWorldMouse))]
	[CanEditMultipleObjects]
	public class UUWorldMouseEditor : Editor
	{
		UUWorldMouse uuWorldMouse;

		private void OnEnable()
		{
			uuWorldMouse = target as UUWorldMouse;
		}

		public override void OnInspectorGUI()
		{
			EditorGUILayout.LabelField("Provides a laser pointer as well as interaction with UI.");
			EditorGUILayout.Space();

			base.OnInspectorGUI();
		}
	}
#endif

	public class UUWorldMouse : WorldMouse
	{
		public VRInput input = VRInput.Trigger;
		public Side side;


		[Space]

		public bool useLaser = true;

		/// <summary>
		/// Default laser color. Can't be changed during runtime
		/// </summary>
		public Color laserColor = Color.black;

		/// <summary>
		/// Default laser width. Can't be changed during runtime
		/// </summary>
		public float laserThickness = .005f;
		private LineRenderer lineRend;

		private bool showThisFrame = false;

		[Space]

		public float vibrateOnClick = .5f;
		public float vibrateOnHover = .1f;

		protected void Start()
		{
			if (useLaser)
			{
				CreateLaser();
			}

			Clicked += OnClicked;
			HoverEntered += OnHover;
		}

		private void OnHover(GameObject obj)
		{
			InputMan.Vibrate(side, vibrateOnHover);
		}

		private void OnClicked(GameObject obj)
		{
			InputMan.Vibrate(side, vibrateOnClick);
		}

		private void CreateLaser()
		{
			lineRend = new GameObject("UI Interaction").AddComponent<LineRenderer>();
			lineRend.transform.SetParent(transform);
			lineRend.transform.localPosition = Vector3.zero;
			lineRend.transform.localRotation = Quaternion.identity;
			lineRend.widthMultiplier = laserThickness;
			lineRend.positionCount = 2;
			lineRend.material = new Material(Shader.Find("Unlit/Color"))
			{
				color = laserColor
			};
		}

		protected override void Update()
		{
			base.Update();
			if (useLaser)
			{
				if (rayDistance < Mathf.Infinity)
				{
					SetLaserLength(rayDistance);
					ShowLaser(true);
				}
				ShowLaserUpdate();
			}
		}

		public override bool PressDown()
		{
			return InputMan.GetDown(input, side);
		}

		public override bool PressUp()
		{
			return InputMan.GetUp(input, side);
		}

		public void ShowLaser(bool show)
		{
			if (show)
			{
				showThisFrame = true;
			}
		}

		/// <summary>
		/// Called once an update to actually show the laser or not.
		/// </summary>
		private void ShowLaserUpdate()
		{
			if (lineRend == null) CreateLaser();
			lineRend.gameObject.SetActive(showThisFrame);
			showThisFrame = false;
		}

		public void SetLaserLength(float length)
		{
			if (lineRend == null) CreateLaser();
			lineRend.SetPositions(new Vector3[] { transform.position, transform.position + transform.forward * length });
		}
	}
}
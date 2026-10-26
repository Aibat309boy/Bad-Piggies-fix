using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : Singleton<InputManager>
{
        public bool UsesTouchInput { get; set; } // * This value may change at any time

#if !UNITY_WEBGL
	private void Awake()
	{
		Input.simulateMouseWithTouches = false;
		RuntimePlatform runtimePlatform = Application.platform;
		this.UsesTouchInput = runtimePlatform == RuntimePlatform.IPhonePlayer || runtimePlatform == RuntimePlatform.Android; // * This is just a default value
		base.SetAsPersistant();
	}

	private void Update()
	{
		bool usesTouch = this.UsesTouchInput;

		if (!usesTouch && Input.touchCount != 0)
		{
			this.UsesTouchInput = true;
		}
		else if (usesTouch && InputManager.CheckMouse())
		{
			this.UsesTouchInput = false;		
		}
	}

	private static bool CheckMouse()
	{
		if (!Input.mousePresent) return false;
		return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
	}
#endif
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlatformFramerateLock : MonoBehaviour
{
	public bool allowUmlimitedFPS = false;
	public int highEndFPS = 60;
	public float highEndFixedTimeStep = .02f;
	public int lowEndFPS = 30;
	public float lowEndFixedTimeStep = .033f;

	public RenderPipelineAsset rpAsset;


    void Start()
    {
		int rate = 60;
		float physRate = .02f;
#if UNITY_STANDALONE || UNITY_EDITOR || UNITY_XBOXONE || UNITY_PS4
		rate = highEndFPS;
		physRate = highEndFixedTimeStep;
#else
		rate = lowEndFPS;
		physRate = lowEndFixedTimeStep;
#endif

		Time.fixedDeltaTime = physRate;

		if(!allowUmlimitedFPS)
			Application.targetFrameRate = rate;
		
		#if UNITY_IOS
	    if (allowUmlimitedFPS)
		    Application.targetFrameRate = highEndFPS;
		#endif
	    
	    // string value = Environment.GetEnvironmentVariable("UNITY_EXT_LOGGING");
     //    
	    // if (value == "1")
	    // {
		   //  Debug.Log("✓ Extended logging is ENABLED");
	    // }
	    // else
	    // {
		   //  Debug.LogWarning("✗ Extended logging is DISABLED");
	    // }
    }
}

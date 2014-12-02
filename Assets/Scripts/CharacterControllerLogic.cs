using UnityEngine;
using System.Collections;

public enum CoverState {onDownFace, OnRightFace, OnLeftFace, nil};

/// #DESCRIPTION OF CLASS#
public class CharacterControllerLogic : MonoBehaviour 
{
	
	#region Variables (private)
	
	// Inspector serialized
	[SerializeField]
	private Animator animator;
	[SerializeField]
	private GameObject gamecam;
	[SerializeField]
	private GameObject radarCam;
	[SerializeField]
	private Camera mainCam;
	[SerializeField]
	private float rotationDegreePerSecond = 120f;
	[SerializeField]
	private float directionSpeed = 1.5f;
	[SerializeField]
	private float directionDampTime = 0.25f;
	[SerializeField]
	private float speedDampTime = 0.05f;
	[SerializeField]
	private float fovDampTime = 3f;
	[SerializeField]
	private HashIds hashIdsScript;
	
	
	// Private global only
	private float joyX = 0f;
	private float joyY = 0f;
	public float turnSmoothing = 15f;	// A smoothing value for turning the player.
	private AnimatorStateInfo stateInfo;
	private AnimatorTransitionInfo transInfo;
	private float speed = 0f;
	private float direction = 0f;
	private float charAngle = 0f;
	private const float SPRINT_SPEED = 2.0f;	
	private float COVER_SPEED = 0.9f;

	private const float SPRINT_FOV = 75.0f;
	private const float NORMAL_FOV = 60.0f;
	private float capsuleHeight;

	private Vector3 cameraRotation;
	private Quaternion cameraRotationQuaternion;
	private Vector3 cameraPosition;

	private Vector3 radarCameraRotation;
	private Vector3 radarCameraPosition;

	private UIJoystick uiJoystickScript;

	private CoverState currentCoverState;
	private Vector3 vecToAlignTo;
	private Vector3 positionToPlaceTo;
	private CapsuleCollider caps;

	private Transform CameraInCoverPos;
	
	private Vector3 savedCamPosition;
	private Quaternion savedCamRotation;

	private bool inCoverMode;
	private bool inPositioningCoverModeCam;
	
	#endregion
		
	
	#region Properties (public)

	public Animator Animator
	{
		get
		{
			return this.animator;
		}
	}

	public float Speed
	{
		get
		{
			return this.speed;
		}
	}

	public CoverState CurrentCoverState
	{
		get
		{
			return this.currentCoverState;
		}
		set
		{
			this.currentCoverState = value;
		}
	}

	public Vector3 VecToAlignTo
	{
		get
		{
			return this.vecToAlignTo;
		}
		set
		{
			this.vecToAlignTo = value;
		}
	}

	public Vector3 PositionToPlaceTo
	{
		get
		{
			return this.positionToPlaceTo;
		}
		set
		{
			this.positionToPlaceTo = value;
		}
	}

	public Vector3 SavedCamPosition
	{
		get
		{
			return this.savedCamPosition;
		}
		set
		{
			this.savedCamPosition = value;
		}
	}

	public Quaternion SavedCamRotation
	{
		get
		{
			return this.savedCamRotation;
		}
		set
		{
			this.savedCamRotation = value;
		}
	}
	
	public float LocomotionThreshold { get { return 0.15f; } }
	
	#endregion
	
	
	#region Unity event functions
	
	/// Use this for initialization.
	void Start() 
	{
		animator = GetComponent<Animator>();
		mainCam = Camera.main;
		gamecam = mainCam.transform.gameObject;
		radarCam = GameObject.Find ("RadarCamera");
		hashIdsScript = GameObject.FindGameObjectWithTag(DoneTags.gameController).GetComponent<HashIds> ();

		if(animator.layerCount >= 2)
		{
			animator.SetLayerWeight(1, 1);
		}		

		cameraRotation = gamecam.transform.eulerAngles;
		cameraRotationQuaternion = gamecam.transform.localRotation;
		cameraPosition = gamecam.transform.position - transform.position;
		radarCameraRotation = radarCam.transform.eulerAngles;
		radarCameraPosition = radarCam.transform.position - transform.position;

		uiJoystickScript = GameObject.Find ("JoyStick").GetComponent<UIJoystick> ();

		currentCoverState = CoverState.nil;

		caps = GetComponent<CapsuleCollider>();

		CameraInCoverPos = GameObject.Find ("CameraInCoverPos").transform;
		inCoverMode = false;
		inPositioningCoverModeCam = false;
	}

	void LateUpdate()
	{

		//Reset Radar Camera Rotation and position
		Vector3 rot2 = radarCam.transform.eulerAngles;
		rot2 = radarCameraRotation; 
		radarCam.transform.eulerAngles = rot2;
		radarCam.transform.position = transform.position+radarCameraPosition;

		//Reset Camera position and rotation if we are not in cover and if we are not in transition from cover
		if(currentCoverState == CoverState.nil)
		{
			if(transInfo.nameHash != hashIdsScript.Cover_LocomotionTrans)
			{
				Vector3 rot = gamecam.transform.eulerAngles;
				rot = cameraRotation; 
				gamecam.transform.eulerAngles = rot;
				gamecam.transform.position = transform.position+cameraPosition;
			}

//			else
//			{
//				Debug.Log("ENTER HERE");
//
//				gamecam.transform.localPosition = Vector3.Lerp(gamecam.transform.localPosition, cameraPosition, 10f*Time.deltaTime);
//				gamecam.transform.localRotation = Quaternion.Lerp(gamecam.transform.localRotation, cameraRotationQuaternion, 10f*Time.deltaTime);
//			}

			//If we still are in idle (not in pivot, not in locomotion, not in Sneak)
			if(stateInfo.nameHash == hashIdsScript.m_IdleState || stateInfo.nameHash == hashIdsScript.m_sneakingState)
			{
				// If there is some axis input...
				if(joyX != 0f || joyY != 0f)
				{
					// ... set the players rotation and set the speed parameter to 5.5f.
					Rotating(joyX, joyY);
				}
			}
		}

		//We are positiong the character close to the wall so we save the camera position until it's correctly set
		else if(inPositioningCoverModeCam == false && inCoverMode == false)
		{
			gamecam.transform.position = savedCamPosition;
			gamecam.transform.rotation = savedCamRotation;
		}

		else if (inCoverMode)
		{
			gamecam.transform.localPosition = CameraInCoverPos.localPosition;
			gamecam.transform.localRotation = CameraInCoverPos.localRotation;
		}
			
//		//Reset camera position and rotation in Cover mode while we are  NOT in transition to Cover mode
//		else if(transInfo.nameHash != hashIdsScript.Locomotion_CoverTrans)
//		{
//			Quaternion rot = gamecam.transform.localRotation;
//			rot = CameraInCoverPos.localRotation; 
//			gamecam.transform.localRotation = rot;
//			gamecam.transform.position = transform.position+CameraInCoverPos.localPosition;
//		}

	}
	
	/// Update is called once per frame.
	void Update() 
	{

		stateInfo = animator.GetCurrentAnimatorStateInfo(0);
		transInfo = animator.GetAnimatorTransitionInfo(0);
		
		charAngle = 0f;
		direction = 0f;	
		float charSpeed = 0f;

		//Get Joystick Vector
		joyX = uiJoystickScript.position.x;
		joyY = uiJoystickScript.position.y;

		//NOT in COVER
		if(currentCoverState == CoverState.nil)
		{
			inCoverMode = false;

			Vector3 stickDirection = new Vector3 (joyX, 0, joyY);
			Vector3 axisSign = Vector3.Cross(this.transform.forward, stickDirection);
			
			float angleRootToMove = Vector3.Angle(transform.forward, stickDirection) * (axisSign.y < 0 ? -1f : 1f);
			
			charSpeed = stickDirection.magnitude;

			direction = angleRootToMove * directionSpeed / 180f;
			
			charAngle = angleRootToMove;


			// Press B to sprint
			if (Input.GetButton("Sprint"))
			{
				speed = Mathf.Lerp(speed, SPRINT_SPEED, Time.deltaTime);
			}
			else
			{
				speed = charSpeed;
			}

			animator.SetFloat(hashIdsScript.speedFloat, speed, speedDampTime, Time.deltaTime);
			animator.SetFloat(hashIdsScript.direction, direction, directionDampTime, Time.deltaTime);
			
			if (speed > LocomotionThreshold)	// Dead zone
			{
				Animator.SetFloat(hashIdsScript.angle, charAngle);
			}

			if (speed < LocomotionThreshold && Mathf.Abs(joyX) < 0.05f)    // Dead zone
			{
				animator.SetFloat(hashIdsScript.direction, 0f);
				animator.SetFloat(hashIdsScript.angle, 0f);
			}

			animator.SetBool(hashIdsScript.sneakingBool, Input.GetButton("Sneak"));
		}

		//IN COVER
		else
		{
			Quaternion q = Quaternion.FromToRotation(transform.forward, vecToAlignTo);
			Quaternion newRotation = q * transform.rotation;

			//We place the player close to the wall while we are in transition
			if(transform.rotation !=newRotation)
			{
				animator.SetFloat(hashIdsScript.speedFloat, 0);
				animator.SetFloat(hashIdsScript.direction, 0);

				transform.rotation = Quaternion.Lerp(transform.rotation, q * transform.rotation, 12f * Time.deltaTime);
				transform.position = Vector3.Lerp (transform.position, positionToPlaceTo, 12f*Time.deltaTime);
			}
			//We place the camera to the right position
			else if(gamecam.transform.localPosition != CameraInCoverPos.localPosition)
			{
				inPositioningCoverModeCam = true;
				gamecam.transform.localPosition = Vector3.Lerp(gamecam.transform.localPosition, CameraInCoverPos.localPosition, 5*Time.deltaTime);
				gamecam.transform.localRotation = Quaternion.Lerp(gamecam.transform.localRotation, CameraInCoverPos.localRotation, 5*Time.deltaTime);
			}

			else
			{
				inCoverMode = true;
				inPositioningCoverModeCam = false;

				Vector3 stickDirection = new Vector3 (joyX, 0, joyY).normalized;
				Vector3 axisSign = Vector3.Cross(this.transform.forward, stickDirection);
				
				float angleRootToMove = Vector3.Angle(transform.forward, stickDirection) * (axisSign.y < 0 ? -1f : 1f);

				direction = angleRootToMove * directionSpeed / 180f;
				
				charAngle = angleRootToMove;

				switch(currentCoverState)
				{
					case CoverState.onDownFace:
						if(joyY > -0.4f)
						{
							speed = Mathf.Abs (joyX);
							direction = joyX;
							charAngle = 0;
							transform.position += new Vector3(Time.deltaTime*direction*Mathf.Abs(direction)*COVER_SPEED,0, 0) ;
						}
						else 
						{
							currentCoverState = CoverState.nil;
							animator.SetBool(hashIdsScript.coverBool, false);
//							caps.radius = 0.4f;
						}
						break;

					case CoverState.OnLeftFace:
						if(joyX > -0.4f)
						{
							speed = Mathf.Abs (joyY);
							direction = -1*joyY;
							charAngle = 0;
							transform.position += new Vector3(0, 0,Time.deltaTime*direction*Mathf.Abs(direction)*COVER_SPEED);
						}
						else 
						{
							currentCoverState = CoverState.nil;
							animator.SetBool(hashIdsScript.coverBool, false);
//							caps.radius = 0.4f;
						}
						break;

					case CoverState.OnRightFace:
						if(joyX < 0.4f)
						{
							speed = Mathf.Abs (joyY);
							direction = joyY;
							charAngle = 0;
							transform.position += new Vector3(0, 0,Time.deltaTime*direction*Mathf.Abs(direction)*COVER_SPEED);
						}
						else 
						{
							currentCoverState = CoverState.nil;
							animator.SetBool(hashIdsScript.coverBool, false);
//							caps.radius = 0.4f;
						}
						break;

					default:
						break;
				}
				animator.SetFloat(hashIdsScript.speedFloat, speed, speedDampTime, Time.deltaTime);
				animator.SetFloat(hashIdsScript.direction, direction, directionDampTime, Time.deltaTime);
			}
		}

	}

	void Rotating (float horizontal, float vertical)
	{
		// Create a new vector of the horizontal and vertical inputs.
		Vector3 targetDirection = new Vector3(horizontal, 0f, vertical);
		
		// Create a rotation based on this new vector assuming that up is the global y axis.
		Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
		
		// Create a rotation that is an increment closer to the target rotation from the player's rotation.
		Quaternion newRotation = Quaternion.Lerp(rigidbody.rotation, targetRotation, turnSmoothing * Time.deltaTime);
		
		// Change the players rotation to this new rotation.
		rigidbody.MoveRotation(newRotation);
	}
	
	/// Any code that moves the character needs to be checked against physics
	void FixedUpdate()
	{							
		// Rotate character model if stick is tilted right or left, but only if character is moving in that direction
		if (IsInLocomotion()&& ((direction >= 0 && joyX >= 0) || (direction < 0 && joyX < 0)))
		{
//			Debug.Log ("HERE");
			Vector3 rotationAmount = Vector3.Lerp(Vector3.zero, new Vector3(0f, rotationDegreePerSecond * (joyX < 0f ? -1f : 1f), 0f), Mathf.Abs(joyX));
			Quaternion deltaRotation = Quaternion.Euler(rotationAmount * Time.deltaTime);
        	this.transform.rotation = (this.transform.rotation * deltaRotation);
		}		


		AudioManagement ();
	}

	void AudioManagement ()
	{
		// If the player is currently in the run state...
		if(animator.GetCurrentAnimatorStateInfo(0).nameHash == hashIdsScript.m_LocomotionIdState)
		{
			// ... and if the footsteps are not playing...
			if(!audio.isPlaying)
				// ... play them.
				audio.Play();
		}
		else
			// Otherwise stop the footsteps.
			audio.Stop();
		
	}
	
	#endregion
	
	
	#region Methods


    public bool IsInLocomotion()
    {
		return stateInfo.nameHash == hashIdsScript.m_LocomotionIdState;
    }

	#endregion Methods
}

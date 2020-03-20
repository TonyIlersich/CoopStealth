using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[System.Serializable]
public class PlayerControllerEvent : UnityEvent { }

public class PlayerController : MonoBehaviour
{
	public enum MovementControllState { MovementEnabled, MovementDisabled }
	public enum GravityState { GravityEnabled, GravityDisabled }
	public enum DamageState { Vulnerable, Invulnerable }
	public enum InputState { InputEnabled, InputDisabled }
	public enum AliveState { IsAlive, IsDead }
	public PlayerState m_states;

	#region Movement Events
	public PlayerMovementEvents m_movementEvents;
	[System.Serializable]
	public struct PlayerMovementEvents
	{
		[Header("Basic Events")]
		public PlayerControllerEvent m_onLandedEvent;
		public PlayerControllerEvent m_onJumpEvent;
		public PlayerControllerEvent m_onRespawnEvent;

		[Header("Wall Run Events")]
		public PlayerControllerEvent m_onWallRunBeginEvent;
		public PlayerControllerEvent m_onWallRunEndEvent;
		public PlayerControllerEvent m_onWallRunJumpEvent;

		[Header("Wall Climb Events")]
		public PlayerControllerEvent m_onWallClimbBeginEvent;
		public PlayerControllerEvent m_onWallClimbEndEvent;
		public PlayerControllerEvent m_onWallClimbJumpEvent;

		[Header("Wall Jump Events")]
		public PlayerControllerEvent m_onWallJumpEvent;

		[Header("Leap Events")]
		public PlayerControllerEvent m_onLeapEvent;

	}
	#endregion

	#region Camera Properties
	[System.Serializable]
	public struct CameraProperties
	{
		public float m_mouseSensitivity;
		public float m_maxCameraAng;
		public bool m_inverted;
		public Camera m_camera;
		public Transform m_cameraTilt;
		public Transform m_cameraMain;
	}

	[Header("Camera Properties")]
	public CameraProperties m_cameraProperties;
	#endregion

	#region Base Movement Properties
	[System.Serializable]
	public struct BaseMovementProperties
	{
		public float m_baseMovementSpeed;
		public float m_accelerationTime;
	}

	[Header("Base Movement Properties")]
	public BaseMovementProperties m_baseMovementProperties;

	private float m_currentMovementSpeed;
	[HideInInspector]
	public Vector3 m_velocity;
	private Vector3 m_velocitySmoothing;
	private CharacterController m_characterController;
	private Coroutine m_jumpBufferCoroutine;
	private Coroutine m_graceBufferCoroutine;
	#endregion

	#region Jumping Properties
	[System.Serializable]
	public struct JumpingProperties
	{
		[Header("Jump Properties")]
		public float m_maxJumpHeight;
		public float m_minJumpHeight;
		public float m_timeToJumpApex;

		[Header("Jump Buffer Properties")]
		public float m_graceTime;
		public float m_jumpBufferTime;
	}

	[Header("Jumping Properties")]
	public JumpingProperties m_jumpingProperties;

	private float m_graceTimer;
	private float m_jumpBufferTimer;

	private float m_gravity;
	private float m_maxJumpVelocity;
	private float m_minJumpVelocity;
	private bool m_isLanded;
	private bool m_offLedge;
	#endregion

	#region Wall Run Properties
	[System.Serializable]
	public struct WallRunProperties
	{
		public LayerMask m_wallMask;

		public AnimationCurve m_wallSpeedCurve;
		public float m_wallSpeedUpTime;
		public float m_maxWallRunSpeed;

		public float m_tiltSpeed;
		public float m_wallRunCameraMaxTilt;

		public int m_wallRidingRayCount;
		public float m_wallRaySpacing;
		public float m_wallRunRayLength;
		public float m_wallRunBufferTime;
		public Vector3 m_wallRunJumpVelocity;

		public float m_wallJumpBufferTime;
		public Vector3 m_wallJumpVelocity;
	}

	[Header("Wall Run Properties")]
	public WallRunProperties m_wallRunProperties;

	private float m_currentWallRunningSpeed;

	private float m_wallRunBufferTimer;
	private float m_wallJumpBufferTimer;

	private float m_tiltTarget;
	private float m_tiltSmoothingVelocity;

	private bool m_isWallRunning;
	private bool m_connectedWithWall;
	[HideInInspector]
	public bool m_holdingWallRideStick;

	private Vector3 m_wallHitPos;
	private float m_wallHitDst;
	private Vector3 m_wallDir;
	private Vector3 m_wallVector;
	private Vector3 m_wallFacingVector;
	private Vector3 m_modelWallRunPos;

	private Coroutine m_wallJumpBufferCoroutine;
	private Coroutine m_wallRunBufferCoroutine;
	#endregion

	#region Wall Climb Properties
	[System.Serializable]
	public struct WallClimbProperties
	{
		public AnimationCurve m_wallClimbSpeedCurve;
		public float m_maxWallClimbSpeed;
		public float m_wallClimbSpeedUpTime;
		public float m_wallClimbFactor;
		public Vector3 m_wallClimbJumpVelocity;
	}

	[Header("Wall Climb Properties")]
	public WallClimbProperties m_wallClimbProperties;


	private float m_currentWallClimbSpeed;
	private bool m_isWallClimbing;
	[HideInInspector]
	public Vector3 m_localWallFacingVector;
	#endregion

	public float m_crouchTime;
	public float m_crouchHeight;

	[HideInInspector]
	public Vector2 m_movementInput;
	private Vector2 m_lookInput;

	private bool m_isStunned;

	private float m_currentSpeedBoost;

	private Animator m_animator;

	private bool m_isCrouched;

	private void Start()
	{
		m_characterController = GetComponent<CharacterController>();

		m_animator = GetComponentInChildren<Animator>();

		CalculateJump();
		LockCursor();

		m_currentMovementSpeed = m_baseMovementProperties.m_baseMovementSpeed;
		m_jumpBufferTimer = m_jumpingProperties.m_jumpBufferTime;

		m_wallJumpBufferTimer = m_wallRunProperties.m_wallJumpBufferTime;
		m_wallRunBufferTimer = m_wallRunProperties.m_wallRunBufferTime;
	}

	private void OnValidate()
	{
		CalculateJump();
	}

	private void FixedUpdate()
	{
		PerformController();
	}

	public void PerformController()
	{
		SetMovementInput(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")));
		SetLookInput(new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));

		if (Input.GetKeyDown(KeyCode.Space))
		{
			OnJumpInputDown();
		}

		if (Input.GetKeyUp(KeyCode.Space))
		{
			OnJumpInputUp();
		}

		if (Input.GetKeyDown(KeyCode.LeftControl))
		{
			OnCrouchInputDown();
		}

		CalculateCurrentSpeed();
		CalculateVelocity();

		m_characterController.Move(m_velocity * Time.deltaTime);

		CalculateGroundPhysics();

		CameraRotation();
		TiltLerp();
	}

	public void OnCrouchInputDown()
	{
		if (!m_isCrouched)
		{
			StartCoroutine(RunCrouchDown());
		}
		else
		{
			StartCoroutine(RunCrouchUp());
		}

		
	}

	private IEnumerator RunCrouchDown()
	{
		float t = 0;

		while (t < m_crouchTime)
		{
			t += Time.fixedDeltaTime;

			float progress = t / m_crouchTime;

			m_characterController.height = Mathf.Lerp(2, m_crouchHeight, progress);

			yield return new WaitForFixedUpdate();
		}

		m_isCrouched = true;
	}

	private IEnumerator RunCrouchUp()
	{
		float t = 0;

		while (t < m_crouchTime)
		{
			t += Time.fixedDeltaTime;

			float progress = t / m_crouchTime;

			m_characterController.height = Mathf.Lerp(m_crouchHeight, 2, progress);

			yield return new WaitForFixedUpdate();
		}

		m_isCrouched = false;
	}

	#region Input Code
	public void SetMovementInput(Vector2 p_input)
	{
		m_movementInput = p_input;
	}

	public void SetLookInput(Vector2 p_input)
	{
		m_lookInput = p_input;
	}

	public void WallRideInputDown()
	{
		m_holdingWallRideStick = true;
	}

	public void WallRideInputUp()
	{
		m_holdingWallRideStick = false;
		OnWallRideRelease();
	}
	#endregion

	#region Camera Code
	private void LockCursor()
	{
		Cursor.lockState = CursorLockMode.Locked;
	}

	public void ResetCamera()
	{
		m_cameraProperties.m_cameraMain.rotation = Quaternion.identity;
		m_cameraProperties.m_cameraTilt.rotation = Quaternion.identity;
	}

	private void CameraRotation()
	{
		//Get the inputs for the camera
		Vector2 cameraInput = new Vector2(m_lookInput.y * ((m_cameraProperties.m_inverted) ? -1 : 1), m_lookInput.x);

		//Rotate the player on the y axis (left and right)
		transform.Rotate(Vector3.up, cameraInput.y * (m_cameraProperties.m_mouseSensitivity));

		float cameraXAng = m_cameraProperties.m_cameraMain.transform.eulerAngles.x;

		//Stops the camera from rotating, if it hits the resrictions
		if (cameraInput.x < 0 && cameraXAng > 360 - m_cameraProperties.m_maxCameraAng || cameraInput.x < 0 && cameraXAng < m_cameraProperties.m_maxCameraAng + 10)
		{
			m_cameraProperties.m_cameraMain.transform.Rotate(Vector3.right, cameraInput.x * (m_cameraProperties.m_mouseSensitivity));

		}
		else if (cameraInput.x > 0 && cameraXAng > 360 - m_cameraProperties.m_maxCameraAng - 10 || cameraInput.x > 0 && cameraXAng < m_cameraProperties.m_maxCameraAng)
		{
			m_cameraProperties.m_cameraMain.transform.Rotate(Vector3.right, cameraInput.x * (m_cameraProperties.m_mouseSensitivity));

		}

		if (m_cameraProperties.m_cameraMain.transform.eulerAngles.x < 360 - m_cameraProperties.m_maxCameraAng && m_cameraProperties.m_cameraMain.transform.eulerAngles.x > 180)
		{
			m_cameraProperties.m_cameraMain.transform.localEulerAngles = new Vector3(360 - m_cameraProperties.m_maxCameraAng, 0f, 0f);
		}
		else if (m_cameraProperties.m_camera.transform.eulerAngles.x > m_cameraProperties.m_maxCameraAng && m_cameraProperties.m_cameraMain.transform.eulerAngles.x < 180)
		{
			m_cameraProperties.m_cameraMain.transform.localEulerAngles = new Vector3(m_cameraProperties.m_maxCameraAng, 0f, 0f);
		}
	}
	#endregion

	#region Input Buffering Code

	private bool CheckBuffer(ref float p_bufferTimer, ref float p_bufferTime, Coroutine p_bufferTimerRoutine)
	{
		if (p_bufferTimer < p_bufferTime)
		{
			if (p_bufferTimerRoutine != null)
			{
				StopCoroutine(p_bufferTimerRoutine);
			}

			p_bufferTimer = p_bufferTime;

			return true;
		}
		else if (p_bufferTimer >= p_bufferTime)
		{
			return false;
		}

		return false;
	}

	private bool CheckOverBuffer(ref float p_bufferTimer, ref float p_bufferTime, Coroutine p_bufferTimerRoutine)
	{
		if (p_bufferTimer >= p_bufferTime)
		{
			p_bufferTimer = p_bufferTime;

			return true;
		}

		return false;
	}

	//Might want to change this so it does not feed the garbage collector monster
	private IEnumerator RunBufferTimer(System.Action<float> m_bufferTimerRef, float p_bufferTime)
	{
		float t = 0;

		while (t < p_bufferTime)
		{
			t += Time.deltaTime;
			m_bufferTimerRef(t);
			yield return null;
		}

		m_bufferTimerRef(p_bufferTime);
	}
	#endregion

	#region Player State Code
	[System.Serializable]
	public struct PlayerState
	{
		public MovementControllState m_movementControllState;
		public GravityState m_gravityControllState;
		public DamageState m_damageState;
		public InputState m_inputState;
		public AliveState m_aliveState;
	}

	public bool IsGrounded()
	{
		if (m_characterController.collisionFlags == CollisionFlags.Below)
		{
			return true;
		}

		return false;
	}

	private bool OnSlope()
	{
		RaycastHit hit;

		Vector3 bottom = m_characterController.transform.position - new Vector3(0, m_characterController.height / 2, 0);

		if (Physics.Raycast(bottom, Vector3.down, out hit, 0.2f))
		{
			if (hit.normal != Vector3.up)
			{
				return true;
			}
		}

		return false;
	}

	private void OnLanded()
	{
		m_isLanded = true;

		if (CheckBuffer(ref m_jumpBufferTimer, ref m_jumpingProperties.m_jumpBufferTime, m_jumpBufferCoroutine))
		{
			JumpMaxVelocity();
		}

		m_movementEvents.m_onLandedEvent.Invoke();
	}

	private void OnOffLedge()
	{
		m_offLedge = true;

		m_graceBufferCoroutine = StartCoroutine(RunBufferTimer((x) => m_graceTimer = (x), m_jumpingProperties.m_graceTime));

	}

	public void Respawn()
	{
		m_movementEvents.m_onRespawnEvent.Invoke();

		ResetCamera();
	}
	#endregion

	#region Physics Calculation Code

	private void CalculateCurrentSpeed()
	{
		float speed = m_baseMovementProperties.m_baseMovementSpeed;


		speed += m_currentWallRunningSpeed;
		speed += m_currentWallClimbSpeed;
		speed += m_currentSpeedBoost;

		m_currentMovementSpeed = speed;
	}

	public void SpeedBoost(float p_boostAmount)
	{
		m_currentSpeedBoost = p_boostAmount;
	}

	private void CalculateGroundPhysics()
	{
		if (IsGrounded() && !OnSlope())
		{
			m_velocity.y = 0;
		}

		if (OnSlope())
		{
			RaycastHit hit;

			Vector3 bottom = m_characterController.transform.position - new Vector3(0, m_characterController.height / 2, 0);

			if (Physics.Raycast(bottom, Vector3.down, out hit))
			{
				m_characterController.Move(new Vector3(0, -(hit.distance), 0));
			}
		}

		if (!IsGrounded() && !m_offLedge)
		{
			OnOffLedge();
		}
		if (IsGrounded())
		{
			m_offLedge = false;
		}

		if (IsGrounded() && !m_isLanded)
		{
			OnLanded();
		}
		if (!IsGrounded())
		{
			m_isLanded = false;
		}
	}

	private void CalculateVelocity()
	{
		if (m_states.m_gravityControllState == GravityState.GravityEnabled)
		{
			m_velocity.y += m_gravity * Time.deltaTime;
		}

		if (m_states.m_movementControllState == MovementControllState.MovementEnabled)
		{
			Vector2 input = new Vector2(m_movementInput.x, m_movementInput.y);

			Vector3 forwardMovement = transform.forward * input.y;
			Vector3 rightMovement = transform.right * input.x;

			Vector3 targetHorizontalMovement = Vector3.ClampMagnitude(forwardMovement + rightMovement, 1.0f) * m_currentMovementSpeed;
			Vector3 horizontalMovement = Vector3.SmoothDamp(m_velocity, targetHorizontalMovement, ref m_velocitySmoothing, m_baseMovementProperties.m_accelerationTime);

			m_velocity = new Vector3(horizontalMovement.x, m_velocity.y, horizontalMovement.z);
		}
		else
		{
			Vector2 input = new Vector2(0, 0);

			Vector3 forwardMovement = transform.forward * input.y;
			Vector3 rightMovement = transform.right * input.x;

			Vector3 targetHorizontalMovement = Vector3.ClampMagnitude(forwardMovement + rightMovement, 1.0f) * m_currentMovementSpeed;
			Vector3 horizontalMovement = Vector3.SmoothDamp(m_velocity, targetHorizontalMovement, ref m_velocitySmoothing, m_baseMovementProperties.m_accelerationTime);

			m_velocity = new Vector3(horizontalMovement.x, m_velocity.y, horizontalMovement.z);
		}

	}

	public void PhysicsSeekTo(Vector3 p_targetPosition)
	{
		Vector3 deltaPosition = p_targetPosition - transform.position;
		m_velocity = deltaPosition / Time.deltaTime;
	}
	#endregion

	#region Jump Code
	public void OnJumpInputDown()
	{
		m_jumpBufferCoroutine = StartCoroutine(RunBufferTimer((x) => m_jumpBufferTimer = (x), m_jumpingProperties.m_jumpBufferTime));

		if (CheckBuffer(ref m_wallJumpBufferTimer, ref m_wallRunProperties.m_wallJumpBufferTime, m_wallJumpBufferCoroutine) && !m_isWallRunning)
		{
			WallJump();
			return;
		}

		if (CheckBuffer(ref m_graceTimer, ref m_jumpingProperties.m_graceTime, m_graceBufferCoroutine) && !IsGrounded() && m_velocity.y <= 0f)
		{
			GroundJump();
			return;
		}

		if (m_isWallClimbing)
		{
			WallRunningJump();
			return;
		}

		if (m_isWallRunning)
		{
			WallRunningJump();
			return;
		}

		if (IsGrounded())
		{
			GroundJump();
			return;
		}

	}

	public void OnJumpInputUp()
	{
		if (m_velocity.y > m_minJumpVelocity)
		{
			JumpMinVelocity();
		}
	}

	private void CalculateJump()
	{
		m_gravity = -(2 * m_jumpingProperties.m_maxJumpHeight) / Mathf.Pow(m_jumpingProperties.m_timeToJumpApex, 2);
		m_maxJumpVelocity = Mathf.Abs(m_gravity) * m_jumpingProperties.m_timeToJumpApex;
		m_minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(m_gravity) * m_jumpingProperties.m_minJumpHeight);
	}

	private void WallJump()
	{
		m_movementEvents.m_onWallJumpEvent.Invoke();

		m_velocity.x = m_wallDir.x * m_wallRunProperties.m_wallJumpVelocity.x;
		m_velocity.y = m_wallRunProperties.m_wallJumpVelocity.y;
		m_velocity.z = m_wallDir.z * m_wallRunProperties.m_wallJumpVelocity.z;
	}

	private void WallRunningJump()
	{
		m_isWallRunning = false;

		m_movementEvents.m_onWallRunJumpEvent.Invoke();

		m_wallRunBufferCoroutine = StartCoroutine(RunBufferTimer((x) => m_wallRunBufferTimer = (x), m_wallRunProperties.m_wallRunBufferTime));

		m_velocity.x = m_wallDir.x * m_wallRunProperties.m_wallRunJumpVelocity.x;
		m_velocity.y = m_wallRunProperties.m_wallRunJumpVelocity.y;
		m_velocity.z = m_wallDir.z * m_wallRunProperties.m_wallRunJumpVelocity.z;
	}

	private void WallClimbingJump()
	{
		m_isWallClimbing = false;

		m_movementEvents.m_onWallClimbJumpEvent.Invoke();

		m_wallRunBufferCoroutine = StartCoroutine(RunBufferTimer((x) => m_wallRunBufferTimer = (x), m_wallRunProperties.m_wallRunBufferTime));

		m_velocity.x = m_wallDir.x * m_wallClimbProperties.m_wallClimbJumpVelocity.x;
		m_velocity.y = m_wallClimbProperties.m_wallClimbJumpVelocity.y;
		m_velocity.z = m_wallDir.z * m_wallClimbProperties.m_wallClimbJumpVelocity.z;
	}

	private void GroundJump()
	{
		m_movementEvents.m_onJumpEvent.Invoke();
		JumpMaxVelocity();
	}

	private void JumpMaxVelocity()
	{
		m_velocity.y = m_maxJumpVelocity;
	}

	private void JumpMinVelocity()
	{
		m_velocity.y = m_minJumpVelocity;
	}

	private void JumpMaxMultiplied(float p_force)
	{
		m_velocity.y = m_maxJumpVelocity * p_force;
	}

	#endregion

	#region Wall Run Code
	private void CheckWallRun()
	{
		float m_angleBetweenRays = m_wallRunProperties.m_wallRaySpacing / m_wallRunProperties.m_wallRidingRayCount;
		bool anyRayHit = false;

		for (int i = 0; i < m_wallRunProperties.m_wallRidingRayCount; i++)
		{
			Quaternion raySpaceQ = Quaternion.Euler(0, (i * m_angleBetweenRays) - (m_angleBetweenRays * (m_wallRunProperties.m_wallRidingRayCount / 2)), 0);
			RaycastHit hit;

			if (Physics.Raycast(m_characterController.transform.position, raySpaceQ * transform.forward, out hit, m_wallRunProperties.m_wallRunRayLength, m_wallRunProperties.m_wallMask))
			{
				if (Vector3.Dot(hit.normal, Vector3.up) == 0)
				{
					anyRayHit = true;

					m_wallVector = Vector3.Cross(hit.normal, Vector3.up);
					m_wallFacingVector = Vector3.Cross(hit.normal, m_cameraProperties.m_camera.transform.forward);
					m_wallDir = hit.normal;
					m_wallHitPos = hit.point;
					m_wallHitDst = hit.distance;

					m_localWallFacingVector = m_cameraProperties.m_camera.transform.InverseTransformDirection(m_wallFacingVector);

					if (!m_connectedWithWall)
					{
						OnWallConnect();
					}

					CheckToStartWallRun();
				}

				Debug.DrawLine(m_characterController.transform.position, hit.point);
			}
		}

		if (!anyRayHit)
		{
			m_isWallRunning = false;
			m_isWallClimbing = false;
			m_connectedWithWall = false;
		}

	}

	private void OnWallConnect()
	{
		m_connectedWithWall = true;
		m_wallJumpBufferCoroutine = StartCoroutine(RunBufferTimer((x) => m_wallJumpBufferTimer = (x), m_wallRunProperties.m_wallJumpBufferTime));
	}

	private void TiltLerp()
	{
		m_cameraProperties.m_cameraTilt.localRotation = Quaternion.Slerp(m_cameraProperties.m_cameraTilt.localRotation, Quaternion.Euler(0, 0, m_tiltTarget), m_wallRunProperties.m_tiltSpeed);
	}

	private void OnWallRideRelease()
	{
		m_isWallRunning = false;
		m_isWallClimbing = false;
		m_wallRunBufferCoroutine = StartCoroutine(RunBufferTimer((x) => m_wallRunBufferTimer = (x), m_wallRunProperties.m_wallRunBufferTime));
	}

	private void CheckToStartWallRun()
	{
		if (m_holdingWallRideStick)
		{
			if (m_isWallClimbing)
			{
				return;
			}

			if (m_isWallRunning)
			{
				return;
			}

			if (m_localWallFacingVector.x >= m_wallClimbProperties.m_wallClimbFactor)
			{
				if (!m_isWallClimbing)
				{
					if (CheckOverBuffer(ref m_wallRunBufferTimer, ref m_wallRunProperties.m_wallRunBufferTime, m_wallRunBufferCoroutine))
					{
						StartCoroutine(WallClimbing());
						return;
					}
				}
			}

			if (!m_isWallRunning)
			{
				if (CheckOverBuffer(ref m_wallRunBufferTimer, ref m_wallRunProperties.m_wallRunBufferTime, m_wallRunBufferCoroutine))
				{
					StartCoroutine(WallRunning());
					return;

				}
			}
		}

	}

	private IEnumerator WallClimbing()
	{
		m_movementEvents.m_onWallClimbBeginEvent.Invoke();

		m_isWallClimbing = true;

		m_states.m_gravityControllState = GravityState.GravityDisabled;
		m_states.m_movementControllState = MovementControllState.MovementDisabled;

		m_currentWallClimbSpeed = 0;

		float t = 0;

		while (m_isWallClimbing)
		{
			t += Time.deltaTime;


			m_velocity = Vector3.zero;

			m_velocity.y = m_localWallFacingVector.x * m_currentMovementSpeed;

			float progress = m_wallClimbProperties.m_wallClimbSpeedCurve.Evaluate(t / m_wallClimbProperties.m_wallClimbSpeedUpTime);
			m_currentWallClimbSpeed = Mathf.Lerp(0f, m_wallClimbProperties.m_maxWallClimbSpeed, progress);

			yield return null;
		}

		m_states.m_movementControllState = MovementControllState.MovementEnabled;
		m_states.m_gravityControllState = GravityState.GravityEnabled;

		m_currentWallClimbSpeed = 0;

		m_movementEvents.m_onWallClimbEndEvent.Invoke();
	}

	private IEnumerator WallRunning()
	{
		m_movementEvents.m_onWallRunBeginEvent.Invoke();

		m_isWallRunning = true;
		m_states.m_gravityControllState = GravityState.GravityDisabled;
		m_states.m_movementControllState = MovementControllState.MovementDisabled;

		m_currentWallRunningSpeed = 0;

		float t = 0;



		while (m_isWallRunning)
		{
			t += Time.deltaTime;


			float result = Mathf.Lerp(-m_wallRunProperties.m_wallRunCameraMaxTilt, m_wallRunProperties.m_wallRunCameraMaxTilt, m_wallFacingVector.y);
			m_tiltTarget = result;

			//m_modelRoot.localPosition = new Vector3(m_wallHitDst * Mathf.Sign(result), m_modelRoot.localPosition.y, m_modelRoot.localPosition.z);

			m_velocity = (m_wallVector * -m_wallFacingVector.y) * m_currentMovementSpeed;

			m_velocity += (transform.right * m_wallFacingVector.y) * m_currentMovementSpeed;

			m_velocity.y = 0;

			float progress = m_wallRunProperties.m_wallSpeedCurve.Evaluate(t / m_wallRunProperties.m_wallSpeedUpTime);
			m_currentWallRunningSpeed = Mathf.Lerp(0f, m_wallRunProperties.m_maxWallRunSpeed, progress);

			yield return null;
		}

		m_states.m_movementControllState = MovementControllState.MovementEnabled;
		m_states.m_gravityControllState = GravityState.GravityEnabled;

		//m_modelRoot.localPosition = Vector3.zero - (Vector3.up * (m_characterController.height / 2));

		m_currentWallRunningSpeed = 0;

		m_tiltTarget = 0f;

		m_movementEvents.m_onWallRunEndEvent.Invoke();
	}

	#endregion

	public void FreezeSelf()
	{
		m_states.m_movementControllState = MovementControllState.MovementDisabled;
	}

	public void UnFreezeSelf()
	{
		m_states.m_movementControllState = MovementControllState.MovementEnabled;
	}

	private void StopAllActions()
	{
		StopAllCoroutines();
	}

	public bool CheckCollisionLayer(LayerMask p_layerMask, GameObject p_object)
	{
		if (p_layerMask == (p_layerMask | (1 << p_object.layer)))
		{
			return true;
		}
		else
		{
			return false;
		}
	}
}
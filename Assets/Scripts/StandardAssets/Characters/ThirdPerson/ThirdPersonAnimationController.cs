﻿using System;
using Attributes;
using Attributes.Types;
using UnityEngine;
using Util;

namespace StandardAssets.Characters.ThirdPerson
{
	/// <summary>
	/// Class that sends Third Person locomotion to the Animator 
	/// </summary>
	[Serializable]
	public class ThirdPersonAnimationController
	{
		[SerializeField]
		protected ThirdPersonAnimationConfiguration configuration;

		/// <summary>
		/// Required motor
		/// </summary>
		private IThirdPersonMotor motor;

		/// <summary>
		/// The animator
		/// </summary>
		private Animator animator;

		private GameObject gameObject;

		/// <summary>
		/// Hashes of the animator parameters
		/// </summary>
		private int hashForwardSpeed;

		private int hashLateralSpeed;
		private int hashTurningSpeed;
		private int hashVerticalSpeed;
		private int hashGrounded;
		private int hashHasInput;
		private int hashFallingTime;
		private int hashFootedness;
		private int hashJumped;
		private int hashJumpedForwardSpeed;
		private int hashJumpedLateralSpeed;
		private int hashPredictedFallDistance;
		private int hashRapidTurn;
		private int hashIsStrafing;

		private bool isGrounded;

		private float headAngle;

		public Animator unityAnimator
		{
			get { return animator; }
		}

		public float animatorForwardSpeed
		{
			get { return animator.GetFloat(hashForwardSpeed); }
		}

		public float animatorLateralSpeed
		{
			get { return animator.GetFloat(hashLateralSpeed); }
		}

		public float animatorTurningSpeed
		{
			get { return animator.GetFloat(hashTurningSpeed); }
		}

		public float animationNormalizedProgress { get; private set; }

		public float footednessNormalizedProgress
		{
			get
			{
				if (isRightFootPlanted)
				{
					return MathUtilities.Wrap1(
						animationNormalizedProgress - configuration.footednessThresholdOffsetValue -
						configuration.footednessThresholdValue);
				}

				return animationNormalizedProgress;
			}
		}

		public bool isRightFootPlanted { get; private set; }

		public bool shouldUseRootMotion { get; private set; }

		private bool isLanding;

		public bool CanJump
		{
			get { return shouldUseRootMotion && !isLanding && isGrounded; }
		}

		public void OnLandAnimationExit()
		{
			isLanding = false;
		}

		public void OnLandAnimationEnter()
		{
			isLanding = true;
		}

		public void OnPhysicsJumpAnimationExit()
		{
			shouldUseRootMotion = true;
		}

		public void OnPhysicsJumpAnimationEnter()
		{
			// check if we are entering into a root movement jump
			if (!isGrounded && motor.normalizedForwardSpeed > 0 &&
			    Mathf.Approximately(Mathf.Abs(motor.normalizedLateralSpeed), 0))
			{
				shouldUseRootMotion = false;
			}
		}

		public void UpdateForwardSpeed(float newSpeed, float deltaTime)
		{
			animator.SetFloat(hashForwardSpeed, newSpeed, configuration.floatInterpolationTime, deltaTime);
		}

		public void UpdateLateralSpeed(float newSpeed, float deltaTime)
		{
			animator.SetFloat(hashLateralSpeed, newSpeed, configuration.floatInterpolationTime, deltaTime);
		}

		public void UpdateTurningSpeed(float newSpeed, float deltaTime)
		{
			animator.SetFloat(hashTurningSpeed, newSpeed, configuration.floatInterpolationTime, deltaTime);
		}

		/// <summary>
		/// Gets the required components
		/// </summary>
		public void Init(ThirdPersonBrain brain, IThirdPersonMotor motorToUse)
		{
			gameObject = brain.gameObject;
			hashForwardSpeed = Animator.StringToHash(configuration.forwardSpeedParameterName);
			hashLateralSpeed = Animator.StringToHash(configuration.lateralSpeedParameterName);
			hashTurningSpeed = Animator.StringToHash(configuration.turningSpeedParameterName);
			hashVerticalSpeed = Animator.StringToHash(configuration.verticalSpeedParameterName);
			hashGrounded = Animator.StringToHash(configuration.groundedParameterName);
			hashHasInput = Animator.StringToHash(configuration.hasInputParameterName);
			hashFallingTime = Animator.StringToHash(configuration.fallingTimeParameterName);
			hashFootedness = Animator.StringToHash(configuration.footednessParameterName);
			hashJumped = Animator.StringToHash(configuration.jumpedParameterName);
			hashJumpedForwardSpeed = Animator.StringToHash(configuration.jumpedForwardSpeedParameterName);
			hashJumpedLateralSpeed = Animator.StringToHash(configuration.jumpedLateralSpeedParameterName);
			hashPredictedFallDistance = Animator.StringToHash(configuration.predictedFallDistanceParameterName);
			hashRapidTurn = Animator.StringToHash(configuration.rapidTurnParameterName);
			hashIsStrafing = Animator.StringToHash(configuration.isStrafingParameterName);
			motor = motorToUse;
			animator = gameObject.GetComponent<Animator>();
			shouldUseRootMotion = true;
		}

		/// <summary>
		/// Sets the Animator parameters
		/// </summary>
		public void Update()
		{
			UpdateTurningSpeed(motor.normalizedTurningSpeed, Time.deltaTime);

			animator.SetBool(hashHasInput,
			                 CheckHasSpeed(motor.normalizedForwardSpeed) ||
			                 CheckHasSpeed(motor.normalizedLateralSpeed));

			UpdateForwardSpeed(motor.normalizedForwardSpeed, Time.deltaTime);
			UpdateLateralSpeed(motor.normalizedLateralSpeed, Time.deltaTime);

			if (!isGrounded)
			{
				animator.SetFloat(hashVerticalSpeed, motor.normalizedVerticalSpeed,
				                  configuration.floatInterpolationTime, Time.deltaTime);
				animator.SetFloat(hashFallingTime, motor.fallTime);
			}
			else
			{
				animator.SetBool(hashIsStrafing, Mathf.Abs(motor.normalizedLateralSpeed) > 0);
				UpdateFoot();
			}
		}

		public void HeadTurn()
		{
			animator.SetLookAtWeight(configuration.lookAtWeight);
			float targetHeadAngle = Mathf.Clamp(
				MathUtilities.Wrap180(motor.targetYRotation - animator.transform.eulerAngles.y),
				-configuration.lookAtMaxRotation, configuration.lookAtMaxRotation);

			headAngle = Mathf.LerpAngle(headAngle, targetHeadAngle, Time.deltaTime * configuration.lookAtRotationSpeed);

			Vector3 lookAtPos = animator.transform.position +
			                    Quaternion.AngleAxis(headAngle, Vector3.up) * animator.transform.forward * 100f;
			animator.SetLookAtPosition(lookAtPos);
		}

		/// <summary>
		/// Subscribe
		/// </summary>
		public void Subscribe()
		{
			motor.jumpStarted += OnJumpStarted;
			motor.landed += OnLanding;
			motor.fallStarted += OnFallStarted;
			motor.rapidlyTurned += OnRapidlyTurned;
		}

		/// <summary>
		/// Unsubscribe
		/// </summary>
		public void Unsubscribe()
		{
			if (motor != null)
			{
				motor.jumpStarted -= OnJumpStarted;
				motor.landed -= OnLanding;
				motor.fallStarted -= OnFallStarted;
				motor.rapidlyTurned -= OnRapidlyTurned;
			}
		}

		private void OnFallStarted(float predictedFallDistance)
		{
			isGrounded = false;
			animator.SetFloat(hashFallingTime, 0);
			animator.SetBool(hashGrounded, false);
			animator.SetFloat(hashPredictedFallDistance, predictedFallDistance);
		}

		private void SetFootednessBool(bool value)
		{
			if (Mathf.Abs(motor.normalizedLateralSpeed) < Mathf.Epsilon)
			{
				animator.SetBool(hashFootedness, value);
				isRightFootPlanted = value;
				return;
			}

			bool lateralSpeedRight = motor.normalizedLateralSpeed < 0;
			animator.SetBool(hashFootedness, lateralSpeedRight);
			isRightFootPlanted = lateralSpeedRight;
		}

		private void OnRapidlyTurned(float normalizedTurn)
		{
			animator.SetTrigger(hashRapidTurn);
		}

		/// <summary>
		/// Logic for dealing with animation on landing
		/// </summary>
		private void OnLanding()
		{
			isGrounded = true;
			animator.SetBool(hashGrounded, true);
		}

		/// <summary>
		/// Logic for dealing with animation on jumping
		/// </summary>
		private void OnJumpStarted()
		{
			if (!isGrounded)
			{
				return;
			}

			isGrounded = false;

			if (Mathf.Abs(motor.normalizedLateralSpeed) < Mathf.Abs(motor.normalizedForwardSpeed))
			{
				animator.SetFloat(hashJumpedLateralSpeed, 0);
				animator.SetFloat(hashJumpedForwardSpeed, motor.normalizedForwardSpeed);
			}
			else
			{
				animator.SetFloat(hashJumpedForwardSpeed, 0);
				animator.SetFloat(hashJumpedLateralSpeed, motor.normalizedLateralSpeed);
			}

			animator.SetTrigger(hashJumped);
			animator.SetFloat(hashFallingTime, 0);
			animator.SetBool(hashGrounded, false);
		}

		private void UpdateFoot()
		{
			AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
			animationNormalizedProgress = MathUtilities.GetFraction(stateInfo.normalizedTime);
			//TODO: remove zero index
			if (MathUtilities.Wrap1(animationNormalizedProgress +
			                        configuration.footednessThresholdOffsetValue) >
			    MathUtilities.Wrap1(configuration.footednessThresholdValue +
			                        configuration.footednessThresholdOffsetValue))
			{
				SetFootednessBool(!configuration.invertFoot);
				return;
			}

			SetFootednessBool(configuration.invertFoot);
		}

		private bool CheckHasSpeed(float speed)
		{
			return Mathf.Abs(speed) > 0;
		}

		/// <summary>
		/// Helper function to get the component of velocity along an axis
		/// </summary>
		/// <param name="axis"></param>
		/// <param name="velocity"></param>
		/// <returns></returns>
		private float GetVectorOnAxis(Vector3 axis, Vector3 vector)
		{
			float dot = Vector3.Dot(axis, vector.normalized);
			float val = dot * vector.magnitude;

			Debug.Log(val);
			return val;
		}
	}
}
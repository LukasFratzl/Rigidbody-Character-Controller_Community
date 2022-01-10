using Cinemachine;
using Unity.Mathematics;
using UnityEngine;

namespace GameDevWithLukas
{
    public class RigidBodyController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] protected Rigidbody _CharacterRigidbody;
        [SerializeField] protected Transform _PreProcessorTransform;
        [SerializeField] protected Collider _CharacterCollider;
        [SerializeField] protected Transform _CameraFollowTransform;
        [SerializeField] protected CinemachineBrain _camBrain;
        protected CinemachineFramingTransposer _camFraming;


        [Header("Force Applicator")]
        [SerializeField, Range(0f, 2f)] protected float _moveSpeed = 0.2f;
        [SerializeField, Range(0f, 50f)] protected float _rotateSpeed = 15f;
        [SerializeField] protected bool _pureRotationPhysics = true;
        [SerializeField, Range(0f, 0.3f)] protected float _isGroundedTolerance = 0.1f;
        [SerializeField] protected LayerMask _isGroundedLayer;
        [SerializeField] protected float _isGroundedUpForce = 1f;

        [SerializeField] protected bool _isGrounded;

        [Header("Input")]
        protected float2 _moveInput;
        [SerializeField] protected bool isIdle;
        protected const string _HorizontalInputValue = "Horizontal";
        protected const string _VerticalInputValue = "Vertical";
        [SerializeField] protected bool isThirdPerson = true;

        [Header("Camera Tweaks")]
        [SerializeField] protected bool _applyCameraSmoothing = true;
        [SerializeField, Range(0f, 10f)] protected float _CameraSmoothing = 5f;


        protected float3 _moveDirection;

        protected virtual void Start()
        {
            if (_CameraFollowTransform != null) _defaultCameraFollowOffsetY = _CameraFollowTransform.position.y - _PreProcessorTransform.position.y;
            if (_CharacterRigidbody != null)
            {
                _CharacterRigidbody.freezeRotation = !_pureRotationPhysics;
            }
            if (_camBrain != null)
            {
                _camFraming = (_camBrain.ActiveVirtualCamera as CinemachineVirtualCamera)?.GetCinemachineComponent<CinemachineFramingTransposer>();

                UpdateCamera(Time.deltaTime, 1f);
            }
        }


        protected virtual void Update()
        {
            float _deltaTime = Time.deltaTime;

            UpdateInput(_deltaTime);
            UpdateLocomotion(_deltaTime);
        }

        float lastTimeTimeFixed;

        protected virtual void FixedUpdate()
        {
            // DELTA TIME
            float _deltaTime = Time.fixedDeltaTime;

            // INTERPOLATION TIME
            float _currentTime = Time.time;
            if (lastTimeTimeFixed == 0f) lastTimeTimeFixed = _currentTime;
            float wantedInterpolationTimeFixedUpdate = (_currentTime - lastTimeTimeFixed) / _deltaTime;
            lastTimeTimeFixed = _currentTime;

            Grounding(_deltaTime, wantedInterpolationTimeFixedUpdate);
            Move(_deltaTime);
            Rotate(_deltaTime, wantedInterpolationTimeFixedUpdate);


            UpdateCamera(_deltaTime, wantedInterpolationTimeFixedUpdate);

        }

        protected virtual void UpdateInput(float _deltaTime)
        {
            _moveInput = new float2(Input.GetAxisRaw(_HorizontalInputValue), Input.GetAxisRaw(_VerticalInputValue));

            isIdle = math.abs(math.length(_moveInput)) < Helper.Epsilon;
        }

        protected virtual void UpdateLocomotion(float _deltaTime) // APPLY
        {
            _moveDirection = isIdle || !_isGrounded ? float3.zero : math.mul(_PreProcessorTransform.rotation, new float3(_moveInput.x, 0f, _moveInput.y));
        }


        protected float _defaultCameraFollowOffsetY;
        protected bool _previousIsThirdPerson;

        protected virtual void UpdateCamera(float _deltaTime, float _interpolationTime)
        {
            if (_CameraFollowTransform == null) return;

            float3 wantedPos = math.mul(_PreProcessorTransform.rotation, Helper.Up * _defaultCameraFollowOffsetY) + _PreProcessorTransform.position.ToFloat3();

            float3 previousPosition = _CameraFollowTransform.position; // NEEDED FOR INTERPOLATION

            _CameraFollowTransform.position = _applyCameraSmoothing ? math.lerp(_CameraFollowTransform.position, wantedPos, math.clamp(_CameraSmoothing * _deltaTime, 0f, 1f)) : wantedPos;

            // LERPING THE INTERPOLATION TIME TO ALWAYS TRAVEL AT THE SAME RELATIVE FRAME SPEED
            _CameraFollowTransform.position = math.lerp(previousPosition, _CameraFollowTransform.position, _interpolationTime);

            if (_camBrain != null)
            {
                if (_camBrain.m_UpdateMethod != CinemachineBrain.UpdateMethod.ManualUpdate) _camBrain.m_UpdateMethod = CinemachineBrain.UpdateMethod.ManualUpdate;
                _camBrain.ManualUpdate();
            }

            if (_previousIsThirdPerson != isThirdPerson)
            {
                _previousIsThirdPerson = isThirdPerson;

                float wantedCameraDistance = isThirdPerson ? 10f : 0f;
                _applyCameraSmoothing = isThirdPerson;

                if (_camFraming != null) _camFraming.m_CameraDistance = wantedCameraDistance;
            }
        }


        void Move(float _deltaTime)
        {
            if (_CharacterRigidbody == null) return;
            if (!_isGrounded) return;

            float3 offset = (_PreProcessorTransform.position.ToFloat3() + (_moveDirection * _moveSpeed)) - _PreProcessorTransform.position.ToFloat3();

            float3 velocity = offset / _deltaTime;

            float3 force = -_CharacterRigidbody.velocity.ToFloat3() + offset + velocity;
            force /= 1f + offset.SqrMagnitude() * 5f;

            force.y = 0f;

            _CharacterRigidbody.AddForce(force, ForceMode.VelocityChange);
        }

        void Rotate(float _deltaTime, float _interpolationTime)
        {
            if (_CharacterRigidbody == null) return;

            float wantedYaw = Helper.GetYawOfQuaternion(_camBrain.transform.rotation);

            if (_pureRotationPhysics)
            {
                if (_CharacterRigidbody.freezeRotation == true) _CharacterRigidbody.freezeRotation = false;

                float3 currentDirection = math.normalize(new float3(_CharacterRigidbody.transform.forward.x, 0f, _CharacterRigidbody.transform.forward.z));
                float3 wantedDirectionFacing = math.normalize(math.mul(Quaternion.AngleAxis(wantedYaw, Helper.Up), Helper.Forward));

                Quaternion wantedFacing = Quaternion.FromToRotation(currentDirection, wantedDirectionFacing);
                Quaternion wantedUp = Quaternion.FromToRotation(_CharacterRigidbody.transform.up, Helper.Up);

                float3 wantedTorque = (new float3(wantedFacing.x, wantedFacing.y, wantedFacing.z) + new float3(wantedUp.x, wantedUp.y, wantedUp.z)) * _rotateSpeed * 4f;

                _CharacterRigidbody.AddTorque(wantedTorque, ForceMode.VelocityChange);
            }
            else // IF YOU LIKE MORE CONTROL OVER THE ROTATION
            {
                if (_CharacterRigidbody.freezeRotation == false) _CharacterRigidbody.freezeRotation = true;

                Quaternion wantedRotation = Quaternion.AngleAxis(wantedYaw, Helper.Up);

                Quaternion previousRotation = _CharacterRigidbody.rotation; // NEEDED FOR INTERPOLATION

                Quaternion calulatedRotation = Quaternion.Lerp(_CharacterRigidbody.rotation, wantedRotation, _rotateSpeed * _deltaTime);

                // LERPING THE INTERPOLATION TIME TO ALWAYS TRAVEL AT THE SAME RELATIVE FRAME SPEED
                _CharacterRigidbody.rotation = _PreProcessorTransform.rotation = Quaternion.Lerp(previousRotation, calulatedRotation, _interpolationTime);
            }
        }

        void Grounding(float _deltaTime, float _interpolationTime)
        {
            if (_CharacterCollider == null) return;
            if (_CharacterRigidbody == null) return;

            float distance = math.distance(_PreProcessorTransform.position, _CharacterCollider.bounds.center);

            distance += _isGroundedTolerance;

            RaycastHit _isGroundedHit;
            _isGrounded = Physics.Raycast(_CharacterCollider.bounds.center, -_PreProcessorTransform.up, out _isGroundedHit, distance, _isGroundedLayer, QueryTriggerInteraction.Ignore);

            if (_isGrounded)
            {
                if (_isGroundedHit.point.y > _PreProcessorTransform.position.y)
                {

                    float localY = _isGroundedHit.point.y - _PreProcessorTransform.position.y;

                    float velocityP = localY + (localY / _deltaTime);
                    velocityP /= 1f + (Helper.Up * localY).SqrMagnitude() * 5f; // PRETTY DAMN EPIC


                    float3 wantedDelta = math.mul(_PreProcessorTransform.rotation, Helper.Up * math.lerp(0f, localY, math.clamp(_isGroundedUpForce * velocityP * _deltaTime, 0f, 1f)));

                    float3 previousPosition = _PreProcessorTransform.position; // NEEDED FOR INTERPOLATION

                    float3 wantedFinalPos = _PreProcessorTransform.position.ToFloat3() + wantedDelta;

                    // LERPING THE INTERPOLATION TIME TO ALWAYS TRAVEL AT THE SAME RELATIVE FRAME SPEED
                    _PreProcessorTransform.position = _CharacterRigidbody.position = math.lerp(previousPosition, wantedFinalPos, _interpolationTime);

                    float3 velocity = _CharacterRigidbody.velocity;
                    velocity.y = 0f;
                    _CharacterRigidbody.velocity = velocity;
                }
            }
        }
    }
}


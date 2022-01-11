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
        [SerializeField] protected Transform _CameraFollowTargetTransform;
        [SerializeField] protected CinemachineBrain _camBrain;
        protected CinemachineFramingTransposer _camFraming;
        [SerializeField] protected MeshRenderer[] _RenderersToCullOnFirstPersonMode;


        [Header("Force Applicator")]
        [SerializeField, Range(0f, 20f)] protected float _moveSpeed = 10f;
        [SerializeField] protected ForceMode _moveForceMode = ForceMode.VelocityChange;
        [SerializeField, Range(0f, 15f)] protected float _moveGroundDrag = 0f;
        [SerializeField, Range(0f, 15f)] protected float _moveAirDrag = 0f;
        [SerializeField, Range(0f, 50f)] protected float _rotateSpeed = 20f;
        [SerializeField] protected ForceMode _rotateForceMode = ForceMode.VelocityChange;
        [SerializeField, Range(0f, 50f)] protected float _rotateDrag = 5f;
        [SerializeField] protected bool _pureRotationPhysics = true;
        [SerializeField, Range(0f, 0.3f)] protected float _isGroundedRayTolerance = 0.1f;
        //[SerializeField, Range(0f, 1f)] protected float _isGroundedRayRadius = 0.3f; // USUALLY THE RADIUS OF YOUR CAPSULE COLLIDER .... 
        [SerializeField, Range(0f, 3f)] protected float _isGroundedUpForce = 1f;
        [SerializeField] protected LayerMask _isGroundedLayer;

        [SerializeField] protected bool _isGrounded;

        [Header("Input")]
        protected float2 _moveInput;
        protected float3 _moveDirection;
        [SerializeField] protected bool isIdle;
        protected const string _HorizontalInputValue = "Horizontal";
        protected const string _VerticalInputValue = "Vertical";
        [SerializeField] protected bool isThirdPerson = true;

        [Header("Camera Tweaks")]
        [SerializeField] protected bool _applyCameraSmoothing = true;
        [SerializeField, Range(0f, 10f)] protected float _CameraSmoothing = 5f;



        protected virtual void Start()
        {
            if (_CharacterRigidbody != null)
            {
                _CharacterRigidbody.freezeRotation = !_pureRotationPhysics;
            }
            if (_camBrain != null)
            {
                _camFraming = (_camBrain.ActiveVirtualCamera as CinemachineVirtualCamera)?.GetCinemachineComponent<CinemachineFramingTransposer>();

                UpdateCamera(Time.deltaTime, force: true);
            }
        }


        protected virtual void Update()
        {
            float _deltaTime = Time.deltaTime;

            UpdateInput(_deltaTime);
            UpdateLocomotion(_deltaTime);
        }

        protected virtual void FixedUpdate()
        {
            // DELTA TIME
            float _deltaTime = Time.fixedDeltaTime;

            Grounding(_deltaTime);
            Move(_deltaTime);
            Rotate(_deltaTime);
            Drag(_deltaTime);

            // NEED TO BE IN FIXED UPDATE BECAUSE OF PHYSICS MOVEMENT
            UpdateCamera(_deltaTime);

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


        //protected float _defaultCameraFollowOffsetY;
        protected bool _previousIsThirdPerson;

        protected virtual void UpdateCamera(float _deltaTime, bool force = false)
        {
            if (_CameraFollowTransform == null) return;
            if (_CameraFollowTargetTransform == null) return;

            float3 wantedPos = _CameraFollowTargetTransform.position;
            Quaternion wantedRot = _CameraFollowTargetTransform.rotation;

            _CameraFollowTransform.position = _applyCameraSmoothing ? math.lerp(_CameraFollowTransform.position, wantedPos, math.clamp(_CameraSmoothing * _deltaTime, 0f, 1f)) : wantedPos;
            _CameraFollowTransform.rotation = _applyCameraSmoothing ? Quaternion.Lerp(_CameraFollowTransform.rotation, wantedRot, math.clamp(_CameraSmoothing * _deltaTime, 0f, 1f)) : wantedRot;

            if (_camBrain != null)
            {
                // if (_camBrain.m_WorldUpOverride != _CameraFollowTargetTransform) _camBrain.m_WorldUpOverride = _CameraFollowTargetTransform;
                if (_camBrain.m_UpdateMethod != CinemachineBrain.UpdateMethod.ManualUpdate) _camBrain.m_UpdateMethod = CinemachineBrain.UpdateMethod.ManualUpdate;
                _camBrain.ManualUpdate();
            }

            if (_previousIsThirdPerson != isThirdPerson || force)
            {
                _previousIsThirdPerson = isThirdPerson;

                float wantedCameraDistance = isThirdPerson ? 10f : 0f;
                _applyCameraSmoothing = isThirdPerson;

                if (_camFraming != null) _camFraming.m_CameraDistance = wantedCameraDistance;

                if (_RenderersToCullOnFirstPersonMode != null)
                {
                    for (int r = 0; r < _RenderersToCullOnFirstPersonMode.Length; r++)
                    {
                        _RenderersToCullOnFirstPersonMode[r].enabled = isThirdPerson;
                    }
                }
            }
        }

        void Grounding(float _deltaTime)
        {
            if (_CharacterCollider == null) return;
            if (_CharacterRigidbody == null) return;

            float distance = math.distance(_PreProcessorTransform.position, _CharacterCollider.bounds.center);

            distance += _isGroundedRayTolerance;

            RaycastHit _isGroundedHit;
            _isGrounded = Physics.Raycast(_CharacterCollider.bounds.center, -_PreProcessorTransform.up, out _isGroundedHit, distance, _isGroundedLayer, QueryTriggerInteraction.Ignore);


            if (_isGrounded)
            {
                if (_isGroundedHit.point.y > _CharacterRigidbody.position.y)
                {
                    float localY = _isGroundedHit.point.y - _CharacterRigidbody.position.y;

                    float velocityP = localY + (localY / _deltaTime);
                    velocityP /= 1f + (Helper.Up * localY).SqrMagnitude() * 5f; // PRETTY DAMN EPIC


                    float3 wantedDelta = math.mul(_CharacterRigidbody.rotation, Helper.Up * math.lerp(0f, localY, math.clamp(_isGroundedUpForce * velocityP * _deltaTime, 0f, 1f)));

                    float3 wantedFinalPos = _CharacterRigidbody.position.ToFloat3() + wantedDelta;

                    _CharacterRigidbody.position = wantedFinalPos;

                    float3 velocity = _CharacterRigidbody.velocity;
                    velocity.y = 0f;
                    _CharacterRigidbody.velocity = velocity;
                }
            }
        }


        void Move(float _deltaTime)
        {
            if (_CharacterRigidbody == null) return;
            if (!_isGrounded) return;

            float calulatedSpeed = _moveSpeed * _deltaTime;

            float3 offset = (_PreProcessorTransform.position.ToFloat3() + (_moveDirection * calulatedSpeed)) - _PreProcessorTransform.position.ToFloat3();

            float3 velocity = offset / _deltaTime;

            float3 force = -_CharacterRigidbody.velocity.ToFloat3() + offset + velocity;
            force /= 1f + offset.SqrMagnitude() * 5f;

            force.y = 0f;

            _CharacterRigidbody.AddForce(force, _moveForceMode);
        }

        void Rotate(float _deltaTime)
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

                _CharacterRigidbody.AddTorque(wantedTorque, _rotateForceMode);
            }
            else // IF YOU LIKE MORE CONTROL OVER THE ROTATION
            {
                if (_CharacterRigidbody.freezeRotation == false) _CharacterRigidbody.freezeRotation = true;

                Quaternion wantedRotation = Quaternion.AngleAxis(wantedYaw, Helper.Up);

                _CharacterRigidbody.rotation = Quaternion.Lerp(_CharacterRigidbody.rotation, wantedRotation, _rotateSpeed * _deltaTime);
            }
        }

        void Drag(float _deltaTime)
        {
            if (_CharacterRigidbody == null) return;

            // MOVE
            if (_CharacterRigidbody.drag != 0f) _CharacterRigidbody.drag = 0f;
            float3 wantedVel = Helper.Up * _CharacterRigidbody.velocity.y;
            if (_isGrounded) _CharacterRigidbody.velocity = math.lerp(_CharacterRigidbody.velocity, wantedVel, math.clamp(_moveGroundDrag * _deltaTime, 0f, 1f));
            else if (!_isGrounded) _CharacterRigidbody.velocity = math.lerp(_CharacterRigidbody.velocity, wantedVel, math.clamp(_moveAirDrag * _deltaTime, 0f, 1f));

            // ROTATE
            if (_CharacterRigidbody.angularDrag != _rotateDrag) _CharacterRigidbody.angularDrag = _rotateDrag;
        }
    }
}


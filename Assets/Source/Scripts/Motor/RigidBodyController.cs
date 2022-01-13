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
        [SerializeField, Range(0f, 15f)] protected float _moveAirDrag = 0f;
        [SerializeField, Range(0f, 50f)] protected float _rotateSpeed = 20f;
        [SerializeField] protected ForceMode _rotateForceMode = ForceMode.VelocityChange;
        [SerializeField, Range(0f, 50f)] protected float _rotateDrag = 5f;
        [SerializeField] protected bool _pureRotationPhysics = true;
        [SerializeField, Range(0f, 0.3f)] protected float _isGroundedRayTolerance = 0.1f;
        [SerializeField, Range(0f, 3f)] protected float _isGroundedUpForce = 1f;
        [SerializeField] protected LayerMask _isGroundedLayer = 1;
        protected bool _isGrounded;
        [SerializeField, Range(0f, 3f)] protected float _jumpHeight = 0.5f;
        protected float _jumpForceRuntime;
        [SerializeField, Range(-20, 20)] protected float _gravity = -9.81f; // -9.81 IS THE GRAVITY ON EARTH

        [Header("Input")]
        [SerializeField] protected bool isThirdPerson = true;
        [SerializeField] protected bool isStrafe = false;
        protected float2 _moveInput;
        protected float3 _moveDirection;
        protected bool isInputIdle;
        protected const string _HorizontalInputValue = "Horizontal";
        protected const string _VerticalInputValue = "Vertical";
        protected bool isStrafeRuntime { get { return isThirdPerson == false || isStrafe; } }

        [Header("Camera Tweaks")]
        [SerializeField, Range(0f, 10f)] protected float _CameraSmoothing = 5f;
        protected bool _applyCameraSmoothing = true;



        protected virtual void Start()
        {
            if (_CharacterRigidbody != null)
            {
                _CharacterRigidbody.freezeRotation = !_pureRotationPhysics;
                _CharacterRigidbody.useGravity = false;
            }
            if (_camBrain != null)
            {
                _camFraming = (_camBrain.ActiveVirtualCamera as CinemachineVirtualCamera)?.GetCinemachineComponent<CinemachineFramingTransposer>();

                UpdateCamera(Time.deltaTime, force: true);
            }
            _previousStrafe = isStrafeRuntime;
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
            HorizontalMove(_deltaTime);
            VerticalMove();
            Rotate(_deltaTime);
            Drag(_deltaTime);

            // NEED TO BE IN FIXED UPDATE BECAUSE OF PHYSICS MOVEMENT
            UpdateCamera(_deltaTime);

        }

        protected virtual void UpdateInput(float _deltaTime)
        {
            _moveInput = new float2(Input.GetAxis(_HorizontalInputValue), Input.GetAxis(_VerticalInputValue));

            isInputIdle = math.abs(math.length(_moveInput)) < Helper.Epsilon;

            if (_isGrounded && Input.GetKeyDown(KeyCode.Space) && _jumpForceRuntime <= 0f)
            {
                _jumpForceRuntime = math.sqrt(_jumpHeight * -2f * (_gravity));

                _CharacterRigidbody.velocity = new float3(_CharacterRigidbody.velocity.x, _jumpForceRuntime, _CharacterRigidbody.velocity.z);
            }
        }


        protected virtual void UpdateLocomotion(float _deltaTime) // APPLY
        {
            Quaternion wantedInputRotation = _PreProcessorTransform.rotation;
            if (isStrafeRuntime == false)
            {
                float wantedCameraYaw = Helper.GetYawOfQuaternion(_camBrain.transform.rotation);

                Quaternion camerRotation = Quaternion.AngleAxis(wantedCameraYaw, Helper.Up);

                wantedInputRotation = camerRotation;
            }

            _moveDirection = isInputIdle || !_isGrounded ? _moveDirection : math.mul(wantedInputRotation, new float3(_moveInput.x, 0f, _moveInput.y));
        }


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

        protected virtual void Grounding(float _deltaTime)
        {
            if (_CharacterCollider == null) return;
            if (_CharacterRigidbody == null) return;

            float distance = math.distance(_PreProcessorTransform.position, _CharacterCollider.bounds.center);

            distance += _isGroundedRayTolerance;

            RaycastHit _isGroundedHit = default;
            _isGrounded = Physics.Raycast(_CharacterCollider.bounds.center, -_PreProcessorTransform.up, out _isGroundedHit, distance, _isGroundedLayer, QueryTriggerInteraction.Ignore);


            if (_isGrounded)
            {
                if (_isGroundedHit.point.y > _CharacterRigidbody.position.y && _jumpForceRuntime <= 0f)
                {
                    float localY = _isGroundedHit.point.y - _CharacterRigidbody.position.y;

                    float velocityP = localY + (localY / _deltaTime);
                    velocityP /= 1f + (Helper.Up * localY).SqrMagnitude() * 5f; // PRETTY DAMN EPIC

                    float3 wantedDelta = Helper.Up * math.lerp(0f, localY, math.clamp(_isGroundedUpForce * velocityP * _deltaTime, 0f, 1f));

                    _CharacterRigidbody.position += wantedDelta.ToVector3();

                    float3 velocity = _CharacterRigidbody.velocity;
                    velocity.y = 0f; // BECAUSE WE SET THE VELOCITY HERE TO ZERO ...
                    _CharacterRigidbody.velocity = velocity;
                }
            }
        }


        protected virtual void HorizontalMove(float _deltaTime)
        {
            if (_CharacterRigidbody == null) return;
            if (!_isGrounded) return;

            float deltaSpeed = _moveSpeed * _deltaTime;

            float3 offset = isInputIdle ? float3.zero : math.normalize(_moveDirection) * deltaSpeed;

            float3 velocity = offset / _deltaTime;

            float3 force = -_CharacterRigidbody.velocity.ToFloat3() + offset + velocity;
            force /= 1f + offset.SqrMagnitude() * 5f;

            force.y = 0f;

            if (float.IsNaN(force.x) == false && float.IsNaN(force.z) == false)
                _CharacterRigidbody.AddForce(force, _moveForceMode);
        }

        protected virtual void VerticalMove()
        {
            // GRAVITY
            _CharacterRigidbody.AddForce(Helper.Up * _gravity, ForceMode.Acceleration); // ACCELERATION BECAUSE GRAVITY IS AN ACCELERATION FORCE ...
            float airFriction = 0.1f;
            if (_CharacterRigidbody.velocity.y < (_gravity + airFriction)) // IF WE REACH THE MAX GRAVITY VELOCITY WE DON'T WANT ACCELERATE MORE TO HAVE REALISTIC GRAVITY ON A PLANET WITH AIR
            {
                float3 velocity = _CharacterRigidbody.velocity;
                velocity.y = _gravity + airFriction;
                _CharacterRigidbody.velocity = velocity;
            }

            if (_jumpForceRuntime <= 0f) return;

            // JUMPING
            if (_CharacterRigidbody.velocity.y > 0f) _jumpForceRuntime = _CharacterRigidbody.velocity.y;
            else _jumpForceRuntime = 0f;
        }

        private bool _previousStrafe;

        protected virtual void Rotate(float _deltaTime)
        {
            if (_CharacterRigidbody == null) return;
            if (isStrafe && _camBrain == null) return;

            if (isStrafeRuntime != _previousStrafe && isStrafeRuntime == false) _moveDirection = math.mul(Quaternion.AngleAxis(Helper.GetYawOfQuaternion(_PreProcessorTransform.rotation), Helper.Up), Helper.Forward);
            _previousStrafe = isStrafeRuntime;

            float wantedYaw = isStrafeRuntime ? Helper.GetYawOfQuaternion(_camBrain.transform.rotation) : Helper.GetYawOfDirection(_moveDirection);

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

        protected virtual void Drag(float _deltaTime)
        {
            if (_CharacterRigidbody == null) return;

            // MOVE
            if (_CharacterRigidbody.drag != 0f) _CharacterRigidbody.drag = 0f;
            float3 wantedVel = Helper.Up * _CharacterRigidbody.velocity.y;
            if (!_isGrounded) _CharacterRigidbody.velocity = math.lerp(_CharacterRigidbody.velocity, wantedVel, math.clamp(_moveAirDrag * _deltaTime, 0f, 1f));

            // ROTATE
            if (_CharacterRigidbody.angularDrag != _rotateDrag) _CharacterRigidbody.angularDrag = _rotateDrag;
        }
    }
}


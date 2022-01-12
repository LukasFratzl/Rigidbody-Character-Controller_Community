using UnityEngine;

namespace GameDevWithLukas.Demo
{
    public class OptionsMenu : MonoBehaviour
    {
        public RigidBodyController _controller;

        protected const System.Reflection.BindingFlags _bestBindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Default;


        public void ChangeFrameRateToMax()
        {
            Application.targetFrameRate = -1;
        }

        public void ChangeFrameRateTo15()
        {
            Application.targetFrameRate = 15;
        }

        public void ChangeIsStrafing()
        {
            if (_controller == null) return;

            bool isSet = (bool)_controller.GetType().GetField("isStrafe", _bestBindingFlags).GetValue(_controller);

            _controller.GetType().GetField("isStrafe", _bestBindingFlags).SetValue(_controller, !isSet);
        }

        public void ChangeIsPhysicsRotation()
        {
            if (_controller == null) return;

            bool isSet = (bool)_controller.GetType().GetField("_pureRotationPhysics", _bestBindingFlags).GetValue(_controller);

            _controller.GetType().GetField("_pureRotationPhysics", _bestBindingFlags).SetValue(_controller, !isSet);
        }

        public void ChangeIsThirdPerson()
        {
            if (_controller == null) return;

            bool isSet = (bool)_controller.GetType().GetField("isThirdPerson", _bestBindingFlags).GetValue(_controller);

            _controller.GetType().GetField("isThirdPerson", _bestBindingFlags).SetValue(_controller, !isSet);
        }

    }
}

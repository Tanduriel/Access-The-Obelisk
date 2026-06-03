using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Prevents an activation key that closed a modal from activating the screen underneath it.
    /// </summary>
    public static class InputActivationGuard
    {
        private const float DefaultBlockSeconds = 0.35f;
        private static float _blockSubmitUntil;
        private static bool _waitForSubmitRelease;

        /// <summary>
        /// Starts a short submit block after a modal or popup consumes activation input.
        /// </summary>
        public static void BlockSubmitAfterModal(float seconds = DefaultBlockSeconds)
        {
            float until = Time.unscaledTime + seconds;
            if (until > _blockSubmitUntil)
            {
                _blockSubmitUntil = until;
            }

            _waitForSubmitRelease = IsSubmitHeld();
        }

        /// <summary>
        /// Returns true while Enter or Space should not activate the next UI layer.
        /// </summary>
        public static bool ShouldBlockSubmit()
        {
            if (_waitForSubmitRelease)
            {
                if (IsSubmitHeld())
                {
                    return true;
                }

                _waitForSubmitRelease = false;
            }

            return Time.unscaledTime < _blockSubmitUntil;
        }

        /// <summary>
        /// Returns true when a submit key is currently held.
        /// </summary>
        public static bool IsSubmitHeld()
        {
            return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space);
        }
    }
}

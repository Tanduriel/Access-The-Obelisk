using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Input facade used by the mod's handlers. Each query returns true when the
    /// real keyboard reports it or when the gamepad layer (<see cref="GamepadInput"/>)
    /// has translated a controller chord into the same key this frame.
    /// </summary>
    public static class ModInput
    {
        /// <summary>Keyboard <c>GetKeyDown</c> plus any matching gamepad chord.</summary>
        public static bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key) || GamepadInput.IsKeyDown(key);

        /// <summary>Keyboard <c>GetKey</c> plus any matching gamepad chord modifier.</summary>
        public static bool GetKey(KeyCode key) => Input.GetKey(key) || GamepadInput.IsKeyHeld(key);

        /// <summary>Keyboard <c>GetKeyUp</c>. The gamepad layer does not emit key-up events.</summary>
        public static bool GetKeyUp(KeyCode key) => Input.GetKeyUp(key);
    }
}
